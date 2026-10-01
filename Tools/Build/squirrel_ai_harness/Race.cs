// A Skim Race: N Squirrels, each with its own domain crystal, on one ribbon, until every racer has
// collected the target or the clock runs out. See World.cs for what is transcribed and from where.
using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Unity.Mathematics;
using UnityEngine;

namespace SquirrelAiHarness
{
    sealed class RacerStats
    {
        public float FinishTime = -1f;
        public int Crystals;
        public int SkimHits, RailSkimHits;
        public int RibbonTouches, RailTouches;
        public float TimeAtFullBoost;
        public float DistanceFlown;
        public float MaxCrossTrack;
        public double CrossTrackSum;
        public int CrossTrackSamples;
        public float TimeToFirstFullBoost = -1f;
        /// <summary>Seconds lost against flying at top speed, by the reason the ship was slower:
        /// the pilot's throttle lifted, the boost multiplier not at its cap, a hull contact's
        /// slow-down, anything else (the speed lerp catching up).</summary>
        public float LostThrottle, LostBoost, LostContact, LostOther;
        /// <summary>Per lap: the race clock when it was completed, and the seconds the skimmer was
        /// over a ribbon prism during it.</summary>
        public readonly List<float> LapTimes = new();
        public readonly float[] InBand = new float[8];
        public readonly float[] LapDuration = new float[8];
        public List<string> Events = new();
        /// <summary>One row per crystal: seconds since the previous one, route arc covered, whether
        /// the brain changed face for it, minimum speed, peak commanded-vs-actual gap.</summary>
        public readonly List<(float dt, float ds, bool face, float vmin, float gap, float bm0)> Segments = new();
        public float SegStartT, SegStartS, SegVmin = 1e9f, SegGap, SegBm0 = 1f;
        public int SegFaces;
    }

    sealed class Racer
    {
        public int Id;
        public Plant Plant;
        public SkimRacerBrain Brain;
        public RacerStats Stats = new();
        public Vector3 Crystal;
        public int AnchorIndex;
        public float NextRailAt;
        public bool WantDrift;
        public readonly HashSet<int> SkimTrack = new(), HullTrack = new(), SkimRails = new(), HullRails = new();
        public readonly HashSet<int> NextSkimTrack = new(), NextHullTrack = new(), NextSkimRails = new(), NextHullRails = new();
        public readonly List<SkimObstacle> LastObstacles = new();
        public bool Finished => Stats.FinishTime >= 0f;
    }

    sealed class RaceOptions
    {
        public int Intensity = 2;
        public int Racers = 1;
        public int Seed = 1;
        public float Dt = 1f / 60f;
        public float DtJitter = 0f;
        public float TimeLimit = 240f;
        public bool Gamepad = false;
        public Func<int, SkimRacerProfile> Profile = _ => SkimRacerProfile.Expert();
        public bool Trace = false;
        public bool Ghost = false;
        public float TraceFrom = 0f, TraceTo = float.MaxValue, TraceDt = 0.25f;
    }

    sealed class Race
    {
        readonly Config _c;
        readonly RaceOptions _o;
        readonly TrackDef _def;
        public readonly List<TrackPrism> Track;
        readonly Grid<int> _trackGrid = new(40f);
        readonly List<Rail> _rails = new();
        readonly Grid<int> _railGrid = new(20f);
        public readonly List<Racer> Racers = new();
        public readonly SkimRoute Route;
        readonly System.Random _rng;
        readonly List<int> _scratch = new();
        readonly List<SkimObstacle> _obstacles = new();
        float _maxShell;
        public float Time;
        int _fixedStep = -1;

        public Race(Config c, RaceOptions o)
        {
            _c = c;
            _o = o;
            _def = c.Tracks[o.Intensity - 1];
            _rng = new System.Random(o.Seed);
            Track = TrackBuilder.Build(c, _def);
            for (int i = 0; i < Track.Count; i++)
            {
                _trackGrid.Add(Track[i].Position, i);
                _maxShell = Mathf.Max(_maxShell, Track[i].ShellSemi.magnitude);
            }

            var prisms = new List<SkimRoutePrism>(Track.Count);
            foreach (var t in Track) prisms.Add(new SkimRoutePrism(t.Position, t.Rotation, t.ShellSemi, t.Marker));
            Route = new SkimRoute(prisms, closed: true);

            // GameDataSO hands each player a RANDOM spawn pose from the authored list.
            var spawnOrder = new List<int>();
            for (int i = 0; i < c.Spawns.Count; i++) spawnOrder.Add(i);
            Shuffle(spawnOrder);
            for (int i = 0; i < o.Racers; i++)
            {
                var pose = c.Spawns[spawnOrder[i % spawnOrder.Count]];
                var r = new Racer
                {
                    Id = i,
                    Plant = new Plant(c, pose) { Gamepad = o.Gamepad },
                    Brain = new SkimRacerBrain(Route, o.Profile(i), o.Seed * 7919 + i),
                    AnchorIndex = 0,
                };
                r.Crystal = CrystalAround(0);   // the first batch: every crystal around anchor 0
                Racers.Add(r);
            }
        }

        void Shuffle(List<int> l)
        {
            for (int i = l.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (l[i], l[j]) = (l[j], l[i]);
            }
        }

        Vector3 OnUnitSphere()
        {
            double z = _rng.NextDouble() * 2 - 1, a = _rng.NextDouble() * Math.PI * 2;
            double r = Math.Sqrt(Math.Max(0, 1 - z * z));
            return new Vector3((float)(r * Math.Cos(a)), (float)(r * Math.Sin(a)), (float)z);
        }

        Vector3 CrystalAround(int anchor) => _def.Anchors[anchor] + OnUnitSphere() * _c.Jitter;

        public int Target => _def.Target;

        public bool Run()
        {
            while (Time < _o.TimeLimit)
            {
                float dt = _o.Dt;
                if (_o.DtJitter > 0f) dt *= 1f + (float)(_rng.NextDouble() * 2 - 1) * _o.DtJitter;
                Step(dt);
                bool all = true;
                foreach (var r in Racers) all &= r.Finished;
                if (all) return true;
            }
            return false;
        }

        void Step(float dt)
        {
            foreach (var r in Racers)
            {
                if (r.Finished) continue;
                var p = r.Plant;
                var sensors = new SkimRacerSensors
                {
                    Time = Time,
                    DeltaTime = dt,
                    Position = p.Position,
                    Rotation = p.Rotation,
                    CommandedRotation = p.Commanded,
                    Course = p.Course,
                    Speed = p.Speed,
                    BoostMultiplier = p.Boost,
                    MaxBoost = _c.BoostMax,
                    ThrottleScaler = _c.ThrottleScaler,
                    PitchRateDegrees = p.PitchScaler,
                    YawRateDegrees = p.YawScaler,
                    RollRateDegrees = p.RollScaler,
                    FollowRate = _c.FollowRate,
                    SkimRadius = _c.SkimRadius,
                    HullHalfExtents = _c.HullHalf,
                    IsDrifting = p.IsDrifting,
                    HasCrystal = true,
                    CrystalPosition = r.Crystal,
                    CrystalRadius = _c.CaptureRadius,
                };
                GatherObstacles(r);
                r.LastObstacles.Clear();
                r.LastObstacles.AddRange(_obstacles);
                var cmd = r.Brain.Tick(sensors, _obstacles);
                p.XSum = cmd.XSum; p.YSum = cmd.YSum; p.YDiff = cmd.YDiff; p.XDiff = cmd.XDiff;
                p.LeftTrigger = cmd.Drift ? 1f : 0f;
                if (cmd.Drift && !r.WantDrift) p.BeginDrift();
                else if (!cmd.Drift && r.WantDrift) p.EndDrift();
                r.WantDrift = cmd.Drift;
            }

            foreach (var r in Racers)
            {
                if (r.Finished) continue;
                Vector3 before = r.Plant.Position;
                r.Plant.Step(dt);
                r.Stats.DistanceFlown += Vector3.Distance(before, r.Plant.Position);
            }

            Time += dt;
            int fixedStep = (int)Math.Floor(Time / 0.04f);
            bool physicsTick = fixedStep != _fixedStep;
            _fixedStep = fixedStep;

            foreach (var r in Racers) if (!r.Finished) SpawnRails(r);
            foreach (var r in Racers)
            {
                if (r.Finished) continue;
                Contacts(r);
                if (physicsTick) CrystalCheck(r);
                var s = r.Stats;
                int lap = Math.Min(s.InBand.Length - 1, s.LapTimes.Count);
                s.LapDuration[lap] += dt;
                if (r.SkimTrack.Count > 0) s.InBand[lap] += dt;
                if (r.Plant.Boost >= _c.BoostMax - 0.1f)
                {
                    s.TimeAtFullBoost += dt;
                    if (s.TimeToFirstFullBoost < 0f) s.TimeToFirstFullBoost = Time;
                }
                float top = _c.ThrottleScaler * _c.BoostMax;
                float lost = Mathf.Max(0f, 1f - r.Plant.Speed / top) * dt;
                if (r.Plant.ThrottleMultiplier < 0.99f) s.LostContact += lost;
                else if (r.Plant.XDiff < 0.95f) s.LostThrottle += lost;
                else if (r.Plant.Boost < _c.BoostMax - 0.2f || !r.Plant.IsBoosting) s.LostBoost += lost;
                else s.LostOther += lost;
                s.SegVmin = Mathf.Min(s.SegVmin, r.Plant.Speed);
                s.SegGap = Mathf.Max(s.SegGap, Quaternion.Angle(r.Plant.Rotation, r.Plant.Commanded));
                float ct = r.Brain.CrossTrackError;
                s.MaxCrossTrack = Mathf.Max(s.MaxCrossTrack, ct);
                s.CrossTrackSum += ct;
                s.CrossTrackSamples++;
                if (_o.Trace && r.Id == 0 && Time >= _o.TraceFrom && Time <= _o.TraceTo
                    && Math.Floor(Time / _o.TraceDt) != Math.Floor((Time - dt) / _o.TraceDt)) TraceLine(r);
            }
        }

        void TraceLine(Racer r)
        {
            var p = r.Plant;
            float s = r.Brain.RouteS;
            Route.Frame(s, out Vector3 c, out Vector3 t, out Vector3 right, out Vector3 up);
            Vector3 d = p.Position - c;
            r.Brain.DescribePlan(s, out float rho, out float phi);
            float cs = r.Brain.TargetCrystalS;
            Vector3 fwd = p.Rotation * Vector3.forward;
            r.Brain.ProfilePlan(s, s + 300f, 300f, _profRates);
            float worst = 0f; int worstAt = 0;
            for (int q = 0; q < _profRates.Count; q++) if (_profRates[q] > worst) { worst = _profRates[q]; worstAt = q; }
            r.Stats.Events.Add($"{Time,7:F2}s s={s,8:F1} v={p.Speed,5:F0} bm={p.Boost:F2} keysDemand300={worst:F2}@{worstAt * 6} " +
                $"off=({Vector3.Dot(d, right),6:F1},{Vector3.Dot(d, up),6:F1},{Vector3.Dot(d, t),5:F1}) " +
                $"plan=({rho * Mathf.Sin(phi),6:F1},{rho * Mathf.Cos(phi),6:F1}) xt={r.Brain.CrossTrackError,5:F1} " +
                $"head={Vector3.Angle(fwd, t),5:F1} crystal ds={(float.IsNaN(cs) ? float.NaN : cs - s),7:F1} dist={Vector3.Distance(p.Position, r.Crystal),6:F1} " +
                $"keys={r.Brain.KeyCount} re={r.Brain.Reanchors} stick=({p.XSum,5:F2},{p.YSum,5:F2},{p.YDiff,5:F2}) " +
                $"gap={Quaternion.Angle(p.Rotation, p.Commanded),5:F1} thr={r.Brain.ThrottleOut:F2} plan={r.Brain.LastChoice} {r.Brain.DebugSteer}" +
                (Environment.GetEnvironmentVariable("SKIM_DEMAND_DEBUG") == "1" ? " || " + r.Brain.DebugDemandCompare() : ""));
        }

        readonly List<float> _profRates = new List<float>(64);

        readonly HashSet<int> _gathered = new HashSet<int>();

        /// <summary>
        /// What a pilot can see of other trails: every rail along the ribbon ahead, out to the length
        /// of line the brain plans (the game's sensor does the same with the prism spatial index,
        /// along the route). A rail the racer laid itself within the hull grace is not solid yet.
        /// </summary>
        void GatherObstacles(Racer r)
        {
            _obstacles.Clear();
            _gathered.Clear();
            float s0 = r.Brain.RouteS;
            for (float ds = 0f; ds <= ObstacleHorizon; ds += 70f)
            {
                Route.Frame(s0 + ds, out Vector3 centre, out _, out _, out _);
                _scratch.Clear();
                _railGrid.Query(centre, ObstacleQueryRadius, _scratch);
                foreach (int i in _scratch)
                {
                    if (!_gathered.Add(i)) continue;
                    var rail = _rails[i];
                    if (rail.Owner == r.Id && Time - rail.Born < _c.HullGrace) continue;
                    if ((rail.Position - centre).sqrMagnitude > ObstacleQueryRadius * ObstacleQueryRadius) continue;
                    Vector3 half = rail.Shielded ? _c.RailHalf * _c.ShellScale : _c.RailHalf;
                    _obstacles.Add(new SkimObstacle(rail.Position, rail.Rotation, half));
                }
            }
        }

        const float ObstacleHorizon = 980f;
        const float ObstacleQueryRadius = 120f;

        void SpawnRails(Racer r)
        {
            var p = r.Plant;
            if (Time < r.NextRailAt) return;
            if (p.Speed > _c.RailMinSpeed)
            {
                Vector3 right = p.Rotation * Vector3.right;
                bool shielded = _c.RailDriftShields && p.IsDrifting;
                for (int side = -1; side <= 1; side += 2)
                {
                    var rail = new Rail
                    {
                        Position = p.Position + right * (_c.RailLateral * side),
                        Rotation = p.Rotation,
                        Owner = r.Id,
                        Born = Time,
                        Shielded = shielded,
                    };
                    Vector3 semi = shielded ? _c.RailHalf * _c.ShellScale : _c.RailHalf;
                    rail.Frame = ShieldShellMath.CreateFrame(TrackBuilder.F3(rail.Position), TrackBuilder.Q(rail.Rotation), TrackBuilder.F3(semi));
                    _railGrid.Add(rail.Position, _rails.Count);
                    _rails.Add(rail);
                }
            }
            float raw = p.Speed > 0f ? _c.RailWavelength / p.Speed : _c.RailWait;
            r.NextRailAt = Time + Mathf.Clamp(raw, 0f, 3f);
        }

        void Contacts(Racer r)
        {
            var p = r.Plant;
            float3 c = TrackBuilder.F3(p.Position);
            float3 e1 = TrackBuilder.F3(p.Rotation * Vector3.right * _c.HullHalf.x);
            float3 e2 = TrackBuilder.F3(p.Rotation * Vector3.up * _c.HullHalf.y);
            float3 e3 = TrackBuilder.F3(p.Rotation * Vector3.forward * _c.HullHalf.z);

            r.NextSkimTrack.Clear(); r.NextHullTrack.Clear();
            _scratch.Clear();
            _trackGrid.Query(p.Position, _c.SkimRadius + _maxShell, _scratch);
            foreach (int i in _scratch)
            {
                var tp = Track[i];
                float reach = _c.SkimRadius + tp.ShellSemi.magnitude;
                if ((tp.Position - p.Position).sqrMagnitude > reach * reach) continue;
                if (ShieldShellMath.SphereOverlapsStella(tp.Frame, c, _c.SkimRadius)) r.NextSkimTrack.Add(i);
                if (ShieldShellMath.BoxOverlapsStella(tp.Frame, c, e1, e2, e3)) r.NextHullTrack.Add(i);
            }
            foreach (int i in r.NextSkimTrack) if (!r.SkimTrack.Contains(i)) Skim(r, rail: false);
            foreach (int i in r.NextHullTrack)
                if (!r.HullTrack.Contains(i))
                {
                    HullTouch(r, Track[i].Volume, danger: false, ownDomain: false);
                    r.Stats.RibbonTouches++;
                    Route.Frame(r.Brain.RouteS, out Vector3 tc, out _, out Vector3 tr, out Vector3 tu);
                    Vector3 td = p.Position - tc;
                    r.Brain.DescribePlan(r.Brain.RouteS, out float prho, out float pphi);
                    Route.Envelope(r.Brain.RouteS, _c.HullHalf.z + 1f, out float ehw, out float ehh);
                    Route.Envelope(r.Brain.RouteS, 0f, out float phw, out float phh);
                    r.Stats.Events.Add($"{Time,7:F2}s ribbon touch at prism {i}{(Track[i].Marker ? " (marker)" : "")} env({ehw:F1},{ehh:F1}) plate({phw:F1},{phh:F1}) " +
                        $"ship at ({Vector3.Dot(td, tr):F1},{Vector3.Dot(td, tu):F1}) plan ({prho * Mathf.Sin(pphi):F1},{prho * Mathf.Cos(pphi):F1}) " +
                        $"v={p.Speed:F0} thr={r.Brain.ThrottleOut:F2} plan={r.Brain.LastChoice} {r.Brain.DebugSteer}");
                }
            Swap(r.SkimTrack, r.NextSkimTrack);
            Swap(r.HullTrack, r.NextHullTrack);

            r.NextSkimRails.Clear(); r.NextHullRails.Clear();
            _scratch.Clear();
            _railGrid.Query(p.Position, _c.SkimRadius + 12f, _scratch);
            foreach (int i in _scratch)
            {
                var rail = _rails[i];
                float age = Time - rail.Born;
                if (age < _c.RailWait) continue;              // collider still off (Prism.waitTime)
                bool own = rail.Owner == r.Id;
                bool skimOk = !own || age >= _c.SkimGrace;
                bool hullOk = !own || age >= _c.HullGrace;
                if (skimOk && (rail.Shielded ? ShieldShellMath.SphereOverlapsOcta(rail.Frame, c, _c.SkimRadius)
                                             : ShieldShellMath.SphereOverlapsBox(rail.Frame, c, _c.SkimRadius)))
                    r.NextSkimRails.Add(i);
                if (hullOk && (rail.Shielded ? ShieldShellMath.BoxOverlapsOcta(rail.Frame, c, e1, e2, e3)
                                             : ShieldShellMath.BoxOverlapsBox(rail.Frame, c, e1, e2, e3)))
                    r.NextHullRails.Add(i);
            }
            foreach (int i in r.NextSkimRails) if (!r.SkimRails.Contains(i)) Skim(r, rail: true);
            foreach (int i in r.NextHullRails)
                if (!r.HullRails.Contains(i))
                {
                    var rail = _rails[i];
                    Vector3 h = _c.RailHalf * 2f;
                    HullTouch(r, h.x * h.y * h.z, danger: false, ownDomain: rail.Owner == r.Id);
                    r.Stats.RailTouches++;
                    bool seen = false;
                    foreach (var o in r.LastObstacles) if ((o.Center - rail.Position).sqrMagnitude < 0.01f) { seen = true; break; }
                    Route.Frame(r.Brain.RouteS, out Vector3 rc, out _, out Vector3 rr, out Vector3 ru);
                    Vector3 rd = rail.Position - rc;
                    r.Stats.Events.Add($"{Time,7:F2}s rail touch ({(rail.Owner == r.Id ? "own" : $"racer {rail.Owner}")}, age {Time - rail.Born:F1}s) " +
                        $"rail at ({Vector3.Dot(rd, rr):F1},{Vector3.Dot(rd, ru):F1}) v={p.Speed:F0} xt={r.Brain.CrossTrackError:F1} " +
                        $"{(seen ? "SEEN" : "NOT-GATHERED")} avoid: {r.Brain.LastAvoidNote} plan={r.Brain.LastChoice} | {r.Brain.DescribeObstacleNear(rail.Position)}");
                }
            Swap(r.SkimRails, r.NextSkimRails);
            Swap(r.HullRails, r.NextHullRails);
        }

        static void Swap(HashSet<int> a, HashSet<int> b)
        {
            a.Clear();
            foreach (int i in b) a.Add(i);
        }

        void Skim(Racer r, bool rail)
        {
            var p = r.Plant;
            p.IsBoosting = true;
            p.Boost = Mathf.Clamp(p.Boost + _c.AddPerHit, _c.BoostBase, _c.BoostMax);
            if (rail) r.Stats.RailSkimHits++; else r.Stats.SkimHits++;
        }

        void HullTouch(Racer r, float volume, bool danger, bool ownDomain)
        {
            if (_o.Ghost) return;   // tooling: count contacts, apply none
            var p = r.Plant;
            p.Boost = _c.BoostBase;          // VesselResetBoostPrismEffectSO
            p.IsBoosting = false;
            if (!danger && ownDomain) return; // VesselChangeSpeedByPrismEffectSO skips own-domain mass
            float strength = danger ? _c.MaxSlow * _c.DangerMult : Mathf.Min(volume * _c.MassScaling, _c.MaxSlow);
            float duration = danger ? _c.SlowDuration * _c.DangerDurationMult : _c.SlowDuration;
            p.ModifyThrottle(Mathf.Clamp01(1f - strength), duration);
        }

        void CrystalCheck(Racer r)
        {
            var p = r.Plant;
            // Sphere vs the hull OBB: distance from the crystal centre to the box.
            Vector3 d = r.Crystal - p.Position;
            Vector3 local = Quaternion.Inverse(p.Rotation) * d;
            Vector3 h = _c.HullHalf;
            Vector3 q = new Vector3(Mathf.Clamp(local.x, -h.x, h.x), Mathf.Clamp(local.y, -h.y, h.y), Mathf.Clamp(local.z, -h.z, h.z));
            if ((local - q).sqrMagnitude > _c.CaptureRadius * _c.CaptureRadius) return;

            r.Stats.Crystals++;
            {
                var st = r.Stats;
                st.Segments.Add((Time - st.SegStartT, r.Brain.RouteS - st.SegStartS, r.Brain.FaceChanges != st.SegFaces, st.SegVmin, st.SegGap, st.SegBm0));
                st.SegStartT = Time; st.SegStartS = r.Brain.RouteS; st.SegVmin = 1e9f; st.SegGap = 0f;
                st.SegFaces = r.Brain.FaceChanges; st.SegBm0 = p.Boost;
            }
            if (r.Stats.Crystals % _def.Waypoints.Count == 0) r.Stats.LapTimes.Add(Time);
            if (_o.Trace) r.Stats.Events.Add($"{Time,7:F2}s crystal {r.Stats.Crystals}/{Target} at anchor {r.AnchorIndex}");
            if (r.Stats.Crystals >= Target)
            {
                r.Stats.FinishTime = Time;
                return;
            }
            r.AnchorIndex = (r.AnchorIndex + 1) % _def.Anchors.Count;
            r.Crystal = CrystalAround(r.AnchorIndex);
        }
    }
}
