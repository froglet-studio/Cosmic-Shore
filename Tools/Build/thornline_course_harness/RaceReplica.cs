// The AI race replica (run.sh --ai): AI Shrikes fly whole Thornline races offline, frame by frame, through the SHIPPED
// ThornlineAIPlan (what to aim at) and the SHIPPED NoseLeadSteering (how the stick answers an aim error), against a
// model of the Shrike's kit that ShrikeConfig.asset and Shrike.prefab pin (ThornlineCourseTests reads the numbers back):
//   - AIPilot (AIPilot.cs Update/StickFor/TryNoseLead): desired = to the target; with a nose lead the error is judged
//     from the COMMAND toward NoseLeadSteering.Direction(nose, desired, max(1, gain x 1.5 / follow), 40); aggressiveness
//     100, _maxDistance 50; a single stick flies (x, -y);
//   - SingleStickVesselTransformer: roll -x 15 about the nose's forward, yaw x 40 x TurnScalar about its up, pitch
//     y 60 x TurnScalar about its right (TurnScalar 3 docked); the nose slerps to the command at the fleet 1.5/s, or the
//     mode's AINoseFollowPerSecond;
//   - ShrikeExecutor.Autopilot: every 0.25 s it counts other-domain blocks within lanceRadius + 2 of its 2,400 u forward
//     line and holds the lance (Space) at 2 or more; otherwise in Time it slings when reloaded, the target is 300 u+
//     away and within 20 degrees of the nose, and the nose's ray anchors (a block's CENTRE within dockQueryReach 12 of
//     the ray, the ray through its box); docked aiDockSeconds 3 and reloaded, it detaches (Mass);
//   - the sling: tether at 2,400 u/s, then 1 s (0.6 s at Time 5, Whipcrack); the cooldown (6 s at Time 0 -> 2.5 s at
//     Time 10) runs x1.6 docked on a plain block, x2.5 on a super-shielded one; a crystal resets it and adds a Time level;
//   - rival sling trails (--pack): blocks every 8 u along each sling, kept TrailSeconds; a follower's ray anchors on one
//     it passes within 2 u of (the first block on the line wins), and they count toward its lance.
// NOT modelled (the editor checklist owns them): the orbit break-off and emergency steering of AIPilot, Charge-mode
// paint/missile diversions, nests left on detach, network latency, physics tunnelling of a crystal at sling speed.
using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

public sealed class RaceReplica
{
    // ── the kit (ShrikeConfig.asset / Shrike.prefab; ThornlineCourseTests pins every one) ──
    public const float Cruise = 18f, TetherSpeed = 2400f, Whipcrack = 0.6f, WhipcrackLevel = 5f;
    public const float Roll = 15f, Yaw = 40f, Pitch = 60f, DockedTurn = 3f, FleetFollow = 1.5f;
    public const float MaxDistance = 50f, Aggressiveness = 100f;
    public const float LanceCooldown = 1.2f, LanceRadiusCharge0 = 1.5f, ModeDelay = 0.5f, ScanEvery = 0.25f;
    public const float CrystalRegrow = 5f, ThornRegrow = 3f, TrailBlockSpacing = 8f, TrailHitRadius = 2f;
    public const float UndockPush = 8f, DockGrace = 0.5f;

    static Vector3 BoxClosest(Vector3 c, float size, Vector3 p)
    {
        float h = size * 0.5f;
        return new Vector3(Mathf.Clamp(p.x, c.x - h, c.x + h), Mathf.Clamp(p.y, c.y - h, c.y + h), Mathf.Clamp(p.z, c.z - h, c.z + h));
    }

    public float Dt = 1f / 60f;
    public float TrailSeconds = 30f;
    public float LanceRadius = LanceRadiusCharge0;
    public bool CentreAim;            // negative control: aim at the thorn's centre (no lanes)
    public bool NoRescue;             // negative control: the stall rescue off
    public float HorizonSeconds = 300f;
    public bool Trace;

    sealed class Trail { public Vector3 A, B; public int Owner; public float Born; }

    sealed class Racer
    {
        public int Id;
        public ThornlineAIRow Row;
        public Quaternion Nose, Cmd;
        public Vector3 Pos;
        public bool Docked;
        public int DockThorn = -1;     // -1 none, -2 a trail
        public PrismKind DockKind;
        public float DockedFor, Cd, TimeLevel, ReadyAt = 0f;
        public bool Tethering, Slinging;
        public float PhaseLeft, SlingDur;
        public Vector3 SlingFrom, SlingTo;
        public int AnchorThorn = -1;
        public int Threaded;
        public float LastRingAt;
        public bool Space;
        public float ModeAt, ScanAt, LanceAt;
        public int Count;
        public float Finish = -1f;
        // stats
        public int Slings, Snipes, Crystals, WakeDocks, Detaches, Rescues;
        public float SpaceSeconds, OverheadSeconds;
        public float DockReadyAt = -1f;
        public float TraceAt, LeftAt = -99f;
        public Vector3 Push;
        public int LeftThorn = -1;
    }

    public struct Result
    {
        public float Finish;          // -1 = did not finish inside the horizon
        public int Threaded, Slings, Snipes, Crystals, WakeDocks, Detaches, Rescues;
        public float SpaceSeconds, OverheadSeconds;
    }

    readonly List<Vector3> _thorns = new();
    readonly List<float> _sizes = new();
    readonly List<bool> _alive = new();
    readonly List<float> _goneAt = new();
    readonly List<PrismKind> _kinds = new();
    readonly List<RaceGate> _rings = new();
    readonly List<Vector3> _crystals = new();
    readonly List<int> _crystalLegs = new();
    readonly List<bool> _crystalLive = new();
    readonly List<float> _crystalAt = new();
    readonly List<Trail> _trails = new();
    readonly List<Racer> _racers = new();

    /// <summary>Lay a course: the thorns, kinds and crystals exactly as the controller lays them from the rings.</summary>
    public void Lay(int seed, int intensity)
    {
        var s = ThornlineCourse.ForIntensity(intensity);
        var chain = new List<Vector3>();
        var rings = ThornlineCourse.Generate(seed, s, chain) ?? throw new Exception($"I{intensity} seed {seed}: no chain");
        var laid = new List<Thorn>();
        ThornlineCourse.Lay(chain, s, laid);
        _thorns.Clear(); _sizes.Clear(); _alive.Clear(); _goneAt.Clear(); _kinds.Clear();
        foreach (var t in laid) { _thorns.Add(t.Position); _sizes.Add(t.Size); _alive.Add(true); _goneAt.Add(-1f); _kinds.Add(t.Kind); }
        _rings.Clear(); _rings.AddRange(rings);
        var crystals = new List<Vector3>();
        ThornlineCourse.PlaceCrystals(chain, s, crystals);
        _crystals.Clear(); _crystalLegs.Clear(); _crystalLive.Clear(); _crystalAt.Clear();
        foreach (var c in crystals)
        {
            int best = 1; float bd = float.MaxValue;
            for (int j = 1; j < chain.Count; j++)
            {
                float d = ThornlineCourse.DistanceToSegment(c, chain[j - 1], chain[j]);
                if (d < bd) { bd = d; best = j; }
            }
            _crystals.Add(c); _crystalLegs.Add(best); _crystalLive.Add(true); _crystalAt.Add(-1f);
        }
        _trails.Clear();
        _racers.Clear();
    }

    /// <summary>Seat <paramref name="count"/> AIs on the start cone (ThornlineCourse.StartPoses), one row each.</summary>
    public void Seat(IReadOnlyList<ThornlineAIRow> rows)
    {
        var poses = ThornlineCourse.StartPoses(rows.Count);
        for (int i = 0; i < rows.Count; i++)
            _racers.Add(new Racer
            {
                Id = i, Row = rows[i], Pos = poses[i].position, Nose = poses[i].rotation, Cmd = poses[i].rotation,
            });
    }

    public List<Result> Run()
    {
        float now = 0f;
        var input = new ThornlineAIPlan.Inputs
        {
            Thorns = _thorns, Sizes = _sizes, Alive = _alive, Rings = _rings,
            Crystals = _crystals, CrystalLegs = _crystalLegs, CrystalLive = _crystalLive,
        };
        while (now < HorizonSeconds)
        {
            bool all = true;
            foreach (var r in _racers)
            {
                if (r.Finish >= 0f) continue;
                all = false;
                Step(r, now, ref input);
            }
            if (all) break;
            Regrow(now);
            now += Dt;
        }
        var results = new List<Result>();
        foreach (var r in _racers)
            results.Add(new Result
            {
                Finish = r.Finish, Threaded = r.Threaded, Slings = r.Slings, Snipes = r.Snipes, Crystals = r.Crystals,
                WakeDocks = r.WakeDocks, Detaches = r.Detaches, Rescues = r.Rescues, SpaceSeconds = r.SpaceSeconds,
                OverheadSeconds = r.OverheadSeconds,
            });
        return results;
    }

    void Regrow(float now)
    {
        for (int i = 0; i < _thorns.Count; i++)
        {
            if (_alive[i]) continue;
            if (now - _goneAt[i] < ThornRegrow) continue;
            bool blocked = false;
            foreach (var r in _racers)
                if (ThornlineCourse.RegrowBlocked(_thorns[i], _sizes[i], r.Pos)) blocked = true;
            if (!blocked) _alive[i] = true;
        }
        for (int i = 0; i < _crystals.Count; i++)
            if (!_crystalLive[i] && now >= _crystalAt[i]) _crystalLive[i] = true;
        _trails.RemoveAll(t => now - t.Born > TrailSeconds);
    }

    void Step(Racer r, float now, ref ThornlineAIPlan.Inputs input)
    {
        float dt = Dt;
        _clock = now;
        // ── the plan ──
        input.Self = r.Pos;
        input.Threaded = r.Threaded;
        input.Seat = r.Id;
        input.Row = r.Row;
        input.Stalled = !NoRescue && now - r.LastRingAt >= ThornlineAIPlan.StallSeconds;
        var kind = ThornlineAIPlan.Choose(input, out Vector3 target);
        if (Trace && now >= r.TraceAt)
        {
            r.TraceAt = now + 1f;
            Console.WriteLine($"t {now,6:F1} #{r.Id} ring {r.Threaded,2} {kind,-7} docked {r.Docked,-5} thorn {r.DockThorn,3} cd {r.Cd,4:F1} " +
                              $"space {r.Space,-5} aimErr {Vector3.Angle(r.Nose * Vector3.forward, target - r.Pos),6:F2} dist {(target - r.Pos).magnitude,6:F0} pos {r.Pos}");
        }
        if (CentreAim && kind is ThornlineAim.Dock or ThornlineAim.Back or ThornlineAim.Snipe or ThornlineAim.Rescue)
            target = NearestThornCentre(target);
        if (kind == ThornlineAim.Rescue && input.Stalled) { r.Rescues++; r.LastRingAt = now; }

        // ── AIPilot: the stick ──
        Vector3 fwd = r.Nose * Vector3.forward, up = r.Nose * Vector3.up, right = r.Nose * Vector3.right;
        Vector3 toTarget = target - r.Pos;
        if (toTarget.sqrMagnitude > 1e-6f)
        {
            Vector3 desired = toTarget.normalized;
            Vector3 judged = fwd, steer = desired;
            float follow = r.Row.NoseFollow < 0f ? FleetFollow : r.Row.NoseFollow > 0f ? r.Row.NoseFollow : float.PositiveInfinity;
            float gain = r.Row.NoseLeadGain <= 0f || float.IsInfinity(follow) ? 0f
                       : Mathf.Max(1f, r.Row.NoseLeadGain * FleetFollow / follow);
            if (gain > 0f)
            {
                steer = NoseLeadSteering.Direction(fwd, desired, gain, ThornlineAIPlan.NoseLeadMaxExtraDegrees);
                judged = r.Cmd * Vector3.forward;
            }
            Vector3 cross = Vector3.Cross(judged, steer);
            var local = new Vector3(Vector3.Dot(cross, right), Vector3.Dot(cross, up), Vector3.Dot(cross, fwd));
            Vector2 stick = NoseLeadSteering.ErrorStick(local, Vector3.Dot(judged, steer), toTarget.sqrMagnitude, MaxDistance, Aggressiveness);
            float ts = r.Docked ? DockedTurn : 1f;
            r.Cmd = Quaternion.AngleAxis(-stick.x * Roll * dt, fwd) * r.Cmd;
            r.Cmd = Quaternion.AngleAxis(stick.x * Yaw * ts * dt, up) * r.Cmd;
            r.Cmd = Quaternion.AngleAxis(stick.y * Pitch * ts * dt, right) * r.Cmd;
            float frac = float.IsInfinity(follow) ? 1f : Mathf.Min(1f, follow * dt);
            r.Nose = Quaternion.Slerp(r.Nose, r.Cmd, frac);
            fwd = r.Nose * Vector3.forward;
        }

        // ── Autopilot ──
        if (now >= r.ScanAt)
        {
            r.ScanAt = now + ScanEvery;
            r.Count = CountRival(r, r.Pos, r.Pos + fwd * ThornlineCourse.SlingRangeOfLance, LanceRadius + ThornlineCourse.LanceCountPad);
        }
        bool wantSpace = r.Count >= 2;
        if (wantSpace != r.Space && now >= r.ModeAt) { r.Space = wantSpace; r.ModeAt = now + ModeDelay; }
        if (r.Space)
        {
            r.SpaceSeconds += dt;
            if (now >= r.LanceAt) { r.LanceAt = now + LanceCooldown; Lance(r, fwd, now); }
        }
        else if (!r.Slinging && !r.Tethering && r.Cd <= 0f && toTarget.magnitude >= ThornlineCourse.AiSlingMinDistance
                 && Vector3.Angle(fwd, toTarget) <= ThornlineCourse.AiSlingDegrees
                 && TryAnchor(r, r.Pos, fwd, out Vector3 point, out int thorn))
        {
            r.Tethering = true;
            r.PhaseLeft = (point - r.Pos).magnitude / TetherSpeed;
            r.SlingFrom = r.Pos;
            r.SlingTo = point - fwd * ThornlineCourse.HullRadius;
            r.AnchorThorn = thorn;
            r.Cd = ThornlineCourse.CooldownAtTime(r.TimeLevel);
            r.Slings++;
            if (kind == ThornlineAim.Snipe) r.Snipes++;
            if (r.DockReadyAt >= 0f) { r.OverheadSeconds += now - r.DockReadyAt; r.DockReadyAt = -1f; }
        }
        if (r.Docked && r.DockedFor >= ThornlineCourse.AiDockSeconds && r.Cd <= 0f && !r.Tethering)
        {
            Vector3 normal = r.DockThorn >= 0 ? r.Pos - BoxClosest(_thorns[r.DockThorn], _sizes[r.DockThorn], r.Pos) : fwd;
            r.Push = (normal.sqrMagnitude > 1e-6f ? normal.normalized : fwd) * UndockPush;
            r.LeftThorn = r.DockThorn; r.LeftAt = now;
            r.Docked = false; r.DockThorn = -1; r.Detaches++;
        }

        // ── motion ──
        Vector3 prev = r.Pos;
        if (r.Tethering)
        {
            r.PhaseLeft -= dt;
            if (r.PhaseLeft <= 0f)
            {
                r.Tethering = false; r.Slinging = true; r.Docked = false; r.DockThorn = -1;
                r.SlingDur = r.TimeLevel >= WhipcrackLevel ? Whipcrack : ThornlineCourse.SlingSeconds;
                r.PhaseLeft = r.SlingDur;
            }
        }
        else if (r.Slinging)
        {
            r.PhaseLeft -= dt;
            float u = Mathf.Clamp01(1f - r.PhaseLeft / r.SlingDur);
            r.Pos = Vector3.Lerp(r.SlingFrom, r.SlingTo, u);
            if (r.PhaseLeft <= 0f)
            {
                r.Slinging = false;
                r.Docked = true; r.DockedFor = 0f;
                r.DockThorn = r.AnchorThorn;
                r.DockKind = r.AnchorThorn >= 0 ? _kinds[r.AnchorThorn] : PrismKind.Plain;
                if (r.AnchorThorn == -2) r.WakeDocks++;
                LayTrail(r, now);
            }
        }
        else if (!r.Docked)
        {
            // Flying (ShrikeExecutor.Fly / TryContact): the undock push decays, and after the dock grace the first block
            // whose CENTRE is within hullRadius + dockQueryReach and whose box is within hullRadius docks the hull. A thorn
            // wider than 30 u never meets the centre test, so a drifting hull does not dock on one.
            r.Pos += (fwd * Cruise + r.Push) * dt;
            r.Push *= Mathf.Exp(-2f * dt);
            if (now - r.LeftAt >= DockGrace)
                for (int i = 0; i < _thorns.Count; i++)
                {
                    if (!_alive[i] || (_thorns[i] - r.Pos).magnitude > ThornlineCourse.HullRadius + ThornlineCourse.AnchorReach) continue;
                    if ((BoxClosest(_thorns[i], _sizes[i], r.Pos) - r.Pos).magnitude >= ThornlineCourse.HullRadius) continue;
                    r.Docked = true; r.DockedFor = 0f; r.DockThorn = i; r.DockKind = _kinds[i]; r.Push = Vector3.zero;
                    break;
                }
        }
        if (r.Docked && r.DockThorn >= 0 && !_alive[r.DockThorn]) { r.Docked = false; r.DockThorn = -1; }   // lanced away

        // ── the clock ──
        if (r.Docked) r.DockedFor += dt;
        if (r.Cd > 0f)
        {
            float rate = r.Docked ? (r.DockKind == PrismKind.SuperShielded ? ThornlineCourse.DockBonusShielded : ThornlineCourse.DockBonus) : 1f;
            r.Cd -= dt * rate;
            if (r.Cd <= 0f) { r.Cd = 0f; r.ReadyAt = now; if (r.Docked) r.DockReadyAt = now; }
        }

        // ── rings and crystals along this frame's path ──
        while (r.Threaded < _rings.Count && Crosses(prev, r.Pos, _rings[r.Threaded]))
        {
            r.Threaded++;
            r.LastRingAt = now;
        }
        if (r.Threaded >= _rings.Count) { r.Finish = now; return; }
        for (int i = 0; i < _crystals.Count; i++)
        {
            if (!_crystalLive[i]) continue;
            if (ThornlineCourse.DistanceToSegment(_crystals[i], prev, r.Pos) > ThornlineCourse.CrystalPickupReach) continue;
            _crystalLive[i] = false; _crystalAt[i] = now + CrystalRegrow;
            r.Cd = 0f; r.ReadyAt = now; r.TimeLevel += 1f; r.Crystals++;
            if (r.Docked) r.DockReadyAt = now;
        }
    }

    Vector3 NearestThornCentre(Vector3 p)
    {
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < _thorns.Count; i++)
        {
            float d = (_thorns[i] - p).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return _thorns[best];
    }

    static bool Crosses(Vector3 a, Vector3 b, in RaceGate g)
    {
        float da = Vector3.Dot(a - g.Position, g.Axis), db = Vector3.Dot(b - g.Position, g.Axis);
        if (da > 0f || db < 0f || Mathf.Approximately(da, db)) return false;
        Vector3 hit = Vector3.Lerp(a, b, da / (da - db));
        return (hit - g.Position).magnitude <= g.Radius;
    }

    /// <summary>The first block the ray anchors on: a live thorn (not the one docked on) or a rival's (or own, older than
    /// a second) trail block within TrailHitRadius of the ray.</summary>
    bool TryAnchor(Racer r, Vector3 o, Vector3 f, out Vector3 point, out int thorn)
    {
        point = default; thorn = -1;
        float best = float.MaxValue;
        for (int i = 0; i < _thorns.Count; i++)
        {
            if (!_alive[i] || (r.Docked && r.DockThorn == i)) continue;
            float t = ThornlineCourse.AnchorAlong(o, f, _thorns[i], _sizes[i]);
            if (t >= 0f && t < best) { best = t; thorn = i; }
        }
        foreach (var tr in _trails)
        {
            if (tr.Owner == r.Id && TrailAge(tr) < 1f) continue;
            if (!RayNearSegment(o, f, tr.A, tr.B, TrailHitRadius, out float t)) continue;
            if (t > 1f && t < best) { best = t; thorn = -2; }
        }
        if (thorn == -1) return false;
        point = o + f * best;
        return best - ThornlineCourse.HullRadius >= 1f;
    }

    float _clock;
    float TrailAge(Trail t) => _clock - t.Born;

    int CountRival(Racer r, Vector3 a, Vector3 b, float radius)
    {
        int n = 0;
        for (int i = 0; i < _thorns.Count; i++)
            if (_alive[i] && ThornlineCourse.DistanceToSegment(_thorns[i], a, b) <= radius) n++;
        Vector3 f = (b - a).normalized;
        foreach (var tr in _trails)
        {
            if (tr.Owner == r.Id) continue;
            if (!RayNearSegment(a, f, tr.A, tr.B, radius, out _)) continue;
            n += 1 + (int)(OverlapLength(a, f, tr.A, tr.B, radius) / TrailBlockSpacing);
        }
        return n;
    }

    void Lance(Racer r, Vector3 f, float now)
    {
        for (int i = 0; i < _thorns.Count; i++)
        {
            if (!_alive[i] || _kinds[i] != PrismKind.Plain) continue;
            if (ThornlineCourse.DistanceToSegment(_thorns[i], r.Pos, r.Pos + f * ThornlineCourse.SlingRangeOfLance) > ThornlineCourse.HalfDiagonal(_sizes[i]) + LanceRadius) continue;
            _alive[i] = false; _goneAt[i] = now;
        }
        _trails.RemoveAll(t => t.Owner != r.Id && RayNearSegment(r.Pos, f, t.A, t.B, LanceRadius + 1f, out _));
    }

    void LayTrail(Racer r, float now)
    {
        if ((r.SlingTo - r.SlingFrom).magnitude > TrailBlockSpacing)
            _trails.Add(new Trail { A = r.SlingFrom, B = r.SlingTo, Owner = r.Id, Born = now });
    }

    /// <summary>Closest approach of the ray o + f t (t in 0..range) to the segment a..b: within <paramref name="radius"/>?
    /// <paramref name="t"/> is the ray's parameter there.</summary>
    static bool RayNearSegment(Vector3 o, Vector3 f, Vector3 a, Vector3 b, float radius, out float t)
    {
        Vector3 d2 = b - a, w = o - a;
        float aa = 1f, bb = Vector3.Dot(f, d2), cc = Vector3.Dot(d2, d2), dd = Vector3.Dot(f, w), ee = Vector3.Dot(d2, w);
        float den = aa * cc - bb * bb;
        float s, u;
        if (den < 1e-6f) { s = 0f; u = cc > 1e-6f ? Mathf.Clamp01(ee / cc) : 0f; }
        else
        {
            s = Mathf.Clamp((bb * ee - cc * dd) / den, 0f, ThornlineCourse.SlingRange);
            u = cc > 1e-6f ? Mathf.Clamp01((bb * s + ee) / cc) : 0f;
            s = Mathf.Clamp(Vector3.Dot(a + d2 * u - o, f), 0f, ThornlineCourse.SlingRange);
        }
        t = s;
        return ((o + f * s) - (a + d2 * u)).magnitude <= radius;
    }

    static float OverlapLength(Vector3 o, Vector3 f, Vector3 a, Vector3 b, float radius)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 1e-3f) return 0f;
        float sin = Vector3.Cross(f, d / len).magnitude;
        return Mathf.Min(len, sin < 0.01f ? len : 2f * radius / sin);
    }

    // ── the sweep driver ─────────────────────────────────────────────

    public static float Median(List<float> xs)
    {
        if (xs.Count == 0) return float.NaN;
        xs.Sort();
        return xs[xs.Count / 2];
    }
}
