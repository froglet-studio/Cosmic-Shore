// The Skim Race world the SHIPPED Squirrel brain is raced in, outside the editor.
//
// What is TRANSCRIBED (and where from) - each is a copy of shipped logic, so a retune of the game
// must be re-transcribed here; every CONSTANT, however, is read from the shipped assets through
// Tools/Build/squirrel_skim_model.py --export-harness-config and none is typed below:
//
//   Plant.Step            VesselTransformer.Update / RotateShip / Roll / Yaw / Pitch /
//                         ApplyAnalogDrift / ApplyThrottleModifiers / MoveShipVector /
//                         StepTowardTarget / ShapeSpeed (vector flight model, the Squirrel's)
//   Plant.BeginDrift/End  VesselTransformer.BeginDrift / EndDrift / RestoreDriftBase
//   BuildTrack            SpawnableWaypointTrack.GetPreviewBlocks + ResolveBlocksThisSegment
//   Skim                  SkimmerBoostPrismEffectSO.Execute (enter only - the stay path is off)
//   HullTouch             VesselResetBoostPrismEffectSO + VesselChangeSpeedByPrismEffectSO
//   Rails                 VesselPrismController.SpawnLoopAsync / CreateBlock (+ SelfTrailContact)
//   Crystals              CrystalManager (one TeamCrystal per racer, next anchor + jitter on respawn)
//
// What is COMPILED VERBATIM from Assets/: the brain (SkimRacing/*.cs), MinimumThrottleBrake.cs and
// ShieldShellMath.cs - the latter is the ground truth for every prism contact, exactly as the
// game's shell tier decides it.
//
// Where the harness is deliberately PESSIMISTIC: rail contacts are tested every frame (PhysX
// samples an unshielded prism at 25 Hz, so the game misses some contacts this counts), and the
// hull is a box bounding the whole mesh rather than the two smaller colliders the game uses.
// Where it is deliberately FAITHFUL: crystal collection is tested only at 25 Hz fixed steps,
// because a trigger that samples is exactly how a crystal is missed at 300 u/s.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Unity.Mathematics;
using UnityEngine;

namespace SquirrelAiHarness
{
    sealed class TrackDef
    {
        public List<Vector3> Waypoints = new();
        public bool Spline;
        public int Laps;
        public List<Vector3> Anchors = new();
        public int Target => Waypoints.Count * Laps;
    }

    sealed class Config
    {
        public float FollowRate, ThrottleScaler, MinSpeed, PitchDps, YawDps, RollDps, RotThrottleScaler;
        public float BoostDecay, BoostMax, BoostBase, OvershootCeiling, DriftMult, DriftGrip;
        public bool VectorFlightModel;
        public float AddPerHit, SkimRadius;
        public Vector3 HullHalf;
        public float MassScaling, MaxSlow, SlowDuration, DangerMult, DangerDurationMult;
        public float HullGrace, SkimGrace;
        public Vector3 RailHalf;
        public float RailLateral, RailWavelength, RailWait, RailStartDelay, RailMinSpeed;
        public bool RailDriftShields;
        public Vector3 PrismScale, RibbonCollider, MarkerCollider;
        public float MarkerMult, Spacing, ShellScale;
        public bool SuperShielded;
        public List<TrackDef> Tracks = new();
        public float CaptureRadius, Jitter;
        public List<Pose> Spawns = new();

        static Vector3 V(JsonElement e) => new Vector3(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
        static float F(JsonElement e, string k) => e.GetProperty(k).GetSingle();

        public static Config Load(string path)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var r = doc.RootElement;
            var c = new Config();
            var f = r.GetProperty("flight");
            c.FollowRate = F(f, "follow_rate"); c.ThrottleScaler = F(f, "throttle_scaler"); c.MinSpeed = F(f, "min_speed");
            c.PitchDps = F(f, "pitch_dps"); c.YawDps = F(f, "yaw_dps"); c.RollDps = F(f, "roll_dps");
            c.RotThrottleScaler = F(f, "rot_throttle_scaler");
            c.BoostDecay = F(f, "boost_decay"); c.BoostMax = F(f, "boost_max"); c.BoostBase = F(f, "boost_base");
            c.VectorFlightModel = f.GetProperty("vector_flight_model").GetBoolean();
            c.OvershootCeiling = F(f, "drift_overshoot_ceiling"); c.DriftMult = F(f, "drift_mult"); c.DriftGrip = F(f, "drift_grip");
            var s = r.GetProperty("skim");
            c.AddPerHit = F(s, "add_per_hit"); c.SkimRadius = F(s, "skimmer_radius");
            c.HullHalf = V(r.GetProperty("hull_half_extents"));
            var h = r.GetProperty("hull_touch");
            c.MassScaling = F(h, "mass_scaling"); c.MaxSlow = F(h, "max_slow"); c.SlowDuration = F(h, "duration");
            c.DangerMult = F(h, "danger_mult"); c.DangerDurationMult = F(h, "danger_duration_mult");
            var st = r.GetProperty("self_trail");
            c.HullGrace = F(st, "hull_grace"); c.SkimGrace = F(st, "skim_grace");
            var rl = r.GetProperty("rails");
            c.RailHalf = V(rl.GetProperty("half_extents")); c.RailLateral = F(rl, "lateral");
            c.RailWavelength = F(rl, "wavelength"); c.RailWait = F(rl, "wait_time"); c.RailStartDelay = F(rl, "start_delay");
            c.RailMinSpeed = F(rl, "min_speed"); c.RailDriftShields = rl.GetProperty("drift_shields").GetBoolean();
            var t = r.GetProperty("track");
            c.PrismScale = V(t.GetProperty("prism_scale")); c.MarkerMult = F(t, "marker_mult"); c.Spacing = F(t, "spacing");
            c.RibbonCollider = V(t.GetProperty("ribbon_collider")); c.MarkerCollider = V(t.GetProperty("marker_collider"));
            c.ShellScale = F(t, "shell_scale"); c.SuperShielded = t.GetProperty("super_shielded").GetBoolean();
            foreach (var it in t.GetProperty("intensities").EnumerateArray())
            {
                var d = new TrackDef { Spline = it.GetProperty("spline").GetBoolean(), Laps = it.GetProperty("laps").GetInt32() };
                foreach (var w in it.GetProperty("waypoints").EnumerateArray()) d.Waypoints.Add(V(w));
                foreach (var a in it.GetProperty("anchors").EnumerateArray()) d.Anchors.Add(V(a));
                c.Tracks.Add(d);
            }
            var cr = r.GetProperty("crystal");
            c.CaptureRadius = F(cr, "capture_radius"); c.Jitter = F(cr, "jitter");
            foreach (var sp in r.GetProperty("spawn_points").EnumerateArray())
            {
                var q = sp.GetProperty("rotation");
                c.Spawns.Add(new Pose(V(sp.GetProperty("position")),
                    new Quaternion(q[0].GetSingle(), q[1].GetSingle(), q[2].GetSingle(), q[3].GetSingle())));
            }
            return c;
        }
    }

    /// <summary>One track prism: pose, the shell it collides as, and the frame ShieldShellMath tests.</summary>
    sealed class TrackPrism
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public Vector3 ShellSemi;
        public bool Marker;
        public ShieldShellMath.ShellFrame Frame;
        public float Volume => Scale.x * Scale.y * Scale.z;
    }

    sealed class Rail
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public int Owner;
        public float Born;
        public bool Shielded;
        public ShieldShellMath.ShellFrame Frame;
    }

    /// <summary>A uniform spatial hash - the harness's stand-in for PrismSpatialIndex.</summary>
    sealed class Grid<T>
    {
        readonly float _cell;
        readonly Dictionary<(int, int, int), List<T>> _cells = new();
        public Grid(float cell) { _cell = cell; }
        (int, int, int) Key(Vector3 p) => ((int)Math.Floor(p.x / _cell), (int)Math.Floor(p.y / _cell), (int)Math.Floor(p.z / _cell));
        public void Add(Vector3 p, T item)
        {
            var k = Key(p);
            if (!_cells.TryGetValue(k, out var l)) _cells[k] = l = new List<T>();
            l.Add(item);
        }
        public void Query(Vector3 p, float r, List<T> into)
        {
            var lo = Key(p - Vector3.one * r);
            var hi = Key(p + Vector3.one * r);
            for (int x = lo.Item1; x <= hi.Item1; x++)
            for (int y = lo.Item2; y <= hi.Item2; y++)
            for (int z = lo.Item3; z <= hi.Item3; z++)
                if (_cells.TryGetValue((x, y, z), out var l)) into.AddRange(l);
        }
    }

    static class TrackBuilder
    {
        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static Vector3 SplinePoint(List<Vector3> pos, int segment, float t)
        {
            int n = pos.Count;
            return CatmullRom(pos[((segment - 1) % n + n) % n], pos[segment], pos[(segment + 1) % n], pos[(segment + 2) % n], t);
        }

        static int BlocksThisSegment(List<Vector3> pos, int segment, bool spline, float spacing)
        {
            int next = (segment + 1) % pos.Count;
            float length;
            if (!spline) length = Vector3.Distance(pos[segment], pos[next]);
            else
            {
                const int samples = 20;
                length = 0f;
                Vector3 prev = SplinePoint(pos, segment, 0f);
                for (int s = 1; s <= samples; s++)
                {
                    Vector3 cur = SplinePoint(pos, segment, (float)s / samples);
                    length += Vector3.Distance(prev, cur);
                    prev = cur;
                }
            }
            return Math.Max(1, Mathf.RoundToInt(length / spacing));
        }

        public static List<TrackPrism> Build(Config c, TrackDef def)
        {
            var list = new List<TrackPrism>();
            var pos = def.Waypoints;
            int segCount = pos.Count;
            for (int segment = 0; segment < segCount; segment++)
            {
                Vector3 a = pos[segment], b = pos[(segment + 1) % segCount];
                int blocks = BlocksThisSegment(pos, segment, def.Spline, c.Spacing);
                for (int i = 0; i < blocks; i++)
                {
                    float t = (float)i / blocks;
                    Vector3 p, look;
                    if (def.Spline)
                    {
                        p = SplinePoint(pos, segment, t);
                        look = i < blocks - 1 ? SplinePoint(pos, segment, (float)(i + 1) / blocks) : SplinePoint(pos, (segment + 1) % segCount, 0f);
                    }
                    else
                    {
                        p = Vector3.Lerp(a, b, t);
                        look = i < blocks - 1 ? Vector3.Lerp(a, b, (float)(i + 1) / blocks) : b;
                    }
                    bool marker = i == 0;
                    Vector3 scale = marker ? c.PrismScale * c.MarkerMult : c.PrismScale;
                    Vector3 fwd = look - p;
                    Quaternion rot = fwd.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(fwd, Vector3.up);
                    Vector3 collider = marker ? c.MarkerCollider : c.RibbonCollider;
                    Vector3 half = Vector3.Scale(collider, scale) * 0.5f;
                    Vector3 semi = c.SuperShielded ? half * c.ShellScale : half;
                    var tp = new TrackPrism { Position = p, Rotation = rot, Scale = scale, ShellSemi = semi, Marker = marker };
                    tp.Frame = ShieldShellMath.CreateFrame(F3(p), Q(rot), F3(semi));
                    list.Add(tp);
                }
            }
            return list;
        }

        public static float3 F3(Vector3 v) => new float3(v.x, v.y, v.z);
        public static quaternion Q(Quaternion q) => new quaternion(q.x, q.y, q.z, q.w);
    }

    /// <summary>
    /// The Squirrel's VesselTransformer, vector flight model, transcribed in the shipped update order.
    /// </summary>
    sealed class Plant
    {
        readonly Config _c;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        public Quaternion Commanded = Quaternion.identity;
        public Vector3 Course = Vector3.forward;
        public float SmoothedSpeed;           // VesselTransformer.speed (|velocity|)
        public float Speed;                   // VesselStatus.Speed (x throttle multiplier)
        public float Boost = 1f;
        public bool IsBoosting;
        public bool IsDrifting;
        public float ThrottleMultiplier = 1f;

        public float XSum, YSum, YDiff, XDiff, LeftTrigger;
        public bool Gamepad;

        public float PitchScaler, YawScaler, RollScaler;
        float _grip;
        Vector3 _velocity;
        bool _seeded;

        bool _singleActive, _singleParamsSet, _hasDriftBase, _easeOutPending;
        float _singleMult = 1f, _singleDamp;
        Vector3 _driftBase;
        float _frameTriggerSum;

        readonly List<(float init, float dur, float el)> _mods = new();

        public Plant(Config c, Pose spawn)
        {
            _c = c;
            Position = spawn.position;
            Rotation = spawn.rotation;
            Commanded = spawn.rotation;
            Course = Rotation * Vector3.forward;
            PitchScaler = c.PitchDps;
            YawScaler = c.YawDps;
            RollScaler = c.RollDps;
            Boost = c.BoostBase;
        }

        public bool DriftActive => _singleActive || _easeOutPending;

        public void BeginDrift()
        {
            _easeOutPending = false;
            if (!_hasDriftBase) { _driftBase = new Vector3(PitchScaler, YawScaler, RollScaler); _hasDriftBase = true; }
            _singleMult = _c.DriftMult;
            _singleDamp = _c.DriftGrip;
            _singleActive = true;
            _singleParamsSet = true;
            IsDrifting = true;
        }

        public void EndDrift()
        {
            _singleActive = false;
            if (!Gamepad) _easeOutPending = true;
            else RestoreDriftBase();
            IsDrifting = _singleActive || _easeOutPending;
        }

        void RestoreDriftBase()
        {
            if (!_hasDriftBase) return;
            PitchScaler = _driftBase.x; YawScaler = _driftBase.y; RollScaler = _driftBase.z;
            _grip = 0f;
            _hasDriftBase = false;
        }

        public void ModifyThrottle(float amount, float duration) => _mods.Add((amount, duration, 0f));

        float DriftBlend01() => (IsDrifting || _easeOutPending) && _hasDriftBase ? Mathf.Clamp01(_frameTriggerSum) : 0f;

        public void Step(float dt)
        {
            // DecayBoost
            Boost = Boost > 1f ? Boost - _c.BoostDecay * dt : Mathf.Min(1f, Boost + _c.BoostDecay * dt);

            // Trigger sum (singleTriggerDrift, one tier)
            float raw = Gamepad ? LeftTrigger : (_singleActive ? 1f : 0f);
            _frameTriggerSum = !Gamepad ? Mathf.MoveTowards(_frameTriggerSum, raw, 12f * dt) : raw;
            if (_easeOutPending && _frameTriggerSum < 0.01f)
            {
                _frameTriggerSum = 0f;
                _easeOutPending = false;
                RestoreDriftBase();
                IsDrifting = false;
            }

            // ApplyAnalogDrift
            if (_hasDriftBase && (IsDrifting || _easeOutPending))
            {
                float sum = _frameTriggerSum;
                float mult = Mathf.Lerp(1f, _singleMult, sum);
                float damp = Mathf.Lerp(1f, _singleDamp, sum);
                PitchScaler = _driftBase.x * mult; YawScaler = _driftBase.y * mult; RollScaler = _driftBase.z * mult;
                _grip = damp;
            }

            // RotateShip: Roll, Yaw, Pitch about the TRANSFORM's axes, then Slerp.
            Vector3 fwd = Rotation * Vector3.forward, up = Rotation * Vector3.up, right = Rotation * Vector3.right;
            float rs = SmoothedSpeed * _c.RotThrottleScaler;
            Commanded = Quaternion.AngleAxis(YDiff * (rs + RollScaler) * dt, fwd) * Commanded;
            Commanded = Quaternion.AngleAxis(XSum * (rs + YawScaler) * dt, up) * Commanded;
            Commanded = Quaternion.AngleAxis(YSum * (rs + PitchScaler) * dt, right) * Commanded;
            Rotation = Quaternion.Slerp(Rotation, Commanded, _c.FollowRate * dt);

            // ApplyThrottleModifiers
            float acc = 1f;
            for (int i = _mods.Count - 1; i >= 0; i--)
            {
                var m = _mods[i];
                m.el += dt;
                _mods[i] = m;
                if (m.el >= m.dur) _mods.RemoveAt(i);
                else if (m.init < 1f) acc *= Mathf.Lerp(m.init, 1f, m.el / m.dur);
                else acc += Mathf.Lerp(m.init - 1f, 0f, m.el / m.dur);
            }
            ThrottleMultiplier = Mathf.Max(Mathf.Clamp(acc, 0f, 6f), 0f);

            // MoveShipVector
            fwd = Rotation * Vector3.forward;
            if (!_seeded) { _velocity = fwd * SmoothedSpeed; _seeded = true; }
            float speedNow = _velocity.magnitude;
            if (speedNow > 1e-4f)
            {
                float drift = DriftBlend01();
                float grip = _grip > 0.0001f ? 1f - Mathf.Exp(-_grip * dt) : 0f;
                float conv = drift > 0f ? Mathf.Clamp01(Mathf.Lerp(1f, grip, drift)) : 1f;
                _velocity = Vector3.Slerp(_velocity / speedNow, fwd, conv) * speedNow;
            }
            else _velocity = Vector3.zero;

            float target = XDiff * _c.ThrottleScaler * (IsBoosting ? Boost : 1f) + _c.MinSpeed;
            float along = Vector3.Dot(_velocity, fwd);
            float stepped = Mathf.Lerp(along, target, _c.FollowRate * dt);
            stepped = MinimumThrottleBrake.Apply(stepped, along, target,
                MinimumThrottleBrake.RateFor(_c.ThrottleScaler, MinimumThrottleBrake.DefaultBrakeSeconds), dt);
            _velocity += fwd * (stepped - along);

            float mag = _velocity.magnitude;
            if (DriftBlend01() > 0f) mag = Mathf.Min(mag, Mathf.Max(speedNow, target * _c.OvershootCeiling));
            _velocity = mag > 1e-4f ? _velocity.normalized * mag : Vector3.zero;
            SmoothedSpeed = mag;
            Speed = mag * ThrottleMultiplier;
            Course = mag > 1e-4f ? _velocity / mag : fwd;
            Position += Course * (Speed * dt);
        }
    }
}
