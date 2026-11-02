using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>How an AI Dolphin takes a Blast Run gate (SWITCHBACK.md "AI: the blast planner").</summary>
    public enum SwitchbackAIPlan : byte
    {
        Direct = 0,   // nothing standing between the pilot and the ring: fly the ring steering
        Thread = 1,   // thread the door's eye down the bore (the ring steering, through the centre)
        Blast = 2,    // take the key crystal with the nose (and slot) on the obstacle, then fly the cleared lane
    }

    /// <summary>
    /// How SMART an AI Dolphin is at Blast Run, one row per <c>AIDifficulty</c> (1 Easy .. 4 Legendary;
    /// <see cref="ForLevel"/>). Serializable so a designer can override the table on the controller
    /// (<c>SwitchbackController.aiSkillOverrides</c>). Every number is documented in SWITCHBACK.md.
    /// </summary>
    [Serializable]
    public struct SwitchbackAISkill
    {
        [Tooltip("0..1: chance a decision is the cost-optimal plan; otherwise the pilot falls back on its habit (thread the eye).")]
        public float DecisionQuality;
        [Tooltip("Multiplier on the Blast plan's cost (> 1 = reluctant to blast, 1 = judges it fairly).")]
        public float BlastBias;
        [Tooltip("Degrees the nose is off the obstacle at key contact (mean magnitude).")]
        public float AimErrorDegrees;
        [Tooltip("Degrees the slot's roll is off the obstacle's long axis at key contact (at most).")]
        public float RollErrorDegrees;
        [Tooltip("Seconds of error in the pilot's read of the movers' clock (the lane is checked at ETA +- this).")]
        public float TimingErrorSeconds;
        [Tooltip("Seconds between re-plans on the same gate (0 = only at the gate and when its key is taken).")]
        public float ReplanSeconds;
        [Tooltip("u: the farthest key crystal the pilot will consider when it plans.")]
        public float CrystalSearchRadius;

        public SwitchbackAISkill(float decisionQuality, float blastBias, float aimErrorDegrees, float rollErrorDegrees,
                                 float timingErrorSeconds, float replanSeconds, float crystalSearchRadius)
        {
            DecisionQuality = decisionQuality; BlastBias = blastBias; AimErrorDegrees = aimErrorDegrees;
            RollErrorDegrees = rollErrorDegrees; TimingErrorSeconds = timingErrorSeconds; ReplanSeconds = replanSeconds;
            CrystalSearchRadius = crystalSearchRadius;
        }

        /// <summary>The shipped table. Index = <c>(int)AIDifficulty</c>: 1 Easy, 2 Medium, 3 Hard, 4 Legendary (Yash's
        /// "Easy / Medium / Medium-Hard / Hard", 2026-10-29). Anything else reads as Medium (AIDifficultyRules.Default).</summary>
        public static SwitchbackAISkill ForLevel(int level) => level switch
        {
            1 => new SwitchbackAISkill(0.30f, 1.60f, 14f, 70f, 0.60f, 3f, 170f),
            3 => new SwitchbackAISkill(0.85f, 1.05f, 4f, 15f, 0.15f, 0.75f, 520f),
            4 => new SwitchbackAISkill(1.00f, 1.00f, 0.75f, 3f, 0.03f, 0.25f, 700f),
            _ => new SwitchbackAISkill(0.60f, 1.15f, 8f, 35f, 0.35f, 1.5f, 360f),
        };
    }

    /// <summary>
    /// The blast as the planner models it: a cone down the nose stretched into a SLOT along the vessel's up (gape) axis
    /// (<c>AOEConicExplosion</c>: a capsule of radius <c>TanCore * d</c> dragged <c>+- TanGape * d</c> along up, at
    /// distance d down the nose). Deliberately NARROWER than the Dolphin's blast at rest, so a plan the model says
    /// covers the lane really does; it is a planning model, not the effect's numbers (the effect asset is untouched).
    /// </summary>
    public readonly struct SwitchbackBlastSlot
    {
        public readonly float Reach, TanCore, TanGape;
        public SwitchbackBlastSlot(float reach, float tanCore, float tanGape) { Reach = reach; TanCore = tanCore; TanGape = tanGape; }
        public static SwitchbackBlastSlot Default => new(400f, 0.12f, 0.22f);

        /// <summary>Whether <paramref name="p"/> (padded by <paramref name="pad"/>) lies inside the slot fired from
        /// <paramref name="origin"/> down <paramref name="forward"/> with its gape along <paramref name="up"/>.</summary>
        public bool Covers(Vector3 origin, Vector3 forward, Vector3 up, Vector3 p, float pad)
        {
            Vector3 w = p - origin;
            float d = Vector3.Dot(w, forward);
            if (d <= 0f || d > Reach) return false;
            w -= forward * d;
            float a = Vector3.Dot(w, up);
            float b2 = (w - up * a).sqrMagnitude;
            float over = Mathf.Max(Mathf.Abs(a) - TanGape * d, 0f);
            float r = TanCore * d + pad;
            return over * over + b2 <= r * r;
        }
    }

    /// <summary>What the planner chose for one gate, and why (the costs, in seconds).</summary>
    public struct SwitchbackAIDecision
    {
        public SwitchbackAIPlan Plan;
        public int Gate, Station;
        public bool Optimal;                  // false = the pilot's habit overrode the cheapest plan
        public float CostDirect, CostThread, CostBlast;   // float.PositiveInfinity = not available
        public Vector3 Key, FireForward, FireUp, AimPoint; // Blast only: where to fire from, the slot, the nose's point
        public float AimErrorDegrees, RollErrorDegrees;     // what the skill put on this blast
        public float Coverage;                // share of the standing lane prisms the (erred) slot covers, 0..1
        public int Standing, LaneHits;        // standing obstacle prisms; standing prisms in the thread lane
        public string Why;                    // a constant reason tag for the log (no allocation)
    }

    /// <summary>The live world the planner reads, behind an interface so it plans headless (sb_harness) exactly as in
    /// a match. Implementations must not allocate.</summary>
    public interface ISwitchbackAIWorld
    {
        /// <summary>Whether the layout's prism <paramref name="k"/> still stands (not blasted).</summary>
        bool PrismStanding(int k);
        /// <summary>Whether station <paramref name="station"/>'s key crystal is there to take.</summary>
        bool KeyAvailable(int station);
    }

    /// <summary>
    /// The Blast Run AI's brain (SWITCHBACK.md "AI: the blast planner"): per gate, Direct / Thread / Blast by cost from
    /// the real layout (stations, keys, field prisms, movers posed at the pilot's ETA off the shared clock) and the live
    /// world (what still stands, which keys are there). Pure C#: no Unity objects, no allocation after construction,
    /// deterministic for a given seed. Runs per gate, on a re-plan or at the skill's low rate - never per frame.
    /// </summary>
    public sealed class SwitchbackBlastPlanner
    {
        // ── the cost model (seconds) ──
        public const float MinSpeed = 40f;              // u/s floor for every time estimate
        public const float ThreadSlowAtEye18 = 0.35f;   // threading an 18 u eye costs this share of the leg's time again
        public const float DoubleDoorSlow = 0.15f;      // I4's second door
        public const float HitPenalty = 0.6f;           // s per standing prism in the lane (a bump, a lost line)
        public const float DangerHitPenalty = 1.5f;     // s per standing DANGER prism in the lane
        public const float KeyContactSeconds = 0.2f;    // the blast itself
        public const float DriftFreeDegrees = 15f;      // a turn this small onto the fire line costs nothing
        public const float DriftTurnRate = 110f;        // deg/s, the Dolphin's turn (SWITCHBACK.md)
        public const float DriftSetupSeconds = 0.6f;    // extra when the turn onto the fire line needs a full drift (> 45 deg)
        public const float ThreadLanePad = 8f;          // u round the centre line: the lane a threading pilot sweeps
        public const float BlastLanePad = 20f;          // u round the centre line: the lane a full-speed pilot sweeps after the blast
        public const float BlockedLane = 10f;           // u round the line to the key that must be clear
        public const int BlockedHits = 2;               // standing prisms on that line that make the key unreachable
        public const float ClearedShare = 0.05f;        // an obstacle with <= this share (or <= 2 prisms) standing is gone
        public const float FieldMargin = 40f;           // u past the field's half-width that still counts as the obstacle

        readonly SwitchbackFeatureLayout _layout;
        readonly SwitchbackBlastSlot _slot;
        readonly float _bore, _eye;
        readonly int[] _stationOfGate;      // course index -> station, -1 = not blocked
        readonly int[] _obstacle;           // flat prism indices, per station [_start[s], _start[s+1])
        readonly int[] _start;
        readonly int[] _moverOf;            // prism -> mover, -1 = static
        readonly float[] _pad;              // prism -> half its largest extent

        public SwitchbackFeatureLayout Layout => _layout;
        public SwitchbackBlastSlot Slot => _slot;
        public float BoreRadius => _bore;

        public SwitchbackBlastPlanner(SwitchbackFeatureLayout layout, int courseCount, int intensity)
            : this(layout, courseCount, SwitchbackBlastRun.BoreRadius(intensity), SwitchbackBlastRun.PortFor(intensity),
                   SwitchbackBlastRun.CellsHalf(intensity) * SwitchbackBlastRun.CellPitch, SwitchbackBlastSlot.Default) { }

        public SwitchbackBlastPlanner(SwitchbackFeatureLayout layout, int courseCount, float boreRadius, float eyeRadius,
                                      float fieldHalfWidth, SwitchbackBlastSlot slot)
        {
            _layout = layout ?? new SwitchbackFeatureLayout();
            _slot = slot;
            _bore = boreRadius;
            _eye = Mathf.Max(eyeRadius, 1f);

            _stationOfGate = new int[Mathf.Max(courseCount, 0)];
            for (int g = 0; g < _stationOfGate.Length; g++) _stationOfGate[g] = -1;
            for (int s = 0; s < _layout.Stations.Count; s++)
            {
                int g = _layout.Stations[s].Gate;
                if (g >= 0 && g < _stationOfGate.Length) _stationOfGate[g] = s;
            }

            int n = _layout.Prisms.Count;
            _moverOf = new int[n];
            _pad = new float[n];
            for (int k = 0; k < n; k++)
            {
                _moverOf[k] = -1;
                var sc = _layout.Prisms[k].Scale;
                _pad[k] = 0.5f * Mathf.Max(sc.x, Mathf.Max(sc.y, sc.z));
            }
            for (int m = 0; m < _layout.Movers.Count; m++)
            {
                var mv = _layout.Movers[m];
                for (int j = 0; j < mv.Count; j++)
                    if (mv.FirstPrism + j < n) _moverOf[mv.FirstPrism + j] = m;
            }

            // Each station's obstacle: every prism in the box from just past the door to the key, as wide as its field.
            var flat = new List<int>(n);
            _start = new int[_layout.Stations.Count + 1];
            float half = fieldHalfWidth + FieldMargin;
            for (int s = 0; s < _layout.Stations.Count; s++)
            {
                _start[s] = flat.Count;
                var st = _layout.Stations[s];
                Vector3 along = st.Key - st.Door;
                float len = along.magnitude;
                if (len < 1f) continue;
                along /= len;
                float back = st.DoubleDoor ? 2f * SwitchbackBlastRun.DoorSetback + 8f : 8f;
                for (int k = 0; k < n; k++)
                {
                    Vector3 w = _layout.Prisms[k].Position - st.Door;
                    float t = Vector3.Dot(w, along);
                    if (t < -back || t > len) continue;
                    if ((w - along * t).sqrMagnitude > half * half) continue;
                    flat.Add(k);
                }
            }
            _start[_layout.Stations.Count] = flat.Count;
            _obstacle = flat.ToArray();
        }

        /// <summary>The station blocking course gate <paramref name="gate"/>, or -1.</summary>
        public int StationFor(int gate) => gate >= 0 && gate < _stationOfGate.Length ? _stationOfGate[gate] : -1;

        /// <summary>How many prisms make up station <paramref name="station"/>'s obstacle (door + field), as laid.</summary>
        public int ObstacleSize(int station) => station < 0 ? 0 : _start[station + 1] - _start[station];

        /// <summary>The obstacle's prisms still standing.</summary>
        public int Standing(int station, ISwitchbackAIWorld world)
        {
            if (station < 0) return 0;
            int c = 0;
            for (int i = _start[station]; i < _start[station + 1]; i++) if (world.PrismStanding(_obstacle[i])) c++;
            return c;
        }

        /// <summary>Where layout prism <paramref name="k"/> stands at shared time <paramref name="t"/> (a mover's closed-form pose).</summary>
        public Vector3 PositionAt(int k, float t)
        {
            var p = _layout.Prisms[k];
            int m = _moverOf[k];
            if (m < 0) return p.Position;
            var mv = _layout.Movers[m];
            mv.PoseAt(t, out Quaternion spin, out Vector3 offset);
            mv.Apply(spin, offset, p.Position, p.Rotation, out Vector3 pos, out _);
            return pos;
        }

        /// <summary>Standing obstacle prisms within <paramref name="band"/> (+ the prism's own size) of segment a-b, each mover
        /// checked at its pose at <paramref name="t"/> and at t +- <paramref name="timingError"/> (any of the three counts).</summary>
        public int LaneHits(int station, ISwitchbackAIWorld world, Vector3 a, Vector3 b, float band, float t, float timingError)
        {
            if (station < 0) return 0;
            int hits = 0;
            for (int i = _start[station]; i < _start[station + 1]; i++)
            {
                int k = _obstacle[i];
                if (!world.PrismStanding(k)) continue;
                float r = band + _pad[k];
                if (_moverOf[k] < 0)
                {
                    if (SwitchbackFeatures.DistanceToSegment(_layout.Prisms[k].Position, a, b) <= r) hits++;
                    continue;
                }
                if (SwitchbackFeatures.DistanceToSegment(PositionAt(k, t), a, b) <= r
                    || (timingError > 0f && (SwitchbackFeatures.DistanceToSegment(PositionAt(k, t - timingError), a, b) <= r
                                         || SwitchbackFeatures.DistanceToSegment(PositionAt(k, t + timingError), a, b) <= r)))
                    hits++;
            }
            return hits;
        }

        /// <summary>Standing prisms ANYWHERE in the layout within <paramref name="band"/> (+ their size) of segment a-b at
        /// time <paramref name="t"/>: is the way to a key clear? A box test first, so most prisms cost a few compares.</summary>
        public int PathHits(ISwitchbackAIWorld world, Vector3 a, Vector3 b, float band, float t)
        {
            float reach = band + SwitchbackBlastRun.CellPitch * 4f;   // a mover's whole swing
            Vector3 lo = Vector3.Min(a, b) - Vector3.one * reach, hi = Vector3.Max(a, b) + Vector3.one * reach;
            int hits = 0;
            for (int k = 0, n = _layout.Prisms.Count; k < n; k++)
            {
                Vector3 p = _layout.Prisms[k].Position;
                if (p.x < lo.x || p.y < lo.y || p.z < lo.z || p.x > hi.x || p.y > hi.y || p.z > hi.z) continue;
                if (!world.PrismStanding(k)) continue;
                if (_moverOf[k] >= 0) p = PositionAt(k, t);
                if (SwitchbackFeatures.DistanceToSegment(p, a, b) <= band + _pad[k]) hits++;
            }
            return hits;
        }

        float LanePenalty(int station, ISwitchbackAIWorld world, Vector3 a, Vector3 b, float band, float t, float timingError,
                          in SwitchbackBlastSlot? slotNull, Vector3 o, Vector3 f, Vector3 u, out int hits, out int covered)
        {
            hits = covered = 0;
            float pen = 0f;
            if (station < 0) return 0f;
            for (int i = _start[station]; i < _start[station + 1]; i++)
            {
                int k = _obstacle[i];
                if (!world.PrismStanding(k)) continue;
                Vector3 p = PositionAt(k, t);
                float r = band + _pad[k];
                bool inLane = SwitchbackFeatures.DistanceToSegment(p, a, b) <= r;
                if (!inLane && _moverOf[k] >= 0 && timingError > 0f)
                    inLane = SwitchbackFeatures.DistanceToSegment(PositionAt(k, t - timingError), a, b) <= r
                          || SwitchbackFeatures.DistanceToSegment(PositionAt(k, t + timingError), a, b) <= r;
                if (!inLane) continue;
                hits++;
                if (slotNull.HasValue && slotNull.Value.Covers(o, f, u, p, 0.5f * _pad[k])) { covered++; continue; }
                pen += _layout.Prisms[k].Kind == CosmicShore.Data.PrismKind.Danger ? DangerHitPenalty : HitPenalty;
            }
            return pen;
        }

        /// <summary>
        /// The slot that best covers station <paramref name="station"/>'s standing obstacle fired from its key: the nose on
        /// the door (down the leg, through the field's bore), the gape (up) along the obstacle's long axis across the
        /// leg - the principal axis of the standing prisms' offsets, so a slab, a beam or a slider track is cut along its length.
        /// </summary>
        public void SolveAim(int station, ISwitchbackAIWorld world, float t, out Vector3 forward, out Vector3 up)
        {
            var st = _layout.Stations[station];
            forward = (st.Door - st.Key).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = st.FlowAxis;
            SwitchbackBlastRun.DoorBasis(forward, out Vector3 u, out Vector3 v);
            float suu = 0f, svv = 0f, suv = 0f;
            for (int i = _start[station]; i < _start[station + 1]; i++)
            {
                int k = _obstacle[i];
                if (!world.PrismStanding(k)) continue;
                Vector3 w = PositionAt(k, t) - st.Key;
                float a = Vector3.Dot(w, u), b = Vector3.Dot(w, v);
                suu += a * a; svv += b * b; suv += a * b;
            }
            float theta = 0.5f * Mathf.Atan2(2f * suv, suu - svv);
            up = (u * Mathf.Cos(theta) + v * Mathf.Sin(theta)).normalized;
        }

        /// <summary>Share (0..1) of the standing prisms in the blast lane (<see cref="BlastLanePad"/> round the key-to-ring line)
        /// that a slot fired from the key down <paramref name="forward"/> / <paramref name="up"/> covers. 1 when the lane is empty.</summary>
        public float LaneCoverage(int station, ISwitchbackAIWorld world, Vector3 ring, float t, Vector3 forward, Vector3 up)
        {
            var st = _layout.Stations[station];
            LanePenalty(station, world, st.Key, ring, BlastLanePad, t, 0f, _slot, st.Key, forward, up, out int hits, out int covered);
            return hits == 0 ? 1f : covered / (float)hits;
        }

        /// <summary>
        /// Plan gate <paramref name="gate"/> (course index) for a pilot at <paramref name="self"/> flying at
        /// <paramref name="speed"/>, with the ring at <paramref name="ring"/>, at shared time <paramref name="clock"/>.
        /// <paramref name="seed"/> drives the skill's mistakes (decision, aim, roll): the same inputs always give the same
        /// decision. No allocation.
        /// </summary>
        public SwitchbackAIDecision Decide(int gate, Vector3 self, float speed, Vector3 ring, float clock,
                                          in SwitchbackAISkill skill, ISwitchbackAIWorld world, uint seed)
        {
            var d = new SwitchbackAIDecision
            {
                Gate = gate, Station = StationFor(gate), Coverage = 1f,
                CostDirect = float.PositiveInfinity, CostThread = float.PositiveInfinity, CostBlast = float.PositiveInfinity,
                Optimal = true,
            };
            float v = Mathf.Max(speed, MinSpeed);
            float l = Vector3.Distance(self, ring);
            float eta = l / v;
            int s = d.Station;

            if (s < 0)
            {
                d.CostDirect = eta; d.Plan = SwitchbackAIPlan.Direct; d.Why = "open";
                return d;
            }
            d.Standing = Standing(s, world);
            int size = ObstacleSize(s);
            if (d.Standing <= 2 || d.Standing <= ClearedShare * size)
            {
                d.CostDirect = eta; d.Plan = SwitchbackAIPlan.Direct; d.Why = "cleared";
                return d;
            }

            var st = _layout.Stations[s];
            uint rng = Hash(seed ^ (uint)(gate * 7919));

            // Thread: the bore, through the eye - always there (the layout keeps it clear), slowed by the eye and the lane.
            float slow = ThreadSlowAtEye18 * (18f / _eye) + (st.DoubleDoor ? DoubleDoorSlow : 0f);
            float threadPen = LanePenalty(s, world, self, ring, ThreadLanePad, clock + 0.5f * eta, skill.TimingErrorSeconds,
                                          null, default, default, default, out int laneHits, out _);
            d.LaneHits = laneHits;
            d.CostThread = eta * (1f + slow) + threadPen;

            // Blast: the station's key, when it is there, near enough, still ahead, and the way to it is clear.
            Vector3 fire = (st.Door - st.Key).normalized;
            float toKey = Vector3.Distance(self, st.Key);
            bool ahead = Vector3.Dot(st.Key - self, fire) > 0f;
            string why = null;
            if (!world.KeyAvailable(s)) why = "key-gone";
            else if (!ahead) why = "key-behind";
            else if (toKey > skill.CrystalSearchRadius) why = "key-far";
            else if (PathHits(world, self, st.Key, BlockedLane, clock) >= BlockedHits) why = "key-blocked";

            if (why == null)
            {
                SolveAim(s, world, clock, out Vector3 f0, out Vector3 u0);
                // The skill's mistakes, from the seed: the nose off by ~AimError in a random direction, the roll off by up to RollError.
                float aimErr = skill.AimErrorDegrees * (0.5f + Next01(ref rng));
                float aimDir = 360f * Next01(ref rng);
                float rollErr = skill.RollErrorDegrees * (2f * Next01(ref rng) - 1f);
                Vector3 side = Quaternion.AngleAxis(aimDir, f0) * u0;
                Vector3 f = (Quaternion.AngleAxis(aimErr, side) * f0).normalized;
                Vector3 u = (Quaternion.AngleAxis(rollErr, f) * Vector3.ProjectOnPlane(u0, f)).normalized;

                float keyEta = toKey / v;
                float blastPen = LanePenalty(s, world, st.Key, ring, BlastLanePad, clock + keyEta, skill.TimingErrorSeconds,
                                             _slot, st.Key, f, u, out int hits, out int covered);
                Vector3 approach = (st.Key - self).normalized;
                float turn = Vector3.Angle(approach, fire);
                float align = Mathf.Max(0f, turn - DriftFreeDegrees) / DriftTurnRate + (turn > 45f ? DriftSetupSeconds : 0f);
                float detour = (toKey + Vector3.Distance(st.Key, ring) - l) / v;
                d.CostBlast = (eta + Mathf.Max(detour, 0f) + align + KeyContactSeconds + blastPen) * Mathf.Max(skill.BlastBias, 0.01f);
                d.Key = st.Key; d.FireForward = f; d.FireUp = u;
                d.AimPoint = st.Key + f * Vector3.Distance(st.Key, st.Door);
                d.AimErrorDegrees = aimErr; d.RollErrorDegrees = rollErr;
                d.Coverage = hits == 0 ? 1f : covered / (float)hits;
            }

            bool blastBest = d.CostBlast < d.CostThread;
            if (Next01(ref rng) < skill.DecisionQuality)
            {
                d.Plan = blastBest ? SwitchbackAIPlan.Blast : SwitchbackAIPlan.Thread;
                d.Why = blastBest ? "cheapest" : why ?? "cheapest";
            }
            else
            {
                // The habit of a pilot who did not think it through: thread the eye.
                d.Plan = SwitchbackAIPlan.Thread;
                d.Optimal = !blastBest;
                d.Why = blastBest ? "habit" : why ?? "cheapest";
            }
            return d;
        }

        // ── deterministic noise (no System.Random: no allocation, same on every platform) ──
        public static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
            return x == 0 ? 0x9e3779b9 : x;
        }

        static float Next01(ref uint state)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state & 0xFFFFFF) / 16777216f;
        }
    }
}
