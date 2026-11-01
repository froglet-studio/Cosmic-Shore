// Switchback Blast Run, raced offline: the SHIPPED course (SwitchbackCourse), Blast Run layout (SwitchbackBlastRun:
// keys, doors, block fields, movers), blast planner (SwitchbackBlastPlanner) and prism-avoidance maths
// (SwitchbackPrismAvoidance) compiled out of Assets/ against a Unity shim, flown by a kinematic Dolphin and the AI's
// executor ported from SwitchbackBlastAI / GateRaceController / AIPilot / ChargeBoostAIPolicySO. See run.sh for usage
// and Assets/_Scripts/Controller/Arcade/SWITCHBACK.md "Offline: the Blast Run simulator" for the model and its limits.
using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

static class Program
{
    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        string mode = args.Length > 0 ? args[0] : "eval";
        var opt = Opts.Parse(args, 1);
        if (mode == "selftest") return SelfTest(opt);
        if (mode == "trace") { Trace(opt); return 0; }
        Eval(opt);
        return 0;
    }

    // ---------------------------------------------------------------- eval: every intensity x level x seed
    static void Eval(Opts o)
    {
        Console.WriteLine($"# Switchback Blast Run offline: {o.Gates} gates, seeds {string.Join(",", o.Seeds)}; " +
                          $"planner={(o.Planner ? "on" : "off")} sense={(o.Sense ? "on" : "off")} emergency={(o.Emergency ? "on" : "off")} " +
                          $"hit={o.Hit.Describe()} blast={(o.RealCone ? "asset cone" : "planner slot")} roll={(o.FlyRoll ? "flown" : "free")}");
        Console.WriteLine("I  level      races finished  median_s  p90_s  rings/min  blasts thread direct  keys  hits(danger)  emerg  misses");
        foreach (int i in o.Intensities)
        foreach (int lvl in o.Levels)
        {
            var times = new List<float>();
            int fin = 0, races = 0, blast = 0, thread = 0, direct = 0, keys = 0, hits = 0, dhits = 0, emerg = 0, misses = 0;
            float ringsPerMin = 0f;
            foreach (int seed in o.Seeds)
            {
                var r = Race.Run(i, seed, lvl, o, null);
                races++;
                if (r.Finished) { fin++; times.Add(r.Time); }
                ringsPerMin += r.Rings / Mathf.Max(r.Time / 60f, 1e-3f);
                blast += r.Blast; thread += r.Thread; direct += r.Direct; keys += r.Keys;
                hits += r.Hits; dhits += r.DangerHits; emerg += r.Emergencies; misses += r.Misses;
            }
            times.Sort();
            string med = times.Count > 0 ? times[times.Count / 2].ToString("F1") : "-";
            string p90 = times.Count > 0 ? times[Math.Min(times.Count - 1, (int)(0.9f * times.Count))].ToString("F1") : "-";
            Console.WriteLine($"{i}  {LevelName(lvl),-9} {races,6} {fin,8} {med,9} {p90,6} {ringsPerMin / races,10:F1} " +
                              $"{blast,7} {thread,6} {direct,6} {keys,5} {hits,7}({dhits}) {emerg,6} {misses,7}");
        }
    }

    static void Trace(Opts o)
    {
        int i = o.Intensities[0], lvl = o.Levels[0], seed = o.Seeds[0];
        var log = new List<string>();
        var r = Race.Run(i, seed, lvl, o, log);
        foreach (var l in log) Console.WriteLine(l);
        Console.WriteLine($"# I{i} {LevelName(lvl)} seed {seed}: {(r.Finished ? r.Time.ToString("F1") + " s" : "DNF")} " +
                          $"rings {r.Rings}, blasts {r.Blast}, keys {r.Keys}, hits {r.Hits} ({r.DangerHits} danger), emergencies {r.Emergencies}");
    }

    public static string LevelName(int l) => l switch { 1 => "Easy", 3 => "Hard", 4 => "Legendary", _ => "Medium" };

    static int SelfTest(Opts o)
    {
        var bad = new List<string>();
        // 1. An empty sky: no layout, the plain gate race - the Dolphin must finish every seed.
        var open = o.With(noLayout: true);
        foreach (int s in new[] { 1, 2, 3 })
        {
            var r = Race.Run(1, s, 4, open, null);
            if (!r.Finished) bad.Add($"open course seed {s} did not finish ({r.Rings} rings)");
            if (r.Hits > 0) bad.Add($"open course seed {s} hit {r.Hits} prisms");
        }
        // 2. Determinism: the same race twice is the same race.
        var a = Race.Run(3, 7, 3, o, null); var b = Race.Run(3, 7, 3, o, null);
        if (a.Time != b.Time || a.Hits != b.Hits || a.Keys != b.Keys) bad.Add("not deterministic");
        // 3. A blast removes what its slot covers (negative control: no key taken, nothing removed).
        var w = World.Build(2, 11, o);
        if (w.L.Stations.Count == 0) bad.Add("I2 seed 11 has no blocked gate");
        else
        {
            var st = w.L.Stations[0];
            int before = w.Planner.Standing(0, w);
            Vector3 f = (st.Door - st.Key).normalized;
            Vector3 u = Vector3.Cross(f, Mathf.Abs(f.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            int removed = w.Blast(st.Key, f, u, 0f, null);
            int after = w.Planner.Standing(0, w);
            if (removed <= 0 || after >= before) bad.Add($"a blast at station 0 removed nothing ({before} -> {after})");
        }
        Console.WriteLine("switchback_sim self-test: " + (bad.Count == 0 ? "ok" : "FAILED " + string.Join("; ", bad)));
        return bad.Count == 0 ? 0 : 1;
    }
}

// ---------------------------------------------------------------- options
sealed class Opts
{
    public int[] Intensities = { 1, 2, 3, 4 };
    public int[] Levels = { 1, 2, 3, 4 };
    public int[] Seeds = { 45501, 45502, 45503, 45504, 1, 2, 3, 4, 5, 6, 7, 8 };
    public int Gates = 20;
    public float Dt = 1f / 60f;
    public float MaxSeconds = 600f;
    public bool Planner = true, Sense = true, Emergency = true, Drive = true, NoLayout, RealCone, FlyRoll;
    public HitModel Hit = HitModel.Default;
    public float PickupRadius = 12f;     // Dolphin EnergySkimmer: radius 0.5 x scale 20 = 10 u, plus the crystal's own
    public float HullRadius = 3f;        // SwitchbackPrismAvoidance.ShipRadius
    // ChargeBoostAIPolicySO (DolphinAIBoostPolicy_Switchback[_I<n>]): align deg, runway s, spend 0..1; per intensity
    public float[] DriveAlign = { 34.14f, 35.308f, 35.308f, 51.947f };
    public float[] DriveRunway = { 0.26f, 1.422f, 1.422f, 1.105f };
    public float[] DriveSpend = { 0.96f, 0.835f, 0.835f, 0.502f };

    public Opts With(bool noLayout) { var c = (Opts)MemberwiseClone(); c.NoLayout = noLayout; return c; }

    static int[] Ints(string s) => Array.ConvertAll(s.Split(','), int.Parse);

    public static Opts Parse(string[] a, int from)
    {
        var o = new Opts();
        for (int k = from; k < a.Length; k++)
        {
            var kv = a[k].Split('=', 2);
            string key = kv[0], v = kv.Length > 1 ? kv[1] : "1";
            switch (key)
            {
                case "i": o.Intensities = Ints(v); break;
                case "level": o.Levels = Ints(v); break;
                case "seeds": o.Seeds = Ints(v); break;
                case "nseeds": { int n = int.Parse(v); o.Seeds = new int[n]; for (int s = 0; s < n; s++) o.Seeds[s] = s < 4 ? 45501 + s : s - 3; break; }
                case "gates": o.Gates = int.Parse(v); break;
                case "dt": o.Dt = float.Parse(v); break;
                case "planner": o.Planner = v != "0"; break;
                case "sense": o.Sense = v != "0"; break;
                case "emergency": o.Emergency = v != "0"; break;
                case "drive": o.Drive = v != "0"; break;
                case "cone": o.RealCone = v != "0"; break;
                case "roll": o.FlyRoll = v != "0"; break;
                case "pickup": o.PickupRadius = float.Parse(v); break;
                case "hull": o.HullRadius = float.Parse(v); break;
                case "hit.slow": o.Hit.SlowFactor = float.Parse(v); break;
                case "hit.secs": o.Hit.SlowSeconds = float.Parse(v); break;
                case "hit.dangerSlow": o.Hit.DangerSlowFactor = float.Parse(v); break;
                case "hit.dangerSecs": o.Hit.DangerSlowSeconds = float.Parse(v); break;
                case "hit.meter": o.Hit.MeterKeep = float.Parse(v); break;
                case "hit.destroys": o.Hit.Destroys = v != "0"; break;
                default: throw new ArgumentException("unknown option " + key);
            }
        }
        return o;
    }
}

/// <summary>What a prism does to the Dolphin that flies into it: DolphinImpactorDataContainer's vessel-prism effects.
/// DolphinVesselChangeSpeedByPrism (VesselChangeSpeedByPrismEffectSO, VesselTransformer.ModifyThrottle): a throttle
/// factor that lands at impact and eases linearly back to 1 over the duration, overlapping factors multiplied - plain:
/// x0.5 over 1 s (every block here is past the mass cap), Danger: x0 (a dead stop) over 3 s. The Change Boost / Change
/// Resource effects halve the charge (boost) meter. VesselDamagePrismEffect destroys the prism. No bounce, no knockback.
/// The danger prism's elemental debuff is not modelled (it does not touch speed).</summary>
struct HitModel
{
    public float SlowFactor, SlowSeconds, DangerSlowFactor, DangerSlowSeconds, MeterKeep;
    public bool Destroys;
    public static HitModel Default => new HitModel
    {
        SlowFactor = 0.5f, SlowSeconds = 1.0f, DangerSlowFactor = 0f, DangerSlowSeconds = 3.0f, MeterKeep = 0.5f, Destroys = true,
    };
    public string Describe() => $"plain x{SlowFactor} back over {SlowSeconds}s, danger x{DangerSlowFactor} back over {DangerSlowSeconds}s, " +
                                $"charge x{MeterKeep}, {(Destroys ? "prism destroyed" : "prism stays")}";
}

// ---------------------------------------------------------------- the world: course, layout, standing prisms, keys
sealed class World : ISwitchbackAIWorld
{
    public const float Inner = 480f, Outer = 1080f;
    public List<RaceGate> Course;
    public SwitchbackFeatureLayout L;
    public SwitchbackBlastPlanner Planner;
    public bool[] Standing;
    public bool[] Key;
    public int[] MoverOf;
    public int Intensity;
    readonly Dictionary<long, List<int>> _grid = new();
    const float Cell = 40f;
    readonly List<int> _moving = new();
    public readonly List<int> Query = new(256);

    public bool PrismStanding(int k) => Standing[k];
    public bool KeyAvailable(int s) => Key[s];

    public static World Build(int intensity, int seed, Opts o)
    {
        var s = SwitchbackCourseSettings.ForIntensity(intensity);
        s.GateCount = o.Gates;
        s.InnerRadius = Inner;
        s.OuterRadius = Outer;
        s.FirstGateDirection = Vector3.up;
        s.FirstGateDistance = 620f;
        var w = new World { Intensity = intensity, Course = SwitchbackCourse.Generate(seed, s) };
        w.L = o.NoLayout ? new SwitchbackFeatureLayout()
                         : SwitchbackBlastRun.Layout(w.Course, intensity, Vector3.zero, Inner / 1.22f + 30f);
        w.Planner = new SwitchbackBlastPlanner(w.L, w.Course.Count, intensity);
        int n = w.L.Prisms.Count;
        w.Standing = new bool[n];
        for (int k = 0; k < n; k++) w.Standing[k] = true;
        w.Key = new bool[w.L.Stations.Count];
        for (int k = 0; k < w.Key.Length; k++) w.Key[k] = true;
        w.MoverOf = new int[n];
        for (int k = 0; k < n; k++) w.MoverOf[k] = -1;
        for (int m = 0; m < w.L.Movers.Count; m++)
        {
            var mv = w.L.Movers[m];
            for (int k = mv.FirstPrism; k < mv.FirstPrism + mv.Count; k++) { w.MoverOf[k] = m; w._moving.Add(k); }
        }
        for (int k = 0; k < n; k++)
            if (w.MoverOf[k] < 0) w.Add(k, w.L.Prisms[k].Position);
        return w;
    }

    static long Hash(int x, int y, int z) => ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);
    static int C(float v) => Mathf.FloorToInt(v / Cell);

    void Add(int k, Vector3 p)
    {
        long h = Hash(C(p.x), C(p.y), C(p.z));
        if (!_grid.TryGetValue(h, out var list)) _grid[h] = list = new List<int>(8);
        list.Add(k);
    }

    /// <summary>The prism's pose at time t (movers posed off the shared clock as the game poses them).</summary>
    public void Pose(int k, float t, out Vector3 pos, out Quaternion rot)
    {
        var p = L.Prisms[k];
        int m = MoverOf[k];
        if (m < 0) { pos = p.Position; rot = p.Rotation; return; }
        var mv = L.Movers[m];
        mv.PoseAt(t, out Quaternion spin, out Vector3 offset);
        mv.Apply(spin, offset, p.Position, p.Rotation, out pos, out rot);
    }

    /// <summary>Standing prisms whose centre is within r (+ the largest prism half-size) of p, into Query.</summary>
    public void Near(Vector3 p, float r, float t)
    {
        Query.Clear();
        float reach = r + 50f;
        int x0 = C(p.x - reach), x1 = C(p.x + reach), y0 = C(p.y - reach), y1 = C(p.y + reach), z0 = C(p.z - reach), z1 = C(p.z + reach);
        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        for (int z = z0; z <= z1; z++)
            if (_grid.TryGetValue(Hash(x, y, z), out var list))
                foreach (int k in list)
                    if (Standing[k] && (L.Prisms[k].Position - p).sqrMagnitude <= reach * reach) Query.Add(k);
        foreach (int k in _moving)
        {
            if (!Standing[k]) continue;
            Pose(k, t, out Vector3 mp, out _);
            if ((mp - p).sqrMagnitude <= reach * reach) Query.Add(k);
        }
    }

    /// <summary>Distance from p to prism k's box (0 inside), at time t. The prism mesh is a unit cube scaled by Scale.</summary>
    public float BoxDistance(int k, Vector3 p, float t)
    {
        Pose(k, t, out Vector3 c, out Quaternion r);
        Vector3 local = Quaternion.Inverse(r) * (p - c);
        Vector3 h = L.Prisms[k].Scale * 0.5f;
        float dx = Mathf.Max(Mathf.Abs(local.x) - h.x, 0f), dy = Mathf.Max(Mathf.Abs(local.y) - h.y, 0f), dz = Mathf.Max(Mathf.Abs(local.z) - h.z, 0f);
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public float Extent(int k) => Mathf.Min(L.Prisms[k].Scale.magnitude * 0.5f, 12f);

    /// <summary>A crystal blast fired from <paramref name="origin"/> down <paramref name="f"/>, gape along
    /// <paramref name="u"/>: every standing prism the slot covers is destroyed. Returns how many.</summary>
    public int Blast(Vector3 origin, Vector3 f, Vector3 u, float t, Opts o)
    {
        var slot = o != null && o.RealCone ? new SwitchbackBlastSlot(500f, 0.16f, 0.30f) : Planner.Slot;
        int removed = 0;
        Near(origin + f * (slot.Reach * 0.5f), slot.Reach * 0.6f, t);
        foreach (int k in Query)
        {
            Pose(k, t, out Vector3 pos, out _);
            if (slot.Covers(origin, f, u, pos, 0f)) { Standing[k] = false; removed++; }
        }
        return removed;
    }
}

// ---------------------------------------------------------------- one race
sealed class Race
{
    public bool Finished; public float Time; public int Rings, Blast, Thread, Direct, Keys, Hits, DangerHits, Emergencies, Misses;

    // AIPilot / GateRaceController / MinigameSwitchback.unity
    const float Cruise = 68f, Turn = 110f;                    // Dolphin.prefab: ThrottleScaler 68, Pitch/Yaw 110
    const float CommitDist = 260f, ApproachLead = 300f, Through = 220f, LeadShare = 0.4f, LeadInside = 0.8f, CommitCos = 0.9f;
    // dolphin_drive_sim.py / DOLPHIN_ENERGY_ECONOMY.md §2
    const float MaxM = 2.259f, Fill = 1f / 3.636f, Drain = 0.4f, Lerp = 1.5f;
    const float PeakMargin = 3f, MinDis = 0.2f, MinGain = 30f, MaxHold = 4f, Grace = 0.25f, UpToSpeed = 0.9f;
    const float NoseFollow = 1.5f;                             // VesselTransformer.LERP_AMOUNT for AI hulls
    // SwitchbackBlastAI
    const float SetupAngle = 25f, SetupBack = 120f, StrikeThrough = 40f, LookRange = 160f, SenseStart = 6f;
    const float EmergHold = 0.18f, HotSeconds = 0.6f, EmergSteerDistance = 60f;

    enum Phase { None, Setup, Strike, Done }

    public static Race Run(int intensity, int seed, int level, Opts o, List<string> log)
    {
        var w = World.Build(intensity, seed, o);
        var r = new Race();
        r.Fly(w, intensity, seed, level, o, log);
        return r;
    }

    static float Ang(Vector3 a, Vector3 b) => Vector3.Angle(a, b);

    static Vector3 RotateToward(Vector3 from, Vector3 to, float maxRad)
    {
        if (to.sqrMagnitude < 1e-8f) return from;
        return Vector3.RotateTowards(from.normalized, to.normalized, maxRad, 0f).normalized;
    }

    // AIPilot's steering law at racing range (scarab_drive_sim.ai_stick); full stick from about 16 deg.
    static float Stick(float errDeg)
    {
        float e = Mathf.Min(errDeg, 90f) * Mathf.Deg2Rad;
        float a = Mathf.Asin(Mathf.Min(1f, Mathf.Sin(e) * Mathf.Sin(e) * 2f)) * Mathf.Rad2Deg;
        return errDeg > 90f ? 1f : Mathf.Min(1f, a * Mathf.Sin(e));
    }

    void Fly(World w, int intensity, int seed, int level, Opts o, List<string> log)
    {
        var skill = SwitchbackAISkill.ForLevel(level);
        var avoid = SwitchbackAvoidSkill.ForLevel(level);
        float emergSecs = SwitchbackAvoidSkill.EmergencySeconds(level);
        var course = w.Course;
        int total = course.Count;
        float dt = o.Dt;
        int ii = Mathf.Clamp(intensity - 1, 0, 3);
        float dAlign = o.DriveAlign[ii], dRunway = o.DriveRunway[ii], dSpend = o.DriveSpend[ii];

        // spawn: 400 u short of gate 0 on its axis, facing it
        Vector3 pos = course[0].Position - course[0].Axis * 400f;
        Vector3 fwd = course[0].Axis, cmd = fwd, travel = fwd, up = Mathf.Abs(fwd.y) < 0.9f ? Vector3.Cross(fwd, Vector3.Cross(Vector3.up, fwd)).normalized : Vector3.forward;
        float v = Cruise, t = 0f;
        int idx = 0, locked = -1; float side = 1f, lead = ApproachLead;
        bool drifting = false, extending = false; float extT = 0f;
        float meter = 0f, pinned = 1f; bool discharging = false, hold = false, wasDis = false; float heldAt = -1e9f;
        var slows = new List<Vector3>();   // (factor, start, duration): VesselTransformer's throttle modifiers
        var orbit = new Orbit();
        uint pseed = SwitchbackBlastPlanner.Hash((uint)seed * 2654435761u ^ (uint)level * 40503u);

        // the executor's per-pilot state (SwitchbackBlastAI.Pilot)
        var d = default(SwitchbackAIDecision); d.Station = -1;
        Phase phase = Phase.None; int planned = -1, plans = 0; float nextPlan = 0f;
        float nextSense = 0f; var avoidAct = SwitchbackAvoidAction.Clear; Vector3 avoidAim = default, blastCrystal = default, blastLook = default;
        bool emergOn = false; float emergHoldUntil = 0f, hotUntil = 0f; Vector3 emergSteer = default; int frame = 0;
        var pts = new Vector3[128]; var radii = new float[128]; var crystals = new Vector3[32];

        while (t < o.MaxSeconds && idx < total)
        {
            var g = course[idx];
            Vector3 ring = g.Position, ax = g.Axis;
            float R = v / (Turn * Mathf.Deg2Rad);
            float run = Mathf.Max(Mathf.Max(0f, 2f * R - 18f), v * 1.5f);
            if (idx != locked)
            {
                locked = idx;
                side = Vector3.Dot(pos - ring, ax) >= 0f ? 1f : -1f;
                Vector3 legFrom = idx > 0 ? course[idx - 1].Position : pos;
                lead = Mathf.Min(ApproachLead, LeadShare * Vector3.Distance(legFrom, ring));
                lead = Mathf.Min(lead, LeadInside * CommitDist);   // Switchback flies the Regatta steering
            }
            // ---- the gate race's own aim (GateRaceController.ArmRacer, Regatta steering), plus this pilot's lane at open gates
            float commit = Mathf.Min(CommitDist, lead + 0.5f * R);
            Vector3 laneOff = w.Planner.StationFor(idx) >= 0 ? Vector3.zero
                : SwitchbackPrismAvoidance.LaneOffset(pseed ^ (uint)(idx * 7919), ax, g.Radius, avoid.LaneShare);
            Vector3 ringAt = ring + laneOff;
            bool approaching = Vector3.Distance(ringAt, pos) > commit;
            Vector3 aim = approaching ? ringAt + ax * side * lead : ringAt + (ringAt - pos).normalized * Through;

            // ---- SwitchbackBlastAI.TryAim: plan, the gate blast, then the any-prism sense
            if (o.Planner && !o.NoLayout)
            {
                if (idx != planned)
                {
                    if (planned >= 0 && log != null) log.Add($"{t,6:F1} gate {planned} done plan={d.Plan} keys={Keys}");
                    planned = idx; plans = 0;
                    Plan();
                }
                else
                {
                    bool keyGone = d.Plan == SwitchbackAIPlan.Blast && phase != Phase.Done && !w.KeyAvailable(d.Station);
                    if (keyGone && Vector3.Distance(pos, d.Key) < 30f) phase = Phase.Done;
                    else if (keyGone || (skill.ReplanSeconds > 0f && t >= nextPlan && phase != Phase.Done && phase != Phase.Strike)) Plan();
                }
                bool gateAim = GateBlastAim(out Vector3 gAim);
                if (gateAim && phase == Phase.Strike) { aim = gAim; avoidAct = SwitchbackAvoidAction.Clear; }
                else
                {
                    Vector3 goal = gateAim ? gAim : ringAt;
                    if (o.Sense && SenseAim(goal, out Vector3 sAim)) aim = sAim;
                    else if (gateAim) aim = gAim;
                }
            }

            Vector3 to = aim - pos;
            Vector3 desired = to.magnitude > 1f ? to.normalized : travel;

            // ---- the emergency sweep (every frame hot, every other frame cold), down the real velocity
            bool emerg = false;
            if (o.Emergency && !o.NoLayout && EmergencyCheck(out Vector3 eSteer))
            {
                emerg = true; desired = (eSteer - pos).normalized; drifting = false; extending = false;
            }

            // ---- AIPilot.UpdateOrbitBreak: never while drifting or dodging
            if (!emerg)
            {
                if (drifting) extending = false;
                else if (extending)
                {
                    extT += dt;
                    if ((to.magnitude >= run && extT >= 0.6f) || extT >= 6f) extending = false;
                }
                else
                {
                    bool closing = Vector3.Dot(desired, fwd) > 0.5f;
                    if ((!closing && Orbit.InsideTurningCircle(to, fwd, R)) || orbit.Tick(to))
                    { extending = true; extT = 0f; orbit.Reset(); }
                }
            }

            // ---- the charge drive (ChargeBoostAIPolicySO.Driver.Tick)
            float targetSpeed = Cruise * (discharging ? (1f + (MaxM - 1f) * meter) * pinned : 1f);
            if (o.Drive)
            {
                if (discharging && !wasDis && !hold) { hold = true; heldAt = t; }
                wasDis = discharging;
                if (hold)
                {
                    float since = t - heldAt;
                    if ((discharging && since >= MinDis && v >= targetSpeed - PeakMargin) || (!discharging && since > Grace) || since > MaxHold) hold = false;
                }
                else if (drifting && !extending)
                {
                    Vector3 toRing = ring - pos;
                    float mm = 1f + (MaxM - 1f) * meter;
                    if (Ang(travel, toRing) <= dAlign && Ang(fwd, toRing) <= dAlign && toRing.magnitude / Mathf.Max(v, 1f) >= dRunway
                        && meter >= dSpend && Cruise * mm * mm - v >= MinGain)
                    { drifting = false; hold = true; heldAt = t; discharging = meter > 0f; travel = fwd; }
                }
            }
            // ---- the commit drift (held off while a discharge climbs; never under 0.9 of the throttle target)
            bool canCommit = !o.Drive || (!hold && v >= UpToSpeed * targetSpeed);
            if (!extending && !emerg)
            {
                if (!drifting && canCommit && Vector3.Dot(desired, travel) >= CommitCos) { drifting = true; travel = desired; discharging = false; }
                else if (drifting && Vector3.Dot(desired, travel) < CommitCos)
                {
                    drifting = false;
                    if (o.Drive) { discharging = meter > 0f; travel = fwd; }
                }
            }
            if (o.Drive)
            {
                if (drifting) { meter = Mathf.Min(1f, meter + Fill * dt); pinned = 1f + (MaxM - 1f) * meter; }
                else
                {
                    if (discharging) { meter = Mathf.Max(0f, meter - Drain * dt); if (meter <= 0f) discharging = false; }
                    targetSpeed = Cruise * (discharging ? (1f + (MaxM - 1f) * meter) * pinned : 1f);
                    v += (targetSpeed - v) * Lerp * dt;
                }
            }
            float throttle = 1f;
            for (int m = slows.Count - 1; m >= 0; m--)
            {
                float e = (t - slows[m].y) / slows[m].z;
                if (e >= 1f) { slows.RemoveAt(m); continue; }
                throttle *= Mathf.Lerp(slows[m].x, 1f, e);
            }
            float vEff = v * Mathf.Clamp(throttle, 0f, 6f);

            // ---- the flight: a drift turns the nose free (onto the drift look on a strike), the stick turns the course
            if (drifting)
            {
                Vector3 look = desired;
                if (DriftLook(out Vector3 lk)) look = (lk - pos).normalized;
                fwd = RotateToward(fwd, look, Turn * Mathf.Deg2Rad * dt); cmd = fwd;
            }
            else
            {
                Vector3 steer = extending ? (fwd + desired * -0.35f) : desired;
                float s = emerg ? 1f : Stick(Ang(fwd, steer));
                cmd = RotateToward(cmd, steer, Turn * s * Mathf.Deg2Rad * dt);
                fwd = RotateToward(fwd, cmd, Ang(fwd, cmd) * Mathf.Deg2Rad * Mathf.Min(1f, NoseFollow * dt));
                travel = fwd;
            }
            up = Vector3.ProjectOnPlane(up, fwd).sqrMagnitude > 1e-4f ? Vector3.ProjectOnPlane(up, fwd).normalized : Vector3.Cross(fwd, Vector3.right).normalized;
            Vector3 newPos = pos + travel * vEff * dt;

            // ---- keys: the skimmer takes a key it passes within the pickup radius; its blast fires down the nose
            if (!o.NoLayout)
                for (int s = 0; s < w.L.Stations.Count; s++)
                {
                    if (!w.Key[s]) continue;
                    if (SegPointDist(pos, newPos, w.L.Stations[s].Key) > o.PickupRadius) continue;
                    w.Key[s] = false; Keys++;
                    Vector3 gape = o.FlyRoll && d.Plan == SwitchbackAIPlan.Blast && d.Station == s ? d.FireUp : up;
                    int removed = w.Blast(w.L.Stations[s].Key, fwd, gape, t, o);
                    if (log != null) log.Add($"{t,6:F1} key {s} (gate {w.L.Stations[s].Gate}) taken, nose {Ang(fwd, w.L.Stations[s].Door - w.L.Stations[s].Key):F0} deg off the door, blasted {removed}");
                }

            // ---- prisms: the hull sphere against every standing prism's box
            if (!o.NoLayout)
            {
                w.Near(newPos, o.HullRadius + 12f, t);
                foreach (int k in w.Query)
                {
                    if (w.BoxDistance(k, newPos, t) > o.HullRadius) continue;
                    bool danger = w.L.Prisms[k].Kind == PrismKind.Danger;
                    Hits++; if (danger) DangerHits++;
                    slows.Add(new Vector3(danger ? o.Hit.DangerSlowFactor : o.Hit.SlowFactor, t,
                                          danger ? o.Hit.DangerSlowSeconds : o.Hit.SlowSeconds));
                    meter *= o.Hit.MeterKeep;
                    if (o.Hit.Destroys) w.Standing[k] = false;
                }
            }

            // ---- the ring
            float a0 = Vector3.Dot(pos - ring, ax), a1 = Vector3.Dot(newPos - ring, ax);
            if (a0 * a1 <= 0f && a0 != a1)
            {
                float kk = a0 / (a0 - a1); Vector3 hit = pos + (newPos - pos) * kk;
                if ((hit - ring).magnitude <= g.Radius) { idx++; Rings++; }
                else { Misses++; locked = -1; drifting = false; extending = true; extT = 0f; }
            }
            pos = newPos; t += dt; frame++;
        }
        Finished = idx >= total; Time = t;

        // ---- local functions: the executor
        void Plan()
        {
            if (idx >= total) return;
            uint ps = pseed ^ (uint)(idx * 92821) ^ (uint)(plans * 68917);
            var prev = d;
            d = w.Planner.Decide(idx, pos, v, course[idx].Position, t, in skill, w, ps);
            plans++;
            nextPlan = t + Mathf.Max(skill.ReplanSeconds, 0.25f);
            if (d.Plan != SwitchbackAIPlan.Blast) phase = Phase.None;
            else if (prev.Plan != SwitchbackAIPlan.Blast || prev.Gate != d.Gate) phase = Phase.None;
            if (plans == 1) { if (d.Plan == SwitchbackAIPlan.Blast) Blast++; else if (d.Plan == SwitchbackAIPlan.Thread) Thread++; else Direct++; }
            if (log != null && (plans == 1 || prev.Plan != d.Plan))
                log.Add($"{t,6:F1} gate {idx} plan={d.Plan} why={d.Why} cost thread={d.CostThread:F1} blast={d.CostBlast:F1} standing={d.Standing} cover={d.Coverage:P0}");
        }

        bool GateBlastAim(out Vector3 a)
        {
            a = default;
            if (d.Plan != SwitchbackAIPlan.Blast || phase == Phase.Done) return false;
            Vector3 key = d.Key, fire = d.FireForward, toKey = key - pos;
            float dist = toKey.magnitude;
            if (Vector3.Dot(-toKey, fire) > 5f) { phase = Phase.Done; return false; }
            float off = dist > 1f ? Ang(toKey, fire) : 0f;
            if (dist > 60f && off > SetupAngle) { phase = Phase.Setup; a = key - fire * Mathf.Min(SetupBack, 0.5f * dist); }
            else { phase = Phase.Strike; a = key + fire * StrikeThrough; }
            return true;
        }

        bool DriftLook(out Vector3 look)
        {
            look = default;
            if (avoidAct == SwitchbackAvoidAction.Blast)
            {
                if (Vector3.Distance(pos, blastCrystal) > LookRange) return false;
                look = blastLook; return true;
            }
            if (d.Plan != SwitchbackAIPlan.Blast || phase != Phase.Strike) return false;
            if (Vector3.Distance(pos, d.Key) > LookRange) return false;
            look = d.AimPoint; return true;
        }

        bool SenseAim(Vector3 goal, out Vector3 a)
        {
            a = default;
            if (avoidAct == SwitchbackAvoidAction.Blast)
            {
                Vector3 toC = blastCrystal - pos;
                bool passed = Vector3.Dot(toC, blastLook - blastCrystal) < -5f;
                if (!passed && KeyAt(blastCrystal)) { a = blastCrystal + (blastLook - blastCrystal).normalized * StrikeThrough; return true; }
                avoidAct = SwitchbackAvoidAction.Clear;
            }
            if (t < nextSense)
            {
                if (avoidAct != SwitchbackAvoidAction.Avoid) return false;
                a = pos + avoidAim; return true;
            }
            nextSense = t + avoid.ReactionSeconds * (0.85f + 0.3f * ((pseed >> 8 & 0xFF) / 255f));
            Vector3 toGoal = goal - pos; float dist = toGoal.magnitude;
            if (dist < 1f) return false;
            Vector3 dir = toGoal / dist;
            float range = Mathf.Min(Mathf.Max(avoid.SenseRange, v * (1.2f + avoid.ReactionSeconds)), dist + 10f);
            float probe = avoid.Clearance * 2.5f;
            // the capsule overlap: standing prisms whose box comes within `probe` of the segment
            Vector3 s0 = pos + dir * SenseStart, s1 = pos + dir * range;
            w.Near((s0 + s1) * 0.5f, (range - SenseStart) * 0.5f + probe, t);
            int n = 0;
            foreach (int k in w.Query)
            {
                if (n >= pts.Length) break;
                w.Pose(k, t, out Vector3 c, out _);
                if (SegPointDist(s0, s1, c) - w.Extent(k) > probe) continue;
                pts[n] = c; radii[n] = w.Extent(k); n++;
            }
            if (n == 0 || !SwitchbackPrismAvoidance.Blocked(pos, dir, range, avoid.Clearance, pts, n, out float along, radii))
            { avoidAct = SwitchbackAvoidAction.Clear; return false; }
            Vector3 corridor = SwitchbackPrismAvoidance.ChooseCorridor(pos, dir, range, pts, n, in avoid, out float clear, radii);
            float turn = Ang(corridor, dir);
            Vector3 blocker = SwitchbackPrismAvoidance.BlockerCentroid(pos, dir, range, avoid.Clearance * 2f, pts, n, out int blocking);
            int nc = 0;
            for (int s = 0; s < w.L.Stations.Count && nc < crystals.Length; s++) if (w.Key[s]) crystals[nc++] = w.L.Stations[s].Key;
            if ((clear < avoid.Clearance || turn > 25f) &&
                SwitchbackPrismAvoidance.TryBlast(pos, fwd, blocker, avoid.Clearance * 2f, pts, n, crystals, nc,
                                                  w.Planner.Slot, in avoid, out Vector3 crystal, out _, out _, out _))
            {
                avoidAct = SwitchbackAvoidAction.Blast; blastCrystal = crystal; blastLook = blocker;
                a = crystal + (blocker - crystal).normalized * StrikeThrough;
                return true;
            }
            avoidAct = SwitchbackAvoidAction.Avoid;
            avoidAim = corridor * range;
            a = pos + avoidAim;
            return true;
        }

        bool KeyAt(Vector3 at)
        {
            for (int s = 0; s < w.L.Stations.Count; s++)
                if (w.Key[s] && (w.L.Stations[s].Key - at).sqrMagnitude <= 16f) return true;
            return false;
        }

        bool EmergencyCheck(out Vector3 steer)
        {
            steer = default;
            if (emergOn && t < emergHoldUntil) { steer = pos + emergSteer * EmergSteerDistance; return true; }
            bool hot = t < hotUntil;
            if (!hot && ((frame + (int)(pseed & 1u)) & 1) != 0) { emergOn = false; return false; }
            float speed = v;
            if (speed < 1f) { emergOn = false; return false; }
            Vector3 dir = travel.normalized;
            float reach = SwitchbackPrismAvoidance.EmergencyReach(speed, emergSecs);
            float radius = SwitchbackPrismAvoidance.ShipRadius + avoid.Clearance * 0.3f;
            float limit = reach;
            bool striking = avoidAct == SwitchbackAvoidAction.Blast || (d.Plan == SwitchbackAIPlan.Blast && phase == Phase.Strike);
            if (striking)
            {
                Vector3 c = avoidAct == SwitchbackAvoidAction.Blast ? blastCrystal : d.Key;
                float alongC = Vector3.Dot(c - pos, dir);
                if (alongC > 0f) limit = Mathf.Min(limit, alongC - 2f);
            }
            bool hit = Sweep(pos, dir, radius, limit, out float hitDist, out Vector3 hitAt);
            if (!hit && Ang(dir, fwd) > 8f) hit = Sweep(pos, fwd, radius, limit * 0.6f, out hitDist, out hitAt);
            if (!hit) { emergOn = false; return false; }
            float urgency = 1f - hitDist / Mathf.Max(limit, 1f);
            Vector3 upv = Vector3.Cross(dir, Vector3.Cross(fwd, dir)).sqrMagnitude > 0.01f ? fwd : Vector3.up;
            emergSteer = SwitchbackPrismAvoidance.EscapeDirection(dir, pos - hitAt, (pos - hitAt).normalized, upv, urgency);
            hotUntil = t + HotSeconds; emergHoldUntil = t + EmergHold;
            if (!emergOn) Emergencies++;
            emergOn = true;
            steer = pos + emergSteer * EmergSteerDistance;
            return true;
        }

        bool Sweep(Vector3 p0, Vector3 dir, float radius, float dist, out float hitDist, out Vector3 centre)
        {
            hitDist = float.MaxValue; centre = default;
            if (dist <= 0.5f) return false;
            w.Near(p0 + dir * (dist * 0.5f), dist * 0.5f + radius, t);
            bool any = false;
            foreach (int k in w.Query)
            {
                w.Pose(k, t, out Vector3 c, out _);
                float along = Vector3.Dot(c - p0, dir);
                if (along < 0f || along > dist + w.Extent(k)) continue;
                // the sphere's sweep against the prism's box, sampled along the line
                for (float s = Mathf.Max(0f, along - w.Extent(k) - radius); s <= Mathf.Min(dist, along + w.Extent(k) + radius); s += 2f)
                {
                    if (w.BoxDistance(k, p0 + dir * s, t) <= radius)
                    {
                        if (s < 0.5f) break;   // a start-overlap: skipped, as the game's SphereCast skips them
                        if (s < hitDist) { hitDist = s; centre = c; any = true; }
                        break;
                    }
                }
            }
            return any;
        }
    }

    static float SegPointDist(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float tt = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return (p - (a + ab * tt)).magnitude;
    }
}

/// <summary>PursuitReachability.OrbitDetector and AIPilot's turning-circle test (regatta_line_sim.py).</summary>
sealed class Orbit
{
    bool _has; Vector3 _last; float _best, _lastD, _swept;
    public void Reset() { _has = false; _swept = 0f; }
    public bool Tick(Vector3 to)
    {
        float d = to.magnitude;
        if (d < 1e-6f) { Reset(); return false; }
        Vector3 b = to / d;
        if (!_has) { _has = true; _last = b; _best = d; _lastD = d; _swept = 0f; return false; }
        if (d > _lastD * 1.6f) { _last = b; _best = d; _lastD = d; _swept = 0f; return false; }
        _swept += Vector3.Angle(_last, b); _last = b; _lastD = d;
        if (d < _best * 0.9f) { _best = d; _swept = 0f; return false; }
        return _swept >= 360f;
    }
    public static bool InsideTurningCircle(Vector3 to, Vector3 heading, float R, float capture = 18f)
    {
        float d = to.magnitude;
        if (R <= 0f || d < 1e-6f) return false;
        Vector3 f = heading.normalized;
        Vector3 lat = to - f * Vector3.Dot(to, f);
        if (lat.magnitude < 1e-6f) return false;
        Vector3 centre = lat.normalized * R;
        return (to - centre).magnitude < Mathf.Max(0f, R - capture);
    }
}
