// Skim Race AI offline simulator — the TRAINING ENVIRONMENT for the Skim Race pilot.
//
// Compiles the SHIPPED decision core (SkimRaceDriver / SkimRaceCourse / SkimRaceObservation /
// SkimRaceAIConfigSO, straight out of Assets/) against UnityShim.cs and flies it through a
// model of the Squirrel that mirrors VesselTransformer line for line:
//   * DecayBoost:   boost -= 0.3/s above 1 (MaxBoost 5), skim hits add +0.1 each
//   * RotateShip:   stick rotates the COMMANDED orientation (roll 130, yaw/pitch 120 deg/s,
//                   about the hull's current axes); the hull slerps toward it at 1.5/s
//   * MoveShip:     speed lerps toward throttle * 60 * boost at 1.5/s; outside a drift the
//                   vector model's grip snaps the velocity onto the nose
// The track is laid exactly as SpawnableWaypointTrack.Spawn lays it (12-unit spacing, linear or
// Catmull-Rom per intensity) and crystals respawn at the next CrystalManager anchor with the
// shipped 35-unit spherical jitter. Physical constants the code does not state (skim reach,
// crystal capture reach, hull contact reach) are CALIBRATED from in-editor probes and passed in;
// see Docs/SKIM_RACE_AI.md. The simulator is a tuning tool — every claim about the pilot is
// re-validated in the real game by the benchmark.
//
// Usage: sim <track.json> <mode> [args]
//   eval  <intensity> <seeds> [key=value ...]          evaluate one config, print stats
//   tune  <intensity> <seeds> <iters> [key=value ...]  cross-entropy search, print best config
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using CosmicShore.Gameplay;
using UnityEngine;

static class Json
{
    // Minimal reader for the harness's own track.json: {"1":{"waypoints":[[x,y,z],...],"anchors":[...],"spline":0,"laps":3}, ...}
    public static Dictionary<int, TrackDef> Load(string path)
    {
        var text = File.ReadAllText(path);
        var result = new Dictionary<int, TrackDef>();
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0) continue;
            var parts = t.Split('|');
            var def = new TrackDef
            {
                Intensity = int.Parse(parts[0], CultureInfo.InvariantCulture),
                Spline = parts[1] == "1",
                Laps = int.Parse(parts[2], CultureInfo.InvariantCulture),
                Waypoints = ParsePts(parts[3]),
                Anchors = ParsePts(parts[4]),
            };
            result[def.Intensity] = def;
        }
        return result;
    }

    static List<Vector3> ParsePts(string s)
    {
        var list = new List<Vector3>();
        foreach (var p in s.Split(';'))
        {
            if (p.Length == 0) continue;
            var c = p.Split(',');
            list.Add(new Vector3(float.Parse(c[0], CultureInfo.InvariantCulture),
                float.Parse(c[1], CultureInfo.InvariantCulture), float.Parse(c[2], CultureInfo.InvariantCulture)));
        }
        return list;
    }
}

class TrackDef
{
    public int Intensity;
    public bool Spline;
    public int Laps;
    public List<Vector3> Waypoints;
    public List<Vector3> Anchors;
}

class Physics
{
    public float SkimReach = 7.5f;       // skimmer sphere radius (probe: 'align skimmer' 15u diameter)
    public float CaptureReach = 25.5f;   // crystal sphere radius 24 (probe) + hull half-extent
    public float HullReach = 0.6f;       // hull box half-height (probe: 4.1 x 0.6 x 3.1) over the shell
    // The hull is a BOX, not a point: bounds 4.12 x 0.59 x 3.11 (probe, full size). A banked
    // wingtip reaches 2 u further than the hull centre; HullBox=1 samples the box's mid-plane.
    public int HullBox = 1;
    // Mass the Squirrel lays itself (both measured in-editor, SkimRaceRaceRecorder hits log):
    //  - its trail: two rails of 0.83 x 0.83 x 6.1 prisms at +-9.66 u along ShipTransform.right,
    //    one pair per 7 u travelled (VesselPrismController Gap 18.5, BaseScale 20x0.75x5.5,
    //    trailVolume 1.35); own-trail grace 1 s (SelfTrailContactConfig);
    //  - the pickup ring: 8 shielded 1.8 x 1.8 x 7.5 prisms on radius 8.2, centred 8 u ahead
    //    of the hull (AOEShieldedRingSpawner / SpawnableRings), no grace.
    // SquirrelVesselChangeSpeedByPrism: each contact multiplies the ACTUAL speed by
    // 1 - min(volume * 0.1, 0.5), easing back over 1 s; concurrent contacts stack
    // (VesselTransformer.ApplyThrottleModifiers). StackedSlow=0 = the old 0.5-on-throttle model.
    public int StackedSlow = 1;
    public int TrailRails = 1;
    public float RailOffset = 9.66f, RailSpacing = 7f, TrailGrace = 1f;
    public int RingGeometry = 1;
    public float HullHalfX = 2.06f, HullHalfZ = 1.55f;
    public float HullSlow = 0.5f;        // throttle multiplier applied for HullSlowSeconds on a strike
    public float HullSlowSeconds = 1f;
    public float SkimAdd = 0.1f;
    public float PickupRingPrisms = 8f;
    public float BoostDecay = 0.3f;
    public float MaxBoost = 5f;
    public float ThrottleScaler = 60f;
    public float TurnRate = 120f;
    public float RollRate = 130f;
    public float Follow = 1.5f;
    public float Jitter = 35f;
    public Vector3 SpawnPos = new(700, 20, -200); // the AI seat's spawn (probe)
    public Vector3 SpawnFwd = new(0, 0, 1);
    public float Dt = 1f / 60f;
    public float DtJitter = 0f;          // frame-time noise as a fraction of Dt (editor frames are uneven)
}

class TrackPrisms
{
    public readonly List<Vector3> Points = new();
    public readonly List<Vector3> Normals = new();
    public readonly List<Quaternion> Rotations = new();
    public readonly List<Vector3> ShellHalf = new();   // super-shield stella reach: 1.5 x leaf size per axis
    // spatial hash for fast reach queries
    readonly Dictionary<long, List<int>> _cells = new();
    const float Cell = 40f;

    public TrackPrisms(TrackDef def)
    {
        var w = def.Waypoints;
        int n = w.Count;
        for (int seg = 0; seg < n; seg++)
        {
            float len;
            if (!def.Spline) len = Vector3.Distance(w[seg], w[(seg + 1) % n]);
            else
            {
                len = 0f; Vector3 prev = Spline(w, seg, 0f);
                for (int s = 1; s <= 20; s++) { var c = Spline(w, seg, s / 20f); len += Vector3.Distance(prev, c); prev = c; }
            }
            int blocks = Math.Max(1, (int)Math.Round(len / 12f, MidpointRounding.ToEven));
            for (int i = 0; i < blocks; i++)
            {
                float t = (float)i / blocks;
                Vector3 pos, look;
                if (def.Spline)
                {
                    pos = Spline(w, seg, t);
                    look = i < blocks - 1 ? Spline(w, seg, (float)(i + 1) / blocks) : Spline(w, (seg + 1) % n, 0f);
                }
                else
                {
                    pos = Vector3.Lerp(w[seg], w[(seg + 1) % n], t);
                    look = i < blocks - 1 ? Vector3.Lerp(w[seg], w[(seg + 1) % n], (float)(i + 1) / blocks) : w[(seg + 1) % n];
                }
                var rot = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
                Points.Add(pos);
                Normals.Add(rot * Vector3.up);
                Rotations.Add(rot);
                // SpawnableWaypointTrack: scale (10,1,3); the waypoint marker (i == 0) is 2x.
                Vector3 leaf = i == 0 ? new Vector3(20, 2, 6) : new Vector3(10, 1, 3);
                ShellHalf.Add(leaf * 1.5f);
            }
        }
        for (int i = 0; i < Points.Count; i++)
        {
            long k = Key(Points[i]);
            if (!_cells.TryGetValue(k, out var l)) _cells[k] = l = new List<int>();
            l.Add(i);
        }
    }

    static Vector3 Spline(List<Vector3> p, int seg, float t)
    {
        int c = p.Count;
        Vector3 p0 = p[((seg - 1) % c + c) % c], p1 = p[seg], p2 = p[(seg + 1) % c], p3 = p[(seg + 2) % c];
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    static long Key(Vector3 p)
    {
        long x = (long)Math.Floor(p.x / Cell), y = (long)Math.Floor(p.y / Cell), z = (long)Math.Floor(p.z / Cell);
        return (x * 73856093L) ^ (y * 19349663L) ^ (z * 83492791L);
    }

    /// <summary>Distance from <paramref name="p"/> to prism <paramref name="i"/>'s super-shield
    /// stella octangula (two tetrahedra inscribed in the box of half-extents 1.5 x leaf), via
    /// SkimRaceShell — the same function the pilot's hull guard uses.</summary>
    public float ShellDistance(int i, Vector3 p) =>
        SkimRaceShell.StellaDistance(Quaternion.Inverse(Rotations[i]) * (p - Points[i]), ShellHalf[i]);

    public void Query(Vector3 p, float r, List<int> into)
    {
        into.Clear();
        int span = (int)Math.Ceiling(r / Cell);
        long bx = (long)Math.Floor(p.x / Cell), by = (long)Math.Floor(p.y / Cell), bz = (long)Math.Floor(p.z / Cell);
        float r2 = r * r;
        for (long dx = -span; dx <= span; dx++)
        for (long dy = -span; dy <= span; dy++)
        for (long dz = -span; dz <= span; dz++)
        {
            long k = ((bx + dx) * 73856093L) ^ ((by + dy) * 19349663L) ^ ((bz + dz) * 83492791L);
            if (!_cells.TryGetValue(k, out var l)) continue;
            foreach (var i in l) if ((Points[i] - p).sqrMagnitude <= r2) into.Add(i);
        }
    }
}

class RaceResult
{
    public float FarFrac, MeanBoost;
    public bool Finished;
    public float Time;
    public int Collected;
    public int Required;
    public int Recoveries;
    public int HullHits;
    public float MeanSpeed;
}

// Boxes the race leaves behind (trail rails, pickup rings), bucketed for cheap queries.
sealed class Obstacles
{
    public readonly List<(Vector3 c, Quaternion r, Vector3 half, float activeAt)> Items = new();
    readonly Dictionary<long, List<int>> _grid = new();
    const float Cell = 24f;
    static long Key(int x, int y, int z) => ((long)(x + 100000) * 200003L + (y + 100000)) * 200003L + (z + 100000);
    public void Add(Vector3 c, Quaternion r, Vector3 half, float activeAt)
    {
        int id = Items.Count;
        Items.Add((c, r, half, activeAt));
        long k = Key(Mathf.FloorToInt(c.x / Cell), Mathf.FloorToInt(c.y / Cell), Mathf.FloorToInt(c.z / Cell));
        if (!_grid.TryGetValue(k, out var l)) _grid[k] = l = new List<int>();
        l.Add(id);
    }
    public void Query(Vector3 p, List<int> outIds)
    {
        outIds.Clear();
        int cx = Mathf.FloorToInt(p.x / Cell), cy = Mathf.FloorToInt(p.y / Cell), cz = Mathf.FloorToInt(p.z / Cell);
        for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
            if (_grid.TryGetValue(Key(cx + x, cy + y, cz + z), out var l)) outIds.AddRange(l);
    }
    public void QueryRadius(Vector3 p, float r, List<int> outIds)
    {
        outIds.Clear();
        int r0 = Mathf.CeilToInt(r / Cell);
        int cx = Mathf.FloorToInt(p.x / Cell), cy = Mathf.FloorToInt(p.y / Cell), cz = Mathf.FloorToInt(p.z / Cell);
        for (int x = -r0; x <= r0; x++) for (int y = -r0; y <= r0; y++) for (int z = -r0; z <= r0; z++)
            if (_grid.TryGetValue(Key(cx + x, cy + y, cz + z), out var l)) outIds.AddRange(l);
    }
    public float Distance(int i, Vector3 p)
    {
        var it = Items[i];
        Vector3 lp = Quaternion.Inverse(it.r) * (p - it.c);
        Vector3 q = new Vector3(Math.Max(Math.Abs(lp.x) - it.half.x, 0f), Math.Max(Math.Abs(lp.y) - it.half.y, 0f), Math.Max(Math.Abs(lp.z) - it.half.z, 0f));
        return q.magnitude;
    }
}

static class Race
{
    // Unity's Random.onUnitSphere stand-in (the shape matters, not the stream).
    static Vector3 OnUnitSphere(System.Random r)
    {
        while (true)
        {
            var v = new Vector3((float)(r.NextDouble() * 2 - 1), (float)(r.NextDouble() * 2 - 1), (float)(r.NextDouble() * 2 - 1));
            float m = v.sqrMagnitude;
            if (m > 1e-4f && m <= 1f) return v / (float)Math.Sqrt(m);
        }
    }

    public static RaceResult Run(TrackDef def, TrackPrisms prisms, SkimRaceCourse course, SkimRaceAIConfigSO cfg,
        Physics ph, int seed, float limit, bool trace = false)
    {
        var rng = new System.Random(seed);
        var driver = new SkimRaceDriver(cfg);
        driver.Reset();

        int required = def.Waypoints.Count * def.Laps;
        Vector3 pos = ph.SpawnPos;
        Quaternion rot = Quaternion.LookRotation(ph.SpawnFwd, Vector3.up);
        Quaternion acc = rot;
        float speed = 0f, boost = 1f;
        bool boosting = false;
        float slowUntil = -1f;
        int anchor = 0;
        Vector3 crystal = def.Anchors[0] + OnUnitSphere(rng) * ph.Jitter;
        int collected = 0;
        var inside = new HashSet<int>();
        var hull = new HashSet<int>();
        var near = new List<int>();
        int hint = -1, hullHits = 0;
        float t = 0f, speedSum = 0f;
        int frames = 0, farFrames = 0; float boostSum = 0f;
        var mods = new List<(float init, float start)>();
        float Mult(float now)
        {
            float m = 1f;
            for (int i = mods.Count - 1; i >= 0; i--)
            {
                float e = now - mods[i].start;
                if (e >= ph.HullSlowSeconds) { mods.RemoveAt(i); continue; }
                m *= Mathf.Lerp(mods[i].init, 1f, e / ph.HullSlowSeconds);
            }
            return m;
        }
        void Slow(float volume, float now) { if (ph.StackedSlow != 0) mods.Add((1f - Math.Min(volume * 0.1f, 0.5f), now)); else slowUntil = now + ph.HullSlowSeconds; }
        Vector3 lastFwd = rot * Vector3.forward;
        float maxT = limit + 60f;
        var ring = new List<(Vector3 c, Quaternion r)>();
        var ringInside = new HashSet<int>();
        var ringHull = new HashSet<int>();
        float ringReady = 0f;
        int ringsLaid = 0;
        var obs = new Obstacles();
        var obsNear = new List<int>();
        var obsInside = new HashSet<int>();
        var obsHull = new HashSet<int>();
        float railTravel = 0f;
        int obsHits = 0;

        while (t < maxT)
        {
            float dt = ph.DtJitter > 0f ? ph.Dt * (1f + ph.DtJitter * (float)(rng.NextDouble() * 2.0 - 1.0)) : ph.Dt;
            // ── observe ──
            Vector3 fwd = rot * Vector3.forward, up = rot * Vector3.up, right = rot * Vector3.right;
            var o = new SkimRaceObservation
            {
                Position = pos, Forward = fwd, Right = right, Up = up,
                CommandedForward = acc * Vector3.forward,
                Rotation = rot, CommandedRotation = acc,
                Speed = speed * (ph.StackedSlow != 0 ? Mult(t) : 1f), Velocity = fwd * speed,
                BoostMultiplier = boosting ? boost : 1f, MaxBoost = ph.MaxBoost,
                TurnRateDegrees = ph.TurnRate, FollowRate = ph.Follow, ThrottleScaler = ph.ThrottleScaler,
                RaceTime = t, Collected = collected, Remaining = required - collected,
                HasTarget = true, TargetPosition = crystal, ToTarget = crystal - pos,
            };
            o.TargetDistance = o.ToTarget.magnitude;
            o.TargetRadius = ph.CaptureReach - 1.5f;
            o.TargetAlignment = Vector3.Dot(fwd, o.ToTarget.normalized);
            o.HasCourse = true;
            o.CourseLength = course.Length;
            o.CourseProgress = course.Project(pos, ref hint, out _, out o.CourseDistance);
            course.Sample(o.CourseProgress, out o.CourseTangent, out _);
            int th = hint;
            float sc = course.Project(crystal, ref th, out _, out _);
            o.TargetAheadOnCourse = course.Ahead(o.CourseProgress, sc);
            o.Sanitize();

            // Perception: the laid mass the pilot can see near its path (SkimRacePilot does the same
            // from PrismSpatialIndex), excluding its own trail still inside the self-contact grace.
            driver.Obstacles.Clear();
            if (cfg.MassGuardSeconds > 0f)
            {
                Vector3 qc = pos + fwd * (speed * cfg.MassGuardSeconds * 0.5f);
                float qr = speed * cfg.MassGuardSeconds * 0.5f + 20f;
                obs.QueryRadius(qc, qr, obsNear);
                foreach (var i in obsNear)
                {
                    var it = obs.Items[i];
                    if (t < it.activeAt) continue;
                    if ((it.c - qc).sqrMagnitude > qr * qr) continue;
                    driver.Obstacles.Add(new SkimRaceObstacle { Center = it.c, Rotation = it.r, Half = it.half });
                }
            }
            var a = driver.Decide(o, course, t, dt);
            if (a.Ring && t >= ringReady)
            {
                // SquirrelTubeAction: 8 danger cubes (scale 4) on radius 8, laid 100 ahead of the nose.
                ringReady = t + 20f;
                ringsLaid++;
                ring.Clear(); ringInside.Clear(); ringHull.Clear();
                Vector3 c0 = pos + fwd * 100f;
                for (int k = 0; k < 8; k++)
                {
                    float ang = k * Mathf.PI * 2f / 8f;
                    ring.Add((c0 + (right * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * 8f, rot));
                }
            }

            // ── VesselTransformer.Update order: DecayBoost, RotateShip, MoveShip ──
            if (boost > 1f) boost -= ph.BoostDecay * dt; else boost = Math.Min(1f, boost + ph.BoostDecay * dt);

            acc = Quaternion.AngleAxis(a.Roll * ph.RollRate * dt, fwd) * acc;
            acc = Quaternion.AngleAxis(a.Yaw * ph.TurnRate * dt, up) * acc;
            acc = Quaternion.AngleAxis(a.Pitch * ph.TurnRate * dt, right) * acc;
            rot = Quaternion.Slerp(rot, acc, ph.Follow * dt);

            float thr = a.Throttle * (t < slowUntil ? ph.HullSlow : 1f);
            float mult = ph.StackedSlow != 0 ? Mult(t) : 1f;
            float target = thr * ph.ThrottleScaler * (boosting ? boost : 1f);
            speed = Mathf.Lerp(speed, target, ph.Follow * dt);
            fwd = rot * Vector3.forward;
            pos += fwd * (speed * mult) * dt;
            speedSum += speed; frames++;
            if (o.CourseDistance > 20f) farFrames++;
            boostSum += boosting ? boost : 1f;
            t += dt;

            // ── lay own trail rails ──
            if (ph.TrailRails != 0 && t > 2f && speed > 3f)
            {
                railTravel += speed * dt;
                while (railTravel >= ph.RailSpacing)
                {
                    railTravel -= ph.RailSpacing;
                    Vector3 rgt = rot * Vector3.right;
                    var half = new Vector3(0.415f, 0.415f, 3.04f);
                    obs.Add(pos + rgt * ph.RailOffset, rot, half, t + ph.TrailGrace);
                    obs.Add(pos - rgt * ph.RailOffset, rot, half, t + ph.TrailGrace);
                }
            }
            // ── contacts with laid mass ──
            obs.Query(pos, obsNear);
            {
                var onow = new HashSet<int>(); var hnowO = new HashSet<int>();
                foreach (var i in obsNear)
                {
                    if (t < obs.Items[i].activeAt) continue;
                    float d = obs.Distance(i, pos);
                    if (d <= ph.SkimReach) onow.Add(i);
                    float dh = d;
                    if (ph.HullBox != 0 && d <= ph.HullReach + 3f)
                        for (int bx = -1; bx <= 1; bx++)
                        for (int bz = -1; bz <= 1; bz++)
                            dh = Math.Min(dh, obs.Distance(i, pos + rot * new Vector3(bx * ph.HullHalfX, 0f, bz * ph.HullHalfZ)));
                    if (dh <= ph.HullReach) hnowO.Add(i);
                }
                foreach (var i in onow)
                    if (obsInside.Add(i)) { boosting = true; boost = Mathf.Clamp(boost + ph.SkimAdd, 1f, ph.MaxBoost); }
                obsInside.IntersectWith(onow);
                foreach (var i in hnowO)
                    if (obsHull.Add(i))
                    {
                        hullHits++; obsHits++; var oh = obs.Items[i].half; Slow(8f * oh.x * oh.y * oh.z, t); boost = 1f;
                        if (trace) Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "   OBSHIT t={0:F2} spd={1:F0} pull={2}", t, speed, driver.LastDiagnostics.CrystalPull));
                    }
                obsHull.IntersectWith(hnowO);
            }

            // ── skimmer & hull contacts (enter events), against each prism's shell ──
            prisms.Query(pos, 40f, near);
            var now = new HashSet<int>();
            var hnow = new HashSet<int>();
            foreach (var i in near)
            {
                float d = prisms.ShellDistance(i, pos);
                if (d <= ph.SkimReach) now.Add(i);
                float dh = d;
                if (ph.HullBox != 0 && d <= ph.HullReach + 3f)
                    for (int bx = -1; bx <= 1; bx++)
                    for (int bz = -1; bz <= 1; bz++)
                        dh = Math.Min(dh, prisms.ShellDistance(i, pos + rot * new Vector3(bx * ph.HullHalfX, 0f, bz * ph.HullHalfZ)));
                if (dh <= ph.HullReach) hnow.Add(i);
            }
            foreach (var i in now)
                if (inside.Add(i))
                {
                    boosting = true;
                    boost = Mathf.Clamp(boost + ph.SkimAdd, 1f, ph.MaxBoost);
                }
            inside.IntersectWith(now);
            foreach (var i in hnow)
                if (hull.Add(i))
                {
                    hullHits++; Slow(30f, t); boost = 1f; // VesselResetBoostPrismEffect + SquirrelVesselChangeSpeedByPrism
                    if (trace)
                    {
                        Vector3 lp = Quaternion.Inverse(prisms.Rotations[i]) * (pos - prisms.Points[i]);
                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "   HIT t={0:F2} prism={1} local=({2:F1},{3:F1},{4:F1}) spd={5:F0} mode={6} pull={7} dist={8:F0}",
                            t, i, lp.x, lp.y, lp.z, speed, driver.CurrentMode, driver.LastDiagnostics.CrystalPull, (crystal - pos).magnitude));
                    }
                }
            hull.IntersectWith(hnow);

            for (int k = 0; k < ring.Count; k++)
            {
                Vector3 lp = Quaternion.Inverse(ring[k].r) * (pos - ring[k].c);
                Vector3 q = new Vector3(Math.Max(Math.Abs(lp.x) - 2f, 0f), Math.Max(Math.Abs(lp.y) - 2f, 0f), Math.Max(Math.Abs(lp.z) - 2f, 0f));
                float d = q.magnitude;
                if (d <= ph.SkimReach) { if (ringInside.Add(k)) { boosting = true; boost = Mathf.Clamp(boost + ph.SkimAdd, 1f, ph.MaxBoost); } }
                if (d <= ph.HullReach && ringHull.Add(k)) { hullHits++; boost = 1f; slowUntil = t + 3f; }
            }

            // ── crystal ──
            if ((crystal - pos).sqrMagnitude <= ph.CaptureReach * ph.CaptureReach)
            {
                collected++;
                // SquirrelVesselExplosionByCrystalEffect lays a ring of 8 shielded boost prisms
                // around the hull at the pickup (AOEShieldedRingSpawner): 8 instant skims.
                if (ph.RingGeometry != 0)
                {
                    Vector3 rc = pos + (rot * Vector3.forward) * 8f;
                    for (int k = 0; k < 8; k++)
                    {
                        float ang = k * Mathf.PI * 2f / 8f;
                        Vector3 radial = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                        obs.Add(rc + rot * (radial * 8.2f), rot * Quaternion.LookRotation(Vector3.forward, radial),
                            new Vector3(0.9f, 0.9f, 3.75f), t);
                    }
                }
                else
                {
                    boosting = true;
                    boost = Mathf.Clamp(boost + ph.SkimAdd * ph.PickupRingPrisms, 1f, ph.MaxBoost);
                }
                if (collected >= required)
                    return new RaceResult { Finished = true, Time = t, Collected = collected, Required = required,
                        Recoveries = driver.Recoveries, HullHits = hullHits, MeanSpeed = speedSum / frames, FarFrac = farFrames / (float)frames, MeanBoost = boostSum / frames };
                anchor = (anchor + 1) % def.Anchors.Count;
                crystal = def.Anchors[anchor] + OnUnitSphere(rng) * ph.Jitter;
            }

            if (trace && frames % 6 == 0)
            {
                Vector3 cpt = course.Sample(o.CourseProgress, out Vector3 tt, out Vector3 nn);
                Vector3 ll = Vector3.Cross(nn, tt).normalized; nn = Vector3.Cross(tt, ll).normalized;
                Vector3 rr = pos - cpt;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:F2} pos=({1:F0},{2:F0},{3:F0}) spd={4:F0} boost={5:F2} dist={6:F0} col={7} mode={8} err={9:F0} lat={10:F1} h={11:F1} skim={12} pull={13}",
                    t, pos.x, pos.y, pos.z, speed, boost, o.TargetDistance, collected, driver.CurrentMode,
                    driver.LastDiagnostics.HeadingErrorDegrees, Vector3.Dot(rr, ll), Vector3.Dot(rr, nn), inside.Count,
                    driver.LastDiagnostics.CrystalPull ? 1 : 0));
            }
        }
        if (trace) Console.WriteLine($"rings laid: {ringsLaid}");
        return new RaceResult { Finished = false, Time = t, Collected = collected, Required = required,
            Recoveries = driver.Recoveries, HullHits = hullHits, MeanSpeed = speedSum / Math.Max(1, frames), FarFrac = farFrames / (float)Math.Max(1, frames), MeanBoost = boostSum / Math.Max(1, frames) };
    }
}

static class Program
{
    static string[] Tunables;   // set in tune mode (static field order would leave it null here)
    static readonly string[] PlannerTunables =
    {
        "LookaheadSeconds", "SkimHeight", "CrystalBumpHalfWidth", "PassMargin", "StickGainPerDegree",
        "HullMargin", "PlannerHorizon", "PlannerSegment", "PlannerSlowThrottle", "PlannerCaptureMargin",
        "PlannerBoostValue", "PlannerExitWeight", "PlannerClearanceBuffer", "PlannerClearancePenalty",
        "PlannerSkimReach",
    };
    static readonly string[] PursuitTunables =
    {
        "LookaheadSeconds", "LookaheadMin", "LookaheadMax", "SkimHeight", "CrystalBumpHalfWidth",
        "CrystalDirectDistance", "LeadGain", "MaxLeadDegrees", "StickGainPerDegree", "MinThrottle",
        "ReachabilityMargin", "PassMargin", "CrossingHeightFraction", "RibbonClearHeight", "RibbonClearLateral",
        "TerminalCentreBias", "StallSeconds", "RecoveryThrottle",
        "MassGuardSeconds", "MassGuardMargin", "MassGuardSegment", "LowBoostApproachScale", "LowBoostFull",
    };
    static readonly Dictionary<string, (float lo, float hi)> Ranges = new()
    {
        ["LowBoostApproachScale"] = (0.3f, 1f), ["LowBoostFull"] = (1.5f, 5f),
        ["MassGuardSeconds"] = (0.2f, 1.0f), ["MassGuardMargin"] = (0.2f, 3f), ["MassGuardSegment"] = (0.08f, 0.5f),
        ["LookaheadSeconds"] = (0.2f, 2.0f), ["LookaheadMin"] = (20f, 200f), ["LookaheadMax"] = (120f, 700f),
        ["SkimHeight"] = (3f, 9f), ["CrystalBumpHalfWidth"] = (150f, 900f), ["CrystalDirectDistance"] = (60f, 500f),
        ["LeadGain"] = (1f, 3f), ["MaxLeadDegrees"] = (10f, 90f), ["StickGainPerDegree"] = (0.02f, 0.5f),
        ["MinThrottle"] = (0.1f, 1f), ["ReachabilityMargin"] = (0f, 2f),
        ["PassMargin"] = (0f, 16f), ["CrossingHeightFraction"] = (0.1f, 0.9f), ["SlabGuardSeconds"] = (0f, 1.2f),
        ["RibbonClearHeight"] = (2.5f, 7f), ["RibbonClearLateral"] = (17f, 40f), ["HullGuardSeconds"] = (0f, 1f), ["HullMargin"] = (0.2f, 4f),
        ["HullEscapeLift"] = (5f, 120f), ["TerminalCentreBias"] = (0f, 1f), ["StallSeconds"] = (1f, 8f),
        ["RecoveryThrottle"] = (0.1f, 1f),
        ["PlannerHorizon"] = (0.6f, 3.5f), ["PlannerSegment"] = (0.1f, 1.2f), ["PlannerSlowThrottle"] = (0.1f, 0.9f),
        ["PlannerCaptureMargin"] = (0f, 14f), ["PlannerBoostValue"] = (0f, 5f), ["PlannerExitWeight"] = (0f, 3f),
        ["PlannerClearanceBuffer"] = (0f, 6f), ["PlannerClearancePenalty"] = (0f, 3f), ["PlannerSkimReach"] = (4f, 9f),
    };

    static void Set(object target, string key, string value)
    {
        var f = target.GetType().GetField(key, BindingFlags.Public | BindingFlags.Instance);
        if (f == null) throw new ArgumentException("unknown field " + key);
        if (f.FieldType == typeof(float)) f.SetValue(target, float.Parse(value, CultureInfo.InvariantCulture));
        else if (f.FieldType == typeof(int)) f.SetValue(target, int.Parse(value, CultureInfo.InvariantCulture));
        else if (f.FieldType == typeof(bool)) f.SetValue(target, value == "1" || value == "true");
        else if (f.FieldType == typeof(Vector3))
        {
            var c = value.Split(',');
            f.SetValue(target, new Vector3(float.Parse(c[0], CultureInfo.InvariantCulture), float.Parse(c[1], CultureInfo.InvariantCulture), float.Parse(c[2], CultureInfo.InvariantCulture)));
        }
        else f.SetValue(target, value);
    }

    static (SkimRaceAIConfigSO cfg, Physics ph) Parse(IEnumerable<string> kv)
    {
        var cfg = new SkimRaceAIConfigSO();
        var ph = new Physics();
        foreach (var a in kv)
        {
            int eq = a.IndexOf('=');
            if (eq < 0) continue;
            string k = a.Substring(0, eq), v = a.Substring(eq + 1);
            if (k.StartsWith("ph.")) Set(ph, k.Substring(3), v); else Set(cfg, k, v);
        }
        return (cfg, ph);
    }

    static (float score, int fin, float median, float worst, float mean, List<RaceResult> runs) Evaluate(
        TrackDef def, TrackPrisms prisms, SkimRaceCourse course, SkimRaceAIConfigSO cfg, Physics ph, int seeds, float limit, int seedBase = 1000)
    {
        var runs = new List<RaceResult>();
        for (int s = 0; s < seeds; s++) runs.Add(Race.Run(def, prisms, course, cfg, ph, seedBase + s, limit));
        var fin = runs.Where(r => r.Finished).Select(r => r.Time).OrderBy(x => x).ToList();
        float median = fin.Count > 0 ? fin[fin.Count / 2] : 999f;
        float worst = runs.All(r => r.Finished) ? fin.Max() : 999f;
        float mean = fin.Count > 0 ? fin.Average() : 999f;
        // Score: every unfinished race is a disaster; then the WORST time, then the mean.
        float score = runs.Sum(r => r.Finished ? r.Time : 300f + (r.Required - r.Collected) * 10f) / seeds
                      + 0.5f * (runs.All(r => r.Finished) ? fin.Max() : 300f);
        return (score, fin.Count, median, worst, mean, runs);
    }

    static SkimRaceAIConfigSO CloneConfig(SkimRaceAIConfigSO src)
    {
        var c = new SkimRaceAIConfigSO();
        foreach (var f in typeof(SkimRaceAIConfigSO).GetFields(BindingFlags.Public | BindingFlags.Instance))
            f.SetValue(c, f.GetValue(src));
        return c;
    }

    static string Describe(SkimRaceAIConfigSO cfg) =>
        string.Join(" ", Tunables.Select(k => k + "=" + ((float)cfg.GetType().GetField(k).GetValue(cfg)).ToString("0.###", CultureInfo.InvariantCulture)));

    static int Main(string[] args)
    {
        var tracks = Json.Load(args[0]);
        string mode = args[1];
        int intensity = int.Parse(args[2]);
        var def = tracks[intensity];
        var prisms = new TrackPrisms(def);
        var course = new SkimRaceCourse(prisms.Points, prisms.Normals, prisms.Rotations, prisms.ShellHalf);
        float limit = 70f;

        if (mode == "eval" || mode == "trace")
        {
            int seeds = int.Parse(args[3]);
            int seedBase = 1000;
            foreach (var a in args.Skip(4))
                if (a.StartsWith("seedbase=")) seedBase = int.Parse(a.Substring(9), CultureInfo.InvariantCulture);
            var (cfg, ph) = Parse(args.Skip(4).Where(a => !a.StartsWith("seedbase=")));
            if (mode == "trace") { var r = Race.Run(def, prisms, course, cfg, ph, 1000 + seeds, limit, true); Console.WriteLine($"finished={r.Finished} t={r.Time:F2} {r.Collected}/{r.Required}"); return 0; }
            var e = Evaluate(def, prisms, course, cfg, ph, seeds, limit, seedBase);
            Console.WriteLine($"I{intensity} track={course.Length:F0}u prisms={prisms.Points.Count} finished {e.fin}/{seeds} " +
                              $"median={e.median:F2} mean={e.mean:F2} worst={e.worst:F2} score={e.score:F2}");
            foreach (var r in e.runs)
                Console.WriteLine($"  {(r.Finished ? "FIN" : "DNF")} t={r.Time:F2} {r.Collected}/{r.Required} recov={r.Recoveries} hull={r.HullHits} mean={r.MeanSpeed:F0} boost={r.MeanBoost:F2} far={r.FarFrac:F2}");
            return 0;
        }

        if (mode == "tune")
        {
            int seeds = int.Parse(args[3]);
            int iters = int.Parse(args[4]);
            float sigmaScale = 0.25f;
            Tunables = PursuitTunables;
            foreach (var a in args.Skip(5))
            {
                if (a.StartsWith("sigma=")) sigmaScale = float.Parse(a.Substring(6), CultureInfo.InvariantCulture);
                if (a == "set=planner") Tunables = PlannerTunables;
            }
            var (baseCfg, ph) = Parse(args.Skip(5).Where(a => !a.StartsWith("sigma=") && !a.StartsWith("set=")));
            var rng = new System.Random(7);
            int dim = Tunables.Length;
            var mu = Tunables.Select(k => (float)baseCfg.GetType().GetField(k).GetValue(baseCfg)).ToArray();
            var sigma = Tunables.Select(k => (Ranges[k].hi - Ranges[k].lo) * sigmaScale).ToArray();
            for (int d = 0; d < dim; d++) mu[d] = Mathf.Clamp(mu[d], Ranges[Tunables[d]].lo, Ranges[Tunables[d]].hi);
            float bestScore = float.MaxValue; float[] best = (float[])mu.Clone();
            int pop = 24, elite = 6;
            for (int it = 0; it < iters; it++)
            {
                var xs = new List<float[]>();
                for (int p = 0; p < pop; p++)
                {
                    var x = new float[dim];
                    for (int d = 0; d < dim; d++)
                    {
                        double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble();
                        float g = (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
                        x[d] = Mathf.Clamp(mu[d] + g * sigma[d], Ranges[Tunables[d]].lo, Ranges[Tunables[d]].hi);
                    }
                    if (p == 0) x = (float[])best.Clone();
                    xs.Add(x);
                }
                var scores = new float[pop];
                int iterSeed = 1000 + it * 7919;
                System.Threading.Tasks.Parallel.For(0, pop, p =>
                {
                    var cfg = CloneConfig(baseCfg);
                    for (int d = 0; d < dim; d++) cfg.GetType().GetField(Tunables[d]).SetValue(cfg, xs[p][d]);
                    scores[p] = Evaluate(def, prisms, course, cfg, ph, seeds, limit, iterSeed).score;
                });
                var samples = new List<(float score, float[] x)>();
                for (int p = 0; p < pop; p++) samples.Add((scores[p], xs[p]));
                samples.Sort((a, b) => a.score.CompareTo(b.score));
                if (samples[0].score < bestScore) { bestScore = samples[0].score; best = (float[])samples[0].x.Clone(); }
                for (int d = 0; d < dim; d++)
                {
                    float m = 0; for (int e = 0; e < elite; e++) m += samples[e].x[d]; m /= elite;
                    float v = 0; for (int e = 0; e < elite; e++) v += (samples[e].x[d] - m) * (samples[e].x[d] - m); v /= elite;
                    mu[d] = m; sigma[d] = Math.Max((float)Math.Sqrt(v), (Ranges[Tunables[d]].hi - Ranges[Tunables[d]].lo) * 0.02f);
                }
                var bc = CloneConfig(baseCfg);
                for (int d = 0; d < dim; d++) bc.GetType().GetField(Tunables[d]).SetValue(bc, best[d]);
                Console.WriteLine($"iter {it} gen-best={samples[0].score:F2} best={bestScore:F2} :: {Describe(bc)}");
            }
            var fc = CloneConfig(baseCfg);
            for (int d = 0; d < dim; d++) fc.GetType().GetField(Tunables[d]).SetValue(fc, best[d]);
            var fe = Evaluate(def, prisms, course, fc, ph, 40, limit, 99000);
            Console.WriteLine($"FINAL (40 fresh seeds) finished {fe.fin}/40 median={fe.median:F2} mean={fe.mean:F2} worst={fe.worst:F2}");
            Console.WriteLine("BEST " + Describe(fc));
            return 0;
        }
        Console.Error.WriteLine("unknown mode");
        return 2;
    }
}
