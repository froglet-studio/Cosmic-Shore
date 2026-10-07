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
//   fingerprint                                         each track's map fingerprint (SkimRaceTrackFingerprint)
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
    public int TrackHits = 1, MassHits = 1;
    public int Seats = 1;           // AI seats racing at once (each its own crystal stream)
    // TEAM RACE (Docs/SKIM_RACE_AI.md section 13). Team=1 puts every seat on ONE domain, as the game
    // does for teammates: one crystal per seat, each walking the anchors on its own
    // (CrystalManager.CalculateNewSpawnPos), any teammate may take any of them, and the team's SUM
    // races the target (SkimRaceScoringRuleSO.IsObjectiveReached; the pilot observes the sum).
    // TeamRule picks each seat's crystal: 1 = the game's team plan (SkimRaceTeamPlan): the shipped
    // SkimRaceTeamAssignment gives every seat a different crystal, the least total distance, kept until
    // another plan is SplitHyst cheaper (SkimRaceTargetTracker.Hysteresis in the game); 0 = the rule
    // before team play, every seat on the nearest crystal (SkimRaceTargetTracker.SelectIndex) - for A/B.
    // A lone seat flies the nearest crystal under either rule, as a lone AI does in the game.
    public int Team = 0;
    public int TeamRule = 1;
    public float SplitHyst = 0.85f;
    public int TargetHintFix = 1;   // 0 = project the crystal from the VESSEL's hint (the pre-fix behaviour), for A/B only
    public int LineDiag = 0;
    public float ExtraTime = 60f;   // a race is cut at limit + this
    public float ColliderDelay = 0.5f;      // VesselPrismController defaultWaitTime: a laid prism's collider is off this long for everyone   // diagnostic switches: 0 = that contact class never strikes
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
    // The lobby AI difficulty's deliberate mistakes (SkimRaceHandicap, Docs/SKIM_RACE_AI.md section 10):
    // seconds before a new crystal is noticed, and the chance per crystal of misjudging its pass.
    // Both 0 = no handicap (Hard). The `handicap` mode searches HcMistake for a target time.
    public float HcReaction = 0f;
    public float HcMistake = 0f;

    /// <summary>A copy to vary one setting on while other races read this one (parallel evaluation).</summary>
    public Physics Clone() => (Physics)MemberwiseClone();
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
    public List<float> TrackErrors = new();
    public List<float> LineClear = new();
    public Dictionary<string, int> ResetCauses = new();
    public Dictionary<string, float> ResetBoostLost = new();
    public float[] AgentTimes = Array.Empty<float>();   // every AI seat's own finish time (999 = did not finish)
    // STEP-1 diagnostics: time / boost / speed / skim time per flight phase, and every track hit.
    public Dictionary<string, float> PhaseTime = new(), PhaseBoostT = new(), PhaseSpeedT = new(), PhaseSkimT = new();
    public List<(string phase, float lat, float hgt, float along, float headErr, float speed, float boost)> TrackHitLog = new();
    public float FarFrac, MeanBoost;
    public bool Finished;
    public float Time;
    public int Collected;
    public int Required;
    public int Recoveries;
    public int HullHits;
    public int Mistakes;      // deliberate misjudged crystals, all seats (SkimRaceHandicap)
    public float MeanSpeed;
}

// Boxes the race leaves behind (trail rails, pickup rings), bucketed for cheap queries.
sealed class Obstacles
{
    public readonly List<(Vector3 c, Quaternion r, Vector3 half, float activeAt)> Items = new();
    public readonly List<(int owner, float ownerActiveAt)> Owners = new();
    public bool ActiveFor(int i, int agent, float t)
        => Owners[i].owner == agent ? t >= Owners[i].ownerActiveAt : t >= Items[i].activeAt;
    readonly Dictionary<long, List<int>> _grid = new();
    const float Cell = 24f;
    static long Key(int x, int y, int z) => ((long)(x + 100000) * 200003L + (y + 100000)) * 200003L + (z + 100000);
    public void Add(Vector3 c, Quaternion r, Vector3 half, float activeAt) => Add(c, r, half, activeAt, -1, activeAt);
    public void Add(Vector3 c, Quaternion r, Vector3 half, float activeAt, int owner, float ownerActiveAt)
    {
        int id = Items.Count;
        Items.Add((c, r, half, activeAt));
        Owners.Add((owner, ownerActiveAt));
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
    // Wall-clock cost of the shipped decision core (the sim's own dt never sees it; the game's frame does).
    public static long DecideTicks, DecideCalls, DecideBytes;
    // eval only (races run one at a time): every seat's decision time added up per frame - what one
    // frame of the game pays for all its AI together. Off in the parallel tuners.
    public static bool RecordFrames;
    public static readonly List<float> FrameMs = new();
    // eval only: the track planner's re-plans, the frames they land in, the frames two or more seats
    // re-planned in together, and the re-plans held a frame for another seat's (SkimRaceReplanGate).
    public static long TrackReplans, TrackReplanFrames, SharedReplanFrames, TrackWaits;
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

    sealed class Agent
    {
        public int Id;
        public SkimRaceDriver Driver;
        public System.Random Rng;
        public Vector3 Pos; public Quaternion Rot, Acc;
        public float Speed, Boost = 1f; public bool Boosting;
        public float SlowUntil = -1f;
        public int Anchor; public Vector3 Crystal; public int Collected;
        public int TeamTarget = -1; public Vector3 TeamTargetAt;   // team race: the crystal flown at, and where it was
        public readonly HashSet<int> Inside = new(), Hull = new(), ObsInside = new(), ObsHull = new();
        public int Hint = -1, TargetHint = -1, HullHits;
        public float SpeedSum, BoostSum; public int Frames, FarFrames;
        public readonly List<(float init, float start)> Mods = new();
        public float RailTravel;
        public bool Done; public float DoneAt = 999f;
        public Dictionary<string, int> Causes = new();
        public Dictionary<string, float> Lost = new();
        public readonly Queue<string> GuardLog = new();
        public readonly List<float> TrackErrors = new();
        public readonly List<float> LineClear = new();
        public float LastPickupAt = -10f;
        public readonly Dictionary<string, float> PhaseTime = new(), PhaseBoostT = new(), PhaseSpeedT = new(), PhaseSkimT = new();
        public readonly List<(string, float, float, float, float, float, float)> TrackHitLog = new();
        public string Phase(SkimRaceDriver d, float now) =>
            now - LastPickupAt < 1f ? "post-pickup"
            : d.CurrentMode == SkimRaceDriver.Mode.Recovering ? "recovery"
            : d.Crossing ? "crossing"
            : d.LastDiagnostics.CrystalPull ? "pull"
            : "line";
        public void Note(string cause, float boostBefore) { if (boostBefore < 1.3f) return; Causes[cause] = Causes.GetValueOrDefault(cause) + 1; Lost[cause] = Lost.GetValueOrDefault(cause) + (boostBefore - 1f); }
    }

    static Dictionary<string, float> Sum(IEnumerable<Dictionary<string, float>> ds)
    {
        var r = new Dictionary<string, float>();
        foreach (var d in ds) foreach (var kv in d) r[kv.Key] = r.GetValueOrDefault(kv.Key) + kv.Value;
        return r;
    }

    /// <summary>The seat's difficulty handicap (null = Hard), seeded per race and seat like the game
    /// seeds per bind: every race errs differently, and a seed always errs the same way.</summary>
    static SkimRaceHandicap HandicapFor(Physics ph, int seed, int seat)
    {
        var level = new SkimRaceHandicapLevel(ph.HcReaction, ph.HcMistake);
        return level.IsNone ? null : new SkimRaceHandicap(level, unchecked(seed * 7919 + seat * 104729 + 17));
    }

    public static RaceResult Run(TrackDef def, TrackPrisms prisms, SkimRaceCourse course, SkimRaceAIConfigSO cfg,
        Physics ph, int seed, float limit, bool trace = false)
    {
        var rng = new System.Random(seed);
        int required = def.Waypoints.Count * def.Laps;
        var obs = new Obstacles();
        var obsNear = new List<int>();
        var near = new List<int>();
        var agents = new List<Agent>();
        // One per race, as SkimRacePilot shares one across the game's seats; marked once a frame below.
        var replanGate = new SkimRaceReplanGate();
        int frame = 0;
        for (int k = 0; k < Math.Max(1, ph.Seats); k++)
        {
            var ag = new Agent { Id = k, Driver = new SkimRaceDriver(cfg) { Lane = k, Handicap = HandicapFor(ph, seed, k), ReplanGate = replanGate }, Rng = new System.Random(seed * 31 + k * 977) };
            ag.Driver.Reset();
            ag.Pos = ph.SpawnPos + Vector3.up * (10f * k);
            ag.Rot = ag.Acc = Quaternion.LookRotation(ph.SpawnFwd, Vector3.up);
            ag.Crystal = def.Anchors[0] + OnUnitSphere(ag.Rng) * ph.Jitter;
            agents.Add(ag);
        }
        float t = 0f, maxT = limit + ph.ExtraTime;

        // Team race: the team's crystals (one per seat, all starting round anchor 0 like the game's
        // first batch) and the team's summed count. Unused when Team=0.
        var teamCrystals = new List<Vector3>();
        var teamAnchor = new List<int>();
        int teamCollected = 0;
        var teamRng = new System.Random(seed * 131 + 7);
        var candidates = new List<SkimRaceTargetTracker.Candidate>();
        if (ph.Team != 0)
            for (int k = 0; k < agents.Count; k++)
            {
                teamCrystals.Add(def.Anchors[0] + OnUnitSphere(teamRng) * ph.Jitter);
                teamAnchor.Add(0);
            }

        // TeamRule 1: the game's team plan - once a frame, every seat a different crystal
        // (SkimRaceTeamAssignment, the shipped code), the last plan kept until another is SplitHyst cheaper.
        var teamPilots = new List<Vector3>();
        var teamPrevious = new List<int>();
        var teamResult = new int[Math.Max(1, ph.Seats)];
        void PlanTeam()
        {
            teamPilots.Clear();
            teamPrevious.Clear();
            foreach (var a in agents)
            {
                teamPilots.Add(a.Pos);
                teamPrevious.Add(a.TeamTarget);
            }
            SkimRaceTeamAssignment.Assign(teamPilots, teamCrystals, teamPrevious, teamResult, ph.SplitHyst);
            for (int k = 0; k < agents.Count; k++) agents[k].TeamTarget = teamResult[k];
        }

        float Mult(Agent ag, float now)
        {
            float m = 1f;
            for (int i = ag.Mods.Count - 1; i >= 0; i--)
            {
                float e = now - ag.Mods[i].start;
                if (e >= ph.HullSlowSeconds) { ag.Mods.RemoveAt(i); continue; }
                m *= Mathf.Lerp(ag.Mods[i].init, 1f, e / ph.HullSlowSeconds);
            }
            return m;
        }
        // SquirrelVesselExplosionByCrystalEffect -> AOEShieldedRingSpawner: 8 prisms, radius 8.2,
        // 8 u ahead of the hull that took the crystal; colliders live from frame 0 for everyone.
        void AddPickupRing(Agent ag, float now)
        {
            Vector3 rc = ag.Pos + (ag.Rot * Vector3.forward) * 8f;
            for (int k = 0; k < 8; k++)
            {
                float ang = k * Mathf.PI * 2f / 8f;
                Vector3 radial = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                obs.Add(rc + ag.Rot * (radial * 8.2f), ag.Rot * Quaternion.LookRotation(Vector3.forward, radial),
                    new Vector3(0.9f, 0.9f, 3.75f), now);
            }
        }
        void Slow(Agent ag, float volume, float now)
        {
            if (ph.StackedSlow != 0) ag.Mods.Add((1f - Math.Min(volume * 0.1f, 0.5f), now));
            else ag.SlowUntil = now + ph.HullSlowSeconds;
        }

        while (t < maxT && agents.Exists(x => !x.Done))
        {
            float dt = ph.DtJitter > 0f ? ph.Dt * (1f + ph.DtJitter * (float)(rng.NextDouble() * 2.0 - 1.0)) : ph.Dt;
            long frameTicks = 0;
            int frameDecides = 0;
            int frameReplans = 0;
            replanGate.BeginFrame(frame++);
            if (ph.Team != 0 && ph.TeamRule == 1 && agents.Count >= 2) PlanTeam();
            foreach (var ag in agents)
            {
                if (ag.Done) continue;
                var driver = ag.Driver;
                if (ph.Team != 0)
                {
                    int pick = ag.TeamTarget;
                    if (ph.TeamRule == 0 || agents.Count < 2 || pick < 0)
                    {
                        // The nearest crystal (SkimRaceTargetTracker.Select): the rule before team play, and
                        // what a lone AI - or one the plan has nothing for - still flies.
                        candidates.Clear();
                        foreach (var c in teamCrystals)
                            candidates.Add(new SkimRaceTargetTracker.Candidate { Alive = true, Domain = CosmicShore.Data.Domains.Jade, Position = c });
                        pick = SkimRaceTargetTracker.SelectIndex(candidates, CosmicShore.Data.Domains.Jade, ag.Pos, ag.TeamTarget);
                    }
                    // A new crystal, or this one moved (a teammate took it): the target hint searches afresh.
                    if (pick != ag.TeamTarget || (teamCrystals[pick] - ag.TeamTargetAt).sqrMagnitude > 1f) ag.TargetHint = -1;
                    ag.TeamTarget = pick;
                    ag.TeamTargetAt = ag.Crystal = teamCrystals[pick];
                }

                // ── observe ──
                Vector3 fwd = ag.Rot * Vector3.forward, up = ag.Rot * Vector3.up, right = ag.Rot * Vector3.right;
                var o = new SkimRaceObservation
                {
                    Position = ag.Pos, Forward = fwd, Right = right, Up = up,
                    CommandedForward = ag.Acc * Vector3.forward,
                    Rotation = ag.Rot, CommandedRotation = ag.Acc,
                    Speed = ag.Speed * (ph.StackedSlow != 0 ? Mult(ag, t) : 1f), Velocity = fwd * (ag.Speed * (ph.StackedSlow != 0 ? Mult(ag, t) : 1f)),
                    BoostMultiplier = ag.Boosting ? ag.Boost : 1f, MaxBoost = ph.MaxBoost,
                    TurnRateDegrees = ph.TurnRate, FollowRate = ph.Follow, ThrottleScaler = ph.ThrottleScaler,
                    RaceTime = t, Collected = ph.Team != 0 ? teamCollected : ag.Collected,
                    Remaining = required - (ph.Team != 0 ? teamCollected : ag.Collected),
                    HasTarget = true, TargetPosition = ag.Crystal, ToTarget = ag.Crystal - ag.Pos,
                };
                o.TargetDistance = o.ToTarget.magnitude;
                o.TargetRadius = ph.CaptureReach - 1.5f;
                o.TargetAlignment = Vector3.Dot(fwd, o.ToTarget.normalized);
                o.HasCourse = true;
                o.CourseLength = course.Length;
                o.CourseProgress = course.Project(ag.Pos, ref ag.Hint, out _, out o.CourseDistance);
                course.Sample(o.CourseProgress, out o.CourseTangent, out _);
                // The target's own hint (SkimRacePilot._targetHint): full search on a new crystal.
                if (ph.TargetHintFix == 0) ag.TargetHint = ag.Hint;   // A/B: the old projection from the vessel's hint
                float sc = course.Project(ag.Crystal, ref ag.TargetHint, out _, out _);
                o.TargetAheadOnCourse = course.Ahead(o.CourseProgress, sc);
                o.Sanitize();

                // Perception: laid mass near the path (SkimRacePilot reads PrismSpatialIndex), excluding
                // what the game would not let this hull hit yet.
                driver.Obstacles.Clear();
                if (cfg.MassGuardSeconds > 0f)
                {
                    Vector3 qc = ag.Pos + fwd * (ag.Speed * cfg.MassGuardSeconds * 0.5f);
                    float qr = ag.Speed * cfg.MassGuardSeconds * 0.5f + 20f;
                    obs.QueryRadius(qc, qr, obsNear);
                    foreach (var i in obsNear)
                    {
                        if (!obs.ActiveFor(i, ag.Id, t)) continue;
                        var it = obs.Items[i];
                        if ((it.c - qc).sqrMagnitude > qr * qr) continue;
                        driver.Obstacles.Add(new SkimRaceObstacle { Center = it.c, Rotation = it.r, Half = it.half });
                    }
                }
                long b0 = GC.GetAllocatedBytesForCurrentThread();
                int replans0 = driver.TrackReplans;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var a = driver.Decide(o, course, t, dt);
                long spent = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                frameReplans += driver.TrackReplans - replans0;
                DecideTicks += spent; DecideCalls++;
                frameTicks += spent; frameDecides++;
                DecideBytes += GC.GetAllocatedBytesForCurrentThread() - b0;
                if (driver.LastTrackError >= 0f && ag.Frames % 4 == 0) ag.TrackErrors.Add(driver.LastTrackError);
                if (ph.LineDiag != 0 && ag.Frames % 20 == 0)
                {
                    float lc = driver.LineMinClearance(course, o.CourseProgress, 300f);
                    ag.LineClear.Add(lc);
                }
                if (trace)
                {
                    ag.GuardLog.Enqueue(string.Format(CultureInfo.InvariantCulture, "{0:F2}:{1:F1}/{2:F1}", t,
                        Math.Min(driver.LastGuardNominal, 99f), Math.Min(driver.LastGuardChosen, 99f)));
                    while (ag.GuardLog.Count > 14) ag.GuardLog.Dequeue();
                }

                // ── VesselTransformer.Update order: DecayBoost, RotateShip, MoveShip ──
                if (ag.Boost > 1f) ag.Boost -= ph.BoostDecay * dt; else ag.Boost = Math.Min(1f, ag.Boost + ph.BoostDecay * dt);
                ag.Acc = Quaternion.AngleAxis(a.Roll * ph.RollRate * dt, fwd) * ag.Acc;
                ag.Acc = Quaternion.AngleAxis(a.Yaw * ph.TurnRate * dt, up) * ag.Acc;
                ag.Acc = Quaternion.AngleAxis(a.Pitch * ph.TurnRate * dt, right) * ag.Acc;
                ag.Rot = Quaternion.Slerp(ag.Rot, ag.Acc, ph.Follow * dt);
                float thr = a.Throttle * (t < ag.SlowUntil ? ph.HullSlow : 1f);
                float mult = ph.StackedSlow != 0 ? Mult(ag, t) : 1f;
                float target = thr * ph.ThrottleScaler * (ag.Boosting ? ag.Boost : 1f);
                ag.Speed = Mathf.Lerp(ag.Speed, target, ph.Follow * dt);
                fwd = ag.Rot * Vector3.forward;
                ag.Pos += fwd * (ag.Speed * mult) * dt;
                ag.SpeedSum += ag.Speed; ag.Frames++;
                if (o.CourseDistance > 20f) ag.FarFrames++;
                ag.BoostSum += ag.Boosting ? ag.Boost : 1f;
                float tn = t + dt;
                {
                    string ph0 = ag.Phase(driver, t);
                    float bst = ag.Boosting ? ag.Boost : 1f;
                    ag.PhaseTime[ph0] = ag.PhaseTime.GetValueOrDefault(ph0) + dt;
                    ag.PhaseBoostT[ph0] = ag.PhaseBoostT.GetValueOrDefault(ph0) + bst * dt;
                    ag.PhaseSpeedT[ph0] = ag.PhaseSpeedT.GetValueOrDefault(ph0) + ag.Speed * mult * dt;
                    if (ag.Inside.Count > 0) ag.PhaseSkimT[ph0] = ag.PhaseSkimT.GetValueOrDefault(ph0) + dt;
                }

                // ── lay own trail rails ──
                if (ph.TrailRails != 0 && tn > 2f && ag.Speed > 3f)
                {
                    ag.RailTravel += ag.Speed * dt;
                    while (ag.RailTravel >= ph.RailSpacing)
                    {
                        ag.RailTravel -= ph.RailSpacing;
                        Vector3 rgt = ag.Rot * Vector3.right;
                        var half = new Vector3(0.415f, 0.415f, 3.04f);
                        obs.Add(ag.Pos + rgt * ph.RailOffset, ag.Rot, half, tn + ph.ColliderDelay, ag.Id, tn + Math.Max(ph.TrailGrace, ph.ColliderDelay));
                        obs.Add(ag.Pos - rgt * ph.RailOffset, ag.Rot, half, tn + ph.ColliderDelay, ag.Id, tn + Math.Max(ph.TrailGrace, ph.ColliderDelay));
                    }
                }
                // ── contacts with laid mass ──
                obs.Query(ag.Pos, obsNear);
                {
                    var onow = new HashSet<int>(); var hnowO = new HashSet<int>();
                    foreach (var i in obsNear)
                    {
                        if (!obs.ActiveFor(i, ag.Id, tn)) continue;
                        float d = obs.Distance(i, ag.Pos);
                        if (d <= ph.SkimReach) onow.Add(i);
                        float dh = d;
                        if (ph.HullBox != 0 && d <= ph.HullReach + 3f)
                            for (int bx = -1; bx <= 1; bx++)
                            for (int bz = -1; bz <= 1; bz++)
                                dh = Math.Min(dh, obs.Distance(i, ag.Pos + ag.Rot * new Vector3(bx * ph.HullHalfX, 0f, bz * ph.HullHalfZ)));
                        if (ph.MassHits != 0 && dh <= ph.HullReach) hnowO.Add(i);
                    }
                    foreach (var i in onow)
                        if (ag.ObsInside.Add(i)) { ag.Boosting = true; ag.Boost = Mathf.Clamp(ag.Boost + ph.SkimAdd, 1f, ph.MaxBoost); }
                    ag.ObsInside.IntersectWith(onow);
                    foreach (var i in hnowO)
                        if (ag.ObsHull.Add(i))
                        {
                            int own = obs.Owners[i].owner;
                            ag.Note(own < 0 ? "pickup-ring" : own == ag.Id ? "own-rail" : "other-rail", ag.Boost);
                            ag.HullHits++; var oh = obs.Items[i].half; Slow(ag, 8f * oh.x * oh.y * oh.z, tn); ag.Boost = 1f;
                            if (trace) Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "   OBSHIT a{0} t={1:F2} spd={2:F0} owner={3} pull={4}", ag.Id, tn, ag.Speed, obs.Owners[i].owner, driver.LastDiagnostics.CrystalPull));
                        }
                    ag.ObsHull.IntersectWith(hnowO);
                }

                // ── skimmer & hull contacts (enter events), against each track prism's shell ──
                prisms.Query(ag.Pos, 40f, near);
                var now = new HashSet<int>();
                var hnow = new HashSet<int>();
                foreach (var i in near)
                {
                    float d = prisms.ShellDistance(i, ag.Pos);
                    if (d <= ph.SkimReach) now.Add(i);
                    float dh = d;
                    if (ph.HullBox != 0 && d <= ph.HullReach + 3f)
                        for (int bx = -1; bx <= 1; bx++)
                        for (int bz = -1; bz <= 1; bz++)
                            dh = Math.Min(dh, prisms.ShellDistance(i, ag.Pos + ag.Rot * new Vector3(bx * ph.HullHalfX, 0f, bz * ph.HullHalfZ)));
                    if (ph.TrackHits != 0 && dh <= ph.HullReach) hnow.Add(i);
                }
                foreach (var i in now)
                    if (ag.Inside.Add(i)) { ag.Boosting = true; ag.Boost = Mathf.Clamp(ag.Boost + ph.SkimAdd, 1f, ph.MaxBoost); }
                ag.Inside.IntersectWith(now);
                foreach (var i in hnow)
                    if (ag.Hull.Add(i))
                    {
                        ag.Note(driver.Crossing ? "track-crossing" : driver.LastDiagnostics.CrystalPull ? "track-pull" : "track-line", ag.Boost);
                        {
                            Vector3 lph = Quaternion.Inverse(prisms.Rotations[i]) * (ag.Pos - prisms.Points[i]);
                            ag.TrackHitLog.Add((ag.Phase(driver, tn), lph.x, lph.y, lph.z, driver.LastDiagnostics.HeadingErrorDegrees, ag.Speed * mult, ag.Boost));
                        }
                        ag.HullHits++; Slow(ag, 30f, tn); ag.Boost = 1f; // VesselResetBoostPrismEffect + SquirrelVesselChangeSpeedByPrism
                        if (trace)
                        {
                            Vector3 lp = Quaternion.Inverse(prisms.Rotations[i]) * (ag.Pos - prisms.Points[i]);
                            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                "   HIT a{9} t={0:F2} prism={1} local=({2:F1},{3:F1},{4:F1}) spd={5:F0} mode={6} pull={7} dist={8:F0} cross={10}",
                                tn, i, lp.x, lp.y, lp.z, ag.Speed, driver.CurrentMode, driver.LastDiagnostics.CrystalPull, (ag.Crystal - ag.Pos).magnitude, ag.Id, driver.Crossing));
                            Console.WriteLine("      guard nominal/chosen clearance: " + string.Join(" ", ag.GuardLog));
                        }
                    }
                ag.Hull.IntersectWith(hnow);

                // ── crystal ──
                if (ph.Team != 0)
                {
                    // Any of the team's crystals in reach is taken (TeamCrystalImpactor admits any crystal of
                    // the vessel's domain); it moves to ITS next anchor; the team's sum finishes everyone.
                    for (int j = 0; j < teamCrystals.Count && !ag.Done; j++)
                    {
                        if ((teamCrystals[j] - ag.Pos).sqrMagnitude > ph.CaptureReach * ph.CaptureReach) continue;
                        teamCollected++;
                        ag.Collected++;
                        ag.LastPickupAt = tn;
                        if (ph.RingGeometry != 0) AddPickupRing(ag, tn);
                        else
                        {
                            ag.Boosting = true;
                            ag.Boost = Mathf.Clamp(ag.Boost + ph.SkimAdd * ph.PickupRingPrisms, 1f, ph.MaxBoost);
                        }
                        if (teamCollected >= required)
                        {
                            foreach (var x in agents) if (!x.Done) { x.Done = true; x.DoneAt = tn; }
                            break;
                        }
                        teamAnchor[j] = (teamAnchor[j] + 1) % def.Anchors.Count;
                        teamCrystals[j] = def.Anchors[teamAnchor[j]] + OnUnitSphere(teamRng) * ph.Jitter;
                    }
                }
                else if ((ag.Crystal - ag.Pos).sqrMagnitude <= ph.CaptureReach * ph.CaptureReach)
                {
                    ag.Collected++;
                    ag.LastPickupAt = tn;
                    if (ph.RingGeometry != 0) AddPickupRing(ag, tn);
                    else
                    {
                        ag.Boosting = true;
                        ag.Boost = Mathf.Clamp(ag.Boost + ph.SkimAdd * ph.PickupRingPrisms, 1f, ph.MaxBoost);
                    }
                    if (ag.Collected >= required) { ag.Done = true; ag.DoneAt = tn; }
                    else
                    {
                        ag.Anchor = (ag.Anchor + 1) % def.Anchors.Count;
                        ag.Crystal = def.Anchors[ag.Anchor] + OnUnitSphere(ag.Rng) * ph.Jitter;
                        ag.TargetHint = -1;
                    }
                }

                if (trace && ag.Frames % 6 == 0)
                {
                    Vector3 cpt = course.Sample(o.CourseProgress, out Vector3 tt, out Vector3 nn);
                    Vector3 ll = Vector3.Cross(nn, tt).normalized; nn = Vector3.Cross(tt, ll).normalized;
                    Vector3 rr = ag.Pos - cpt;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0:F2} a{14} pos=({1:F0},{2:F0},{3:F0}) spd={4:F0} boost={5:F2} dist={6:F0} col={7} mode={8} err={9:F0} lat={10:F1} h={11:F1} skim={12} pull={13}",
                        tn, ag.Pos.x, ag.Pos.y, ag.Pos.z, ag.Speed, ag.Boost, o.TargetDistance, ag.Collected, driver.CurrentMode,
                        driver.LastDiagnostics.HeadingErrorDegrees, Vector3.Dot(rr, ll), Vector3.Dot(rr, nn), ag.Inside.Count,
                        driver.LastDiagnostics.CrystalPull ? 1 : 0, ag.Id));
                }
            }
            if (RecordFrames && frameDecides > 0) FrameMs.Add((float)(1000.0 * frameTicks / System.Diagnostics.Stopwatch.Frequency));
            if (RecordFrames && frameReplans > 0)
            {
                TrackReplans += frameReplans;
                TrackReplanFrames++;
                if (frameReplans >= 2) SharedReplanFrames++;
            }
            t += dt;
        }
        if (RecordFrames) foreach (var ag in agents) TrackWaits += ag.Driver.TrackWaits;

        // The STRICT reading of "each AI completes in 70 s": the race result is the SLOWEST seat.
        var worst = agents[0];
        foreach (var ag in agents) if (!ag.Done || ag.DoneAt > worst.DoneAt || (!ag.Done && worst.Done)) worst = ag;
        bool allDone = agents.TrueForAll(x => x.Done);
        int frames = Math.Max(1, worst.Frames);
        return new RaceResult
        {
            Finished = allDone, Time = allDone ? agents.Max(x => x.DoneAt) : t,
            Collected = ph.Team != 0 ? teamCollected : agents.Min(x => x.Collected), Required = required,
            Recoveries = agents.Sum(x => x.Driver.Recoveries), HullHits = agents.Sum(x => x.HullHits),
            Mistakes = agents.Sum(x => x.Driver.Handicap != null ? x.Driver.Handicap.Mistakes : 0),
            MeanSpeed = worst.SpeedSum / frames, FarFrac = worst.FarFrames / (float)frames, MeanBoost = worst.BoostSum / frames,
            AgentTimes = agents.Select(x => x.Done ? x.DoneAt : 999f).ToArray(),
            PhaseTime = Sum(agents.Select(x => x.PhaseTime)), PhaseBoostT = Sum(agents.Select(x => x.PhaseBoostT)),
            PhaseSpeedT = Sum(agents.Select(x => x.PhaseSpeedT)), PhaseSkimT = Sum(agents.Select(x => x.PhaseSkimT)),
            TrackHitLog = agents.SelectMany(x => x.TrackHitLog).ToList(),
            TrackErrors = agents.SelectMany(x => x.TrackErrors).ToList(),
            LineClear = agents.SelectMany(x => x.LineClear).ToList(),
            ResetCauses = agents.SelectMany(x => x.Causes).GroupBy(k => k.Key).ToDictionary(g => g.Key, g => g.Sum(k => k.Value)),
            ResetBoostLost = agents.SelectMany(x => x.Lost).GroupBy(k => k.Key).ToDictionary(g => g.Key, g => g.Sum(k => k.Value)),
        };
    }
}

static class Program
{
    static Vector3 OnUnit(System.Random r)
    {
        while (true)
        {
            var v = new Vector3((float)(r.NextDouble() * 2 - 1), (float)(r.NextDouble() * 2 - 1), (float)(r.NextDouble() * 2 - 1));
            float m = v.sqrMagnitude;
            if (m > 1e-4f && m <= 1f) return v / (float)Math.Sqrt(m);
        }
    }
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
        "TrackGuardMargin", "DirectBoost", "DirectBoostHysteresis", "DirectViaClearance", "DirectViaLift", "DirectViaLateral",
        "TerminalChordClearance", "ChordClearance",
        "CrossingLeadSeconds", "CrossingLookaheadScale", "CrossingThrottle", "CrossingSlowDistance",
        "LaneHeightStep",
    };
    // set=mpc: the predictive controllers' weights (pin UseMpc=1 / UseTrackMpc=1 on the command line).
    static readonly string[] MpcTunables =
    {
        "MpcHullMargin", "MpcStrikeCost", "MpcBoostValue", "MpcHorizon", "MpcSegment",
        "TrackMpcHorizon", "TrackMpcLead", "TrackMpcCaptureReward", "TrackMpcNominalBias", "TrackMpcStrikeCost",
        "LookaheadSeconds", "StickGainPerDegree", "LeadGain", "MinThrottle",
    };
    // set=winner: the pursuit set plus the tracking-MPC weights (pin UseTrackMpc=1 on the command line).
    // Excludes the direct-flight switch and its sub-parameters and CrystalDirectDistance: their ranges have
    // floors above their OFF values (DirectBoost >= 2.5 turns direct flight ON), so tuning them would
    // silently re-enable what the command line pinned off.
    static readonly string[] WinnerTunables = PursuitTunables
        .Where(k => !k.StartsWith("Direct") && k != "CrystalDirectDistance").Concat(new[]
        { "TrackMpcHorizon", "TrackMpcLead", "TrackMpcCaptureReward", "TrackMpcNominalBias", "TrackMpcStrikeCost", "MpcHullMargin",
          "LevelApproachSeconds", "LevelSegment", "LevelExitSeconds", "LevelStrikeMargin", "LevelStrikeCost", "LevelClearanceWeight", "CaptureMargin", "HullGuardSeconds", "HullMargin", "PickupClearDistance" }).ToArray();
    static readonly Dictionary<string, (float lo, float hi)> Ranges = new()
    {
        ["TrackGuardMargin"] = (0.2f, 3f),
        ["TerminalChordClearance"] = (0.5f, 5f), ["ChordClearance"] = (0f, 6f),
        ["MpcHullMargin"] = (0.2f, 3f), ["MpcStrikeCost"] = (0f, 40f), ["MpcBoostValue"] = (0f, 5f),
        ["MpcHorizon"] = (0.4f, 2.5f), ["MpcSegment"] = (0.1f, 0.8f),
        ["TrackMpcHorizon"] = (0.3f, 2f), ["TrackMpcLead"] = (0f, 60f), ["TrackMpcCaptureReward"] = (0f, 600f),
        ["TrackMpcNominalBias"] = (0f, 0.9f), ["TrackMpcStrikeCost"] = (0f, 5000f),
        ["CrossingLeadSeconds"] = (0f, 1.5f), ["CrossingLookaheadScale"] = (0.2f, 1f),
        ["CrossingThrottle"] = (0.3f, 1f), ["CrossingSlowDistance"] = (30f, 400f),
        ["LaneHeightStep"] = (0.8f, 3f),
        ["LevelApproachSeconds"] = (0.8f, 3f), ["LevelSegment"] = (0.1f, 0.6f), ["LevelExitSeconds"] = (0f, 1.5f),
        ["LevelStrikeMargin"] = (0.3f, 1.5f), ["LevelStrikeCost"] = (1f, 15f), ["LevelClearanceWeight"] = (0f, 0.3f),
        ["CaptureMargin"] = (0f, 8f), ["PickupClearDistance"] = (0f, 30f),
        ["DirectBoost"] = (2.5f, 5f), ["DirectBoostHysteresis"] = (0.2f, 2.5f), ["DirectViaClearance"] = (2f, 10f),
        ["DirectViaLift"] = (6f, 40f), ["DirectViaLateral"] = (18f, 50f),
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

    static float Med(IEnumerable<float> xs) { var l = xs.OrderBy(x => x).ToList(); return l.Count == 0 ? 0f : l[l.Count / 2]; }

    static float GetNum(object target, string key)
    {
        var f = target.GetType().GetField(key, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException("unknown field " + key);
        object v = f.GetValue(target);
        return v switch { float x => x, int i => i, bool b => b ? 1f : 0f, _ => throw new ArgumentException($"{key} is not numeric") };
    }

    static void SetNum(object target, string key, float value)
    {
        var f = target.GetType().GetField(key, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException("unknown field " + key);
        if (f.FieldType == typeof(float)) f.SetValue(target, value);
        else if (f.FieldType == typeof(int)) f.SetValue(target, (int)Math.Round(value));
        else if (f.FieldType == typeof(bool)) f.SetValue(target, value >= 0.5f);
        else throw new ArgumentException($"{key} is not numeric");
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
        TrackDef def, TrackPrisms prisms, SkimRaceCourse course, SkimRaceAIConfigSO cfg, Physics ph, int seeds, float limit, int seedBase = 1000, float overWeight = 0f, bool winnerScore = false)
    {
        var runs = new List<RaceResult>();
        for (int s = 0; s < seeds; s++) runs.Add(Race.Run(def, prisms, course, cfg, ph, seedBase + s, limit));
        var fin = runs.Where(r => r.Finished).Select(r => r.Time).OrderBy(x => x).ToList();
        float median = fin.Count > 0 ? fin[fin.Count / 2] : 999f;
        float worst = runs.All(r => r.Finished) ? fin.Max() : 999f;
        float mean = fin.Count > 0 ? fin.Average() : 999f;
        if (winnerScore)
        {
            // score=winner: the race is judged by its FIRST finisher (the editor's and the scoreboard's
            // reading); no finisher = 300. over=w adds w x 50 x the fraction of winners above the limit.
            var winners = runs.Select(r => r.AgentTimes.Length > 0 ? r.AgentTimes.Min() : 999f).ToList();
            float ws = winners.Sum(x => x >= 999f ? 300f : x) / seeds;
            if (overWeight > 0f) ws += overWeight * 50f * winners.Count(x => x > limit) / (float)winners.Count;
            return (ws, fin.Count, median, worst, mean, runs);
        }
        // Score: every unfinished race is a disaster; then the WORST time, then the mean.
        float score = runs.Sum(r => r.Finished ? r.Time : 300f + (r.Required - r.Collected) * 10f) / seeds
                      + 0.5f * (runs.All(r => r.Finished) ? fin.Max() : 300f);
        // over=w: w x 50 x the fraction of SEAT times above the limit (every seat must be <= limit).
        if (overWeight > 0f)
        {
            var seatTimes = runs.SelectMany(r => r.AgentTimes).ToList();
            if (seatTimes.Count > 0) score += overWeight * 50f * seatTimes.Count(x => x > limit) / (float)seatTimes.Count;
        }
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
        string.Join(" ", Tunables.Select(k => k + "=" + GetNum(cfg, k).ToString("0.###", CultureInfo.InvariantCulture)));

    // The benchmark limit per intensity: a COPY of SkimRaceRaceRecorder.DefaultLimitSeconds (keep the
    // two in step). I2 was re-baselined to 80 s by product decision; `limit=` overrides.
    static float DefaultLimit(int intensity) => intensity == 2 ? 80f : 70f;

    /// <summary>
    /// The race flown ALONG the ribbon at the Squirrel's top speed (300 u/s): each anchor to the next,
    /// measured forward along the course, every lap. A straight line between anchors is not the race -
    /// on a winding track it is half the distance the pilot actually flies. Dividing by this puts a
    /// short track and a long one on one scale.
    /// </summary>
    static float IdealSeconds(TrackDef d, SkimRaceCourse course)
    {
        float loop = 0f;
        for (int i = 0; i < d.Anchors.Count; i++)
        {
            int h0 = -1, h1 = -1;
            float s0 = course.Project(d.Anchors[i], ref h0, out _, out _);
            float s1 = course.Project(d.Anchors[(i + 1) % d.Anchors.Count], ref h1, out _, out _);
            loop += course.Ahead(s0, s1);
        }
        return Math.Max(1f, loop * Math.Max(1, d.Laps) / 300f);
    }

    /// <summary>
    /// <c>tuneall &lt;i,j,...&gt; &lt;seeds&gt; &lt;iters&gt; [sigma=s] [final=n] [Field=v ...] [ph.Field=v ...]</c>: ONE policy
    /// tuned on several tracks at once. This is the GENERAL policy - the one every intensity without its
    /// own tuning file flies (<c>SkimRaceAIConfigSO.LoadFor</c> falls back to it) - so it is judged on
    /// finishing EVERY track, not on being the fastest on one. Same cross-entropy loop as <c>tune</c>.
    ///
    /// <para>Scoring, per track, in units of that track's ideal time (<see cref="IdealSeconds"/>): a
    /// finished race is its time; a race that does not finish is the time it was cut at plus twice the
    /// fraction of crystals it missed, so a DNF always scores worse than any finish. Each track adds its
    /// mean plus half its worst race, and the tracks are averaged. Every track is raced to a generous
    /// limit (three times its ideal time) because finishing is the thing being tuned for.</para>
    ///
    /// <para><c>set=winner</c> searches the tracking-MPC set instead of the pursuit set (as in <c>tune</c>).
    /// <c>final=0</c> skips the fresh-seed check (<c>skimrace_retune.py</c> races its own, against the general policy).
    /// <c>only=stated</c> narrows the search to the fields the command line states AND whose stated value
    /// lies inside the search range: a value outside it (0 for a control whose range starts above 0) is a
    /// control the policy keeps OFF, and searching it would switch it on. <c>Tools/Build/skimrace_retune.py</c>
    /// uses it so a retune re-fits the numbers a policy already uses and never changes which controls it uses.</para>
    /// </summary>
    static int TuneAll(Dictionary<int, TrackDef> tracks, string[] args)
    {
        var ints = args[2].Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        int seeds = int.Parse(args[3]);
        int iters = int.Parse(args[4]);
        float sigmaScale = 0.25f;
        int finalSeeds = 20;
        bool onlyStated = false;
        Tunables = PursuitTunables;
        foreach (var a in args.Skip(5))
        {
            if (a.StartsWith("sigma=")) sigmaScale = float.Parse(a.Substring(6), CultureInfo.InvariantCulture);
            if (a.StartsWith("final=")) finalSeeds = int.Parse(a.Substring(6), CultureInfo.InvariantCulture);
            if (a == "set=winner") Tunables = WinnerTunables;
            if (a == "only=stated") onlyStated = true;
        }
        var policyArgs = args.Skip(5).Where(a => !a.StartsWith("sigma=") && !a.StartsWith("final=") && !a.StartsWith("set=") && !a.StartsWith("only=")).ToArray();
        var (baseCfg, ph) = Parse(policyArgs);
        if (onlyStated)
        {
            var stated = new HashSet<string>(policyArgs.Where(a => a.Contains('=') && !a.StartsWith("ph.")).Select(a => a.Substring(0, a.IndexOf('='))));
            Tunables = Tunables.Where(k => stated.Contains(k) && GetNum(baseCfg, k) >= Ranges[k].lo && GetNum(baseCfg, k) <= Ranges[k].hi).ToArray();
            Console.WriteLine($"  tuning {Tunables.Length} stated fields: {string.Join(" ", Tunables)}");
        }

        var sets = ints.Select(i =>
        {
            var d = tracks[i];
            var pr = new TrackPrisms(d);
            var co = new SkimRaceCourse(pr.Points, pr.Normals, pr.Rotations, pr.ShellHalf);
            float ideal = IdealSeconds(d, co);
            return (i, d, pr, co, ideal, lim: Math.Max(DefaultLimit(i), 3f * ideal));
        }).ToArray();
        foreach (var s in sets)
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  I{0}: ideal {1:F1} s, raced to {2:F0} s (+{3:F0} s cut)", s.i, s.ideal, s.lim, ph.ExtraTime));

        float Score(SkimRaceAIConfigSO cfg, int seedBase)
        {
            float total = 0f;
            foreach (var s in sets)
            {
                var e = Evaluate(s.d, s.pr, s.co, cfg, ph, seeds, s.lim, seedBase);
                float cut = s.lim + ph.ExtraTime, sum = 0f, worst = 0f;
                foreach (var r in e.runs)
                {
                    float v = r.Finished
                        ? r.Time / s.ideal
                        : cut / s.ideal + 2f * (r.Required - r.Collected) / Math.Max(1, r.Required);
                    sum += v;
                    worst = Math.Max(worst, v);
                }
                total += sum / Math.Max(1, e.runs.Count) + 0.5f * worst;
            }
            return total / sets.Length;
        }

        var rng = new System.Random(7);
        int dim = Tunables.Length;
        var mu = Tunables.Select(k => GetNum(baseCfg, k)).ToArray();
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
                for (int d = 0; d < dim; d++) SetNum(cfg, Tunables[d], xs[p][d]);
                scores[p] = Score(cfg, iterSeed);
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
            for (int d = 0; d < dim; d++) SetNum(bc, Tunables[d], best[d]);
            Console.WriteLine($"iter {it} gen-best={samples[0].score:F3} best={bestScore:F3} :: {Describe(bc)}");
        }

        var fc = CloneConfig(baseCfg);
        for (int d = 0; d < dim; d++) SetNum(fc, Tunables[d], best[d]);
        foreach (var s in finalSeeds > 0 ? sets : sets.Take(0)) // final=0: the caller runs its own check
        {
            var fe = Evaluate(s.d, s.pr, s.co, fc, ph, finalSeeds, s.lim, 99000);
            var w = fe.runs.Select(r => r.AgentTimes.Min()).OrderBy(x => x).ToList();
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "FINAL I{0} ({1} fresh seeds): finished {2}/{1}, median {3:F1} s, worst {4:F1} s, winner median {5:F1} s",
                s.i, finalSeeds, fe.fin, fe.median, fe.worst, w[w.Count / 2]));
        }
        Console.WriteLine("BEST " + Describe(fc));
        return 0;
    }

    /// <summary>
    /// <c>handicap &lt;intensity&gt; &lt;seeds&gt; &lt;targetSeconds&gt; [ph.HcReaction=r] [hi=h] [steps=n] [Field=v ...] [ph.Field=v ...]</c>:
    /// the lobby difficulty's mistake chance (<c>ph.HcMistake</c>) at which an AI seat's MEDIAN finish
    /// time is the target, at the given reaction time. Bisection over 0..hi (default 1; a smaller bound
    /// skips the slow races at a high chance - each misjudged crystal costs ~10 s) on the same seeds every step
    /// (common random numbers, so a step's answer differs from the last only by the chance), then a check
    /// on fresh seeds. Every seat counts - a match is judged by how long each AI takes, not by the
    /// fastest - and races run to 2.5x the target so a slow seat is measured, not cut.
    /// </summary>
    static int TuneHandicap(Dictionary<int, TrackDef> tracks, string[] args)
    {
        int intensity = int.Parse(args[2]);
        int seeds = int.Parse(args[3]);
        float target = float.Parse(args[4], CultureInfo.InvariantCulture);
        int steps = 8;
        float upper = 1f;
        foreach (var a in args.Skip(5))
        {
            if (a.StartsWith("steps=")) steps = int.Parse(a.Substring(6), CultureInfo.InvariantCulture);
            if (a.StartsWith("hi=")) upper = Mathf.Clamp01(float.Parse(a.Substring(3), CultureInfo.InvariantCulture));
        }
        var (cfg, ph) = Parse(args.Skip(5).Where(a => !a.StartsWith("steps=") && !a.StartsWith("hi=")));
        var def = tracks[intensity];
        var prisms = new TrackPrisms(def);
        var course = new SkimRaceCourse(prisms.Points, prisms.Normals, prisms.Rotations, prisms.ShellHalf);
        float lim = Math.Max(DefaultLimit(intensity), 2.5f * target);

        (float median, float p10, float p90, int fin, int total, float mistakes) Measure(float chance, int seedBase, int n)
        {
            var local = ph.Clone();
            local.HcMistake = chance;
            var runs = new RaceResult[n];
            System.Threading.Tasks.Parallel.For(0, n, s => runs[s] = Race.Run(def, prisms, course, cfg, local, seedBase + s, lim));
            var times = runs.SelectMany(r => r.AgentTimes).OrderBy(x => x).ToList();
            int fin = times.Count(x => x < 999f);
            return (times[times.Count / 2], times[times.Count / 10], times[times.Count * 9 / 10], fin, times.Count,
                runs.Sum(r => r.Mistakes) / (float)times.Count);
        }

        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "I{0}: target seat median {1:F1} s at reaction {2:0.###} s ({3} seeds x {4} seats, races to {5:F0} s)",
            intensity, target, ph.HcReaction, seeds, Math.Max(1, ph.Seats), lim));
        var at0 = Measure(0f, 1000, seeds);
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  chance 0.000 -> median {0:F1} s", at0.median));
        if (at0.median >= target)
        {
            Console.WriteLine("  the reaction time alone already reaches the target - lower ph.HcReaction");
            Console.WriteLine("BEST HcMistake=0");
            return 0;
        }
        float lo = 0f, hi = upper;
        var atHi = Measure(hi, 1000, seeds);
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  chance {0:0.000} -> median {1:F1} s", hi, atHi.median));
        if (atHi.median <= target)
        {
            Console.WriteLine(hi < 1f
                ? "  the upper bound stays under the target - raise hi="
                : "  even misjudging every crystal stays under the target - raise ph.HcReaction");
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "BEST HcMistake={0:0.000}", hi));
            return 0;
        }
        for (int i = 0; i < steps; i++)
        {
            float mid = 0.5f * (lo + hi);
            var m = Measure(mid, 1000, seeds);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  chance {0:0.000} -> median {1:F1} s (misjudged {2:F2}/seat/race)", mid, m.median, m.mistakes));
            if (m.median < target) lo = mid; else hi = mid;
        }
        float best = 0.5f * (lo + hi);
        var check = Measure(best, 99000, seeds);
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "FINAL fresh seeds: chance {0:0.000} -> seat median {1:F1} s, p10 {2:F1}, p90 {3:F1}, finished {4}/{5}, misjudged {6:F2}/seat/race",
            best, check.median, check.p10, check.p90, check.fin, check.total, check.mistakes));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "BEST HcMistake={0:0.000}", best));
        return 0;
    }

    static int Main(string[] args)
    {
        var tracks = Json.Load(args[0]);
        string mode = args[1];
        if (mode == "fingerprint")
        {
            // The C# SkimRaceTrackFingerprint of every track the scene file gave us - the value the game
            // computes in the scene. skimrace_retune.py checks it equals the Python script's before stamping.
            foreach (var kv in tracks.OrderBy(k => k.Key))
                Console.WriteLine($"I{kv.Key}: {SkimRaceTrackFingerprint.Compute(kv.Value.Waypoints, kv.Value.Spline, kv.Value.Laps, kv.Value.Anchors)}");
            return 0;
        }
        if (mode == "tuneall") return TuneAll(tracks, args);
        if (mode == "handicap") return TuneHandicap(tracks, args);
        int intensity = int.Parse(args[2]);
        var def = tracks[intensity];
        var prisms = new TrackPrisms(def);
        var course = new SkimRaceCourse(prisms.Points, prisms.Normals, prisms.Rotations, prisms.ShellHalf);
        float limit = DefaultLimit(intensity);
        foreach (var a in args)
            if (a.StartsWith("limit=")) limit = float.Parse(a.Substring(6), CultureInfo.InvariantCulture);
        if (mode == "eval" || mode == "tune") Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  benchmark limit: {0:F0} s (I{1})", limit, intensity));

        if (mode == "shell")
        {
            // Landmarks for SkimRaceShell on the track plate's shell (15 x 1.5 x 4.5): exact vs the retired bound.
            var plate = new Vector3(15f, 1.5f, 4.5f);
            foreach (var (label, p, expect) in new[]
            {
                ("above +3", new Vector3(0f, 4.5f, 0f), 3f),
                ("beside edge +11", new Vector3(26f, 0f, 0f), 11f),
                ("beyond tip (2,1,3)", new Vector3(17f, 2.5f, 7.5f), new Vector3(2, 1, 3).magnitude),
                ("inside", Vector3.zero, 0f),
            })
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0,-20} exact {1:F4} (expect {2:F4})  old bound {3:F4}",
                    label, SkimRaceShell.StellaDistance(p, plate), expect, SkimRaceShell.FacePlaneBound(p, plate)));
            return 0;
        }
        if (mode == "geo")
        {
            // How often does the STRAIGHT line between consecutive crystals pass through the
            // ribbon's contact shell? (hull centre clearance, capture spheres excluded)
            var rng = new System.Random(3);
            int N = def.Anchors.Count, blocked = 0, total = 0;
            var perGap = new int[N];
            for (int trial = 0; trial < 200; trial++)
            for (int k = 0; k < N; k++)
            {
                Vector3 a = def.Anchors[k] + OnUnit(rng) * 35f, b = def.Anchors[(k + 1) % N] + OnUnit(rng) * 35f;
                float L = Vector3.Distance(a, b), minC = float.MaxValue;
                for (float u = 24f; u < L - 24f; u += 2f)
                {
                    Vector3 p = Vector3.Lerp(a, b, u / L);
                    var near = new List<int>(); prisms.Query(p, 30f, near);
                    foreach (var i in near) minC = Math.Min(minC, prisms.ShellDistance(i, p));
                }
                total++;
                if (minC < 2.5f) { blocked++; perGap[k]++; }
            }
            for (int k = 0; k < N; k++)
            {
                int h = -1;
                float sc = course.Project(def.Anchors[k], ref h, out Vector3 cp, out float dist);
                course.Sample(sc, out Vector3 tg, out Vector3 nn);
                Vector3 ll = Vector3.Cross(nn, tg).normalized; nn = Vector3.Cross(tg, ll).normalized;
                Vector3 rel = def.Anchors[k] - cp;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  anchor {0}: lateral {1:F0}, height {2:F0}, along {3:F0} (dist {4:F0})",
                    k, Vector3.Dot(rel, ll), Vector3.Dot(rel, nn), Vector3.Dot(rel, tg), dist));
            }
            // Arc-length gap between consecutive anchors (a full search per anchor, no hint).
            var arcs = new float[N];
            for (int k = 0; k < N; k++) { int h = -1; arcs[k] = course.Project(def.Anchors[k], ref h, out _, out _); }
            var gaps = Enumerable.Range(0, N).Select(k => course.Ahead(arcs[k], arcs[(k + 1) % N])).ToArray();
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  course {0:F0} u; anchor arc gaps (u): {1}  (max {2:F0}, gaps > 250: {3})",
                course.Length, string.Join(" ", gaps.Select(g => g.ToString("F0", CultureInfo.InvariantCulture))), gaps.Max(), gaps.Count(g => g > 250f)));
            Console.WriteLine($"I{intensity}: straight crystal-to-crystal lines through the ribbon shell: {blocked}/{total} = {100f * blocked / total:F0}%");
            Console.WriteLine("  per gap %: " + string.Join(" ", perGap.Select(x => (x / 2).ToString())));
            return 0;
        }
        if (mode == "eval" || mode == "trace")
        {
            int seeds = int.Parse(args[3]);
            int seedBase = 1000;
            foreach (var a in args.Skip(4))
                if (a.StartsWith("seedbase=")) seedBase = int.Parse(a.Substring(9), CultureInfo.InvariantCulture);
            var (cfg, ph) = Parse(args.Skip(4).Where(a => !a.StartsWith("seedbase=") && !a.StartsWith("diag=") && !a.StartsWith("limit=")));
            Race.RecordFrames = true;
            if (mode == "trace") { var r = Race.Run(def, prisms, course, cfg, ph, 1000 + seeds, limit, true); Console.WriteLine($"finished={r.Finished} t={r.Time:F2} {r.Collected}/{r.Required}"); return 0; }
            var e = Evaluate(def, prisms, course, cfg, ph, seeds, limit, seedBase);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  decide cost: {0:F3} ms per seat per frame (sim runtime, {1} calls), {2:F0} bytes allocated per decision",
                1000.0 * Race.DecideTicks / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, Race.DecideCalls), Race.DecideCalls,
                Race.DecideBytes / (double)Math.Max(1, Race.DecideCalls)));
            if (Race.FrameMs.Count > 0)
            {
                var fm = Race.FrameMs.OrderBy(x => x).ToList();
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  AI thinking per frame, all {0} seats together: median {1:F3} ms, p90 {2:F3} ms, p99 {3:F3} ms, max {4:F3} ms ({5} frames)",
                    ph.Seats, fm[fm.Count / 2], fm[fm.Count * 9 / 10], fm[fm.Count * 99 / 100], fm[fm.Count - 1], fm.Count));
            }
            if (Race.TrackReplans > 0)
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  track planner: {0} re-plans in {1} frames; {2} frames with 2+ seats re-planning ({3:F1}%); {4} re-plans held a frame for another seat",
                    Race.TrackReplans, Race.TrackReplanFrames, Race.SharedReplanFrames,
                    100.0 * Race.SharedReplanFrames / Math.Max(1, Race.TrackReplanFrames), Race.TrackWaits));
            // Where it goes: the pilot's own Profiler markers (the names the Unity Profiler shows), per decision.
            var tally = Unity.Profiling.ProfilerTally.Names.Select((n, k) => (n, k)).Where(x => Unity.Profiling.ProfilerTally.Calls[x.k] > 0).ToList();
            if (tally.Count > 0)
                foreach (var x in tally)
                {
                    double ms = 1000.0 * Unity.Profiling.ProfilerTally.Ticks[x.k] / System.Diagnostics.Stopwatch.Frequency;
                    long calls = Unity.Profiling.ProfilerTally.Calls[x.k];
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "  marker {0,-28} {1,7:F3} ms per decision  {2,5:F2} calls per decision  {3,7:F3} ms per call",
                        x.n, ms / Math.Max(1, Race.DecideCalls), calls / (double)Math.Max(1, Race.DecideCalls), ms / Math.Max(1, calls)));
                }
            Console.WriteLine($"I{intensity} track={course.Length:F0}u prisms={prisms.Points.Count} finished {e.fin}/{seeds} " +
                              $"median={e.median:F2} mean={e.mean:F2} worst={e.worst:F2} score={e.score:F2}");
            {
                var seatTimes = e.runs.SelectMany(r => r.AgentTimes).OrderBy(x => x).ToList();
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  seats <= {0:F0} s: {1}/{2}; races with EVERY seat <= {0:F0} s: {3}/{4}; seat median {5:F2}",
                    limit, seatTimes.Count(x => x <= limit), seatTimes.Count,
                    e.runs.Count(r => r.AgentTimes.All(x => x <= limit)), e.runs.Count, seatTimes[seatTimes.Count / 2]));
                // What the EDITOR benchmark can observe: the race ends at the first finisher, so it judges
                // the WINNING seat only. Comparable to Docs/SKIM_RACE_AI.md section 8.
                var first = e.runs.Select(r => r.AgentTimes.Min()).OrderBy(x => x).ToList();
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  first finisher (editor-comparable): <= {0:F0} s in {1}/{2}, median {3:F2}",
                    limit, first.Count(x => x <= limit), first.Count, first[first.Count / 2]));
            }
            var causes = e.runs.SelectMany(r => r.ResetCauses).GroupBy(k => k.Key).Select(g => (g.Key, n: g.Sum(k => k.Value))).OrderByDescending(x => x.n);
            var lost = e.runs.SelectMany(r => r.ResetBoostLost).GroupBy(k => k.Key).ToDictionary(g => g.Key, g => g.Sum(k => k.Value));
            Console.WriteLine("  boost resets per race by cause: " + string.Join(", ", causes.Select(c =>
                string.Format(CultureInfo.InvariantCulture, "{0} {1:F1} (boost lost {2:F1})", c.Key, c.n / (float)seeds, lost[c.Key] / seeds))));
            if (ph.HcReaction > 0f || ph.HcMistake > 0f)
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  handicap: reaction {0:0.###} s, mistake chance {1:0.###}; misjudged crystals {2:F2} per seat per race",
                    ph.HcReaction, ph.HcMistake, e.runs.Sum(r => r.Mistakes) / (float)seeds / Math.Max(1, ph.Seats)));
            if (args.Contains("diag=1"))
            {
                float seatsN = Math.Max(1, ph.Seats);
                var pt = new Dictionary<string, float>(); var pb = new Dictionary<string, float>(); var ps = new Dictionary<string, float>(); var pk = new Dictionary<string, float>();
                foreach (var r in e.runs)
                {
                    foreach (var kv in r.PhaseTime) pt[kv.Key] = pt.GetValueOrDefault(kv.Key) + kv.Value;
                    foreach (var kv in r.PhaseBoostT) pb[kv.Key] = pb.GetValueOrDefault(kv.Key) + kv.Value;
                    foreach (var kv in r.PhaseSpeedT) ps[kv.Key] = ps.GetValueOrDefault(kv.Key) + kv.Value;
                    foreach (var kv in r.PhaseSkimT) pk[kv.Key] = pk.GetValueOrDefault(kv.Key) + kv.Value;
                }
                float total = pt.Values.Sum();
                Console.WriteLine("  PHASES (per seat per race): phase  time(s)  share  mean-boost  mean-speed  skimming%  track-hits");
                var hits = e.runs.SelectMany(r => r.TrackHitLog).ToList();
                foreach (var kv in pt.OrderByDescending(k => k.Value))
                {
                    float tt = kv.Value;
                    int nh = hits.Count(h => h.phase == kv.Key);
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "    {0,-12} {1,7:F1} {2,6:P0} {3,10:F2} {4,10:F0} {5,9:P0} {6,10:F1}",
                        kv.Key, tt / seeds / seatsN, tt / total, pb.GetValueOrDefault(kv.Key) / tt, ps.GetValueOrDefault(kv.Key) / tt,
                        pk.GetValueOrDefault(kv.Key) / tt, nh / (float)seeds / seatsN));
                }
                // Where on the plate the hull was when it struck: prism-local (x = lateral, y = normal, z = along).
                Console.WriteLine("  TRACK HITS by plate-local position (per seat per race):");
                foreach (var g in hits.GroupBy(h =>
                    (Math.Abs(h.lat) < 10f ? "over plate |x|<10" : Math.Abs(h.lat) < 15f ? "over edge 10-15" : Math.Abs(h.lat) < 20f ? "beside edge 15-20" : "beside >20")
                    + (h.hgt > 0.5f ? ", above" : h.hgt < -0.5f ? ", below" : ", in plane")).OrderByDescending(g => g.Count()))
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "    {0,-34} {1,5:F2}  (mean heading err {2:F0} deg, speed {3:F0}, boost lost {4:F1})",
                        g.Key, g.Count() / (float)seeds / seatsN, g.Average(h => h.headErr), g.Average(h => h.speed), g.Average(h => h.boost - 1f)));
                foreach (var g in hits.GroupBy(h => h.phase).OrderByDescending(g => g.Count()))
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "    phase {0,-12} {1,5:F2}/seat/race  |lat| median {2:F1}  |hgt| median {3:F1}  heading err median {4:F0}",
                        g.Key, g.Count() / (float)seeds / seatsN, Med(g.Select(h => Math.Abs(h.lat))), Med(g.Select(h => Math.Abs(h.hgt))), Med(g.Select(h => h.headErr))));
            }
            var te = e.runs.SelectMany(r => r.TrackErrors).OrderBy(x => x).ToList();
            if (te.Count > 0)
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  cross-track error (hull vs line at its own progress): median {0:F1}  p75 {1:F1}  p90 {2:F1}  p99 {3:F1}",
                    te[te.Count / 2], te[te.Count * 3 / 4], te[te.Count * 9 / 10], te[te.Count * 99 / 100]));
            var lc = e.runs.SelectMany(r => r.LineClear).ToList();
            if (lc.Count > 0)
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  planned line (next 300 u) min shell clearance: <0.6 in {0:P0}, <1.5 in {1:P0}, <3 in {2:P0} of decisions",
                    lc.Count(x => x < 0.6f) / (float)lc.Count, lc.Count(x => x < 1.5f) / (float)lc.Count, lc.Count(x => x < 3f) / (float)lc.Count));
            foreach (var r in e.runs)
                Console.WriteLine($"  {(r.Finished ? "FIN" : "DNF")} t={r.Time:F2} {r.Collected}/{r.Required} recov={r.Recoveries} mist={r.Mistakes} hull={r.HullHits} mean={r.MeanSpeed:F0} boost={r.MeanBoost:F2} far={r.FarFrac:F2} seats=[{string.Join(" ", r.AgentTimes.Select(x => x.ToString("F1", CultureInfo.InvariantCulture)))}]");
            return 0;
        }

        if (mode == "tune")
        {
            int seeds = int.Parse(args[3]);
            int iters = int.Parse(args[4]);
            float sigmaScale = 0.25f, over = 0f;
            bool winnerScore = args.Skip(5).Contains("score=winner");
            Tunables = PursuitTunables;
            foreach (var a in args.Skip(5))
            {
                if (a.StartsWith("sigma=")) sigmaScale = float.Parse(a.Substring(6), CultureInfo.InvariantCulture);
                if (a == "set=planner") Tunables = PlannerTunables;
                if (a == "set=mpc") Tunables = MpcTunables;
                if (a == "set=winner") Tunables = WinnerTunables;
                if (a.StartsWith("over=")) over = float.Parse(a.Substring(5), CultureInfo.InvariantCulture);
            }
            var (baseCfg, ph) = Parse(args.Skip(5).Where(a => !a.StartsWith("sigma=") && !a.StartsWith("set=") && !a.StartsWith("over=") && !a.StartsWith("score=") && !a.StartsWith("limit=")));
            var rng = new System.Random(7);
            int dim = Tunables.Length;
            var mu = Tunables.Select(k => GetNum(baseCfg, k)).ToArray();
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
                    for (int d = 0; d < dim; d++) SetNum(cfg, Tunables[d], xs[p][d]);
                    scores[p] = Evaluate(def, prisms, course, cfg, ph, seeds, limit, iterSeed, over, winnerScore).score;
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
                for (int d = 0; d < dim; d++) SetNum(bc, Tunables[d], best[d]);
                Console.WriteLine($"iter {it} gen-best={samples[0].score:F2} best={bestScore:F2} :: {Describe(bc)}");
            }
            var fc = CloneConfig(baseCfg);
            for (int d = 0; d < dim; d++) SetNum(fc, Tunables[d], best[d]);
            var fe = Evaluate(def, prisms, course, fc, ph, 40, limit, 99000);
            Console.WriteLine($"FINAL (40 fresh seeds) finished {fe.fin}/40 median={fe.median:F2} mean={fe.mean:F2} worst={fe.worst:F2}");
            {
                var w = fe.runs.Select(r => r.AgentTimes.Min()).OrderBy(x => x).ToList();
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "FINAL winner <= {0:F0} s in {1}/{2}, median {3:F2}, worst {4:F2}",
                    limit, w.Count(x => x <= limit), w.Count, w[w.Count / 2], w[w.Count - 1]));
            }
            Console.WriteLine("BEST " + Describe(fc));
            return 0;
        }
        Console.Error.WriteLine("unknown mode");
        return 2;
    }
}
