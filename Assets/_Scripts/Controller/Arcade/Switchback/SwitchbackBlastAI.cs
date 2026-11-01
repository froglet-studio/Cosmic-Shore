using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// SERVER ONLY. The Blast Run AI's executor (SWITCHBACK.md "AI: the blast planner"): asks
    /// <see cref="SwitchbackBlastPlanner"/> what to do at each blocked gate and flies the answer through the gate race's
    /// existing hooks - <c>TryOverrideAim</c> (the approach to the key) and <c>TryOverrideDriftLook</c> (the nose on the
    /// obstacle at contact). Thread and Direct are the gate race's own ring steering through the centre, which is the bore.
    ///
    /// <para>Per pilot it plans when the pilot starts a gate, every <see cref="SwitchbackAISkill.ReplanSeconds"/>, and at once
    /// when its key is taken by a rival (and the blast is not yet fired). No per-frame allocation: state is per pilot,
    /// created once; the key scan runs at 10 Hz; strings are built only on a decision. Nothing is networked - the
    /// pilots it steers are server-owned, the movers and keys are read off the shared clock and the crystal manager.</para>
    /// </summary>
    public sealed class SwitchbackBlastAI : ISwitchbackAIWorld
    {
        enum Phase { None, Setup, Strike, Done }

        sealed class Pilot
        {
            public int Threaded = -1;
            public float NextPlan;
            public SwitchbackAIDecision D;
            public Phase Phase;
            public bool KeyTaken;
            public int StandingAtPlan;
            public uint Seed;
            public int Plans;
            // Any-prism sense (SWITCHBACK.md "AI: any prism in the way").
            public float NextSense;
            public SwitchbackAvoidAction Avoid;
            public Vector3 AvoidAim, BlastCrystal, BlastLook;
            public int Avoids, Blasts;
            // Emergency sweep (SWITCHBACK.md "AI: the emergency sweep").
            public bool EmergOn;
            public float EmergHoldUntil, HotUntil;
            public Vector3 EmergSteer;
            public int Emergencies;
            // Debug view: the last sense tube and the last sweep.
            public Vector3 SenseFrom, SenseTo, SweepFrom, SweepTo, SweepHit;
            public float SenseRadius, SweepRadius;
            public bool SenseBlocked, SweepHitAny;
        }

        /// <summary>Debug view on (the controller's aiSenseDebug): lines for every sense and sweep, Info-level dodge logs.</summary>
        public static bool DebugSense;
        static readonly RaycastHit[] s_rayHits = new RaycastHit[32];
        /// <summary>After a sweep hit the pilot re-sweeps every frame for this long (else every other frame, staggered).</summary>
        const float HotSeconds = 0.6f;
        /// <summary>An emergency dodge is held at least this long, so the hull actually turns before the next look.</summary>
        const float EmergHoldSeconds = 0.18f;
        const float EmergSteerDistance = 60f;

        /// <summary>The TrailBlocks layer: every prism - course blocks, rival trails, own trail.</summary>
        static int s_prismMask = -1;
        const int SenseCap = 128;
        static readonly Collider[] s_hits = new Collider[SenseCap];
        static readonly Vector3[] s_pts = new Vector3[SenseCap];
        static readonly float[] s_radii = new float[SenseCap];
        static readonly Vector3[] s_crystals = new Vector3[32];
        /// <summary>u ahead of the hull the sense tube starts (its own fresh trail is behind it).</summary>
        const float SenseStart = 6f;

        /// <summary>A key crystal counts as present when a managed crystal is this close to its laid point.</summary>
        const float KeyMatchDistance = 4f;
        const float KeyScanSeconds = 0.1f;
        /// <summary>Off the fire line by more than this and the pilot first flies to the setup point behind the key.</summary>
        const float SetupAngle = 25f;
        const float SetupBack = 120f;
        const float StrikeThrough = 40f;
        /// <summary>From this close the nose is held on the obstacle (drift look) for the contact.</summary>
        const float LookRange = 160f;

        readonly SwitchbackBlastPlanner _planner;
        readonly ISwitchbackAIWorld _prisms;
        readonly Func<IPlayer, SwitchbackAISkill> _skillFor;
        readonly Func<IPlayer, SwitchbackAvoidSkill> _avoidFor;
        readonly Func<IPlayer, int> _levelFor;
        readonly Func<int, int> _ringIndexFor;
        readonly Func<int> _raceLength;
        readonly IReadOnlyList<RaceGate> _course;
        readonly int _seed;
        readonly Dictionary<IPlayer, Pilot> _pilots = new();
        readonly bool[] _keyPresent;
        float _nextKeyScan;

        public SwitchbackBlastAI(SwitchbackBlastPlanner planner, ISwitchbackAIWorld prisms, IReadOnlyList<RaceGate> course,
                                 Func<int, int> ringIndexFor, Func<int> raceLength, Func<IPlayer, SwitchbackAISkill> skillFor, int seed,
                                 Func<IPlayer, SwitchbackAvoidSkill> avoidFor = null, Func<IPlayer, int> levelFor = null)
        {
            _levelFor = levelFor ?? (_ => 2);
            if (s_prismMask < 0) s_prismMask = LayerMask.GetMask("TrailBlocks");
            // One line per course: if mask is 0 or layer is -1 the TrailBlocks layer is gone and no AI can see a prism.
            CSDebug.Log($"[Switchback AI] sense active mask={s_prismMask} layer={LayerMask.NameToLayer("TrailBlocks")} " +
                      $"stations={planner.Layout.Stations.Count} debug={DebugSense}");
            _avoidFor = avoidFor ?? (_ => SwitchbackAvoidSkill.ForLevel(2));
            _planner = planner;
            _prisms = prisms;
            _course = course;
            _ringIndexFor = ringIndexFor;
            _raceLength = raceLength;
            _skillFor = skillFor;
            _seed = seed;
            _keyPresent = new bool[planner.Layout.Stations.Count];
            for (int i = 0; i < _keyPresent.Length; i++) _keyPresent[i] = true;
        }

        public SwitchbackBlastPlanner Planner => _planner;

        /// <summary>This pilot's sense and sweep tallies so far (the hit counter's race summary). False before its first plan.</summary>
        public bool TryCounts(IPlayer p, out int avoids, out int blasts, out int emergencies)
        {
            avoids = blasts = emergencies = 0;
            if (p == null || !_pilots.TryGetValue(p, out var st)) return false;
            avoids = st.Avoids; blasts = st.Blasts; emergencies = st.Emergencies;
            return true;
        }

        public bool PrismStanding(int k) => _prisms == null || _prisms.PrismStanding(k);
        public bool KeyAvailable(int station) => station >= 0 && station < _keyPresent.Length && _keyPresent[station];

        static float Clock()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening ? nm.ServerTime.TimeAsFloat : Time.time;
        }

        void ScanKeys()
        {
            if (Time.time < _nextKeyScan) return;
            _nextKeyScan = Time.time + KeyScanSeconds;
            var stations = _planner.Layout.Stations;
            var crystals = Crystal.Active;
            float sq = KeyMatchDistance * KeyMatchDistance;
            for (int s = 0; s < stations.Count; s++)
            {
                bool present = false;
                Vector3 key = stations[s].Key;
                for (int i = 0, n = crystals.Count; i < n && !present; i++)
                {
                    var c = crystals[i];
                    if (c == null || c.CrystalManager == null) continue;
                    present = (c.transform.position - key).sqrMagnitude <= sq;
                }
                _keyPresent[s] = present;
            }
        }

        Pilot StateFor(IPlayer p)
        {
            if (_pilots.TryGetValue(p, out var st)) return st;
            st = new Pilot { Seed = SwitchbackBlastPlanner.Hash(unchecked((uint)_seed * 2654435761u ^ (uint)p.PlayerNetId * 40503u)) };
            _pilots[p] = st;
            return st;
        }

        /// <summary>The aim override: true (with the point) while the pilot is flying a blast to its key.</summary>
        public bool TryAim(IPlayer p, out Vector3 aim)
        {
            aim = default;
            var tf = p?.Vessel?.Transform;
            if (tf == null) return false;
            int threaded = p.RoundStats?.SwitchesThreaded ?? 0;
            if (threaded < 0 || threaded >= _raceLength()) return false;

            ScanKeys();
            var st = StateFor(p);
            Vector3 self = tf.position;
            var skill = _skillFor(p);

            if (threaded != st.Threaded)
            {
                if (st.Threaded >= 0) LogResult(p, st);
                st.Threaded = threaded;
                st.Plans = 0;
                Plan(p, st, self, in skill);
            }
            else
            {
                bool keyGone = st.D.Plan == SwitchbackAIPlan.Blast && st.Phase != Phase.Done && !KeyAvailable(st.D.Station);
                if (keyGone && Vector3.Distance(self, st.D.Key) < 30f) { st.KeyTaken = true; st.Phase = Phase.Done; }
                else if (keyGone || (skill.ReplanSeconds > 0f && Time.time >= st.NextPlan && st.Phase != Phase.Done && st.Phase != Phase.Strike)) Plan(p, st, self, in skill);
            }

            bool gateAim = GateBlastAim(st, self, out aim);
            // On the strike the obstacle ahead IS the point: no dodging it.
            if (gateAim && st.Phase == Phase.Strike) { st.Avoid = SwitchbackAvoidAction.Clear; return true; }

            int ring = _ringIndexFor(threaded);
            Vector3 goal = gateAim ? aim
                : ring >= 0 && ring < _course.Count ? _course[ring].Position + LaneOffset(p, ring, _course[ring]) : self + tf.forward * 100f;
            if (Sense(p, st, tf, self, goal, ring, out Vector3 avoidAim)) { aim = avoidAim; return true; }
            return gateAim;
        }

        bool GateBlastAim(Pilot st, Vector3 self, out Vector3 aim)
        {
            aim = default;
            if (st.D.Plan != SwitchbackAIPlan.Blast || st.Phase == Phase.Done) return false;
            Vector3 key = st.D.Key, fire = st.D.FireForward;
            Vector3 toKey = key - self;
            float dist = toKey.magnitude;
            if (Vector3.Dot(-toKey, fire) > 5f) { st.Phase = Phase.Done; return false; }   // past the key's plane
            float off = dist > 1f ? Vector3.Angle(toKey, fire) : 0f;
            if (dist > 60f && off > SetupAngle)
            {
                st.Phase = Phase.Setup;
                aim = key - fire * Mathf.Min(SetupBack, 0.5f * dist);
            }
            else
            {
                st.Phase = Phase.Strike;
                aim = key + fire * StrikeThrough;
            }
            return true;
        }

        /// <summary>
        /// This pilot's own lane through ring <paramref name="ring"/> (<see cref="SwitchbackPrismAvoidance.LaneOffset"/>).
        /// Zero at a blocked gate: there the line is the door's eye or the blast lane, which the planner chose.
        /// </summary>
        public Vector3 LaneOffset(IPlayer p, int ring, in RaceGate gate)
        {
            if (p == null || _planner.StationFor(ring) >= 0) return Vector3.zero;
            var st = StateFor(p);
            return SwitchbackPrismAvoidance.LaneOffset(st.Seed ^ (uint)(ring * 7919), gate.Axis, gate.Radius, _avoidFor(p).LaneShare);
        }

        /// <summary>
        /// SERVER ONLY. The any-prism sense: every <see cref="SwitchbackAvoidSkill.ReactionSeconds"/> (staggered per
        /// pilot) one non-alloc capsule overlap down the line to <paramref name="goal"/> on the TrailBlocks layer - course
        /// blocks, rival trails, own trail alike. Blocked: blast it from a crystal ahead when the slot would clear it and
        /// dodging is poor, else steer down the clearest corridor. The answer is held until the next look.
        /// </summary>
        bool Sense(IPlayer p, Pilot st, Transform tf, Vector3 self, Vector3 goal, int ring, out Vector3 aim)
        {
            aim = default;
            var skill = _avoidFor(p);
            if (st.Avoid == SwitchbackAvoidAction.Blast)
            {
                // Held until the crystal is taken or passed.
                Vector3 toC = st.BlastCrystal - self;
                bool passed = Vector3.Dot(toC, st.BlastLook - st.BlastCrystal) < -5f;
                if (!passed && CrystalAt(st.BlastCrystal)) { aim = st.BlastCrystal + (st.BlastLook - st.BlastCrystal).normalized * StrikeThrough; return true; }
                Log(p, st, ring, SwitchbackAvoidAction.Clear, passed ? "blast-done" : "blast-key-gone", 0f, 0);
                st.Avoid = SwitchbackAvoidAction.Clear;
            }
            if (Time.time < st.NextSense)
            {
                if (st.Avoid != SwitchbackAvoidAction.Avoid) return false;
                aim = self + st.AvoidAim; return true;
            }
            st.NextSense = Time.time + skill.ReactionSeconds * (0.85f + 0.3f * ((st.Seed >> 8 & 0xFF) / 255f));

            Vector3 to = goal - self;
            float dist = to.magnitude;
            if (dist < 1f) return false;
            Vector3 dir = to / dist;
            // Range by time to impact: at boost (well past 100 u/s) a fixed 70-110 u is under a second of warning.
            float speed = p.Vessel?.VesselStatus?.Speed ?? 0f;
            float range = Mathf.Min(Mathf.Max(skill.SenseRange, speed * (1.2f + skill.ReactionSeconds)), dist + 10f);
            if (s_prismMask < 0) s_prismMask = LayerMask.GetMask("TrailBlocks");
            float probe = skill.Clearance * 2.5f;
            int n = Physics.OverlapCapsuleNonAlloc(self + dir * SenseStart, self + dir * range, probe, s_hits, s_prismMask,
                                                   QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var b = s_hits[i].bounds;
                s_pts[i] = b.center;
                s_radii[i] = Mathf.Min(b.extents.magnitude, 12f);
            }
            var prev = st.Avoid;
            st.SenseFrom = self + dir * SenseStart; st.SenseTo = self + dir * range; st.SenseRadius = probe;
            st.SenseBlocked = n > 0;
            if (DebugSense) Debug.DrawLine(st.SenseFrom, st.SenseTo, n > 0 ? Color.yellow : Color.green, skill.ReactionSeconds);
            if (n == 0 || !SwitchbackPrismAvoidance.Blocked(self, dir, range, skill.Clearance, s_pts, n, out float along, s_radii))
            {
                if (prev != SwitchbackAvoidAction.Clear) Log(p, st, ring, SwitchbackAvoidAction.Clear, "clear", 0f, n);
                st.Avoid = SwitchbackAvoidAction.Clear;
                return false;
            }

            Vector3 corridor = SwitchbackPrismAvoidance.ChooseCorridor(self, dir, range, s_pts, n, in skill, out float clear, s_radii);
            float turn = Vector3.Angle(corridor, dir);
            Vector3 heading = tf.forward;
            Vector3 blocker = SwitchbackPrismAvoidance.BlockerCentroid(self, dir, range, skill.Clearance * 2f, s_pts, n, out int blocking);
            int nc = ManagedCrystals();
            if ((clear < skill.Clearance || turn > 25f) &&
                SwitchbackPrismAvoidance.TryBlast(self, heading, blocker, skill.Clearance * 2f, s_pts, n, s_crystals, nc,
                                                  _planner.Slot, in skill, out Vector3 crystal, out _, out _, out float cov))
            {
                st.Avoid = SwitchbackAvoidAction.Blast;
                st.BlastCrystal = crystal; st.BlastLook = blocker; st.Blasts++;
                if (CSDebug.IsVerbose(CSLogChannel.ArcadeMatch))
                    Log(p, st, ring, SwitchbackAvoidAction.Blast, $"crystal={Vector3.Distance(self, crystal):F0}u cover={cov:P0}", along, blocking);
                aim = crystal + (blocker - crystal).normalized * StrikeThrough;
                return true;
            }
            st.Avoid = SwitchbackAvoidAction.Avoid;
            st.AvoidAim = corridor * range;
            if (prev != SwitchbackAvoidAction.Avoid) { st.Avoids++; if (CSDebug.IsVerbose(CSLogChannel.ArcadeMatch)) Log(p, st, ring, SwitchbackAvoidAction.Avoid, $"turn={turn:F0} clear={clear:F0}u", along, blocking); }
            aim = self + st.AvoidAim;
            return true;
        }

        /// <summary>
        /// SERVER ONLY, every frame (AIPilot's emergency hook): one SphereCastNonAlloc at hull radius down the REAL velocity
        /// (course x speed, which a drift locks away from the nose), reach = speed x the level's
        /// <see cref="SwitchbackAvoidSkill.EmergencySeconds"/>, plus a second down the nose while turning (the side the
        /// hull swings into). A hit means impact inside that time: steer hard off the line - AIPilot lets a drift go and
        /// the boost driver's line-hold stand down. On a strike (the gate blast or a crystal blast) only hits nearer than
        /// the crystal count: the crystal's blast clears what is behind it. Every other frame when cold, every frame for
        /// <see cref="HotSeconds"/> after a hit. No allocation.
        /// </summary>
        public bool Emergency(IPlayer p, Vector3 pos, Vector3 vel, Vector3 fwd, out Vector3 steer)
        {
            steer = default;
            if (p == null) return false;
            var st = StateFor(p);
            float now = Time.time;
            if (st.EmergOn && now < st.EmergHoldUntil) { steer = pos + st.EmergSteer * EmergSteerDistance; return true; }
            bool hot = now < st.HotUntil;
            if (!hot && ((Time.frameCount + (int)(st.Seed & 1u)) & 1) != 0) { st.EmergOn = false; return false; }

            float speed = vel.magnitude;
            if (speed < 1f) { st.EmergOn = false; return false; }
            Vector3 dir = vel / speed;
            int level = _levelFor(p);
            var skill = _avoidFor(p);
            float reach = SwitchbackPrismAvoidance.EmergencyReach(speed, SwitchbackAvoidSkill.EmergencySeconds(level));
            float radius = SwitchbackPrismAvoidance.ShipRadius + skill.Clearance * 0.3f;
            if (s_prismMask < 0) s_prismMask = LayerMask.GetMask("TrailBlocks");

            // On a strike the crystal is the point: only what is in front of it is in the way.
            float limit = reach;
            bool striking = st.Avoid == SwitchbackAvoidAction.Blast || (st.D.Plan == SwitchbackAIPlan.Blast && st.Phase == Phase.Strike);
            if (striking)
            {
                Vector3 c = st.Avoid == SwitchbackAvoidAction.Blast ? st.BlastCrystal : st.D.Key;
                float along = Vector3.Dot(c - pos, dir);
                if (along > 0f) limit = Mathf.Min(limit, along - 2f);
            }

            bool hit = Sweep(pos, dir, radius, limit, out float hitDist, out Vector3 hitAt, out Vector3 normal);
            if (!hit && Vector3.Angle(dir, fwd) > 8f)
                hit = Sweep(pos, fwd, radius, limit * 0.6f, out hitDist, out hitAt, out normal);

            st.SweepFrom = pos; st.SweepTo = pos + dir * Mathf.Max(limit, 0f); st.SweepRadius = radius;
            st.SweepHitAny = hit; st.SweepHit = hitAt;
            if (DebugSense)
            {
                Debug.DrawLine(pos, st.SweepTo, hit ? Color.red : Color.cyan);
                if (hit) Debug.DrawLine(pos, hitAt, Color.magenta, 0.3f);
            }
            if (!hit) { st.EmergOn = false; return false; }

            float urgency = 1f - hitDist / Mathf.Max(limit, 1f);
            Vector3 up = Vector3.Cross(dir, Vector3.Cross(fwd, dir)).sqrMagnitude > 0.01f ? fwd : Vector3.up;
            st.EmergSteer = SwitchbackPrismAvoidance.EscapeDirection(dir, pos - hitAt, normal, up, urgency);
            st.HotUntil = now + HotSeconds;
            st.EmergHoldUntil = now + EmergHoldSeconds;
            if (!st.EmergOn)
            {
                st.Emergencies++;
                if (DebugSense || CSDebug.IsVerbose(CSLogChannel.ArcadeMatch))
                {
                string line = $"[Switchback AI] emergency pilot={p.PlayerNetId} impactIn={hitDist / speed:F2}s dist={hitDist:F0}u " +
                              $"speed={speed:F0} reach={reach:F0}u level={level} drifting={p.Vessel?.VesselStatus?.IsDrifting} " +
                              $"striking={striking} emergencies={st.Emergencies}";
                if (DebugSense) CSDebug.Log(line); else CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, line);
                }
            }
            st.EmergOn = true;
            steer = pos + st.EmergSteer * EmergSteerDistance;
            return true;
        }

        /// <summary>The nearest TrailBlocks hit down <paramref name="dir"/> (start-overlaps skipped: the hull's own fresh trail).</summary>
        static bool Sweep(Vector3 pos, Vector3 dir, float radius, float dist, out float hitDist, out Vector3 centre, out Vector3 normal)
        {
            hitDist = float.MaxValue; centre = default; normal = default;
            if (dist <= 0.5f) return false;
            int n = Physics.SphereCastNonAlloc(pos, radius, dir, s_rayHits, dist, s_prismMask, QueryTriggerInteraction.Collide);
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                ref RaycastHit h = ref s_rayHits[i];
                if (h.distance <= 0f || h.distance >= hitDist || h.collider == null) continue;
                hitDist = h.distance; centre = h.collider.bounds.center; normal = h.normal; any = true;
            }
            return any;
        }

        static int ManagedCrystals()
        {
            var crystals = Crystal.Active;
            int nc = 0;
            for (int i = 0, n = crystals.Count; i < n && nc < s_crystals.Length; i++)
            {
                var c = crystals[i];
                if (c == null || c.CrystalManager == null) continue;
                s_crystals[nc++] = c.transform.position;
            }
            return nc;
        }

        static bool CrystalAt(Vector3 at)
        {
            var crystals = Crystal.Active;
            float sq = KeyMatchDistance * KeyMatchDistance;
            for (int i = 0, n = crystals.Count; i < n; i++)
            {
                var c = crystals[i];
                if (c != null && c.CrystalManager != null && (c.transform.position - at).sqrMagnitude <= sq) return true;
            }
            return false;
        }

        static void Log(IPlayer p, Pilot st, int ring, SwitchbackAvoidAction action, string detail, float along, int blocking) =>
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Switchback AI] prism gate={ring} action={action} ahead={(float.IsInfinity(along) ? 0f : along):F0}u " +
                $"blocking={blocking} {detail} avoids={st.Avoids} blasts={st.Blasts} pilot={p.PlayerNetId}");

        /// <summary>The drift-look override: on the strike, the nose (and so the blast cone) on the obstacle.</summary>
        public bool TryDriftLook(IPlayer p, out Vector3 look)
        {
            look = default;
            if (p == null || !_pilots.TryGetValue(p, out var st)) return false;
            if (st.Avoid == SwitchbackAvoidAction.Blast)
            {
                var t = p.Vessel?.Transform;
                if (t == null || Vector3.Distance(t.position, st.BlastCrystal) > LookRange) return false;
                look = st.BlastLook;
                return true;
            }
            if (st.D.Plan != SwitchbackAIPlan.Blast || st.Phase != Phase.Strike) return false;
            var tf = p.Vessel?.Transform;
            if (tf == null || Vector3.Distance(tf.position, st.D.Key) > LookRange) return false;
            look = st.D.AimPoint;
            return true;
        }

        void Plan(IPlayer p, Pilot st, Vector3 self, in SwitchbackAISkill skill)
        {
            int ring = _ringIndexFor(st.Threaded);
            if (ring < 0 || ring >= _course.Count) return;
            var prev = st.D;
            float speed = p.Vessel?.VesselStatus?.Speed ?? 0f;
            st.D = _planner.Decide(ring, self, speed, _course[ring].Position, Clock(), in skill, this,
                                   st.Seed ^ (uint)(st.Threaded * 92821) ^ (uint)(st.Plans * 68917));
            st.Plans++;
            st.NextPlan = Time.time + Mathf.Max(skill.ReplanSeconds, 0.25f);
            if (st.D.Plan != SwitchbackAIPlan.Blast) st.Phase = Phase.None;
            else if (prev.Plan != SwitchbackAIPlan.Blast || prev.Gate != st.D.Gate) st.Phase = Phase.None;
            if (st.Plans == 1) { st.StandingAtPlan = st.D.Standing; st.KeyTaken = false; }
            if ((st.Plans == 1 || prev.Plan != st.D.Plan) && CSDebug.IsVerbose(CSLogChannel.ArcadeMatch))
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                    $"[Switchback AI] gate={ring} plan={st.D.Plan} crystal=" +
                    (st.D.Plan == SwitchbackAIPlan.Blast ? $"{Vector3.Distance(self, st.D.Key):F0}u" : "none") +
                    $" aimErr={st.D.AimErrorDegrees:F1} roll={st.D.RollErrorDegrees:F0} cover={st.D.Coverage:P0}" +
                    $" cost thread={Fmt(st.D.CostThread)} blast={Fmt(st.D.CostBlast)} why={st.D.Why}" +
                    $" result={(st.Plans == 1 ? "planned" : "replanned")} pilot={p.PlayerNetId}");
        }

        void LogResult(IPlayer p, Pilot st)
        {
            if (st.D.Station < 0 && st.D.Plan == SwitchbackAIPlan.Direct) return;   // open gate: nothing to say
            if (!CSDebug.IsVerbose(CSLogChannel.ArcadeMatch)) return;   // the strings below are the only work left
            int now = st.D.Station >= 0 ? _planner.Standing(st.D.Station, this) : 0;
            string result = st.D.Plan == SwitchbackAIPlan.Blast
                ? (st.KeyTaken ? $"blasted standing {st.StandingAtPlan}->{now}" : "missed-key")
                : st.D.Plan == SwitchbackAIPlan.Thread ? (st.D.Why == "key-gone" ? "rival-took-key threaded" : "threaded") : "direct";
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Switchback AI] gate={st.D.Gate} plan={st.D.Plan} crystal={(st.KeyTaken ? "taken" : "none")}" +
                $" aimErr={st.D.AimErrorDegrees:F1} result={result} pilot={p.PlayerNetId}");
        }

        /// <summary>The sense view (aiSenseDebug: the controller's OnDrawGizmos, or the in-game Third Eye's "AI sense"
        /// overlay): per pilot the sense tube (green clear / yellow blocked), the emergency sweep (cyan clear / red hit,
        /// magenta to the hit) and the dodge (white). Drawn into <paramref name="sink"/> - Gizmos or the Game view.</summary>
        public void DrawSenseGizmos(IDebugLineSink sink)
        {
            foreach (var kv in _pilots)
            {
                var st = kv.Value;
                var tube = st.SenseBlocked ? Color.yellow : Color.green;
                sink.Line(st.SenseFrom, st.SenseTo, tube);
                sink.WireSphere(st.SenseTo, st.SenseRadius, tube);
                var sweep = st.SweepHitAny ? Color.red : Color.cyan;
                sink.Line(st.SweepFrom, st.SweepTo, sweep);
                sink.WireSphere(st.SweepTo, st.SweepRadius, sweep);
                if (st.SweepHitAny) sink.WireSphere(st.SweepHit, 3f, Color.magenta);
                if (st.EmergOn) sink.Line(st.SweepFrom, st.SweepFrom + st.EmergSteer * EmergSteerDistance, Color.white);
                if (st.Avoid == SwitchbackAvoidAction.Avoid) sink.Line(st.SweepFrom, st.SweepFrom + st.AvoidAim, new Color(1f, 0.6f, 0f));
            }
        }

        static string Fmt(float s) => float.IsInfinity(s) ? "-" : s.ToString("F2");

        /// <summary>The plan view (aiPlanGizmos: the controller's OnDrawGizmosSelected, or the in-game Third Eye's
        /// "AI plan" overlay): per pilot planning a blast, the line to its key, the fire line (yellow), the gape axis
        /// (cyan) and the aim point; a threading pilot's line to the ring it threads.</summary>
        public void DrawGizmos(IDebugLineSink sink)
        {
            foreach (var kv in _pilots)
            {
                var d = kv.Value.D;
                var tf = kv.Key?.Vessel?.Transform;
                if (tf == null) continue;
                if (d.Plan != SwitchbackAIPlan.Blast)
                {
                    if (kv.Value.Threaded >= 0 && kv.Value.Threaded < _raceLength())
                        sink.Line(tf.position, _course[_ringIndexFor(kv.Value.Threaded)].Position,
                                  d.Plan == SwitchbackAIPlan.Thread ? new Color(1f, 0.5f, 0f) : Color.green);
                    continue;
                }
                var keyCol = kv.Value.Phase == Phase.Done ? Color.gray : Color.magenta;
                sink.Line(tf.position, d.Key, keyCol);
                sink.WireSphere(d.Key, 6f, keyCol);
                sink.Line(d.Key, d.AimPoint, Color.yellow);
                sink.WireSphere(d.AimPoint, 8f, Color.yellow);
                sink.Line(d.Key - d.FireUp * 40f, d.Key + d.FireUp * 40f, Color.cyan);
            }
        }
    }
}
