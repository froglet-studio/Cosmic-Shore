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
    /// <para><b>The line is SOLVED, not assembled.</b> Every plan is a cubic B-spline from where
    /// the ship is to past its crystal, shaped by <see cref="SkimPathOptimizer"/>: at the speed the
    /// ship will be doing at each point it never asks the stick for more than the plan's share of
    /// it, it clears the plates with the hull at any roll, it passes within reach of the crystal,
    /// it keeps off every rail it can see, and — as far as all of that allows — it stays in
    /// skimming reach of the plates, which is what the boost is paid for. A crystal round the far
    /// side of the ribbon is solved both ways round and the faster flown (<see cref="Evaluate"/>).
    /// The first planner assembled lines out of a handful of keys and picked the best of a dozen;
    /// every one put its bends where its keys fell, and at 300 u/s asked for two to four times the
    /// stick the ship has.</para>
    ///
    /// <para><b>The line and its speed are solved together.</b> Wherever the line still asks for
    /// more stick than the ship has at the speed it could reach, the plan's speed there is lowered,
    /// braked back toward the ship, and the line re-solved for the slower ship
    /// (<see cref="SolveLine"/>). The throttle follows the plan's speeds, so the ship slows BEFORE
    /// the bend that needs it rather than finding out in the bend.</para>
    ///
    /// <para><b>The line is C2, including where it is re-planned.</b> Every re-plan pins its first
    /// three control points to the position, slope and curvature of the old line under the ship
    /// (or, re-planned from the ship, of its own motion and the turn it is already in), so a new
    /// crystal or a refresh never moves the line the ship is on. The line is re-solved from where it
    /// stands every <see cref="RefreshInterval"/> — the speed and boost it was planned for have moved
    /// on, and rails come into view — at once when the line ahead runs into a rail, and from the ship
    /// when the ship has strayed from it.</para>
    ///
    /// <para><b>Two things are flown by hand.</b> Coming up to the ribbon from the spawn, a hundred
    /// units off it, the line runs ACROSS the ribbon, and read per unit of ribbon arc its turns are
    /// several times too sharp — so until the ship is skimming along the ribbon it is read per unit
    /// of its OWN length (<see cref="ArcPhase"/>). And a crystal the ship has flown past, or is about
    /// to, is turned round for directly (<see cref="SeekTick"/>): missing it costs a whole lap.</para>
    ///
    /// <para><b>Steering</b> is cross-track tracking of the line: its curvature as feed-forward,
    /// plus a damped correction of position and lateral velocity, turned into sticks by
    /// <see cref="SkimFlightController"/>. Roll holds the ship's belly at an angle to the nearest
    /// point of the plates — pitch and yaw turn equally fast, so the line never depends on roll,
    /// and clearance is planned for a hull at ANY roll.</para>
    ///
    /// <para><b>What it never does</b>: read another pilot's state, collect anything it did not fly
    /// through, or touch the ship through anything but the sticks and the throttle (ARCHITECTURE.md
    /// R1/R2). It is pure — no Unity object — and Tools/Build/squirrel_ai_harness compiles and races
    /// this exact file.</para>
    /// </summary>
    public sealed class SkimRacerBrain
    {
        /// <summary>One control point of the line being flown — the line is the uniform cubic B-spline
        /// through them (<see cref="SplineEval"/>), knot i at route arc <see cref="S"/> — with the angle
        /// of its offset round the ribbon (what angles read off the line are unwrapped near) and the
        /// speed the plan is to be flown at there.</summary>
        struct Key
        {
            public float S;
            public float Phi;
            public Vector3 P;
            public float V;
        }

        const float SampleStep = 6f;        // arc between samples; tangent half-span
        const float BrakePerUnit = 1.2f;    // u/s of speed shed per unit travelled with the throttle off
        const float SpeedScanInterval = 0.1f;
        const float AddPerHit = 0.1f;       // SkimmerBoostPrismEffectSO: boost gained per prism entered
        const float BoostDecay = 0.3f;      // VesselTransformer: boost lost per second above 1
        const float ConflictSeconds = 5f;   // what the evaluator charges a plan through a rail: a touch resets the boost

        readonly SkimRoute _route;
        readonly SkimRacerProfile _p;
        SkimRacingLine _line;
        readonly List<Key> _keys = new List<Key>(SkimPathOptimizer.MaxPoints);
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

        float _throttle = 1f;
        int _rollSide;
        float _nextSpeedScan;

        // Telemetry, read by the harness and by a debug overlay.
        public float RouteS => _s;
        public int Reanchors { get; private set; }
        public int CrystalsPlanned { get; private set; }
        public int CrystalsDeferred { get; private set; }
        public int FaceChanges { get; private set; }
        public float CrossTrackError { get; private set; }
        /// <summary>Arc the crystal is targeted at, planned or not (NaN before it is projected).</summary>
        public float TargetCrystalS => _crystalS;
        public int KeyCount => _keys.Count;
        public float ThrottleOut => _throttle;
        /// <summary>How many lines the optimiser has solved.</summary>
        public int SolveCount { get; private set; }

        /// <summary>Tooling: when set, the brain describes its decisions in <see cref="LastChoice"/>,
        /// <see cref="DebugThrottle"/> and <see cref="DebugSteer"/>. Off in the game: the strings would
        /// be garbage every tick.</summary>
        public bool Trace { get; set; }
        /// <summary>What the last solve chose — for tooling (<see cref="Trace"/>).</summary>
        public string LastChoice { get; private set; } = "";
        /// <summary>The last throttle decision's terms — for tooling (<see cref="Trace"/>).</summary>
        public string DebugThrottle { get; private set; } = "";
        /// <summary>The last steering terms — for tooling (<see cref="Trace"/>).</summary>
        public string DebugSteer { get; private set; } = "";

        /// <summary>The plan's world point at arc <paramref name="s"/> — for tooling.</summary>
        public Vector3 PlanPosition(float s) => IsSpline(_keys) ? LinePoint(_keys, s) : Vector3.zero;

        /// <summary>The planned offset (distance, angle) at arc <paramref name="s"/> — for tooling.</summary>
        public void DescribePlan(float s, out float rho, out float phi)
        {
            if (IsSpline(_keys)) Offset(_keys, s, out rho, out phi);
            else rho = phi = 0f;
        }

        public SkimRacerBrain(SkimRoute route, SkimRacerProfile profile, int seed)
        {
            _route = route ?? throw new ArgumentNullException(nameof(route));
            _p = profile ?? SkimRacerProfile.Expert();
            var rng = new System.Random(seed);
            _wanderPhaseA = (float)(rng.NextDouble() * Math.PI * 2.0);
            _wanderPhaseB = (float)(rng.NextDouble() * Math.PI * 2.0);
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
            _crystalS = float.NaN;
            _crystalSeenAt = -1f;
            _approach = false;
            _seeking = false;
            _realigning = false;
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
                _approach = _p.ApproachArcAware && OffBandDistance() > 4f;
                _approachStart = sensors.Time;
                Optimize(fromShip: true, cold: true);
            }

            _s = _route.Project(sensors.Position, _s, 60f, 60f + v * sensors.DeltaTime * 4f);
            if (_seeking)
            {
                if (!SeekOver()) return SeekTick(_seekCrystal);
                _seeking = false;
                ObserveCrystal(force: true);
                _route.Frame(_s, out _, out Vector3 tSeek, out _, out _);
                _realigning = Vector3.Dot(sensors.Rotation * Vector3.forward, tSeek) < 0.5f;
                if (!_realigning) Optimize(fromShip: true, cold: true);
            }
            if (_realigning)
            {
                _route.Frame(_s, out _, out Vector3 tAl, out _, out _);
                if (Vector3.Dot(sensors.Rotation * Vector3.forward, tAl) < 0.7f && _sensors.Time - _seekStart < _p.RecoverMaxSeconds + 6f)
                    return SeekTick(MergePoint());
                _realigning = false;
                Optimize(fromShip: true, cold: true);
            }
            if (_approach) UpdateApproach();
            PruneKeys();

            if (RescueCrystal()) return SeekTick(_seekCrystal);
            TickOptimized(obstacles);
            UpdateThrottle();
            return Steer();
        }

        // ============================================================================== missed crystal

        bool _seeking;
        bool _realigning;
        Vector3 _seekCrystal;
        float _seekStart;

        /// <summary>Starts the turn for the crystal now, when it is close ahead and the line being flown
        /// passes too wide of it to collect it — the plan cannot be re-solved onto it in time.</summary>
        bool RescueCrystal()
        {
            if (!_p.RecoverMissedCrystal || _seeking || !_haveCrystal || float.IsNaN(_crystalS) || _keys.Count < 4) return false;
            float ahead = _crystalS - _s;
            float v = Mathf.Max(_sensors.Speed, 30f);
            if (ahead <= 0f || ahead > Mathf.Max(_p.RescueMinAhead, v * _p.RescueSeconds)) return false;
            float miss = Vector3.Distance(LinePoint(_keys, _crystalS), _crystal);
            if (miss < _p.RescueMiss) return false;
            _seeking = true;
            _seekCrystal = _crystal;
            _seekStart = _sensors.Time;
            CrystalsRescued++;
            return true;
        }

        /// <summary>Tooling: crystals the plan was about to miss, gone for directly.</summary>
        public int CrystalsRescued { get; private set; }

        /// <summary>Tooling: crystals flown past and turned round for.</summary>
        public int CrystalsRecovered { get; private set; }

        /// <summary>The turn-round ends when the crystal is collected (it moves on) — or, as a backstop,
        /// after <see cref="SkimRacerProfile.RecoverMaxSeconds"/>, when it is left for the next lap.</summary>
        bool SeekOver()
        {
            if (!_sensors.HasCrystal) return true;
            if ((_sensors.CrystalPosition - _seekCrystal).sqrMagnitude > 25f) return true;
            if (_sensors.Time - _seekStart <= _p.RecoverMaxSeconds) return false;
            if (!float.IsNaN(_crystalS) && _route.Closed) { _crystalS += _route.Length; CrystalsDeferred++; }
            return true;
        }

        /// <summary>
        /// Back to a crystal flown past: nose onto it (round the ribbon's nearer edge if the plates are
        /// in the way) as hard as the stick allows, slowed until it lies outside the turning circle —
        /// a ship that keeps its speed through a turn-round circles a point inside its turn for ever.
        /// </summary>
        SkimRacerCommand SeekTick(Vector3 target)
        {
            var s = _sensors;
            Vector3 pos = s.Position;
            Vector3 fwd = s.Rotation * Vector3.forward;
            Vector3 aim = target;
            if (PathBlocked(pos, target, out float sb, out float ab, out float bb, out float kw, out float kh))
                aim = Detour(pos, target, sb, ab, bb, kw, kh);
            Vector3 d = aim - pos;
            float dist = d.magnitude;
            Vector3 omega = Vector3.zero;
            float angle = 0f;
            if (dist > 1e-3f)
            {
                Vector3 turn = SkimFlightController.TurnVector(fwd, d / dist, s.Rotation * Vector3.up);
                angle = turn.magnitude;
                float rate = Mathf.Min(TurnRateRad, _p.RecoverGain * angle);
                if (angle > 1e-5f) omega = turn * (rate / angle);
            }
            // The aim is reachable from a turn of radius r = v / w while it lies outside the circle:
            // dist >= 2 r sin(angle). Hold the speed under that, with a margin.
            float full = s.ThrottleScaler * Mathf.Max(1f, s.BoostMultiplier);
            float sin = Mathf.Sin(Mathf.Min(angle, 0.5f * Mathf.PI));
            float vReach = sin > 0.05f ? 0.7f * TurnRateRad * dist / (2f * sin) : float.MaxValue;
            _throttle = full > 1f ? Mathf.Clamp(vReach / full, 0.15f, 1f) : 1f;
            if (s.Speed > vReach * 1.1f) _throttle = 0f;
            SkimFlightController.Solve(s, omega, Vector3.zero, _p.ServoGain, out float xSum, out float ySum, out float yDiff);
            if (Trace)
                DebugThrottle = $"seek{(_realigning ? "-align" : "")} aim={(aim == target ? "target" : "detour")} d={dist:F0} ang={angle * Mathf.Rad2Deg:F0} vReach={Mathf.Min(vReach, 999f):F0}";
            CrossTrackError = 0f;
            return new SkimRacerCommand { XSum = xSum, YSum = ySum, YDiff = yDiff, XDiff = _throttle };
        }

        /// <summary>
        /// Where the ship heads after a turn-round to get back onto the ribbon flying along it: the skim
        /// band of the ribbon's nearer broad face (over or under the plates), across from where the
        /// ship is now, an approach angle ahead — <see cref="SkimRacerProfile.MergeLead"/> times the
        /// ship's distance out. The point moves on with the ship, so the line to it flattens as it
        /// closes: pure pursuit onto the band.
        /// </summary>
        Vector3 MergePoint()
        {
            _route.Frame(_s, out Vector3 c, out _, out Vector3 r, out Vector3 u);
            Vector3 d = _sensors.Position - c;
            float a = Vector3.Dot(d, r), b = Vector3.Dot(d, u);
            float off = Mathf.Max(0f, OffBandDistance());
            float sm = _s + Mathf.Max(_p.MergeMinLead, _p.MergeLead * off);
            _route.Frame(sm, out Vector3 cm, out _, out Vector3 rm, out Vector3 um);
            _route.Envelope(sm, 0f, out float hw, out float hh);
            float height = hh + Mathf.Min(_p.NominalHeight, _sensors.SkimRadius - 1f);
            float am = Mathf.Clamp(a, -0.6f * hw, 0.6f * hw);
            float bm = b >= 0f ? height : -height;
            return cm + rm * am + um * bm;
        }

        /// <summary>
        /// Whether the straight line from <paramref name="from"/> to <paramref name="to"/> passes
        /// through the ribbon's keep-out box (the plates' envelope grown by the hull's reach and a
        /// margin); if so, the arc and cross-section offset where it is deepest inside, and the box.
        /// </summary>
        bool PathBlocked(Vector3 from, Vector3 to, out float sb, out float ab, out float bb, out float kw, out float kh)
        {
            sb = ab = bb = kw = kh = 0f;
            float s1 = float.IsNaN(_crystalS) ? _s + Vector3.Distance(from, to) : _crystalS;
            float deepest = 0f;
            const int samples = 24;
            float hint = _s;
            for (int k = 1; k < samples; k++)
            {
                float t = k / (float)samples;
                Vector3 p = Vector3.Lerp(from, to, t);
                float guess = s1 >= _s ? Mathf.Max(hint, Mathf.Lerp(_s, s1, t) - 40f) : Mathf.Lerp(_s, s1, t);
                float sp = _route.Project(p, guess, 60f, s1 >= _s ? 120f : 60f);
                hint = sp;
                _route.Frame(sp, out Vector3 c, out _, out Vector3 r, out Vector3 u);
                _route.Envelope(sp, _sensors.HullHalfExtents.z, out float hw, out float hh);
                float bw = hw + HullReach + _p.RecoverMargin, bh = hh + HullReach + _p.RecoverMargin;
                Vector3 dp = p - c;
                float a = Vector3.Dot(dp, r), b = Vector3.Dot(dp, u);
                float depth = Mathf.Min(bw - Mathf.Abs(a), bh - Mathf.Abs(b));
                if (depth > deepest)
                {
                    deepest = depth;
                    sb = sp; ab = a; bb = b; kw = bw; kh = bh;
                }
            }
            return deepest > 0f;
        }

        /// <summary>
        /// The corner of the keep-out box at arc <paramref name="sb"/> to go round: of the four ways
        /// past the plates (over, under, either edge), the one with the shortest path from
        /// <paramref name="from"/> through it to <paramref name="to"/>.
        /// </summary>
        Vector3 Detour(Vector3 from, Vector3 to, float sb, float ab, float bb, float kw, float kh)
        {
            _route.Frame(sb, out Vector3 c, out _, out Vector3 r, out Vector3 u);
            float pad = _p.RecoverMargin;
            Vector3 best = to;
            float bestCost = float.MaxValue;
            for (int k = 0; k < 4; k++)
            {
                float a = k < 2 ? (k == 0 ? kw + pad : -(kw + pad)) : Mathf.Clamp(ab, -kw, kw);
                float b = k < 2 ? Mathf.Clamp(bb, -kh, kh) : (k == 2 ? kh + pad : -(kh + pad));
                Vector3 v = c + r * a + u * b;
                float cost = Vector3.Distance(from, v) + Vector3.Distance(v, to);
                if (cost < bestCost) { bestCost = cost; best = v; }
            }
            return best;
        }

        // ============================================================================== approach

        bool _approach;
        float _approachStart;

        /// <summary>The approach ends by this many seconds after it began whatever the ship is doing.</summary>
        const float ApproachMaxSeconds = 20f;

        /// <summary>Tooling: true while the brain is still on its approach to the ribbon.</summary>
        public bool Approaching => _approach;

        /// <summary>Whether lines are read per unit of their OWN length rather than of ribbon arc: on
        /// the approach, while the ship is coming up to the ribbon from its spawn and its line runs
        /// across the ribbon — where ribbon arc reads the line's turns several times too sharp (and
        /// their rate of change that factor squared), which held the throttle near half, and where a
        /// segment between two knots on opposite faces would pass through the plates.</summary>
        bool ArcPhase => _approach;

        /// <summary>The approach ends once the ship is in the skim band flying along the ribbon (or,
        /// as a backstop, once the boost has taken off or <see cref="ApproachMaxSeconds"/> have
        /// passed).</summary>
        void UpdateApproach()
        {
            if (_sensors.BoostMultiplier >= 2f || _sensors.Time - _approachStart > ApproachMaxSeconds) { _approach = false; return; }
            if (OffBandDistance() > 0f) return;
            _route.Frame(_s, out _, out Vector3 t, out _, out _);
            Vector3 fwd = _sensors.Rotation * Vector3.forward;
            if (Vector3.Dot(fwd, t) > Mathf.Cos(_p.ApproachEndDegrees * Mathf.Deg2Rad)) _approach = false;
        }

        /// <summary>How far (u) the ship is outside the skimmer's reach of the ribbon, at its own arc.</summary>
        float OffBandDistance()
        {
            _route.Frame(_s, out Vector3 c, out _, out Vector3 r, out Vector3 u);
            Vector3 d = _sensors.Position - c;
            float a = Vector3.Dot(d, r), b = Vector3.Dot(d, u);
            float rho = Mathf.Sqrt(a * a + b * b);
            return rho - SkimRho(Mathf.Atan2(a, b), _s);
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

        static float Unwrap(float angle, float near)
            => angle + Mathf.Round((near - angle) / (2f * Mathf.PI)) * 2f * Mathf.PI;

        // ============================================================================== the line

        /// <summary>The plan's offset at arc <paramref name="s"/> — its distance from the ribbon's centre
        /// line and its angle round it, measured from the plates' up toward their right — without the
        /// pilot's wander.</summary>
        void Offset(List<Key> keys, float s, out float rho, out float phi)
        {
            Vector3 d = SplinePoint(keys, s);
            _route.Frame(s, out Vector3 c, out _, out Vector3 right, out Vector3 up);
            d -= c;
            float a = Vector3.Dot(d, right), b = Vector3.Dot(d, up);
            rho = Mathf.Max(1e-3f, Mathf.Sqrt(a * a + b * b));
            phi = Unwrap(Mathf.Atan2(a, b), SplinePhiNear(keys, s));
        }

        /// <summary>World point of the line at <paramref name="s"/>: the plan's offset, the pilot's
        /// wander on top.</summary>
        Vector3 LinePoint(List<Key> keys, float s) => LinePoint(keys, s, out _, out _);

        Vector3 LinePoint(List<Key> keys, float s, out float rho, out float phi)
        {
            _route.Frame(s, out Vector3 c, out _, out Vector3 right, out Vector3 up);
            Vector3 sp = SplinePoint(keys, s);
            Vector3 d = sp - c;
            float sa = Vector3.Dot(d, right), sb = Vector3.Dot(d, up);
            rho = Mathf.Max(1e-3f, Mathf.Sqrt(sa * sa + sb * sb));
            phi = Unwrap(Mathf.Atan2(sa, sb), SplinePhiNear(keys, s));
            if (_p.LineWander > 0f)
            {
                Wander(s, phi, out float wa, out float wb);
                sp += right * wa + up * wb;
            }
            return sp;
        }

        /// <summary>The pilot's wander off the plan at arc <paramref name="s"/>: across the plates,
        /// and away from them on the side <paramref name="phi"/> is on.</summary>
        void Wander(float s, float phi, out float da, out float db)
        {
            float lambda = Mathf.Max(60f, _p.LineWanderSeconds * _sensors.ThrottleScaler * Mathf.Max(1f, _sensors.MaxBoost));
            da = _p.LineWander * Mathf.Sin(s / lambda * 2f * Mathf.PI + _wanderPhaseA);
            float lift = 0.5f + 0.5f * Mathf.Sin(s / (lambda * 1.37f) * 2f * Mathf.PI + _wanderPhaseB);
            db = (Mathf.Cos(phi) >= 0f ? 1f : -1f) * _p.LineWander * 0.6f * lift;
        }

        /// <summary>True when <paramref name="keys"/> are enough of a line to read: a cubic B-spline
        /// needs four control points. Every plan the optimiser hands over has more; an empty list is a
        /// brain that has not planned yet.</summary>
        static bool IsSpline(List<Key> keys) => keys.Count >= 4;

        /// <summary>
        /// Control point <paramref name="idx"/> of an optimised line. Past either end of the stored
        /// points the line goes on as the cubic its end segment already is (control points on a
        /// cubic in the index make a B-spline that IS that cubic), so it is C-infinity across the
        /// ends — which is what the samples a few metres behind the ship need.
        /// </summary>
        static Vector3 SplineControl(List<Key> keys, int idx)
        {
            int n = keys.Count;
            if (idx >= 0 && idx < n) return keys[idx].P;
            int b = idx < 0 ? 0 : n - 4;
            float x = idx - b;
            float l0 = -(x - 1f) * (x - 2f) * (x - 3f) / 6f;
            float l1 = x * (x - 2f) * (x - 3f) / 2f;
            float l2 = -x * (x - 1f) * (x - 3f) / 2f;
            float l3 = x * (x - 1f) * (x - 2f) / 6f;
            return keys[b].P * l0 + keys[b + 1].P * l1 + keys[b + 2].P * l2 + keys[b + 3].P * l3;
        }

        /// <summary>
        /// World point and its first and second derivatives (per unit of route arc) of an optimised
        /// line: the uniform cubic B-spline of its control points, knot i at <c>keys[i].S</c>.
        ///
        /// <para><b>Why a B-spline and not a Hermite through the points.</b> The optimiser bounds the
        /// stick a line asks for through its third differences, and the third derivative of a uniform
        /// cubic B-spline IS its control points' third difference over h³ (and its second derivative
        /// at the knots their second difference over h²) — so the line flown asks for exactly what
        /// the solver allowed. The first cut interpolated the points with a quintic Hermite whose
        /// slopes and curvatures came from finite differences; that adds sub-grid jerk of several
        /// times the true jerk (a slope error of h² J / 6 at both ends of a span is a jerk error of up
        /// to 10 J inside it), and the line flown asked for 1.3-1.8x what the solver had promised —
        /// enough at 300 u/s to saturate the stick, fall off the line and lose a crystal.</para>
        /// </summary>
        static void SplineEval(List<Key> keys, float s, out Vector3 p, out Vector3 d1, out Vector3 d2)
        {
            float h = Mathf.Max(1e-3f, keys[1].S - keys[0].S);
            float u = (s - keys[0].S) / h;
            int j = Mathf.FloorToInt(u);
            float t = u - j;
            Vector3 p0 = SplineControl(keys, j - 1), p1 = SplineControl(keys, j);
            Vector3 p2 = SplineControl(keys, j + 1), p3 = SplineControl(keys, j + 2);
            float t2 = t * t, t3 = t2 * t, mt = 1f - t;
            p = (p0 * (mt * mt * mt) + p1 * (3f * t3 - 6f * t2 + 4f) + p2 * (-3f * t3 + 3f * t2 + 3f * t + 1f) + p3 * t3) / 6f;
            d1 = (p0 * (-3f * mt * mt) + p1 * (9f * t2 - 12f * t) + p2 * (-9f * t2 + 6f * t + 3f) + p3 * (3f * t2)) / (6f * h);
            d2 = (p0 * mt + p1 * (3f * t - 2f) + p2 * (1f - 3f * t) + p3 * t) / (h * h);
        }

        static Vector3 SplinePoint(List<Key> keys, float s)
        {
            float h = Mathf.Max(1e-3f, keys[1].S - keys[0].S);
            float u = (s - keys[0].S) / h;
            int j = Mathf.FloorToInt(u);
            float t = u - j;
            Vector3 p0 = SplineControl(keys, j - 1), p1 = SplineControl(keys, j);
            Vector3 p2 = SplineControl(keys, j + 1), p3 = SplineControl(keys, j + 2);
            float t2 = t * t, t3 = t2 * t, mt = 1f - t;
            return (p0 * (mt * mt * mt) + p1 * (3f * t3 - 6f * t2 + 4f) + p2 * (-3f * t3 + 3f * t2 + 3f * t + 1f) + p3 * t3) / 6f;
        }

        /// <summary>The angle of the control point nearest <paramref name="s"/>: what an offset read
        /// off an optimised line is unwrapped near.</summary>
        static float SplinePhiNear(List<Key> keys, float s)
        {
            float h = Mathf.Max(1e-3f, keys[1].S - keys[0].S);
            int i = Mathf.Clamp(Mathf.RoundToInt((s - keys[0].S) / h), 0, keys.Count - 1);
            return keys[i].Phi;
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
            // _t[i] and _k[i] are sample index i - window. On the approach the curvature is per unit of
            // the LINE's own length: a line running across the ribbon covers several units of path per
            // unit of ribbon arc, and read per unit of arc its turn is that many times too sharp
            // (squared for the rate-of-change term) — which, off the ribbon at the start, kept the
            // throttle near half. Along the ribbon the two lengths agree to a few per cent, and arc is
            // what the optimiser plans in.
            _t.Clear();
            _k.Clear();
            _ds.Clear();
            for (int q = 1; q < _tangents.Count - 1; q++)
            {
                _t.Add(_tangents[q]);
                float ds = SampleStep;
                if (ArcPhase)
                    ds = Mathf.Max(0.25f * SampleStep, 0.5f * (_smooth[q + 2] - _smooth[q]).magnitude);
                _ds.Add(ds);
                _k.Add((_tangents[q + 1] - _tangents[q - 1]) / (2f * ds));
            }
            _window = window;
        }

        /// <summary>Path length per sample step around each sampled point (parallel to <see cref="_t"/>).</summary>
        readonly List<float> _ds = new List<float>(512);

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
            Vector3 dk = (_k[c + _window] - _k[c - _window]) / (2f * _window * _ds[c]);
            return (Vector3.Cross(t, _k[c]) * v + Vector3.Cross(t, dk) * (v * v / Follow)).magnitude;
        }

        /// <summary>The fastest the sampled line can be flown at sample <paramref name="i"/> inside
        /// <paramref name="budget"/> rad/s of stick.</summary>
        float SampleVMax(int i, float budget)
        {
            int c = i + _window;
            float a = Vector3.Cross(_t[c], _k[c]).magnitude;
            Vector3 dk = (_k[c + _window] - _k[c - _window]) / (2f * _window * _ds[c]);
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
            dk = (_k[2 * w] - _k[0]) / (2f * w * _ds[w]);
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
            // A B-spline reads control points behind the ship too, and the samples the steering
            // smooths over reach 70-odd units back: keep what lies within SplineKeepBehind.
            int drop = 0;
            while (drop + 4 < _keys.Count && _keys[drop].S < _s - SplineKeepBehind) drop++;
            if (drop > 0) _keys.RemoveRange(0, drop);
        }

        // ============================================================================== planning

        /// <summary>True when the ship is further than <see cref="SkimRacerProfile.ReanchorDistance"/>
        /// from the line under it: re-plan from the ship rather than steer back onto a line it has
        /// left.</summary>
        bool OffPlan()
        {
            if (!IsSpline(_keys)) return true;
            _route.Frame(_s, out Vector3 c, out _, out Vector3 right, out Vector3 up);
            Vector3 d = _sensors.Position - c;
            float av = Vector3.Dot(d, right), bv = Vector3.Dot(d, up);
            Offset(_keys, _s, out float r, out float p);
            float ap = r * Mathf.Sin(p), bp = r * Mathf.Cos(p);
            float dx = av - ap, dy = bv - bp;
            return dx * dx + dy * dy > _p.ReanchorDistance * _p.ReanchorDistance;
        }

        /// <summary>The crystal's offset in the plates' frame at arc <paramref name="sc"/>.</summary>
        void CrystalOffset(float sc, out float a, out float b)
        {
            _route.Frame(sc, out Vector3 c, out _, out Vector3 right, out Vector3 up);
            Vector3 d = _crystal - c;
            a = Vector3.Dot(d, right);
            b = Vector3.Dot(d, up);
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
            for (int i = count - 2; i >= 0; i--) _cap[i] = Mathf.Min(_cap[i], _cap[i + 1] + BrakePerUnit * _ds[i + _window]);

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

            // Flown past it without collecting: the next chance is a lap on — unless it is close enough
            // behind to turn round for, which costs a few seconds where the lap costs twenty.
            if (_s > _crystalS + 40f && _route.Closed)
            {
                if (_p.RecoverMissedCrystal && !_seeking
                    && (_crystal - _sensors.Position).sqrMagnitude < _p.RecoverMaxDistance * _p.RecoverMaxDistance)
                {
                    _seeking = true;
                    _seekCrystal = _crystal;
                    _seekStart = _sensors.Time;
                    CrystalsRecovered++;
                    return false;
                }
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
        const float SplineKeepBehind = 160f;  // control points kept behind the ship (the steering's samples reach ~70 u back)

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
            float toCrystal = _haveCrystal && !float.IsNaN(_crystalS) ? _crystalS - _s : float.MaxValue;
            bool inRange = toCrystal <= MaxPlanLength - PlanTail && toCrystal >= 2f * MinGridStep;
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
                ObstacleRhoScale = _p.OptObstacleRhoScale,
                SegmentClearanceSamples = ArcPhase ? _p.ApproachSegmentSamples : 0,
                ArcAware = ArcPhase,
            };
        }

        /// <summary>
        /// Solve a new line from the ship forward. <paramref name="cold"/>: seeded fresh, round the
        /// ribbon each way the crystal can be reached, and the faster flown; otherwise seeded on the
        /// line already being flown and refined.
        ///
        /// <para><b>The hand-over is C2.</b> The line is a B-spline (<see cref="SplineEval"/>) with
        /// knot 0 one step BEHIND the ship and knot 1 at it, and its first three control points are
        /// pinned so that position, slope and curvature at the ship are exactly what the old line
        /// (or, from the ship, its motion and the turn it is already in) has there:
        /// <c>P1 = C - h² C''/6</c>, <c>P0,2 = C ∓ h C' + h² C''/3</c>. A re-plan never moves the
        /// line under the ship and never asks the stick for a jump.</para>
        /// </summary>
        void Optimize(bool fromShip, bool cold)
        {
            float s0 = _s;
            SolveCount++;
            _lastRefresh = _sensors.Time;
            _nextRefresh = _sensors.Time + RefreshInterval;

            // The grid is a fixed fraction of a second of flight: coarse enough at full speed that the
            // solver's slow modes (whole swings sliding along the ribbon) converge in a hundred
            // iterations, fine enough on the ramp to bend round a crystal 150 u away.
            float vRef = Mathf.Max(_sensors.Speed, PredictSkimSpeed(300f));
            float grid = Mathf.Clamp(_p.OptGridSeconds * vRef, MinGridStep, MaxGridStep);
            float ahead = _haveCrystal && !float.IsNaN(_crystalS) ? _crystalS - s0 : float.MaxValue;
            // A crystal is planned for while there are two knots of free line before it: a line re-
            // planned from the ship close to its crystal must still go for it (the first cut planned a
            // plain band line inside three and a half grid steps, and a ship knocked off its line
            // there flew past its crystal and lost a lap).
            bool target = ahead >= 2f * MinGridStep && ahead <= MaxPlanLength - PlanTail;
            float step = grid;
            int count, ic = -1;
            if (target)
            {
                // The crystal on a knot, so its pass is a point of the line and not a blend.
                int nc = Mathf.Max(2, Mathf.RoundToInt(ahead / grid));
                step = ahead / nc;
                ic = nc + 1;
                count = ic + Mathf.CeilToInt(PlanTail / step) + 1;
            }
            else count = Mathf.CeilToInt(MaxPlanLength / step) + 2;
            count = Mathf.Min(count, SkimPathOptimizer.MaxPoints);
            float start = s0 - step;

            _opt.Setup(_route, start, step, count, _sensors.HullHalfExtents.z + _p.ClearanceMargin, HullReach, OptSettings());
            count = _opt.Count;

            // The three pinned control points: where the ship is flying, as a B-spline must see it.
            StartState(fromShip, s0, out Vector3 c, out Vector3 d1, out Vector3 d2);
            float h2 = step * step;
            SetControl(0, c - d1 * step + d2 * (h2 / 3f));
            SetControl(1, c - d2 * (h2 / 6f));
            SetControl(2, c + d1 * step + d2 * (h2 / 3f));

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
                    // A rod: its own thickness, the hull's reach at any roll, a little for the hull's
                    // length when the line crosses it at an angle, and the margin.
                    float r = HullReach + Mathf.Max(ob.HalfExtents.x, ob.HalfExtents.y) + _p.ObstacleMargin + 0.6f;
                    _opt.AddObstacle(ob.Center, ob.Rotation * Vector3.forward, ob.HalfExtents.z, r);
                }
            }

            float sEnd = start + (count - 2) * step;
            int chosen = 0;
            string note;
            if (!cold && _keys.Count > 0)
            {
                SeedFromPlan(count, step);
                SolveLine(count, step, cold: false);
                GridToKeys(_optCands[0]);
                note = "warm";
            }
            else if (!target)
            {
                // No line yet (the start, a reset): merge onto the nearest face's lane; otherwise carry on
                // along the line being flown.
                if (_keys.Count == 0) SeedMerge(count, step);
                else SeedFromPlan(count, step);
                SolveLine(count, step, cold: true);
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
                for (int k = 0; k < options; k++)
                {
                    float phiT = k == 0 ? phiP : phiP - Mathf.Sign(phiP - phi0) * 2f * Mathf.PI;
                    SeedTowards(count, step, ic, phiT, rhoP);
                    SolveLine(count, step, cold: true);
                    GridToKeys(_optCands[k]);
                    float cost = options > 1 ? Evaluate(_optCands[k], s0, sEnd) : 0f;
                    if (cost < bestCost) { bestCost = cost; chosen = k; }
                }
                note = options > 1 ? (chosen == 0 ? "short-way" : "long-way") : "crystal";
            }

            int oldFace = _keys.Count > 0 ? FaceOf(_keys[_keys.Count - 1].Phi) : -1;
            _keys.Clear();
            _keys.AddRange(_optCands[chosen]);
            _planHasCrystal = target;
            _planStep = step;
            int newFace = FaceOf(_keys[_keys.Count - 1].Phi);
            if (oldFace >= 0 && newFace != oldFace) FaceChanges++;
            if (Trace)
                LastChoice = $"{note} it={_opt.Iterations} d={_opt.WorstDemand:F2} miss={_opt.CrystalMiss:F1} clr={_opt.WorstClearDeficit:F1}/{_opt.WorstSegmentDeficit:F1} obs={_opt.WorstObstacleDeficit:F1}/{_opt.ActiveObstacleCount}a{_opt.ObstacleCount}of{(_obstacles != null ? _obstacles.Count : 0)}";
        }

        /// <summary>Pins optimiser point <paramref name="i"/> to world control point
        /// <paramref name="p"/>, as an offset in the ribbon's frame at its knot.</summary>
        void SetControl(int i, Vector3 p)
        {
            _route.Frame(_opt.Start + i * _opt.Step, out Vector3 c, out _, out Vector3 r, out Vector3 u);
            Vector3 d = p - c;
            _opt.SetPoint(i, Vector3.Dot(d, r), Vector3.Dot(d, u));
        }

        /// <summary>
        /// World position, and slope and curvature per unit of route arc, of where the ship is flying
        /// at arc <paramref name="s0"/>: read off the line it is on, or — from the ship — its own
        /// position, the way it is going and the turn the transform is already closing on (the
        /// commanded rotation's lead times the follow rate).
        /// </summary>
        void StartState(bool fromShip, float s0, out Vector3 c, out Vector3 d1, out Vector3 d2)
        {
            if (!fromShip && IsSpline(_keys))
            {
                SplineEval(_keys, s0, out c, out d1, out d2);
                return;
            }
            _route.Frame(s0, out _, out Vector3 tangent, out _, out _);
            Vector3 fwd = _sensors.Course.sqrMagnitude > 0.5f ? _sensors.Course.normalized : _sensors.Rotation * Vector3.forward;
            float v = Mathf.Max(_sensors.Speed, 1f);
            Quaternion gap = _sensors.CommandedRotation * Quaternion.Inverse(_sensors.Rotation);
            gap.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            Vector3 omega = axis.sqrMagnitude > 1e-6f ? axis.normalized * (angle * Mathf.Deg2Rad * Follow) : Vector3.zero;
            Vector3 kappa = Vector3.Cross(omega, fwd) / v;
            float along = Mathf.Max(0.25f, Vector3.Dot(fwd, tangent));
            c = _sensors.Position;
            d1 = fwd / along;
            d2 = kappa / (along * along);
        }

        /// <summary>
        /// Seed the free control points on the line being flown: its offsets at the new knots, less a
        /// sixth of their second difference — the control points whose B-spline passes through those
        /// offsets, to second order — so a warm start does not shave every swing by <c>h² κ / 6</c>
        /// per re-plan.
        /// </summary>
        void SeedFromPlan(int count, float step)
        {
            float start = _opt.Start;
            for (int i = 2; i < count + 1; i++)
            {
                PlanOffset(_keys, start + i * step, out float a, out float b);
                _seedA[i] = a;
                _seedB[i] = b;
            }
            for (int i = 3; i < count; i++)
            {
                float a = _seedA[i] - (_seedA[i - 1] - 2f * _seedA[i] + _seedA[i + 1]) / 6f;
                float b = _seedB[i] - (_seedB[i - 1] - 2f * _seedB[i] + _seedB[i + 1]) / 6f;
                _opt.SetPoint(i, a, b);
            }
        }

        /// <summary>
        /// Seed the line from the ship onto its nearest face's lane, blended over a stretch long
        /// enough to turn onto it — never round the ribbon, and never through it. Seeding the lane
        /// itself one knot after a ship a hundred units off hands the solver a step it can only smooth
        /// by overshooting, and at the start that overshoot went up through the plates and round the
        /// far edge.
        /// </summary>
        void SeedMerge(int count, float step)
        {
            float s0 = _opt.Start;
            float a2 = _opt.A(2), b2 = _opt.B(2);
            float phi2 = Mathf.Atan2(a2, b2);
            int face = FaceOf(phi2);
            float excess = Mathf.Max(0f, Mathf.Sqrt(a2 * a2 + b2 * b2) - SkimRho(phi2, s0 + 2f * step));
            float merge = Mathf.Clamp(1.5f * excess, 4f * step, 300f);
            for (int i = 3; i < count; i++)
            {
                float s = s0 + i * step;
                _line.Offset(face, s, out float la, out float lb);
                float t = Mathf.Clamp01((i - 2f) * step / merge);
                float w = t * t * (3f - 2f * t);
                float a = a2 + (la - a2) * w, b = b2 + (lb - b2) * w;
                float phi = Mathf.Atan2(a, b);
                float rho = Mathf.Max(Mathf.Sqrt(a * a + b * b), ClearRho(phi, s) + 0.75f);
                _opt.SetPoint(i, rho * Mathf.Sin(phi), rho * Mathf.Cos(phi));
            }
        }

        readonly float[] _seedA = new float[SkimPathOptimizer.MaxPoints + 2];
        readonly float[] _seedB = new float[SkimPathOptimizer.MaxPoints + 2];

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

        /// <summary>
        /// Solve the line and the speed it is flown at together. The line is first solved for the
        /// speed the ship could reach skimming flat out (<see cref="PlanSpeeds"/>); wherever that
        /// line still asks for more stick than the budget at that speed — a crystal too close to swing
        /// to at full boost, a twist of the ribbon too sharp — the speed there is lowered to what the
        /// line CAN be flown at, braked back toward the ship (<see cref="FitSpeeds"/>), and the line
        /// re-solved for the slower ship, which can bend tighter. The plan then carries its speeds and
        /// the throttle follows them, so the ship slows BEFORE the bend that needs it, smoothly,
        /// instead of finding out in the bend and shutting the throttle there.
        /// </summary>
        void SolveLine(int count, float step, bool cold)
        {
            if (_p.OptSeedPolishPasses > 0) _opt.PolishSeed(_p.OptSeedPolishPasses);
            if (cold)
            {
                PlanSpeeds(count, step, useBand: false);
                _opt.Solve(_p.OptColdIterations);
            }
            PlanSpeeds(count, step, useBand: true);
            _opt.Solve(_p.OptWarmIterations);
            for (int pass = 0; pass < _p.SpeedFitPasses; pass++)
            {
                if (!FitSpeeds(count, step)) break;
                _opt.Solve(_p.OptWarmIterations);
            }
        }

        readonly float[] _fitV = new float[SkimPathOptimizer.MaxPoints];

        /// <summary>
        /// Lowers each stretch's design speed to the fastest the solved line can be flown there inside
        /// the planning budget, then brakes the profile back toward the ship at the rate the throttle
        /// can shed speed. The ship's own speed at the knots it is already on cannot change. True when
        /// any knot got slower.
        /// </summary>
        bool FitSpeeds(int count, float step)
        {
            float budget = _p.SpeedFitAuthority * TurnRateRad * _p.RollBonus;
            for (int i = 0; i < count; i++) _fitV[i] = _opt.DesignSpeed(i);
            bool changed = false;
            for (int i = 2; i <= count - 3; i++)
            {
                float vm = _opt.MaxSpeed(i, budget, Mathf.Max(_fitV[i], _fitV[i + 1]));
                if (vm < _fitV[i] - 0.5f) { _fitV[i] = vm; changed = true; }
                if (vm < _fitV[i + 1] - 0.5f) { _fitV[i + 1] = vm; changed = true; }
            }
            if (!changed) return false;
            for (int i = count - 2; i >= 2; i--) _fitV[i] = Mathf.Min(_fitV[i], _fitV[i + 1] + _p.PlanBrakePerUnit * _opt.SegmentLength(i));
            for (int i = 0; i < count; i++) _opt.SetSpeed(i, Mathf.Max(_fitV[i], MinPlanSpeed));
            return true;
        }

        const float MinPlanSpeed = 30f;

        /// <summary>The speed the plan is to be flown at, at arc <paramref name="s"/>.</summary>
        float PlanSpeedAt(float s)
        {
            int n = _keys.Count;
            float h = Mathf.Max(1e-3f, _keys[1].S - _keys[0].S);
            float u = Mathf.Clamp((s - _keys[0].S) / h, 0f, n - 1.001f);
            int i = Mathf.FloorToInt(u);
            return Mathf.Lerp(_keys[i].V, _keys[i + 1].V, u - i);
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
                float dt = (ArcPhase && i < count - 1 ? _opt.SegmentLength(i) : step) / Mathf.Max(v, 20f);
                if (!useBand || _opt.InBand(i)) bm = Mathf.Min(bmMax, bm + perUnit * step);
                if (bm > 1f) bm = Mathf.Max(1f, bm - BoostDecay * dt);
                float targetV = _sensors.ThrottleScaler * thr * bm;
                v += (targetV - v) * (1f - Mathf.Exp(-Follow * dt));
            }
        }

        /// <summary>The optimiser's points as the line's keys: each a B-spline control point
        /// (<see cref="Key.P"/>), with its offset angle kept for unwrapping reads of the line.</summary>
        void GridToKeys(List<Key> keys)
        {
            keys.Clear();
            int n = _opt.Count;
            float h = _opt.Step, s0 = _opt.Start;
            float phiNear = Mathf.Atan2(_opt.A(1), _opt.B(1));
            for (int i = 0; i < n; i++)
            {
                float phi = Unwrap(Mathf.Atan2(_opt.A(i), _opt.B(i)), phiNear);
                keys.Add(new Key { S = s0 + i * h, Phi = phi, P = _opt.Point(i), V = _opt.DesignSpeed(i) });
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

        // ============================================================================== obstacles

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
            float run = 0f;
            for (int i = 0; i < count; i++)
            {
                cap = Mathf.Min(cap, SampleVMax(i, budget) + BrakePerUnit * run);
                run += _ds[i + _window];
            }

            float full = _sensors.ThrottleScaler * Mathf.Max(1f, _sensors.BoostMultiplier);
            float want = Mathf.Clamp01(_p.Throttle);
            if (_p.FollowPlanSpeed && IsSpline(_keys) && full >= 1f)
            {
                // The plan's speed, led by the throttle lag: the speed lerps toward its target at the
                // follow rate, so a target of v_plan + v dv/ds / k makes it ARRIVE at the planned speed
                // where the plan wants it rather than 0.7 s later.
                float vp = PlanSpeedAt(_s);
                float slope = (PlanSpeedAt(_s + 12f) - PlanSpeedAt(_s - 12f)) / 24f;
                float lead = vp + v * slope / Follow;
                if (lead < full * want) want = Mathf.Clamp01(lead / full);
            }
            // Over the cap, shut the throttle: the thrust lerp then sheds speed as fast as it can, and the
            // cap is read back from every bend ahead at about that rate. (Braking more gently onto the
            // cap — aiming the throttle at it, or at the throttle that lands on its braking curve by
            // the next scan — was raced and did not pay.) Under the cap, aim at it.
            if (full < 1f || cap >= full * want) _throttle = want;
            else if (v > cap) _throttle = 0f;
            else _throttle = Mathf.Clamp01(Mathf.Min(want, cap / full));
            if (Trace)
                DebugThrottle = $"cap={Mathf.Min(cap, 999f):F0} vplan={(IsSpline(_keys) ? PlanSpeedAt(_s) : -1f):F0} want={want:F2} full={full:F0}";
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
            if (Trace)
            {
                _route.Frame(_s, out _, out _, out Vector3 dbgR, out Vector3 dbgU);
                Vector3 ff = dTl * (v * v), pp = -e * (wn * wn), dd = -eDot * (2f * _p.TrackDamping * wn);
                DebugSteer = $"ff=({Vector3.Dot(ff, dbgR):F0},{Vector3.Dot(ff, dbgU):F0}) P=({Vector3.Dot(pp, dbgR):F0},{Vector3.Dot(pp, dbgU):F0}) " +
                             $"D=({Vector3.Dot(dd, dbgR):F0},{Vector3.Dot(dd, dbgU):F0}) e=({Vector3.Dot(e, dbgR):F1},{Vector3.Dot(e, dbgU):F1})";
            }
            Vector3 omega = Vector3.Cross(fwd, accel) / v;
            // How fast the turn the line asks for is changing: the line's own curvature change at this
            // speed, plus the speed change at this curvature. The second is the term that matters when
            // the throttle moves: the throttle lerp sheds 1.5 v per second with the throttle shut, and
            // a ship holding a 77°/s bend through that keeps turning at the rate the OLD speed needed —
            // the same commanded lead on a slower ship is a tighter circle, and it flies off the line
            // into whatever is inside the bend.
            float vDot = Follow * (_sensors.ThrottleScaler * _throttle * Mathf.Max(1f, _sensors.BoostMultiplier) - s.Speed);
            Vector3 omegaRate = Vector3.Cross(tl, ddTl) * (v * v) + Vector3.Cross(fwd, dTl) * vDot;
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
