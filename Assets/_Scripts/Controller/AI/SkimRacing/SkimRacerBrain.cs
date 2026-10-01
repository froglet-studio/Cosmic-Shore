using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A skim racer: flies a ribbon of prisms close enough to skim it and never close enough to
    /// touch it, swings out to collect its crystal, and threads what other pilots leave in the air.
    /// Docs/AISystem/SQUIRREL_SKIM.md is the design; this is the Squirrel's Skim Race brain.
    ///
    /// <para><b>The line is an OFFSET, not a path.</b> It is the ribbon's own centre line plus a
    /// displacement in the plates' frame, written in POLAR form around the ribbon — a distance
    /// <c>rho</c> and an angle <c>phi</c> measured from the plates' up toward their right — so the
    /// line follows every bend of the ribbon for free and moving between two offsets goes AROUND
    /// the ribbon, never through it. Where a segment would still bring the hull inside the ribbon's
    /// envelope (<see cref="ClearRho"/>), a key is inserted at the offending point, lifted clear
    /// (<see cref="Subdivide"/>).</para>
    ///
    /// <para><b>At rest the line is a LANE.</b> Each face of the ribbon carries a racing line
    /// (<see cref="SkimRacingLine"/>) — the smoothest path that stays inside the skim corridor —
    /// and between two keys on the same lane the offset IS that lane. A constant height over the
    /// centre line inherits every bend of the ribbon; the lane uses the plate's width to cut them,
    /// which is most of what the ship's stick authority is spent on at full boost.</para>
    ///
    /// <para><b>The line is C2, including where it is re-planned.</b> Keys carry value, slope and
    /// curvature and are joined by quintic Hermite segments; every re-plan starts from a key that
    /// samples the OLD line's full state under the ship, so a new crystal or a re-anchor never moves
    /// the line the ship is on.</para>
    ///
    /// <para><b>A plan is CHOSEN, not constructed.</b> For each crystal the planner builds a small
    /// set of candidates — rest on either lane then swing out, go straight from where the ship is,
    /// or stay on a lane that already passes the crystal — against a few pass points on the crystal's
    /// capture circle, and flies the one a forward simulation of the ship says is FASTEST
    /// (<see cref="Evaluate"/>): the speed the stick authority allows along that exact line, the
    /// boost it skims or bleeds, and the rails it would cross. The two things that cost a Squirrel
    /// most at full boost — a face change round the ribbon's edge (the better part of 800 u to do
    /// at 300 u/s) and a swing it has no room for — are then priced rather than forbidden, so a
    /// crystal on the far face is taken the cheap way when there is one.</para>
    ///
    /// <para><b>Every segment is sized against the ship.</b> What limits a Squirrel at speed is not
    /// how hard it can turn but how fast it can START turning — the transform lags the stick by
    /// 0.67 s (<see cref="SkimFlightController"/>) — so a segment's length comes from the closed form
    /// for a jerk-limited move at the speed it will be flown (<see cref="SizeSegment"/>), counting
    /// the lift round an edge when it changes face (<see cref="SegmentLength"/>).</para>
    ///
    /// <para><b>Steering</b> is cross-track tracking of that line: the line's curvature as
    /// feed-forward (read a servo-lag ahead), plus a damped correction of position and lateral
    /// velocity, turned into sticks by <see cref="SkimFlightController"/>. Roll keeps the ship's
    /// belly toward the nearest point of the plates — pitch and yaw turn equally fast, so the line
    /// never depends on roll, and clearance is planned for a hull at ANY roll.</para>
    ///
    /// <para><b>What it never does</b>: read another pilot's state, collect anything it did not fly
    /// through, or touch the ship through anything but the sticks and the throttle (ARCHITECTURE.md
    /// R1/R2). It is pure — no Unity object — and Tools/Build/squirrel_ai_harness compiles and races
    /// this exact file.</para>
    /// </summary>
    public sealed class SkimRacerBrain
    {
        enum KeyKind : byte { Anchor, Edge, Pass, Lane, Avoid, Rejoin, Opt }

        struct Key
        {
            public float S;
            public float Rho, Phi;
            public float DRho, DPhi;
            public float DDRho, DDPhi;
            public KeyKind Kind;
            /// <summary>The lane this key sits on (0 over the plates, 1 under them), or -1. Between
            /// two keys on the same lane the line IS that lane.</summary>
            public sbyte Lane;
        }

        const float SampleStep = 6f;        // arc between samples; tangent half-span
        const float EdgeLift = 1.5f;        // how far clear of the envelope an edge key sits
        const float BrakePerUnit = 1.2f;    // u/s of speed shed per unit travelled with the throttle off
        const float SpeedScanInterval = 0.1f;
        const float LaneStep = 4f;          // finite-difference step for a lane's slope and curvature
        const float AddPerHit = 0.1f;       // SkimmerBoostPrismEffectSO: boost gained per prism entered
        const float BoostDecay = 0.3f;      // VesselTransformer: boost lost per second above 1
        const float ConflictSeconds = 5f;   // what the evaluator charges a plan through a rail: a touch resets the boost
        const int MaxCandidates = 16;

        readonly SkimRoute _route;
        readonly SkimRacerProfile _p;
        SkimRacingLine _line;
        readonly List<Key> _keys = new List<Key>(24);
        readonly List<Key> _trial = new List<Key>(24);
        readonly List<Key> _backup = new List<Key>(24);
        readonly List<Key>[] _cands = new List<Key>[MaxCandidates];
        readonly bool[] _candTargets = new bool[MaxCandidates];
        readonly float[] _candSpeeds = new float[MaxCandidates];
        int _candCount;
        readonly float _wanderPhaseA, _wanderPhaseB;

        SkimRacerSensors _sensors;
        IReadOnlyList<SkimObstacle> _obstacles;
        bool _initialized;
        float _s;

        // The crystal this plan is for.
        bool _haveCrystal;
        Vector3 _crystal;
        float _crystalS = float.NaN;      // unwrapped arc the plan passes it at; NaN until planned
        float _crystalSeenAt = -1f;
        bool _crystalPlanned;

        float _throttle = 1f;
        int _rollSide;
        float _nextSpeedScan;
        float _planSpeed;

        // Telemetry, read by the harness and by a debug overlay.
        public float RouteS => _s;
        public int Reanchors { get; private set; }
        public int CrystalsPlanned { get; private set; }
        public int CrystalsDeferred { get; private set; }
        public int Avoidances { get; private set; }
        public int UnresolvedConflicts { get; private set; }
        public int FaceChanges { get; private set; }
        public float CrossTrackError { get; private set; }
        /// <summary>Arc of the crystal the current plan passes, NaN when none.</summary>
        public float PlannedCrystalS => _crystalPlanned ? _crystalS : float.NaN;
        /// <summary>Arc the crystal is targeted at, planned or not (NaN before it is projected).</summary>
        public float TargetCrystalS => _crystalS;
        public int KeyCount => _keys.Count;
        public float ThrottleOut => _throttle;
        /// <summary>Which candidate the last plan chose — for tooling.</summary>
        public string LastChoice { get; private set; } = "";
        /// <summary>The last steering terms — for tooling.</summary>
        public string DebugSteer { get; private set; } = "";
        /// <summary>What the last obstacle check concluded — for tooling.</summary>
        public string LastAvoidNote { get; private set; } = "";

        /// <summary>The planned offset (distance, angle) at arc <paramref name="s"/> — for tooling.</summary>
        public void DescribePlan(float s, out float rho, out float phi) => Offset(_keys, s, out rho, out phi);

        public SkimRacerBrain(SkimRoute route, SkimRacerProfile profile, int seed)
        {
            _route = route ?? throw new ArgumentNullException(nameof(route));
            _p = profile ?? SkimRacerProfile.Expert();
            var rng = new System.Random(seed);
            _wanderPhaseA = (float)(rng.NextDouble() * Math.PI * 2.0);
            _wanderPhaseB = (float)(rng.NextDouble() * Math.PI * 2.0);
            for (int i = 0; i < MaxCandidates; i++) _cands[i] = new List<Key>(16);
        }

        /// <summary>Hand the brain a racing line solved elsewhere — several racers on one route
        /// share one (it is a property of the ribbon and the hull, not of the pilot).</summary>
        public void UseRacingLine(SkimRacingLine line) => _line = line;

        /// <summary>The racing line this brain flies — for tooling, and for sharing with other racers
        /// on the same route.</summary>
        public SkimRacingLine RacingLine => _line;

        /// <summary>Forget where the ship is; the next <see cref="Tick"/> picks it up from scratch.
        /// Call on a respawn or a teleport.</summary>
        public void Reset()
        {
            _initialized = false;
            _keys.Clear();
            _haveCrystal = false;
            _crystalPlanned = false;
            _crystalS = float.NaN;
            _crystalSeenAt = -1f;
        }

        public SkimRacerCommand Tick(in SkimRacerSensors sensors, IReadOnlyList<SkimObstacle> obstacles)
        {
            _sensors = sensors;
            _obstacles = obstacles;
            float v = Mathf.Max(sensors.Speed, 1f);

            if (!_initialized)
            {
                if (_line == null)
                    _line = new SkimRacingLine(_route, _p.NominalHeight, _p.LaneWidth, HullReach, _p.LaneMargin,
                        sensors.SkimRadius, sensors.HullHalfExtents.z + _p.ClearanceMargin,
                        sensors.ThrottleScaler * Mathf.Max(1f, sensors.MaxBoost), sensors.FollowRate, _p.BandWeight);
                _s = _route.ProjectGlobal(sensors.Position);
                _initialized = true;
                _throttle = Mathf.Clamp01(_p.Throttle);
                ObserveCrystal(force: true);
                if (_p.UseOptimizer) Optimize(fromShip: true, cold: true);
                else Replan(fromShip: true);
            }

            _s = _route.Project(sensors.Position, _s, 60f, 60f + v * sensors.DeltaTime * 4f);
            PruneKeys();

            if (_p.UseOptimizer)
            {
                TickOptimized(obstacles);
                UpdateThrottle();
                return Steer();
            }

            bool replan = ObserveCrystal(force: false);
            // A plan sized for the speed the ship had is too tight for the speed it has now.
            if (sensors.Speed > _planSpeed * 1.2f + 10f) replan = true;
            if (OffPlan())
            {
                Replan(fromShip: true);
                Reanchors++;
            }
            else if (replan)
            {
                Replan(fromShip: false);
            }

            if (obstacles != null && obstacles.Count > 0) Avoid(obstacles);
            UpdateThrottle();
            return Steer();
        }

        // ============================================================================== speed

        float TurnRateRad => Mathf.Min(_sensors.PitchRateDegrees, _sensors.YawRateDegrees) * Mathf.Deg2Rad;
        float Follow => Mathf.Max(0.05f, _sensors.FollowRate);

        /// <summary>The speed the ship will fly a manoeuvre planned NOW at: its current speed, or
        /// the speed its throttle is already driving it to at the boost it has.</summary>
        float SpeedNow()
        {
            float target = _sensors.ThrottleScaler * Mathf.Clamp01(_p.Throttle) * Mathf.Max(1f, _sensors.BoostMultiplier);
            return Mathf.Max(Mathf.Max(_sensors.Speed, target), 30f);
        }

        /// <summary>The top speed the pilot's throttle reaches once skimming.</summary>
        float SpeedTop()
        {
            float top = _sensors.ThrottleScaler * Mathf.Clamp01(_p.Throttle) * Mathf.Max(1f, _sensors.MaxBoost);
            return Mathf.Max(SpeedNow(), top);
        }

        /// <summary>
        /// The speed the ship will have after <paramref name="distance"/> more units of skimming. The
        /// economy, per the shipped effects: each prism entered adds 0.1 to the boost multiplier,
        /// which decays 0.3 / s above 1 and caps at MaxBoost, and the throttle target is
        /// <c>ThrottleScaler x Throttle x boost</c>, chased at the thrust lerp's 1.5 / s. Optimistic
        /// (every prism skimmed), which is the safe direction for sizing: a line planned for a faster
        /// ship is only gentler for a slower one.
        /// </summary>
        float PredictSkimSpeed(float distance)
        {
            float v = Mathf.Max(_sensors.Speed, 1f);
            float b = Mathf.Max(1f, _sensors.BoostMultiplier);
            float max = Mathf.Max(1f, _sensors.MaxBoost);
            float throttle = Mathf.Clamp01(_p.Throttle);
            float perUnit = AddPerHit / Mathf.Max(1f, _route.MeanSpacing);
            const float dt = 0.05f;
            float x = 0f;
            for (int i = 0; i < 400 && x < distance; i++)
            {
                b = Mathf.Min(max, b + (perUnit * v - (b > 1f ? BoostDecay : 0f)) * dt);
                v += (_sensors.ThrottleScaler * throttle * b - v) * Follow * dt;
                x += v * dt;
            }
            return Mathf.Max(v, 30f);
        }

        /// <summary>
        /// Seconds a ship at speed <paramref name="v"/> and boost <paramref name="bm"/> will lose,
        /// against one already at full speed, by the time unbroken skimming has brought it there —
        /// what arriving somewhere slow still costs AFTER the plan ends.
        ///
        /// <para><b>Simulated, because the economy is not linear.</b> Boost grows at
        /// <c>prisms per second x 0.1 - 0.3</c>, and prisms per second is the speed — so a ship at
        /// boost 1 gains 0.2 a second while one at boost 4 gains 1.7: the ramp is EXPONENTIAL. A
        /// quadratic in the boost deficit (the first cut) priced a ship at boost 1 at 1.2 s behind,
        /// where the ramp really costs it over 4 s, and so it saw no reason to keep skimming at the
        /// start — which is exactly where every swing out of the skim band is most expensive.</para>
        /// </summary>
        float RampCost(float v, float bm)
        {
            float thr = Mathf.Clamp01(_p.Throttle);
            float bmMax = Mathf.Max(1f, _sensors.MaxBoost);
            float vFull = _sensors.ThrottleScaler * thr * bmMax;
            if (vFull < 1f) return 0f;
            float perUnit = AddPerHit / Mathf.Max(1f, _route.MeanSpacing);
            const float dt = 0.1f;
            float lost = 0f;
            bm = Mathf.Clamp(bm, 1f, bmMax);
            for (int i = 0; i < 300; i++)
            {
                float deficit = 1f - v / vFull;
                if (deficit < 0.002f && bm >= bmMax - 0.01f) break;
                lost += Mathf.Max(0f, deficit) * dt;
                bm = Mathf.Min(bmMax, bm + (perUnit * v - BoostDecay) * dt);
                if (bm < 1f) bm = 1f;
                v += (_sensors.ThrottleScaler * thr * bm - v) * (1f - Mathf.Exp(-Follow * dt));
            }
            return lost;
        }

        /// <summary>Largest speed at which a line with turn rate <c>a v</c> and turn-onset
        /// <c>b v²</c> (per unit speed) fits in <paramref name="budget"/> rad/s of stick.</summary>
        static float VMax(float a, float b, float budget)
            => b > 1e-7f
                ? (-a + Mathf.Sqrt(a * a + 4f * b * budget)) / (2f * b)
                : (a > 1e-7f ? budget / a : float.MaxValue);

        // ============================================================================== geometry

        /// <summary>Radius a hull at any roll reaches from its centre line, in the ribbon's
        /// cross-section.</summary>
        float HullReach
        {
            get
            {
                Vector3 h = _sensors.HullHalfExtents;
                return Mathf.Sqrt(h.x * h.x + h.y * h.y);
            }
        }

        /// <summary>
        /// Smallest <c>rho</c> at angle <paramref name="phi"/> that keeps a hull of any roll clear of
        /// the ribbon's envelope at arc <paramref name="s"/>. The envelope is the shells' bounding box
        /// (half-width A, half-thickness B in the plates' frame); outside it on either axis is outside
        /// it, hence the min.
        /// </summary>
        float ClearRho(float phi, float s)
        {
            _route.Envelope(s, _sensors.HullHalfExtents.z + _p.ClearanceMargin, out float hw, out float hh);
            float reach = HullReach + _p.ClearanceMargin;
            float sin = Mathf.Abs(Mathf.Sin(phi)), cos = Mathf.Abs(Mathf.Cos(phi));
            float byX = sin > 1e-4f ? (hw + reach) / sin : float.MaxValue;
            float byU = cos > 1e-4f ? (hh + reach) / cos : float.MaxValue;
            return Mathf.Min(byX, byU);
        }

        /// <summary>
        /// Largest <c>rho</c> at angle <paramref name="phi"/> from which the skimmer still reaches the
        /// ribbon at arc <paramref name="s"/>: the shells' bounding box grown by the skim radius, less
        /// half a unit. Between <see cref="ClearRho"/> and this is the SKIM BAND — a ring all the way
        /// round the ribbon, where the ship both clears the plates and is paid for them.
        /// </summary>
        float SkimRho(float phi, float s)
        {
            _route.Envelope(s, 0f, out float hw, out float hh);
            float reach = Mathf.Max(0f, _sensors.SkimRadius - 0.5f);
            float sin = Mathf.Abs(Mathf.Sin(phi)), cos = Mathf.Abs(Mathf.Cos(phi));
            float byX = sin > 1e-4f ? (hw + reach) / sin : float.MaxValue;
            float byU = cos > 1e-4f ? (hh + reach) / cos : float.MaxValue;
            return Mathf.Min(byX, byU);
        }

        /// <summary>The face (0 over, 1 under) an angle is nearer.</summary>
        static int FaceOf(float phi)
        {
            int k = Mathf.RoundToInt(phi / Mathf.PI);
            return ((k % 2) + 2) % 2;
        }

        /// <summary>The centre angle of <paramref name="lane"/>'s face nearest <paramref name="near"/>
        /// — which way round the ribbon a lane is reached.</summary>
        static float LaneAngleNear(int lane, float near)
        {
            float centre = lane * Mathf.PI;
            return centre + Mathf.Round((near - centre) / (2f * Mathf.PI)) * 2f * Mathf.PI;
        }

        static float Unwrap(float angle, float near)
            => angle + Mathf.Round((near - angle) / (2f * Mathf.PI)) * 2f * Mathf.PI;

        /// <summary>True when an angular move from <paramref name="a"/> to <paramref name="b"/> goes
        /// round one of the ribbon's edges (an odd multiple of 90° lies between them).</summary>
        static bool CrossesEdge(float a, float b, out float edge)
        {
            float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            float k = Mathf.Ceil(lo / Mathf.PI - 0.5f);
            edge = (k + 0.5f) * Mathf.PI;
            return edge < hi;
        }

        /// <summary>Polar state from a Cartesian one (a across the plates, b along their up, with
        /// slopes and curvatures per unit arc).</summary>
        static void ToPolar(float a, float b, float da, float db, float dda, float ddb, float phiNear,
                            out float rho, out float phi, out float dRho, out float dPhi, out float ddRho, out float ddPhi)
        {
            rho = Mathf.Max(1e-3f, Mathf.Sqrt(a * a + b * b));
            phi = Unwrap(Mathf.Atan2(a, b), phiNear);
            float sn = a / rho, cs = b / rho;
            dRho = da * sn + db * cs;
            dPhi = (da * cs - db * sn) / rho;
            ddRho = dda * sn + ddb * cs + rho * dPhi * dPhi;
            ddPhi = (dda * cs - ddb * sn - 2f * dRho * dPhi) / rho;
        }

        static void ToCartesian(in Key k, out float a, out float b, out float da, out float db)
        {
            float sn = Mathf.Sin(k.Phi), cs = Mathf.Cos(k.Phi);
            a = k.Rho * sn;
            b = k.Rho * cs;
            da = k.DRho * sn + k.Rho * k.DPhi * cs;
            db = k.DRho * cs - k.Rho * k.DPhi * sn;
        }

        // ============================================================================== the line

        /// <summary>Quintic Hermite: value and first/second derivative (per unit arc) at
        /// <paramref name="t"/> in [0,1] of a span <paramref name="len"/> long.</summary>
        static float Hermite(float p0, float v0, float a0, float p1, float v1, float a1, float len, float t,
                             out float d1, out float d2)
        {
            float t2 = t * t, t3 = t2 * t, t4 = t3 * t, t5 = t4 * t;
            float V0 = v0 * len, A0 = a0 * len * len, V1 = v1 * len, A1 = a1 * len * len;

            float h0 = 1f - 10f * t3 + 15f * t4 - 6f * t5;
            float h1 = t - 6f * t3 + 8f * t4 - 3f * t5;
            float h2 = 0.5f * t2 - 1.5f * t3 + 1.5f * t4 - 0.5f * t5;
            float h3 = 0.5f * t3 - t4 + 0.5f * t5;
            float h4 = -4f * t3 + 7f * t4 - 3f * t5;
            float h5 = 10f * t3 - 15f * t4 + 6f * t5;

            float g0 = -30f * t2 + 60f * t3 - 30f * t4;
            float g1 = 1f - 18f * t2 + 32f * t3 - 15f * t4;
            float g2 = t - 4.5f * t2 + 6f * t3 - 2.5f * t4;
            float g3 = 1.5f * t2 - 4f * t3 + 2.5f * t4;
            float g4 = -12f * t2 + 28f * t3 - 15f * t4;
            float g5 = 30f * t2 - 60f * t3 + 30f * t4;

            float k0 = -60f * t + 180f * t2 - 120f * t3;
            float k1 = -36f * t + 96f * t2 - 60f * t3;
            float k2 = 1f - 9f * t + 18f * t2 - 10f * t3;
            float k3 = 3f * t - 12f * t2 + 10f * t3;
            float k4 = -24f * t + 84f * t2 - 60f * t3;
            float k5 = 60f * t - 180f * t2 + 120f * t3;

            float inv = 1f / len;
            d1 = (g0 * p0 + g1 * V0 + g2 * A0 + g3 * A1 + g4 * V1 + g5 * p1) * inv;
            d2 = (k0 * p0 + k1 * V0 + k2 * A0 + k3 * A1 + k4 * V1 + k5 * p1) * inv * inv;
            return h0 * p0 + h1 * V0 + h2 * A0 + h3 * A1 + h4 * V1 + h5 * p1;
        }

        /// <summary>Full polar state of <paramref name="lane"/>'s racing line at arc
        /// <paramref name="s"/>, its angle unwrapped near <paramref name="phiNear"/>.</summary>
        void LaneState(int lane, float s, float phiNear, out float rho, out float phi,
                       out float dRho, out float dPhi, out float ddRho, out float ddPhi)
        {
            const float h = LaneStep;
            _line.Offset(lane, s - h, out float a0, out float b0);
            _line.Offset(lane, s, out float a, out float b);
            _line.Offset(lane, s + h, out float a2, out float b2);
            ToPolar(a, b, (a2 - a0) / (2f * h), (b2 - b0) / (2f * h),
                (a2 - 2f * a + a0) / (h * h), (b2 - 2f * b + b0) / (h * h), phiNear,
                out rho, out phi, out dRho, out dPhi, out ddRho, out ddPhi);
        }

        Key LaneKey(int lane, float s, float phiNear)
        {
            LaneState(lane, s, phiNear, out float r, out float p, out float dr, out float dp, out float ddr, out float ddp);
            return new Key { S = s, Rho = r, Phi = p, DRho = dr, DPhi = dp, DDRho = ddr, DDPhi = ddp, Kind = KeyKind.Lane, Lane = (sbyte)lane };
        }

        /// <summary>The lane the line at <paramref name="s"/> is ON, or -1.</summary>
        static int LaneAt(List<Key> keys, float s)
        {
            int n = keys.Count;
            if (n == 0) return -1;
            if (s >= keys[n - 1].S) return keys[n - 1].Lane;
            if (s <= keys[0].S) return -1;
            int i = 0;
            while (i + 1 < n && keys[i + 1].S <= s) i++;
            return keys[i].Lane >= 0 && keys[i].Lane == keys[i + 1].Lane ? keys[i].Lane : -1;
        }

        /// <summary>
        /// Full state of the key list at arc <paramref name="s"/>. Before the first key the line
        /// continues along that key's own slope and curvature (so a plan anchored under the ship is
        /// smooth behind it too); between two keys on one lane, and past a last key on a lane, it is
        /// the lane; past any other last key it holds.
        /// </summary>
        void State(List<Key> keys, float s, out float rho, out float phi,
                   out float dRho, out float dPhi, out float ddRho, out float ddPhi)
        {
            int n = keys.Count;
            if (n == 0)
            {
                LaneState(0, s, 0f, out rho, out phi, out dRho, out dPhi, out ddRho, out ddPhi);
                return;
            }
            if (s <= keys[0].S)
            {
                var k = keys[0];
                float d = Mathf.Max(s - k.S, -60f);
                rho = k.Rho + k.DRho * d + 0.5f * k.DDRho * d * d;
                phi = k.Phi + k.DPhi * d + 0.5f * k.DDPhi * d * d;
                dRho = k.DRho + k.DDRho * d;
                dPhi = k.DPhi + k.DDPhi * d;
                ddRho = k.DDRho;
                ddPhi = k.DDPhi;
                return;
            }
            if (s >= keys[n - 1].S)
            {
                var k = keys[n - 1];
                if (k.Lane >= 0)
                {
                    LaneState(k.Lane, s, k.Phi, out rho, out phi, out dRho, out dPhi, out ddRho, out ddPhi);
                    return;
                }
                rho = k.Rho; phi = k.Phi;
                dRho = dPhi = ddRho = ddPhi = 0f;
                return;
            }
            int i = 0;
            while (i + 1 < n && keys[i + 1].S <= s) i++;
            var a = keys[i];
            var b = keys[i + 1];
            if (a.Lane >= 0 && a.Lane == b.Lane)
            {
                LaneState(a.Lane, s, a.Phi, out rho, out phi, out dRho, out dPhi, out ddRho, out ddPhi);
                return;
            }
            float len = Mathf.Max(1e-3f, b.S - a.S);
            float t = Mathf.Clamp01((s - a.S) / len);
            rho = Hermite(a.Rho, a.DRho, a.DDRho, b.Rho, b.DRho, b.DDRho, len, t, out dRho, out ddRho);
            phi = Hermite(a.Phi, a.DPhi, a.DDPhi, b.Phi, b.DPhi, b.DDPhi, len, t, out dPhi, out ddPhi);
        }

        void Offset(List<Key> keys, float s, out float rho, out float phi)
            => State(keys, s, out rho, out phi, out _, out _, out _, out _);

        Key StateKey(List<Key> keys, float s, KeyKind kind)
        {
            State(keys, s, out float r, out float p, out float dr, out float dp, out float ddr, out float ddp);
            return new Key { S = s, Rho = r, Phi = p, DRho = dr, DPhi = dp, DDRho = ddr, DDPhi = ddp, Kind = kind, Lane = -1 };
        }

        /// <summary>World point of the line at <paramref name="s"/>: the plan's offset, the pilot's
        /// wander on top.</summary>
        Vector3 LinePoint(List<Key> keys, float s) => LinePoint(keys, s, out _, out _);

        Vector3 LinePoint(List<Key> keys, float s, out float rho, out float phi)
        {
            _route.Frame(s, out Vector3 c, out _, out Vector3 right, out Vector3 up);
            Offset(keys, s, out rho, out phi);
            float a = rho * Mathf.Sin(phi), b = rho * Mathf.Cos(phi);
            if (_p.LineWander > 0f)
            {
                float lambda = Mathf.Max(60f, _p.LineWanderSeconds * _sensors.ThrottleScaler * Mathf.Max(1f, _sensors.MaxBoost));
                a += _p.LineWander * Mathf.Sin(s / lambda * 2f * Mathf.PI + _wanderPhaseA);
                float lift = 0.5f + 0.5f * Mathf.Sin(s / (lambda * 1.37f) * 2f * Mathf.PI + _wanderPhaseB);
                b += (Mathf.Cos(phi) >= 0f ? 1f : -1f) * _p.LineWander * 0.6f * lift;
            }
            return c + right * a + up * b;
        }

        /// <summary>Unit direction from the nearest point of the plates to the line at
        /// <paramref name="s"/> — where the ship's belly points. Over a face that is the face's own
        /// normal whatever the line's lateral cut; past an edge it turns toward the edge.</summary>
        Vector3 BellyDirection(float s)
        {
            _route.Frame(s, out _, out _, out Vector3 right, out Vector3 up);
            Offset(_keys, s, out float rho, out float phi);
            float a = rho * Mathf.Sin(phi), b = rho * Mathf.Cos(phi);
            _route.Envelope(s, 0f, out float hw, out float hh);
            float ea = a - Mathf.Clamp(a, -hw, hw), eb = b - Mathf.Clamp(b, -hh, hh);
            if (ea * ea + eb * eb < 0.25f)
            {
                ea = 0f;
                eb = b >= 0f ? 1f : -1f;
            }
            return (right * ea + up * eb).normalized;
        }

        Vector3 TangentAt(List<Key> keys, float s)
        {
            Vector3 d = LinePoint(keys, s + SampleStep) - LinePoint(keys, s - SampleStep);
            return d.sqrMagnitude > 1e-8f ? d.normalized : _sensors.Rotation * Vector3.forward;
        }

        /// <summary>
        /// Samples the line <paramref name="keys"/> at <paramref name="count"/> points
        /// <c>from + i SampleStep</c> and fills <see cref="_t"/>, <see cref="_k"/> (tangent and
        /// curvature vector dT/ds) with <paramref name="window"/> extra samples of curvature on either
        /// side, for <see cref="Demand"/> to difference across. Sample <c>i</c>'s raw point and offset
        /// are at <c>_raw[i + _rawPad]</c>, <c>_rawRho</c>, <c>_rawPhi</c>.
        ///
        /// <para><b>Smoothed before it is differenced, and it has to be.</b> The ribbon's centre line
        /// is a chain of 12 u chords that kink ~3° at every prism, and the term that matters most at
        /// speed — how fast the curvature CHANGES, <c>(v² / k) dκ/ds</c>, 60,000 × dκ/ds at 300 u/s —
        /// reads those kinks as ~5 rad/s of stick; differenced raw, the throttle lifted off on
        /// straight ribbon. A 9-tap binomial at a 6 u step has exactly zero gain at the chords' 12 u
        /// wavelength and passes a 200 u detour at 96 %.</para>
        /// </summary>
        void SampleCurve(List<Key> keys, float from, int count, int window)
        {
            const int filterHalf = 4;
            int pad = filterHalf + 2 + window;
            _raw.Clear();
            _rawRho.Clear();
            _rawPhi.Clear();
            for (int i = -pad; i < count + pad; i++)
            {
                _raw.Add(LinePoint(keys, from + i * SampleStep, out float r, out float p));
                _rawRho.Add(r);
                _rawPhi.Add(p);
            }
            _rawPad = pad;

            // _smooth[m] is sample index m - (pad - filterHalf).
            _smooth.Clear();
            for (int j = filterHalf; j < _raw.Count - filterHalf; j++)
            {
                Vector3 acc = Vector3.zero;
                for (int k = -filterHalf; k <= filterHalf; k++) acc += _raw[j + k] * Binomial9[k + filterHalf];
                _smooth.Add(acc);
            }
            // _tangents[q] is sample index q - (pad - filterHalf - 1).
            _tangents.Clear();
            for (int m = 1; m < _smooth.Count - 1; m++)
            {
                Vector3 d = _smooth[m + 1] - _smooth[m - 1];
                _tangents.Add(d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward);
            }
            // _t[i] and _k[i] are sample index i - window.
            _t.Clear();
            _k.Clear();
            for (int q = 1; q < _tangents.Count - 1; q++)
            {
                _t.Add(_tangents[q]);
                _k.Add((_tangents[q + 1] - _tangents[q - 1]) / (2f * SampleStep));
            }
            _window = window;
        }

        int _window;
        int _rawPad;

        /// <summary>Samples of curvature the rate-of-change term is differenced across at speed
        /// <paramref name="v"/>: a quarter second of travel. A curvature step shorter than that — the
        /// ribbon's own, at every Catmull-Rom knot — costs the ship a tenth of a second of saturated
        /// stick and a few tenths of a unit of line, not a lifted throttle.</summary>
        static int WindowFor(float v) => Mathf.Clamp(Mathf.RoundToInt(v * 0.125f / SampleStep), 2, 12);

        /// <summary>Stick rate (rad/s, pitch/yaw) the sampled line needs at sample
        /// <paramref name="i"/> at speed <paramref name="v"/>: the ship's turn rate plus what it takes
        /// to change it through the follow lag, the change read across <see cref="WindowFor"/>.</summary>
        float Demand(int i, float v)
        {
            int c = i + _window;
            Vector3 t = _t[c];
            Vector3 dk = (_k[c + _window] - _k[c - _window]) / (2f * _window * SampleStep);
            return (Vector3.Cross(t, _k[c]) * v + Vector3.Cross(t, dk) * (v * v / Follow)).magnitude;
        }

        /// <summary>The fastest the sampled line can be flown at sample <paramref name="i"/> inside
        /// <paramref name="budget"/> rad/s of stick.</summary>
        float SampleVMax(int i, float budget)
        {
            int c = i + _window;
            float a = Vector3.Cross(_t[c], _k[c]).magnitude;
            Vector3 dk = (_k[c + _window] - _k[c - _window]) / (2f * _window * SampleStep);
            float b = Vector3.Cross(_t[c], dk).magnitude / Follow;
            return VMax(a, b, budget);
        }

        static readonly float[] Binomial9 =
        {
            1f / 256f, 8f / 256f, 28f / 256f, 56f / 256f, 70f / 256f, 56f / 256f, 28f / 256f, 8f / 256f, 1f / 256f,
        };

        readonly List<Vector3> _raw = new List<Vector3>(512);
        readonly List<float> _rawRho = new List<float>(512);
        readonly List<float> _rawPhi = new List<float>(512);
        readonly List<Vector3> _smooth = new List<Vector3>(512);
        readonly List<Vector3> _tangents = new List<Vector3>(512);
        readonly List<Vector3> _t = new List<Vector3>(512);
        readonly List<Vector3> _k = new List<Vector3>(512);
        float[] _cap = new float[256];

        /// <summary>Tangent, curvature vector and its rate of change (read across the speed's
        /// window) at one arc.</summary>
        void Curvature(List<Key> keys, float s, float v, out Vector3 t, out Vector3 k, out Vector3 dk)
        {
            int w = WindowFor(v);
            SampleCurve(keys, s, 1, w);
            t = _t[w];
            k = _k[w];
            dk = (_k[2 * w] - _k[0]) / (2f * w * SampleStep);
        }

        /// <summary>Tooling: the stick rate the current plan needs at every sample over
        /// [<paramref name="from"/>, <paramref name="to"/>] at speed <paramref name="v"/>, and the part
        /// of it that is the turn rate alone.</summary>
        public void ProfilePlan(float from, float to, float v, List<float> rates, List<float> turnRates = null)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt((to - from) / SampleStep)) + 1;
            SampleCurve(_keys, from, count, WindowFor(v));
            rates.Clear();
            turnRates?.Clear();
            for (int i = 0; i < count; i++)
            {
                rates.Add(Demand(i, v));
                turnRates?.Add(Vector3.Cross(_t[i + _window], _k[i + _window]).magnitude * v);
            }
        }

        // ============================================================================== keys

        void PruneKeys()
        {
            // Keep the last key at or behind the ship: it is the left end of the segment it is on.
            int lastBehind = -1;
            for (int i = 0; i < _keys.Count; i++)
                if (_keys[i].S <= _s) lastBehind = i;
            if (lastBehind > 0) _keys.RemoveRange(0, lastBehind);
        }

        /// <summary>
        /// Where the segment between keys <paramref name="index"/> and the next would bring the hull
        /// inside the envelope, insert a key at the worst point — the segment's own angular slope and
        /// curvature kept, only lifted clear — and recurse on both halves. This is what turns "get to
        /// the other face" into "go round the edge". A lane segment is clear by construction.
        /// </summary>
        void Subdivide(List<Key> keys, int index, int depth)
        {
            if (depth > 4 || index + 1 >= keys.Count) return;
            var a = keys[index];
            var b = keys[index + 1];
            if (b.S - a.S < 8f) return;
            if (a.Lane >= 0 && a.Lane == b.Lane) return;
            float worst = 0f, worstS = float.NaN;
            for (int i = 1; i < 16; i++)
            {
                float s = Mathf.Lerp(a.S, b.S, i / 16f);
                Offset(keys, s, out float r, out float p);
                float deficit = ClearRho(p, s) + 0.25f - r;
                if (deficit > worst) { worst = deficit; worstS = s; }
            }
            if (float.IsNaN(worstS)) return;
            var mid = StateKey(keys, worstS, KeyKind.Edge);
            mid.Rho = ClearRho(mid.Phi, worstS) + EdgeLift;
            // Keep the radial slope across the lift the segment already had, less what took it in.
            mid.DRho = 0f;
            mid.DDRho = 0f;
            keys.Insert(index + 1, mid);
            Subdivide(keys, index + 1, depth + 1);
            Subdivide(keys, index, depth + 1);
        }

        void SubdivideAll(List<Key> keys)
        {
            for (int i = keys.Count - 2; i >= 0; i--) Subdivide(keys, i, 0);
        }

        // ============================================================================== planning

        /// <summary>The ship's own offset and heading as a key: where a plan starts when the ship is
        /// not on the old one.</summary>
        Key ShipKey()
        {
            _route.Frame(_s, out Vector3 c, out Vector3 tangent, out Vector3 right, out Vector3 up);
            Vector3 d = _sensors.Position - c;
            float a = Vector3.Dot(d, right), b = Vector3.Dot(d, up);
            Vector3 fwd = _sensors.Rotation * Vector3.forward;
            float along = Mathf.Max(0.25f, Vector3.Dot(fwd, tangent));
            float da = Mathf.Clamp(Vector3.Dot(fwd, right) / along, -3f, 3f);
            float db = Mathf.Clamp(Vector3.Dot(fwd, up) / along, -3f, 3f);
            float near = 0f;
            if (_keys.Count > 0) Offset(_keys, _s, out _, out near);
            ToPolar(a, b, da, db, 0f, 0f, near, out float rho, out float phi, out float dRho, out float dPhi, out _, out _);
            return new Key
            {
                S = _s,
                Rho = Mathf.Max(1f, rho),
                Phi = phi,
                DRho = dRho,
                DPhi = dPhi,
                Kind = KeyKind.Anchor,
                Lane = -1,
            };
        }

        bool OffPlan()
        {
            if (_keys.Count == 0) return true;
            _route.Frame(_s, out Vector3 c, out _, out Vector3 right, out Vector3 up);
            Vector3 d = _sensors.Position - c;
            float av = Vector3.Dot(d, right), bv = Vector3.Dot(d, up);
            Offset(_keys, _s, out float r, out float p);
            float ap = r * Mathf.Sin(p), bp = r * Mathf.Cos(p);
            float dx = av - ap, dy = bv - bp;
            return dx * dx + dy * dy > _p.ReanchorDistance * _p.ReanchorDistance;
        }

        /// <summary>
        /// Length a segment from <paramref name="a"/> to <paramref name="b"/>'s offset needs so the
        /// manoeuvre itself never asks for more than the plan's share of stick at speed
        /// <paramref name="v"/>: the closed form for a jerk-limited move, <c>T³ ≥ 60 Δ / (v k f U)</c>
        /// (the quintic's peak jerk, through the follow lag), or the acceleration bound
        /// <c>T² ≥ 5.8 Δ / (v f U)</c>, whichever is longer.
        /// </summary>
        float SizeSegment(in Key a, in Key b, float v)
        {
            float delta = Mathf.Abs(b.Rho - a.Rho) + 0.5f * (a.Rho + b.Rho) * Mathf.Abs(b.Phi - a.Phi)
                        + 20f * (Mathf.Abs(a.DRho) + a.Rho * Mathf.Abs(a.DPhi));
            delta = Mathf.Max(delta, 0.5f);
            float budget = _p.PlanAuthority * TurnRateRad;
            float jerkT = Mathf.Pow(60f * delta / (v * Follow * budget), 1f / 3f);
            float accT = Mathf.Sqrt(5.8f * delta / (v * budget));
            return Mathf.Max(30f, v * Mathf.Max(jerkT, accT));
        }

        /// <summary><see cref="SizeSegment"/>, counting the lift round an edge when the move changes
        /// face: the hull has to clear the plates' edge on the way, which is a move of its own.</summary>
        float SegmentLength(in Key a, in Key b, float v)
        {
            if (!CrossesEdge(a.Phi, b.Phi, out float edge)) return SizeSegment(a, b, v);
            float sMid = 0.5f * (a.S + b.S);
            var e = new Key { S = sMid, Rho = ClearRho(edge, sMid) + EdgeLift, Phi = edge, Lane = -1 };
            return SizeSegment(a, e, v) + SizeSegment(e, b, v);
        }

        /// <summary>True when <paramref name="k"/> is already flying <paramref name="lane"/>: on it,
        /// and heading along it.</summary>
        bool OnLane(in Key k, int lane)
        {
            if (k.Lane == lane) return true;
            var l = LaneKey(lane, k.S, k.Phi);
            ToCartesian(k, out float a, out float b, out float da, out float db);
            ToCartesian(l, out float la, out float lb, out float lda, out float ldb);
            return (a - la) * (a - la) + (b - lb) * (b - lb) < 0.3f && Mathf.Abs(da - lda) + Mathf.Abs(db - ldb) < 0.02f;
        }

        /// <summary>The crystal's offset in the plates' frame at arc <paramref name="sc"/>.</summary>
        void CrystalOffset(float sc, out float a, out float b)
        {
            _route.Frame(sc, out Vector3 c, out _, out Vector3 right, out Vector3 up);
            Vector3 d = _crystal - c;
            a = Vector3.Dot(d, right);
            b = Vector3.Dot(d, up);
        }

        bool ClearAt(float a, float b, float s)
        {
            float rho = Mathf.Sqrt(a * a + b * b);
            return rho >= ClearRho(Mathf.Atan2(a, b), s) + 0.5f;
        }

        /// <summary>
        /// The point of the crystal's capture circle (radius PassRadius around it, in the plates'
        /// frame) nearest <paramref name="toA"/>,<paramref name="toB"/> that the hull clears. The
        /// circle is searched, not projected onto: a crystal beside a waypoint marker — a plate 60 u
        /// wide — has its nearest point inside the marker.
        /// </summary>
        bool NearestPass(float sc, float ac, float bc, float toA, float toB, out float pa, out float pb)
        {
            float r = _p.PassRadius;
            float best = float.MaxValue;
            pa = pb = 0f;
            bool found = false;
            // The lane point may already be inside the circle — then it IS the pass.
            float dl = Mathf.Sqrt((toA - ac) * (toA - ac) + (toB - bc) * (toB - bc));
            if (dl <= r && ClearAt(toA, toB, sc)) { pa = toA; pb = toB; return true; }
            for (int ring = 2; ring <= 4; ring++)
            {
                float rr = r * ring / 4f;
                for (int k = 0; k < 32; k++)
                {
                    float th = k * (2f * Mathf.PI / 32f);
                    float qa = ac + rr * Mathf.Cos(th), qb = bc + rr * Mathf.Sin(th);
                    if (!ClearAt(qa, qb, sc)) continue;
                    float d = (qa - toA) * (qa - toA) + (qb - toB) * (qb - toB);
                    if (d < best) { best = d; pa = qa; pb = qb; found = true; }
                }
            }
            return found;
        }

        /// <summary>
        /// The capture-circle point that leaves the ship LEAST far outside the skim band — a crystal
        /// beside the ribbon is taken without leaving it at all — then nearest the lane point.
        /// </summary>
        bool BandPass(float sc, float ac, float bc, float laneA, float laneB, out float pa, out float pb)
        {
            float best = float.MaxValue;
            pa = pb = 0f;
            bool found = false;
            for (int ring = 0; ring <= 4; ring++)
            {
                float r = _p.PassRadius * ring / 4f;
                int spokes = ring == 0 ? 1 : 24;
                for (int k = 0; k < spokes; k++)
                {
                    float th = k * (2f * Mathf.PI / spokes);
                    float qa = ac + r * Mathf.Cos(th), qb = bc + r * Mathf.Sin(th);
                    float qr = Mathf.Sqrt(qa * qa + qb * qb);
                    float qp = Mathf.Atan2(qa, qb);
                    if (qr < ClearRho(qp, sc) + 0.5f) continue;
                    float outside = Mathf.Max(0f, qr - SkimRho(qp, sc));
                    float fromLane = Mathf.Sqrt((qa - laneA) * (qa - laneA) + (qb - laneB) * (qb - laneB));
                    float cost = outside + 0.15f * fromLane + 0.05f * r;
                    if (cost < best) { best = cost; pa = qa; pb = qb; found = true; }
                }
            }
            return found;
        }

        List<Key> NewCandidate(bool targetsCrystal, float speed)
        {
            if (_candCount >= MaxCandidates) return null;
            var list = _cands[_candCount];
            list.Clear();
            _candTargets[_candCount] = targetsCrystal;
            _candSpeeds[_candCount] = speed;
            _candCount++;
            return list;
        }

        /// <summary>A pass key at the crystal's arc, heading from its predecessor to its successor
        /// (its slope is the chord between them, its curvature zero), so the line crosses the
        /// capture circle instead of stopping in it.</summary>
        static void SetPassSlope(List<Key> keys, int i)
        {
            if (i <= 0 || i + 1 >= keys.Count) return;
            ToCartesian(keys[i - 1], out float pa, out float pb, out _, out _);
            ToCartesian(keys[i + 1], out float na, out float nb, out _, out _);
            var k = keys[i];
            ToCartesian(k, out float a, out float b, out _, out _);
            float span = Mathf.Max(1f, keys[i + 1].S - keys[i - 1].S);
            ToPolar(a, b, (na - pa) / span, (nb - pb) / span, 0f, 0f, k.Phi,
                out _, out _, out float dr, out float dp, out float ddr, out float ddp);
            k.DRho = dr; k.DPhi = dp; k.DDRho = ddr; k.DDPhi = ddp;
            keys[i] = k;
        }

        /// <summary>Anchor, then onto <paramref name="lane"/>. The home plan when there is nothing
        /// to go and get.</summary>
        void AddHome(in Key anchor, int lane, float vNow)
        {
            var list = NewCandidate(false, vNow);
            if (list == null) return;
            list.Add(anchor);
            float phiNear = LaneAngleNear(lane, anchor.Phi);
            if (OnLane(anchor, lane))
            {
                var a = anchor;
                a.Lane = (sbyte)lane;
                list[0] = a;
                list.Add(LaneKey(lane, anchor.S + 30f, phiNear));
                return;
            }
            var guess = LaneKey(lane, anchor.S + 100f, phiNear);
            float len = SegmentLength(anchor, guess, vNow);
            list.Add(LaneKey(lane, anchor.S + len, phiNear));
            SubdivideAll(list);
        }

        /// <summary>
        /// Rest on <paramref name="lane"/> (skimming) until the last moment, swing to the pass point
        /// <paramref name="pa"/>,<paramref name="pb"/>, and settle on the lane nearest it. Where there
        /// is no room for both swings at their own length, touch down on the lane between them anyway,
        /// both compressed in proportion — a tenth of a second of skimming at full boost pays back a
        /// second of decay.
        /// </summary>
        void AddRest(in Key anchor, int lane, float sc, float pa, float pb, float vNow, float vTop)
        {
            float phiLane = LaneAngleNear(lane, anchor.Phi);
            float room = sc - anchor.S;
            var pass = PassKey(sc, pa, pb, phiLane);
            bool onLane = OnLane(anchor, lane);
            float len1 = onLane ? 0f : SegmentLength(anchor, LaneKey(lane, anchor.S + 100f, phiLane), vNow);

            float vRise = vTop;
            float riseLen = SegmentLength(LaneKey(lane, sc - 150f, phiLane), pass, vRise);
            for (int iter = 0; iter < 2; iter++)
            {
                vRise = PredictSkimSpeed(Mathf.Max(0f, room - riseLen - len1));
                riseLen = SegmentLength(LaneKey(lane, sc - riseLen, phiLane), pass, vRise);
            }

            float touch;
            bool both = anchor.S + len1 + 1f <= sc - riseLen;
            if (both) touch = float.NaN;
            else if (len1 > 0f && room > 80f) touch = anchor.S + Mathf.Clamp(room * len1 / (len1 + riseLen), 30f, room - 30f);
            else return;                                    // no room to rest: that is a direct plan

            var list = NewCandidate(true, vRise);
            if (list == null) return;
            var a0 = anchor;
            if (onLane) a0.Lane = (sbyte)lane;
            list.Add(a0);
            if (both)
            {
                if (len1 > 0f) list.Add(LaneKey(lane, anchor.S + len1, phiLane));
                list.Add(LaneKey(lane, sc - riseLen, phiLane));
            }
            else list.Add(LaneKey(lane, touch, phiLane));
            list.Add(pass);
            AddReturn(list, pass, Mathf.Max(vNow, vRise));
            SetPassSlope(list, list.Count - 2);
            SubdivideAll(list);
        }

        /// <summary>Straight from where the ship is to the pass point, then back to a lane: no rest,
        /// the gentlest line when the crystal is close or the lane is far.</summary>
        void AddDirect(in Key anchor, float sc, float pa, float pb, float vNow, float vTop)
        {
            var list = NewCandidate(true, vNow);
            if (list == null) return;
            var pass = PassKey(sc, pa, pb, anchor.Phi);
            list.Add(anchor);
            list.Add(pass);
            AddReturn(list, pass, Mathf.Max(vNow, PredictSkimSpeed(sc - anchor.S)));
            SetPassSlope(list, 1);
            SubdivideAll(list);
        }

        /// <summary>Onto <paramref name="lane"/>, which already passes within reach of the crystal.</summary>
        void AddLaneOnly(in Key anchor, int lane, float sc, float vNow)
        {
            float phiLane = LaneAngleNear(lane, anchor.Phi);
            bool onLane = OnLane(anchor, lane);
            float len1 = onLane ? 30f : SegmentLength(anchor, LaneKey(lane, anchor.S + 100f, phiLane), vNow);
            if (anchor.S + len1 > sc - 10f) return;
            var list = NewCandidate(true, vNow);
            if (list == null) return;
            var a0 = anchor;
            if (onLane) a0.Lane = (sbyte)lane;
            list.Add(a0);
            list.Add(LaneKey(lane, anchor.S + len1, phiLane));
            if (!onLane) SubdivideAll(list);
        }

        Key PassKey(float sc, float pa, float pb, float phiNear)
        {
            float rho = Mathf.Sqrt(pa * pa + pb * pb);
            return new Key { S = sc, Rho = rho, Phi = Unwrap(Mathf.Atan2(pa, pb), phiNear), Kind = KeyKind.Pass, Lane = -1 };
        }

        void AddReturn(List<Key> list, in Key pass, float v)
        {
            int lane = FaceOf(pass.Phi);
            float phiLane = LaneAngleNear(lane, pass.Phi);
            float len = SegmentLength(pass, LaneKey(lane, pass.S + 150f, phiLane), v);
            list.Add(LaneKey(lane, pass.S + len, phiLane));
        }

        /// <summary>
        /// Rebuild the plan from the ship forward, choosing among candidates by
        /// <see cref="Evaluate"/>. The anchor is the old line's full state under the ship (or the
        /// ship itself when it is off that line), so whatever is chosen starts exactly where the ship
        /// already is and already turning.
        /// </summary>
        void Replan(bool fromShip)
        {
            Key anchor;
            if (fromShip || _keys.Count == 0) anchor = ShipKey();
            else
            {
                anchor = StateKey(_keys, _s, KeyKind.Anchor);
                anchor.Lane = (sbyte)LaneAt(_keys, _s);
            }
            anchor.S = _s;
            anchor.Kind = KeyKind.Anchor;
            float vNow = SpeedNow();
            float vTop = SpeedTop();
            _planSpeed = Mathf.Max(_sensors.Speed, 1f);
            int face = FaceOf(anchor.Phi);
            _candCount = 0;

            bool haveTarget = _haveCrystal && !float.IsNaN(_crystalS) && _crystalS - anchor.S > 40f;
            if (haveTarget)
            {
                float sc = _crystalS;
                CrystalOffset(sc, out float ac, out float bc);
                ToCartesian(anchor, out float a0, out float b0, out float da0, out float db0);
                // Where the ship would be at the crystal if it kept its current slope — what a
                // direct swing bends least from.
                float ahead = sc - anchor.S;
                float ea = a0 + Mathf.Clamp(da0 * ahead, -40f, 40f), eb = b0 + Mathf.Clamp(db0 * ahead, -40f, 40f);
                for (int li = 0; li < 2; li++)
                {
                    int lane = li == 0 ? face : 1 - face;
                    _line.Offset(lane, sc, out float la, out float lb);
                    if ((la - ac) * (la - ac) + (lb - bc) * (lb - bc) <= (_p.PassRadius - 1f) * (_p.PassRadius - 1f))
                    {
                        AddLaneOnly(anchor, lane, sc, vNow);
                        continue;
                    }
                    if (NearestPass(sc, ac, bc, la, lb, out float pa, out float pb))
                        AddRest(anchor, lane, sc, pa, pb, vNow, vTop);
                    if (BandPass(sc, ac, bc, la, lb, out float qa, out float qb)
                        && (qa - pa) * (qa - pa) + (qb - pb) * (qb - pb) > 2f)
                        AddRest(anchor, lane, sc, qa, qb, vNow, vTop);
                }
                if (NearestPass(sc, ac, bc, ea, eb, out float da, out float db))
                    AddDirect(anchor, sc, da, db, vNow, vTop);
                if (NearestPass(sc, ac, bc, a0, b0, out float na, out float nb)
                    && (na - da) * (na - da) + (nb - db) * (nb - db) > 2f)
                    AddDirect(anchor, sc, na, nb, vNow, vTop);
            }
            if (_candCount == 0) AddHome(anchor, face, vNow);

            int best = 0;
            if (_candCount > 1)
            {
                float sEnd = anchor.S + 60f;
                for (int c = 0; c < _candCount; c++) sEnd = Mathf.Max(sEnd, _cands[c][_cands[c].Count - 1].S + 40f);
                float bestCost = float.MaxValue;
                for (int c = 0; c < _candCount; c++)
                {
                    float cost = Evaluate(_cands[c], anchor.S, sEnd);
                    if (cost < bestCost) { bestCost = cost; best = c; }
                }
            }
            int oldFace = _keys.Count > 0 ? FaceOf(anchor.Phi) : -1;
            _keys.Clear();
            _keys.AddRange(_cands[best]);
            _crystalPlanned = _candTargets[best];
            _planSpeed = Mathf.Max(_planSpeed, _candSpeeds[best]);
            var last = _keys[_keys.Count - 1];
            if (oldFace >= 0 && last.Lane >= 0 && last.Lane != oldFace) FaceChanges++;
            LastChoice = DescribeCandidate(_keys);
        }

        static string DescribeCandidate(List<Key> keys)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                sb.Append(k.Kind == KeyKind.Lane ? "L" + k.Lane : k.Kind.ToString().Substring(0, 1));
                if (i + 1 < keys.Count) sb.Append('>');
            }
            return sb.ToString();
        }

        /// <summary>
        /// How long the ship takes to fly <paramref name="keys"/> from <paramref name="s0"/> to
        /// <paramref name="sEnd"/>, simulated: the speed the stick authority allows along that exact
        /// line (<see cref="SampleVMax"/>, read back from every bend ahead at the throttle-off shed
        /// rate), the thrust lerp chasing the throttle target, and the boost the line skims or bleeds
        /// — plus what the state it arrives in will cost afterwards (speed and boost still to rebuild),
        /// and a price for any rail on the way.
        /// </summary>
        float Evaluate(List<Key> keys, float s0, float sEnd)
        {
            float vTop = SpeedTop();
            int count = Mathf.Max(2, Mathf.CeilToInt((sEnd - s0) / SampleStep)) + 1;
            SampleCurve(keys, s0, count, WindowFor(vTop));
            if (_cap.Length < count) _cap = new float[count * 2];
            float budget = _p.SpeedAuthority * TurnRateRad * _p.RollBonus;
            for (int i = 0; i < count; i++) _cap[i] = SampleVMax(i, budget);
            for (int i = count - 2; i >= 0; i--) _cap[i] = Mathf.Min(_cap[i], _cap[i + 1] + BrakePerUnit * SampleStep);

            float v = Mathf.Max(_sensors.Speed, 20f);
            float bmMax = Mathf.Max(1f, _sensors.MaxBoost);
            float bm = Mathf.Clamp(_sensors.BoostMultiplier, 1f, bmMax);
            float thr = Mathf.Clamp01(_p.Throttle);
            float perUnit = AddPerHit / Mathf.Max(1f, _route.MeanSpacing);
            bool boosting = bm > 1.001f;
            float t = 0f;
            for (int i = 0; i < count - 1; i++)
            {
                float s = s0 + i * SampleStep;
                int j = i + _rawPad;
                float rho = _rawRho[j], phi = _rawPhi[j];
                if (rho < ClearRho(phi, s) - 0.5f) t += 0.5f;           // the hull would clip the plates
                if (rho <= SkimRho(phi, s))
                {
                    bm = Mathf.Min(bmMax, bm + perUnit * SampleStep);
                    boosting = true;
                }
                float step = (_raw[j + 1] - _raw[j]).magnitude;
                float dt = step / Mathf.Max(v, 20f);
                if (bm > 1f) bm = Mathf.Max(1f, bm - BoostDecay * dt);
                float target = Mathf.Min(_sensors.ThrottleScaler * thr * (boosting ? bm : 1f), _cap[i]);
                v += (target - v) * (1f - Mathf.Exp(-Follow * dt));
                t += dt;
            }
            t += RampCost(v, boosting ? bm : 1f);

            if (_obstacles != null && _obstacles.Count > 0)
            {
                float horizon = s0 + Mathf.Max(80f, Mathf.Max(_sensors.Speed, 60f) * 0.9f);
                if (FindConflict(keys, _obstacles, s0 + 2f, Mathf.Min(sEnd, horizon), out _, out _)) t += ConflictSeconds;
            }
            return t;
        }

        /// <summary>
        /// Watch the crystal the ship is after. A new one is planned for once the pilot has had its
        /// reaction time; one the ship has flown past is re-targeted a lap later. True when the plan
        /// must be rebuilt.
        /// </summary>
        bool ObserveCrystal(bool force)
        {
            if (!_sensors.HasCrystal)
            {
                bool had = _haveCrystal;
                _haveCrystal = false;
                _crystalS = float.NaN;
                return had;
            }

            Vector3 c = _sensors.CrystalPosition;
            if (!_haveCrystal || (c - _crystal).sqrMagnitude > 25f)
            {
                _crystal = c;
                _haveCrystal = true;
                _crystalSeenAt = _sensors.Time;
                _crystalS = float.NaN;
                _crystalPlanned = false;
            }

            if (float.IsNaN(_crystalS))
            {
                if (!force && _sensors.Time - _crystalSeenAt < _p.ReactionSeconds) return false;
                float lap = _route.Length;
                // Search the ribbon AHEAD, not the whole loop: a knot passes near itself, and a
                // crystal matched to the wrong strand is a pass point in the wrong place.
                float sc = _route.Project(c, _s + 0.4f * lap, 0.45f * lap, 0.45f * lap);
                if (sc < _s + 20f && _route.Closed) sc += lap;
                _crystalS = sc;
                CrystalsPlanned++;
                return true;
            }

            // Flown past it without collecting: the next chance is a lap on.
            if (_s > _crystalS + 40f && _route.Closed)
            {
                _crystalS += _route.Length;
                CrystalsDeferred++;
                return true;
            }
            return false;
        }

        // ============================================================================== optimised planning

        const float MinGridStep = 12f;        // arc between the optimiser's points: the prism spacing at most
        const float MaxGridStep = 36f;        // and a little over a tenth of a second at full speed
        const float PlanTail = 240f;          // line planned past the crystal
        const float MaxPlanLength = 960f;     // longest line planned
        const float RefreshInterval = 0.25f;  // seconds between warm re-solves

        readonly SkimPathOptimizer _opt = new SkimPathOptimizer();
        readonly List<Key>[] _optCands =
        {
            new List<Key>(SkimPathOptimizer.MaxPoints), new List<Key>(SkimPathOptimizer.MaxPoints),
        };
        float _nextRefresh;
        float _lastRefresh = -1f;
        bool _planHasCrystal;
        float _planStep = MinGridStep;

        /// <summary>
        /// One tick of the optimised planner: a fresh line (both ways round the ribbon, the faster
        /// kept) whenever the crystal changes or comes within planning range, a fresh line from the
        /// ship when it has strayed from the old one, and otherwise the old line re-solved from where
        /// it is under the ship every <see cref="RefreshInterval"/> — the speed and boost it was
        /// planned for have moved on, and rails come into view — or at once when the line ahead runs
        /// into one.
        /// </summary>
        void TickOptimized(IReadOnlyList<SkimObstacle> obstacles)
        {
            bool crystalChanged = ObserveCrystal(force: false);
            float now = _sensors.Time;
            if (OffPlan())
            {
                Optimize(fromShip: true, cold: true);
                Reanchors++;
                return;
            }
            bool inRange = _haveCrystal && !float.IsNaN(_crystalS) && _crystalS - _s <= MaxPlanLength - PlanTail;
            if (crystalChanged || (inRange && !_planHasCrystal))
            {
                Optimize(fromShip: false, cold: true);
                return;
            }
            // Too close to move the pass: keep the line that is already through the crystal.
            if (_planHasCrystal && _crystalS - _s < 3.5f * _planStep) return;
            bool conflict = false;
            if (obstacles != null && obstacles.Count > 0 && now - _lastRefresh > 0.08f)
            {
                float horizon = _s + Mathf.Max(80f, Mathf.Max(_sensors.Speed, 60f) * 0.9f);
                conflict = FindConflict(_keys, obstacles, _s + 2f, horizon, out _, out _);
            }
            if (now >= _nextRefresh || conflict) Optimize(fromShip: false, cold: false);
        }

        SkimPathOptimizer.Settings OptSettings()
        {
            float bmMax = Mathf.Max(1f, _sensors.MaxBoost);
            float bm = Mathf.Clamp(_sensors.BoostMultiplier, 1f, bmMax);
            float ramp = bmMax > 1.001f ? (bmMax - bm) / (bmMax - 1f) : 0f;
            return new SkimPathOptimizer.Settings
            {
                Budget = _p.PlanAuthority * TurnRateRad * _p.RollBonus,
                FollowRate = Follow,
                HingeWeight = _p.OptHingeWeight,
                DemandWeight = _p.OptDemandWeight,
                BandWeight = Mathf.Lerp(_p.OptBandWeightCruise, _p.OptBandWeightRamp, ramp),
                Rho = _p.OptRho,
                ClearMargin = _p.ClearanceMargin,
                SkimReach = Mathf.Max(0f, _sensors.SkimRadius - 0.5f),
                ObstacleMargin = _p.ObstacleMargin,
            };
        }

        /// <summary>
        /// Solve a new line from the ship forward. <paramref name="cold"/>: seeded fresh, round the
        /// ribbon each way the crystal can be reached, and the faster flown; otherwise seeded on the
        /// line already being flown and refined.
        /// </summary>
        void Optimize(bool fromShip, bool cold)
        {
            float s0 = _s;
            _lastRefresh = _sensors.Time;
            _nextRefresh = _sensors.Time + RefreshInterval;

            // The grid is a fixed fraction of a second of flight: coarse enough at full speed that the
            // solver's slow modes (whole swings sliding along the ribbon) converge in a hundred
            // iterations, fine enough on the ramp to bend round a crystal 150 u away.
            float vRef = Mathf.Max(_sensors.Speed, PredictSkimSpeed(300f));
            float grid = Mathf.Clamp(_p.OptGridSeconds * vRef, MinGridStep, MaxGridStep);
            float ahead = _haveCrystal && !float.IsNaN(_crystalS) ? _crystalS - s0 : float.MaxValue;
            bool target = ahead > 3.5f * grid && ahead <= MaxPlanLength - PlanTail;
            float step = grid;
            int count, ic = -1;
            if (target)
            {
                // The crystal on a grid point, so its pass is a point of the line and not a blend.
                int nc = Mathf.Max(4, Mathf.RoundToInt(ahead / grid));
                step = ahead / nc;
                ic = nc;
                count = nc + Mathf.CeilToInt(PlanTail / step) + 1;
            }
            else count = Mathf.CeilToInt(MaxPlanLength / step) + 1;
            count = Mathf.Min(count, SkimPathOptimizer.MaxPoints);

            _opt.Setup(_route, s0, step, count, _sensors.HullHalfExtents.z + _p.ClearanceMargin, HullReach, OptSettings());
            count = _opt.Count;

            // Where the ship is and how it is already turning: read off the old line when the ship
            // is on it, so a re-plan never moves the line under the ship.
            bool fromPlan = !fromShip && _keys.Count > 0;
            for (int i = 0; i < 3; i++)
            {
                float a, b;
                if (fromPlan) PlanOffset(_keys, s0 + i * step, out a, out b);
                else ShipOffsetAhead(i * step, out a, out b);
                _opt.SetPoint(i, a, b);
            }

            float ac = 0f, bc = 0f;
            if (target)
            {
                CrystalOffset(_crystalS, out ac, out bc);
                _opt.SetCrystal(ic, ac, bc, _p.PassRadius);
            }
            if (_obstacles != null)
            {
                for (int o = 0; o < _obstacles.Count; o++)
                {
                    var ob = _obstacles[o];
                    float r = HullReach + Mathf.Max(ob.HalfExtents.x, ob.HalfExtents.y) + _p.ObstacleMargin + 0.3f;
                    _opt.AddObstacle(ob.Center, r);
                }
            }

            float sEnd = s0 + (count - 1) * step - 12f;
            int chosen = 0;
            string note;
            if (!cold && _keys.Count > 0)
            {
                for (int i = 3; i < count; i++)
                {
                    PlanOffset(_keys, s0 + i * step, out float a, out float b);
                    _opt.SetPoint(i, a, b);
                }
                PlanSpeeds(count, step, useBand: true);
                _opt.Solve(_p.OptWarmIterations);
                GridToKeys(_optCands[0]);
                note = "warm";
            }
            else if (!target)
            {
                int face = FaceOf(Mathf.Atan2(_opt.A(0), _opt.B(0)));
                for (int i = 3; i < count; i++)
                {
                    float a, b;
                    if (_keys.Count > 0) PlanOffset(_keys, s0 + i * step, out a, out b);
                    else _line.Offset(face, s0 + i * step, out a, out b);
                    _opt.SetPoint(i, a, b);
                }
                PlanSpeeds(count, step, useBand: false);
                _opt.Solve(_p.OptColdIterations);
                PlanSpeeds(count, step, useBand: true);
                _opt.Solve(_p.OptWarmIterations);
                GridToKeys(_optCands[0]);
                note = "band";
            }
            else
            {
                float a0 = _opt.A(2), b0 = _opt.B(2);
                float phi0 = Mathf.Atan2(a0, b0);
                int face = FaceOf(phi0);
                _line.Offset(face, _crystalS, out float la, out float lb);
                if (!BandPass(_crystalS, ac, bc, la, lb, out float pa, out float pb)) { pa = ac; pb = bc; }
                float phiP = Unwrap(Mathf.Atan2(pa, pb), phi0);
                float rhoP = Mathf.Sqrt(pa * pa + pb * pb);
                int options = Mathf.Abs(phiP - phi0) > 0.6f * Mathf.PI ? 2 : 1;
                float bestCost = float.MaxValue;
                for (int c = 0; c < options; c++)
                {
                    float phiT = c == 0 ? phiP : phiP - Mathf.Sign(phiP - phi0) * 2f * Mathf.PI;
                    SeedTowards(count, step, ic, phiT, rhoP);
                    PlanSpeeds(count, step, useBand: false);
                    _opt.Solve(_p.OptColdIterations);
                    PlanSpeeds(count, step, useBand: true);
                    _opt.Solve(_p.OptWarmIterations);
                    GridToKeys(_optCands[c]);
                    float cost = options > 1 ? Evaluate(_optCands[c], s0, sEnd) : 0f;
                    if (cost < bestCost) { bestCost = cost; chosen = c; }
                }
                note = options > 1 ? (chosen == 0 ? "short-way" : "long-way") : "crystal";
            }

            int oldFace = _keys.Count > 0 ? FaceOf(_keys[_keys.Count - 1].Phi) : -1;
            _keys.Clear();
            _keys.AddRange(_optCands[chosen]);
            _planHasCrystal = target;
            _crystalPlanned = target;
            _planStep = step;
            _planSpeed = Mathf.Max(_sensors.Speed, 1f);
            int newFace = FaceOf(_keys[_keys.Count - 1].Phi);
            if (oldFace >= 0 && newFace != oldFace) FaceChanges++;
            LastChoice = $"{note} it={_opt.Iterations} d={_opt.WorstDemand:F2} miss={_opt.CrystalMiss:F1} clr={_opt.WorstClearDeficit:F1} obs={_opt.WorstObstacleDeficit:F1}";
        }

        /// <summary>Seed the line from point 2 round the ribbon to angle <paramref name="phiT"/>
        /// (unwrapped: the sign of the move says which way round) at the crystal's point, then back
        /// into the skim band beyond it — lifted clear of the plates wherever the blend would cut them.</summary>
        void SeedTowards(int count, float step, int ic, float phiT, float rhoT)
        {
            float a2 = _opt.A(2), b2 = _opt.B(2);
            float rho2 = Mathf.Sqrt(a2 * a2 + b2 * b2);
            float phi2 = Unwrap(Mathf.Atan2(a2, b2), Mathf.Atan2(_opt.A(0), _opt.B(0)));
            float s0 = _opt.Start;
            for (int i = 3; i < count; i++)
            {
                float s = s0 + i * step;
                float phi, rho;
                if (i <= ic)
                {
                    float t = (i - 2f) / Mathf.Max(1f, ic - 2f);
                    float w = t * t * (3f - 2f * t);
                    phi = phi2 + (phiT - phi2) * w;
                    rho = rho2 + (rhoT - rho2) * w;
                }
                else
                {
                    float t = Mathf.Clamp01((i - ic) / Mathf.Max(1f, (count - 1f - ic) * 0.6f));
                    float w = t * t * (3f - 2f * t);
                    float band = 0.5f * (ClearRho(phiT, s) + SkimRho(phiT, s));
                    phi = phiT;
                    rho = rhoT + (band - rhoT) * w;
                }
                rho = Mathf.Max(rho, ClearRho(phi, s) + 0.75f);
                _opt.SetPoint(i, rho * Mathf.Sin(phi), rho * Mathf.Cos(phi));
            }
        }

        /// <summary>The speed the ship will be doing at each point of the line: its own now, then the
        /// throttle target at the boost it will have, skimming wherever the line is in reach
        /// (<paramref name="useBand"/>) or everywhere.</summary>
        void PlanSpeeds(int count, float step, bool useBand)
        {
            float bmMax = Mathf.Max(1f, _sensors.MaxBoost);
            float v = Mathf.Max(_sensors.Speed, 20f);
            float bm = Mathf.Clamp(_sensors.BoostMultiplier, 1f, bmMax);
            float thr = Mathf.Clamp01(_p.Throttle);
            float perUnit = AddPerHit / Mathf.Max(1f, _route.MeanSpacing);
            for (int i = 0; i < count; i++)
            {
                _opt.SetSpeed(i, v);
                float dt = step / Mathf.Max(v, 20f);
                if (!useBand || _opt.InBand(i)) bm = Mathf.Min(bmMax, bm + perUnit * step);
                if (bm > 1f) bm = Mathf.Max(1f, bm - BoostDecay * dt);
                float targetV = _sensors.ThrottleScaler * thr * bm;
                v += (targetV - v) * (1f - Mathf.Exp(-Follow * dt));
            }
        }

        /// <summary>The optimiser's points as keys: value, slope and curvature at each, so the
        /// quintic between consecutive keys is the smooth line through them.</summary>
        void GridToKeys(List<Key> keys)
        {
            keys.Clear();
            int n = _opt.Count;
            float h = _opt.Step, s0 = _opt.Start;
            float phiNear = Mathf.Atan2(_opt.A(0), _opt.B(0));
            for (int i = 0; i < n; i++)
            {
                int i0 = Mathf.Max(0, i - 1), i1 = Mathf.Min(n - 1, i + 1);
                float span = (i1 - i0) * h;
                float da = (_opt.A(i1) - _opt.A(i0)) / span, db = (_opt.B(i1) - _opt.B(i0)) / span;
                int c = Mathf.Clamp(i, 1, n - 2);
                float dda = (_opt.A(c + 1) - 2f * _opt.A(c) + _opt.A(c - 1)) / (h * h);
                float ddb = (_opt.B(c + 1) - 2f * _opt.B(c) + _opt.B(c - 1)) / (h * h);
                ToPolar(_opt.A(i), _opt.B(i), da, db, dda, ddb, phiNear,
                    out float rho, out float phi, out float dRho, out float dPhi, out float ddRho, out float ddPhi);
                keys.Add(new Key
                {
                    S = s0 + i * h, Rho = rho, Phi = phi, DRho = dRho, DPhi = dPhi, DDRho = ddRho, DDPhi = ddPhi,
                    Kind = i == 0 ? KeyKind.Anchor : KeyKind.Opt, Lane = -1,
                });
                phiNear = phi;
            }
        }

        /// <summary>Offset (across the plates, along their up) of a key list at arc <paramref name="s"/>.</summary>
        void PlanOffset(List<Key> keys, float s, out float a, out float b)
        {
            Offset(keys, s, out float rho, out float phi);
            a = rho * Mathf.Sin(phi);
            b = rho * Mathf.Cos(phi);
        }

        /// <summary>Where the ship will be, as an offset, after <paramref name="ds"/> of arc if it
        /// holds the turn it is already in — the transform closing on the commanded rotation at the
        /// follow rate.</summary>
        void ShipOffsetAhead(float ds, out float a, out float b)
        {
            _route.Frame(_s, out _, out Vector3 tangent, out _, out _);
            Vector3 fwd = _sensors.Course.sqrMagnitude > 0.5f ? _sensors.Course.normalized : _sensors.Rotation * Vector3.forward;
            float v = Mathf.Max(_sensors.Speed, 1f);
            Quaternion gap = _sensors.CommandedRotation * Quaternion.Inverse(_sensors.Rotation);
            gap.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            Vector3 omega = axis.sqrMagnitude > 1e-6f ? axis.normalized * (angle * Mathf.Deg2Rad * Follow) : Vector3.zero;
            Vector3 kappa = Vector3.Cross(omega, fwd) / v;
            float along = Mathf.Max(0.25f, Vector3.Dot(fwd, tangent));
            float sigma = ds / along;
            Vector3 p = _sensors.Position + fwd * sigma + kappa * (0.5f * sigma * sigma);
            _route.Frame(_s + ds, out Vector3 c, out _, out Vector3 r, out Vector3 u);
            Vector3 d = p - c;
            a = Vector3.Dot(d, r);
            b = Vector3.Dot(d, u);
        }

        // ============================================================================== avoidance

        readonly Vector3[] _axA = new Vector3[3];
        readonly Vector3[] _axB = new Vector3[3];

        /// <summary>
        /// Would a hull flown along <paramref name="keys"/> between <paramref name="from"/> and
        /// <paramref name="to"/> touch an obstacle? Returns the first and last conflicting arc of the
        /// FIRST conflicting stretch. The hull is posed as the controller will pose it — nose along the
        /// line, belly to the plates — and tested as an oriented box, inflated by the margin.
        /// </summary>
        bool FindConflict(List<Key> keys, IReadOnlyList<SkimObstacle> obstacles, float from, float to,
                          out float first, out float last)
        {
            first = last = 0f;
            bool any = false;
            Vector3 half = _sensors.HullHalfExtents + Vector3.one * _p.ObstacleMargin;
            float reach = half.magnitude;
            for (float s = from; s <= to; s += 3f)
            {
                Vector3 p = LinePoint(keys, s, out float rho, out float phi);
                Vector3 t = TangentAt(keys, s);
                _route.Frame(s, out _, out _, out Vector3 fr, out Vector3 fu);
                Vector3 radial = fr * Mathf.Sin(phi) + fu * Mathf.Cos(phi);
                Vector3 up = (radial - t * Vector3.Dot(radial, t)).normalized;
                if (up.sqrMagnitude < 0.5f) up = fu;
                Vector3 right = Vector3.Cross(up, t);
                bool hit = false;
                for (int i = 0; i < obstacles.Count; i++)
                {
                    var o = obstacles[i];
                    float r = reach + o.HalfExtents.magnitude;
                    if ((o.Center - p).sqrMagnitude > r * r) continue;
                    if (ObbOverlap(p, right, up, t, half, o)) { hit = true; break; }
                }
                if (hit)
                {
                    if (!any) first = s;
                    last = s;
                    any = true;
                }
                else if (any && s - last > 12f)
                {
                    break;
                }
            }
            return any;
        }

        bool ObbOverlap(Vector3 ca, Vector3 ax, Vector3 ay, Vector3 az, Vector3 ea, in SkimObstacle o)
        {
            _axA[0] = ax; _axA[1] = ay; _axA[2] = az;
            _axB[0] = o.Rotation * Vector3.right;
            _axB[1] = o.Rotation * Vector3.up;
            _axB[2] = o.Rotation * Vector3.forward;
            Vector3 eb = o.HalfExtents;
            Vector3 d = o.Center - ca;
            for (int i = 0; i < 3; i++)
            {
                if (Separated(_axA[i], d, ea, eb)) return false;
                if (Separated(_axB[i], d, ea, eb)) return false;
            }
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                Vector3 l = Vector3.Cross(_axA[i], _axB[j]);
                if (l.sqrMagnitude < 1e-8f) continue;
                if (Separated(l, d, ea, eb)) return false;
            }
            return true;
        }

        bool Separated(Vector3 l, Vector3 d, Vector3 ea, Vector3 eb)
        {
            float ra = ea.x * Mathf.Abs(Vector3.Dot(_axA[0], l)) + ea.y * Mathf.Abs(Vector3.Dot(_axA[1], l))
                     + ea.z * Mathf.Abs(Vector3.Dot(_axA[2], l));
            float rb = eb.x * Mathf.Abs(Vector3.Dot(_axB[0], l)) + eb.y * Mathf.Abs(Vector3.Dot(_axB[1], l))
                     + eb.z * Mathf.Abs(Vector3.Dot(_axB[2], l));
            return Mathf.Abs(Vector3.Dot(d, l)) > ra + rb;
        }

        static readonly Vector2[] Nudges =
        {
            new Vector2(0f, 2.5f), new Vector2(4f, 0f), new Vector2(-4f, 0f), new Vector2(0f, -1f),
            new Vector2(0f, 4.5f), new Vector2(8f, 0f), new Vector2(-8f, 0f),
            new Vector2(4f, 3f), new Vector2(-4f, 3f), new Vector2(12f, 0f), new Vector2(-12f, 0f),
            new Vector2(0f, 8f),
        };

        /// <summary>
        /// Look along the planned line for a hull-sized conflict with anything that is not the
        /// ribbon; if one is found, try the smallest nudge of the line — over it, beside it — that
        /// clears the whole stretch, and keep the first that does. A nudge rejoins the old plan at a
        /// key sampled from it, so outside the nudge the line is exactly the one it replaced; a nudge
        /// across a crystal pass is kept only if it still passes inside the crystal's reach.
        /// </summary>
        void Avoid(IReadOnlyList<SkimObstacle> obstacles)
        {
            float speed = Mathf.Max(_sensors.Speed, 60f);
            float horizon = _s + Mathf.Max(80f, speed * 0.9f);
            if (!FindConflict(_keys, obstacles, _s + 2f, horizon, out float first, out float last))
            {
                LastAvoidNote = "clear";
                return;
            }

            _backup.Clear();
            _backup.AddRange(_keys);
            float pad = _sensors.HullHalfExtents.z + 4f;
            float inS = first - pad, outS = last + pad;
            Offset(_backup, first, out float rMid, out float pMid);
            float am = rMid * Mathf.Sin(pMid), bm = rMid * Mathf.Cos(pMid);
            float away = Mathf.Cos(pMid) >= 0f ? 1f : -1f;
            float budget = _p.PlanAuthority * TurnRateRad;
            bool crystalInside = _crystalPlanned && _crystalS > first - 120f && _crystalS < last + 120f;
            float ac = 0f, bc = 0f;
            if (crystalInside) CrystalOffset(_crystalS, out ac, out bc);
            float reach = _p.PassRadius + 1.5f;

            for (int n = 0; n < Nudges.Length; n++)
            {
                float ta = am + Nudges[n].x, tb = bm + Nudges[n].y * away;
                float tphi = Unwrap(Mathf.Atan2(ta, tb), pMid);
                float trho = Mathf.Sqrt(ta * ta + tb * tb);
                if (trho < ClearRho(tphi, first) + 0.25f) continue;
                float blend = Mathf.Max(40f, speed * Mathf.Pow(60f * Nudges[n].magnitude
                    / (speed * Follow * budget), 1f / 3f));
                float sIn = Mathf.Max(_s + 4f, inS - blend);
                float sHoldA = Mathf.Max(inS, sIn + 1f);
                float sHoldB = Mathf.Max(outS, sHoldA + 1f);
                float sRejoin = sHoldB + blend;

                _trial.Clear();
                var anchor = StateKey(_backup, _s, KeyKind.Anchor);
                anchor.Lane = (sbyte)LaneAt(_backup, _s);
                _trial.Add(anchor);
                _trial.Add(new Key { S = sHoldA, Rho = trho, Phi = tphi, Kind = KeyKind.Avoid, Lane = -1 });
                _trial.Add(new Key { S = sHoldB, Rho = trho, Phi = tphi, Kind = KeyKind.Avoid, Lane = -1 });
                var rejoin = StateKey(_backup, sRejoin, KeyKind.Rejoin);
                rejoin.Phi = Unwrap(rejoin.Phi, tphi);
                int laneAtRejoin = LaneAt(_backup, sRejoin);
                if (laneAtRejoin >= 0) rejoin = LaneKey(laneAtRejoin, sRejoin, rejoin.Phi);
                _trial.Add(rejoin);
                for (int i = 0; i < _backup.Count; i++)
                    if (_backup[i].S > sRejoin + 1f) _trial.Add(_backup[i]);
                SubdivideAll(_trial);

                if (crystalInside)
                {
                    Offset(_trial, _crystalS, out float cr, out float cp);
                    float ca = cr * Mathf.Sin(cp) - ac, cb = cr * Mathf.Cos(cp) - bc;
                    if (ca * ca + cb * cb > reach * reach) continue;
                }
                if (FindConflict(_trial, obstacles, _s + 2f, sRejoin, out _, out _)) continue;
                _keys.Clear();
                _keys.AddRange(_trial);
                Avoidances++;
                LastAvoidNote = $"nudge {n} for conflict {first - _s:F0}..{last - _s:F0} ahead";
                return;
            }
            UnresolvedConflicts++;
            LastAvoidNote = $"UNRESOLVED conflict {first - _s:F0}..{last - _s:F0} ahead{(crystalInside ? " (crystal)" : "")}";
        }

        // ============================================================================== flying

        /// <summary>
        /// Lift off the throttle when the line ahead would need more stick than the ship has at the
        /// current speed. Speed is shed at about 1.5 u/s per unit travelled with the throttle closed
        /// (the thrust lerp's own rate), so a bend <c>d</c> ahead that needs speed <c>v*</c> caps the
        /// speed now at <c>v* + 1.2 d</c>.
        /// </summary>
        void UpdateThrottle()
        {
            if (_sensors.Time < _nextSpeedScan) return;
            _nextSpeedScan = _sensors.Time + SpeedScanInterval;

            float v = Mathf.Max(_sensors.Speed, 1f);
            float budget = _p.SpeedAuthority * TurnRateRad * _p.RollBonus;
            float horizon = Mathf.Clamp(v * 0.8f, 60f, 300f);
            float cap = float.MaxValue;
            int count = Mathf.CeilToInt(horizon / SampleStep) + 1;
            SampleCurve(_keys, _s, count, WindowFor(v));
            for (int i = 0; i < count; i++)
                cap = Mathf.Min(cap, SampleVMax(i, budget) + BrakePerUnit * i * SampleStep);

            float full = _sensors.ThrottleScaler * Mathf.Max(1f, _sensors.BoostMultiplier);
            float want = Mathf.Clamp01(_p.Throttle);
            if (full < 1f || cap >= full * want) _throttle = want;
            else if (v > cap) _throttle = 0f;
            else _throttle = Mathf.Clamp01(Mathf.Min(want, cap / full));
        }

        SkimRacerCommand Steer()
        {
            var s = _sensors;
            float v = Mathf.Max(s.Speed, 1f);
            Vector3 fwd = s.Rotation * Vector3.forward;
            Vector3 upT = s.Rotation * Vector3.up;

            Vector3 g = LinePoint(_keys, _s);
            Vector3 tg = TangentAt(_keys, _s);
            Vector3 e = s.Position - g;
            e -= tg * Vector3.Dot(e, tg);
            CrossTrackError = e.magnitude;
            Vector3 eDot = (fwd - tg * Vector3.Dot(fwd, tg)) * v;

            Curvature(_keys, _s + v * _p.LeadSeconds, v, out Vector3 tl, out Vector3 dTl, out Vector3 ddTl);

            float wn = _p.TrackFrequency;
            Vector3 accel = dTl * (v * v) - e * (wn * wn) - eDot * (2f * _p.TrackDamping * wn);
            {
                _route.Frame(_s, out _, out _, out Vector3 dbgR, out Vector3 dbgU);
                Vector3 ff = dTl * (v * v), pp = -e * (wn * wn), dd = -eDot * (2f * _p.TrackDamping * wn);
                DebugSteer = $"ff=({Vector3.Dot(ff, dbgR):F0},{Vector3.Dot(ff, dbgU):F0}) P=({Vector3.Dot(pp, dbgR):F0},{Vector3.Dot(pp, dbgU):F0}) " +
                             $"D=({Vector3.Dot(dd, dbgR):F0},{Vector3.Dot(dd, dbgU):F0}) e=({Vector3.Dot(e, dbgR):F1},{Vector3.Dot(e, dbgU):F1})";
            }
            Vector3 omega = Vector3.Cross(fwd, accel) / v;
            Vector3 omegaRate = Vector3.Cross(tl, ddTl) * (v * v);
            omegaRate -= fwd * Vector3.Dot(omegaRate, fwd);

            Vector3 belly = BellyDirection(_s);
            Vector3 want = belly - fwd * Vector3.Dot(belly, fwd);
            if (want.sqrMagnitude > 1e-4f)
            {
                want.Normalize();
                if (_p.RollOffsetDegrees > 0f)
                {
                    // Belly at an angle to the plates rather than square on: the two directions a
                    // ribbon bends in — across its plates and out of them — then both lie on
                    // DIAGONALS of the ship's pitch/yaw axes, where the stick has √2 of either
                    // alone. Keep the side the ship is already on unless the other is much nearer.
                    Vector3 wantA = Quaternion.AngleAxis(_p.RollOffsetDegrees, fwd) * want;
                    Vector3 wantB = Quaternion.AngleAxis(-_p.RollOffsetDegrees, fwd) * want;
                    float errA = Mathf.Abs(Vector3.SignedAngle(upT, wantA, fwd));
                    float errB = Mathf.Abs(Vector3.SignedAngle(upT, wantB, fwd));
                    if (_rollSide == 0) _rollSide = errA <= errB ? 1 : -1;
                    else if (_rollSide > 0 && errB + 30f < errA) _rollSide = -1;
                    else if (_rollSide < 0 && errA + 30f < errB) _rollSide = 1;
                    want = _rollSide > 0 ? wantA : wantB;
                }
                float rollErr = Vector3.SignedAngle(upT, want, fwd) * Mathf.Deg2Rad;
                float cap = _p.RollAuthority * s.RollRateDegrees * Mathf.Deg2Rad;
                omega += fwd * Mathf.Clamp(rollErr * _p.RollGain, -cap, cap);
            }

            SkimFlightController.Solve(s, omega, omegaRate, _p.ServoGain, out float xSum, out float ySum, out float yDiff);
            return new SkimRacerCommand
            {
                XSum = xSum,
                YSum = ySum,
                YDiff = yDiff,
                XDiff = _throttle,
            };
        }
    }
}
