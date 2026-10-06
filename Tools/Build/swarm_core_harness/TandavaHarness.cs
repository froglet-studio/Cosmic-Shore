// Headless proof of Tandava (Assets/_Scripts/Controller/Arcade/TANDAVA.md §8): the SHIPPED sort core in its scripted-plan
// mode with Tandava's levers (cruise and turn scale, the laying hold, the pose commit, the turn carry, plan-tier danger),
// and the SHIPPED director (TandavaDirectorCore) running the creature in a CLOSED cell of dispersed, regrowing plants.
//
//   bash Tools/Build/swarm_core_harness/run.sh <plans> tandava <tandava plans dir>
//
// T1-T6 prove the core under the director's levers: the plans; the swarm hatching whole as its first form; the cruise and
// turn levers; a cut body regrowing into the SAME form; the laying hold; and the feed pose (the plates go out to orbit
// the mouth as danger plates). T7-T15 run the director: a free run through all four forms to the last feast; threat and
// mood; where it chooses to eat; the broken meal; denial; the dance; the shatter; the variant draw; the clock.
//
// The FOOD model keeps the one thing SwarmFauna.Feed's arithmetic depends on - GEOMETRY. Each tick BitersPerStep members
// are asked round-robin, and a member bites only if it is within PlantRadius + BiteRadius of a plant that still has food,
// one prism (its species' leaf) a bite. So the intake is BitersPerStep x the share of the body touching a plant - which is why
// the mouth matters. A plant regrows RegrowPrismsPerSecond while it has not been bitten for RegrowPause (Borromean's
// orbit-a-period growth and its 2 s damage pause), dies at DiesAtPrisms and is replanted ReplantSeconds later somewhere
// else (the cell's seeder). The METABOLISM is SwarmFauna's: unfed StarvationSeconds while hungry, it sheds one member
// every ShedIntervalSeconds, the core choosing who. The PILOTS are scripted policies that move points and kill members;
// the halo is the glue's (rings placed off the dance plan's baked ring and the body axes when the drum starts, each
// guarded while enough attendants are at its post).
// What it is not: the game's bites are prism queries against real plants, its pilots are people, and its kills are
// collisions - so the TIMES below are a model, not a measurement. QA-TANDAVA-9..12 measure them in the Editor.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CosmicShore.Gameplay;

/// <summary>The arena author_tandava_assets.py lays (its MEMBRANE_RADIUS, the flora band, the flora table).</summary>
static class TandavaArena
{
    public const float MembraneRadius = 800f;    // TandavaMembrane: the CapsuleMembrane at two thirds - a crowded hunt
    public const float NucleusRadius = 196f;     // HalfNucleus (the Scurry cell's core): fauna eat nothing inside it
    public const float PlantInner = 240f, PlantOuter = 720f;    // the flora band (world, from the centre)
    public const float BodyReach = 160f;          // the longest body's half-length: its centre roams the membrane less this
    public static readonly Vector3 Hatch = new(-430f, 0f, 0f);
    /// <summary>The cramped reef: species, element (research index), how many, prisms a plant may grow, one leaf's volume
    /// (its canonical LeafSize - the generator reads every row back).</summary>
    public static readonly (string Species, int Element, int Count, int Prisms, float Leaf)[] Flora =
    {
        ("Borromean", 1, 5, 60, 72.866f),
        ("Coral", 1, 5, 110, 24.640f),
        ("Lantern", 1, 4, 70, 43.740f),
        ("Reed", 1, 4, 130, 16.796f),
        ("Frond", 1, 3, 120, 22.964f),
        ("Tendril", 2, 3, 84, 3.268f),
        ("Coral", 2, 3, 140, 3.377f),
    };
}

static class TandavaHarness
{
    static int _fail;

    // ── the swarm config (TandavaSwarmFaunaConfig) - author_tandava_assets.py reads every one of these back
    const float UnitScale = 2f, TickHz = 10f;
    const int Density = 3;
    const int SeedMembers = 180;          // x Density, capped at the seeded form's plan: it hatches WHOLE
    static readonly float[] EggVolume = { 20.45f, 40.31f, 22.18f, 12.8f };
    const int StomachEggs = 500;
    const float SortVMaxScale = 3f;       // a re-sort reads in about a second (x the research's top member speed)
    const float SortWellClip = 1f;
    static readonly float StomachCapacity = StomachEggs * Density * (20.45f + 40.31f + 22.18f + 12.8f) * 0.25f;   // SwarmFauna.StomachCapacity
    const float Cruise = 3.0f;            // voxels/step: x UnitScale 2 x 10 Hz = 60 u/s calm (126 fleeing)
    const float TurnPerStep = 0.03f;
    const float SortTurnCarry = 1f;
    const float SortLayRate = 0.084f;
    const int SortLayMax = 8;             // x Density: 24 eggs a step at most
    const float KillLayHoldSeconds = 0f;  // a cut never stops it regrowing - only feeding does (the director's hold)
    const float SortLayRampSeconds = 1.5f;
    const int BitersPerStep = 8;
    const int SortWellsPerType = 24;
    const float SortWellDead = 0f;
    const float StarvationSeconds = 30f, ShedIntervalSeconds = 0.25f, ForageBelow = 0.5f;
    const float BodyFill = 0.939f;        // SortBodyFill

    // ── the economy (author_tandava_assets.py build_forms: the same arithmetic)
    const float FillToEvolve = 0.9f;
    // the banks, as shares of the stomach - RISING, so what one form carries over never skips the next, and all under
    // the 0.98 fill at which a meal ends full (a bank above it could never be reached: a full stomach stops grazing)
    static readonly float[] BankShare = { 0.15f, 0.28f, 0f, 0.4f };   // Great Serpent, Many-Headed (the dance's offering), -, Sea Lion (the feast)
    const float MealVolume = 4000f;       // one meal (flora volume): most of a 60-prism plant - about 13 s at it

    // ── the food (a bite is one prism: its species' leaf)
    const float PlantRadius = 28f, BiteRadius = 10f;
    const float RegrowPrismsPerSecond = 7.5f, RegrowPause = 2f, ReplantSeconds = 120f;
    const int DiesAtPrisms = 4;

    // ── the halo
    const int HaloCount = 12, HaloToBreak = 9, GuardMembers = 6, GuardElement = 3;   // Time: the attendant packs
    const float GuardRadius = 40f;
    /// <summary>A test run's length: the hunt has no clock, so a test stops itself.</summary>
    const float RunCap = 420f;

    /// <summary>Every plan, in the swarm config's ScriptedPlans order (tandava_plans.plan_keys - the generator checks).</summary>
    public static readonly string[] Keys =
    {
        "great_serpent_1", "great_serpent_1_feed", "great_serpent_1_coil", "great_serpent_1_wrap", "great_serpent_1_eight",
        "great_serpent_2", "great_serpent_2_feed", "great_serpent_2_coil", "great_serpent_2_wrap", "great_serpent_2_eight",
        "great_serpent_3", "great_serpent_3_feed", "great_serpent_3_coil", "great_serpent_3_wrap", "great_serpent_3_eight",
        "many_headed_5", "many_headed_5_feed", "many_headed_5_coil", "many_headed_5_wrap", "many_headed_5_eight",
        "many_headed_7", "many_headed_7_feed", "many_headed_7_coil", "many_headed_7_wrap", "many_headed_7_eight",
        "many_headed_10", "many_headed_10_feed", "many_headed_10_coil", "many_headed_10_wrap", "many_headed_10_eight",
        "dancer_1", "dancer_2", "dancer_3",
        "sea_lion_1", "sea_lion_1_feed", "sea_lion_2", "sea_lion_2_feed", "sea_lion_3", "sea_lion_3_feed",
    };
    /// <summary>The meal formations the two serpents roll up in (tandava_plans.COILS).</summary>
    static readonly string[] Coils = { "coil", "wrap", "eight" };
    static readonly string[][] Variants =
    {
        new[] { "great_serpent_1", "great_serpent_2", "great_serpent_3" },
        new[] { "many_headed_5", "many_headed_7", "many_headed_10" },
        new[] { "dancer_1", "dancer_2", "dancer_3" },
        new[] { "sea_lion_1", "sea_lion_2", "sea_lion_3" },
    };
    static readonly string[] FormNames = { "Great Serpent", "Many-Headed Serpent", "Lord of the Dance", "Sea Lion" };

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    // ──────────────────────────────────────────────────────────────── the bake

    sealed class Bake
    {
        public SwarmPlanData[] Plans;
        public readonly Dictionary<string, int> Ix = new();
        public readonly Dictionary<string, Vector3> Mouth = new();
        /// <summary>The farthest any of a plan's units comes from its centre, in any frame (world).</summary>
        public readonly Dictionary<string, float> Reach = new();
        public readonly Dictionary<string, (Vector3 centre, float radius, float orbit)> Ring = new();
    }

    static Bake Load(string dir)
    {
        var b = new Bake();
        var plans = new List<SwarmPlanData>();
        float k = MathF.Pow(Density, 1f / 3f) * UnitScale;   // plan voxels -> world (SwarmPlanData.Upsample scales by cbrt(m))
        foreach (var key in Keys)
        {
            string text = File.ReadAllText(Path.Combine(dir, $"SwarmPlan_tandava_{key}.json"));
            b.Ix[key] = plans.Count;
            plans.Add(JsonSerializer.Deserialize<SwarmPlanJson>(text, new JsonSerializerOptions { IncludeFields = true })
                      .ToPlanData().Upsample(Density, SwarmPlanData.DefaultUpsampleRadius));
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            {
                var pos = root.GetProperty("pos"); float reach = 0f;
                for (int q = 0; q + 2 < pos.GetArrayLength(); q += 3)
                    reach = MathF.Max(reach, new Vector3(pos[q].GetSingle(), pos[q + 1].GetSingle(), pos[q + 2].GetSingle()).Length());
                b.Reach[key] = reach * k;   // author_tandava_assets.py coil_roam_radius: the same reach, off the same JSON
            }
            if (root.TryGetProperty("mouth", out var m)) b.Mouth[key] = new Vector3(m[0].GetSingle(), m[1].GetSingle(), m[2].GetSingle()) * k;
            if (root.TryGetProperty("ring", out var r))
            {
                var c = r.GetProperty("centre");
                b.Ring[key] = (new Vector3(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle()) * k,
                               r.GetProperty("radius").GetSingle() * k, r.GetProperty("guardOrbit").GetSingle() * k);
            }
        }
        b.Plans = plans.ToArray();
        return b;
    }

    /// <summary>The forms a match with these variant picks runs - author_tandava_assets.py build_forms: each eating form's
    /// bank is its BankShare of the stomach, its meal one MealVolume; the dance does not eat; the Sea Lion's bank is the
    /// FEAST that completes the cycle.</summary>
    /// <summary>The serpents eat in their strike pose, as before the coils (T18's baseline; TANDAVA_NOCOIL=1 for every run).</summary>
    static bool NoCoils = Environment.GetEnvironmentVariable("TANDAVA_NOCOIL") == "1";

    static List<TandavaForm> BuildForms(Bake b, IReadOnlyList<int> picks)
    {
        var forms = new List<TandavaForm>();
        for (int f = 0; f < 4; f++)
        {
            string key = Variants[f][picks[f]];
            var plan = b.Plans[b.Ix[key]];
            var form = new TandavaForm
            {
                Name = FormNames[f], PlanIndex = b.Ix[key], PlanCount = plan.N, FillToEvolve = FillToEvolve,
                Role = f == 2 ? TandavaFormRole.Dance : f == 3 ? TandavaFormRole.Final : TandavaFormRole.Eater,
            };
            if (f != 2)
            {
                form.FeedPlanIndex = b.Ix[key + "_feed"];
                form.Mouth = b.Mouth[key]; form.FeedMouth = b.Mouth[key + "_feed"];
                if (f < 2 && !NoCoils)
                {
                    form.CoilPlanIndices = Coils.Select(c => b.Ix[$"{key}_{c}"]).ToArray();
                    form.CoilMouths = Coils.Select(c => b.Mouth[$"{key}_{c}"]).ToArray();
                    form.CoilRoamRadius = TandavaArena.MembraneRadius * 0.97f - Coils.Max(c => b.Reach[$"{key}_{c}"]);
                }
                form.Bank = BankShare[f] * StomachCapacity;
                form.MealVolume = MealVolume;
            }
            else form.MealVolume = 1f;
            forms.Add(form);
        }
        return forms;
    }

    static SwarmSortParams Params(SwarmPlanData[] plans)
    {
        var p = SortHarness.Game(plans);
        p.Scripted = true;
        p.PlanPeriods = plans.Select(x => x.FrameSteps).ToArray();   // TandavaSwarmFaunaConfig ScriptedPlanPeriods
        p.K = SortWellsPerType;
        p.WellDead = SortWellDead;
        p.Cap = plans.Max(x => x.N);
        p.LayRate = SortLayRate;
        p.LayMax = SortLayMax * Density;
        p.KillLayHoldSteps = (int)MathF.Round(KillLayHoldSeconds * TickHz);
        p.LayRamp = (int)MathF.Round(SortLayRampSeconds * TickHz);
        p.ThreatGain = 3f * MathF.Pow(Density, 2f / 3f);
        p.Membrane = TandavaArena.MembraneRadius * 0.97f / UnitScale;
        p.Cruise = Cruise; p.Turn = TurnPerStep; p.TurnCarry = SortTurnCarry;
        p.WellClip = SortWellClip;
        for (int e = 0; e < 4; e++) p.VMax[e] *= SortVMaxScale;   // SwarmFauna.BuildSortCore (SortVMaxScale)
        p.Over = BodyFill;
        for (int e = 0; e < 4; e++) p.EggCost[e] = EggVolume[e];
        return p;
    }

    static TandavaDirectorSettings DirectorSettings() => new()
    {
        Centre = Vector3.Zero,
        RoamRadius = TandavaArena.MembraneRadius * 0.97f - TandavaArena.BodyReach,   // the membrane less the longest body's half-length
        HaloCount = HaloCount, HaloToBreak = HaloToBreak,
    };

    static int Active(SwarmSortCore c) { int n = 0; for (int i = 0; i < c.Cap; i++) if (c.Active[i]) n++; return n; }
    static int[] Mix(SwarmPlanData p) { var m = new int[4]; for (int u = 0; u < p.N; u++) m[p.Elem[u]]++; return m; }

    /// <summary>Shape coverage: the share of the plan's units (body frame, at the pose the core is steering to this step)
    /// with a same-element member within <paramref name="reach"/> voxels - "every part of the animal is there NOW".</summary>
    static float Coverage(SwarmSortCore c, float reach)
    {
        var plan = c.Plan; int n = plan.N;
        var live = new List<(Vector3 p, int e)>();
        Vector3 cen = Vector3.Zero; int k = 0;
        for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) { cen += c.Pos[i]; k++; }
        if (k == 0) return 0f;
        cen /= k;
        for (int i = 0; i < c.Cap; i++)
            if (c.Active[i] && c.Hatched[i])
            {
                var d = c.Pos[i] - cen;
                live.Add((new Vector3(Vector3.Dot(d, c.BX), Vector3.Dot(d, c.BY), Vector3.Dot(d, c.BZ)), c.EffectiveElement(i)));
            }
        int per = c.C.PlanPeriods != null && c.PlanIx < c.C.PlanPeriods.Length && c.C.PlanPeriods[c.PlanIx] > 0 ? c.C.PlanPeriods[c.PlanIx] : 8;
        int L = plan.Order.Length, slot = (int)(c.Clock / per);
        int fA = plan.Order[slot % L], fB = plan.Order[(slot + 1) % L]; float fa = (c.Clock % per) / (float)per;
        var pt = new Vector3[n]; var fc = Vector3.Zero;
        for (int u = 0; u < n; u++) { pt[u] = plan.P[fA][u] + fa * (plan.P[fB][u] - plan.P[fA][u]); fc += pt[u]; }
        fc /= n;
        int hit = 0;
        for (int u = 0; u < n; u++)
        {
            var pu = pt[u] - fc; int eu = plan.Elem[u];
            foreach (var (p, e) in live) if (e == eu && Vector3.DistanceSquared(p, pu) <= reach * reach) { hit++; break; }
        }
        return hit / (float)n;
    }

    /// <summary>TANDAVA_DUMP=dir: the live body (body coordinates, element) for a picture.</summary>
    static void Dump(SwarmSortCore c, string name)
    {
        var dir = Environment.GetEnvironmentVariable("TANDAVA_DUMP");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        Vector3 cen = Vector3.Zero; int k = 0;
        for (int i = 0; i < c.Cap; i++) if (c.Active[i] && c.Hatched[i]) { cen += c.Pos[i]; k++; }
        cen /= Math.Max(1, k);
        var sb = new System.Text.StringBuilder("{\"live\":[");
        bool first = true;
        for (int i = 0; i < c.Cap; i++)
            if (c.Active[i] && c.Hatched[i])
            {
                var d = c.Pos[i] - cen;
                sb.Append(FormattableString.Invariant($"{(first ? "" : ",")}[{Vector3.Dot(d, c.BX):F2},{Vector3.Dot(d, c.BY):F2},{Vector3.Dot(d, c.BZ):F2},{c.EffectiveElement(i)}]"));
                first = false;
            }
        sb.Append(FormattableString.Invariant($"],\"plan\":\"{c.Plan.Kind}\",\"coverage\":{Coverage(c, 3.2f):F3}}}"));
        File.WriteAllText(Path.Combine(dir, name + ".json"), sb.ToString());
    }

    static SwarmSortCore MakeCore(Bake b, int plan, Vector3 worldAt, int seed, bool fed)
    {
        var c = new SwarmSortCore(b.Plans, Params(b.Plans), seed);
        c.Seed(plan, SeedMembers * Density, worldAt / UnitScale, Vector3.UnitX);
        c.SwimTarget = c.Anchor;
        if (fed) { c.Stomach[0] = c.Stomach[2] = c.Stomach[3] = 0.1f * StomachCapacity; c.Stomach[1] = 0.6f * StomachCapacity; }
        return c;
    }

    static int _selfDeaths;
    static void Step(SwarmSortCore c, int n = 1)
    {
        for (int q = 0; q < n; q++)
        {
            var before = (bool[])c.Active.Clone();
            c.Step(ReadOnlySpan<SwarmPredator>.Empty);
            for (int i = 0; i < c.Cap; i++) if (before[i] && !c.Active[i]) _selfDeaths++;
            c.Events.Clear();
        }
    }

    /// <summary>Kill the <paramref name="n"/> live members furthest along -<paramref name="along"/> (body frame) - a cut
    /// from that end. Returns how many died.</summary>
    static int Cut(SwarmSortCore c, int n, Vector3 along, Func<int, bool> spare = null)
    {
        var order = Enumerable.Range(0, c.Cap).Where(i => c.Active[i] && c.Hatched[i] && (spare == null || !spare(i)))
                              .OrderBy(i => Vector3.Dot(c.Pos[i] - c.Anchor, along)).Take(n).ToList();
        foreach (int i in order) c.Kill(i);
        return order.Count;
    }

    // ──────────────────────────────────────────────────────────────── the closed cell, its plants, its pilots

    sealed class Plant
    {
        public int Id, Element, Prisms;
        public float Leaf;
        public Vector3 At;
        public float Store, LastBite = -1e9f, ReplantAt;
        public bool Alive = true;
    }

    static Vector3 PlantSite(Random rng, List<Plant> others)
    {
        // SpreadPlanting: the best of 8 points (uniform by volume in the band) - the one farthest from its neighbours
        Vector3 best = default; float bd = -1f;
        for (int t = 0; t < 8; t++)
        {
            Vector3 d;
            do d = new Vector3((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1));
            while (d.LengthSquared() > 1f || d.LengthSquared() < 1e-3f);
            d = Vector3.Normalize(d);
            float lo = TandavaArena.PlantInner, hi = TandavaArena.PlantOuter;
            float r = MathF.Cbrt((float)(lo * lo * lo + rng.NextDouble() * (hi * hi * hi - lo * lo * lo)));
            var p = d * r;
            float near = float.MaxValue;
            foreach (var o in others) if (o.Alive) near = MathF.Min(near, Vector3.Distance(o.At, p));
            if (near > bd) { bd = near; best = p; }
        }
        return best;
    }

    static List<Plant> LayPlants(int seed)
    {
        var rng = new Random(seed * 131 + 9);
        var plants = new List<Plant>();
        foreach (var f in TandavaArena.Flora)
            for (int k = 0; k < f.Count; k++)
                plants.Add(new Plant { Id = plants.Count, Element = f.Element, Prisms = f.Prisms, Leaf = f.Leaf,
                                       At = PlantSite(rng, plants), Store = f.Prisms * f.Leaf });
        return plants;
    }

    /// <summary>A scripted pilot: where it is and how it moves.</summary>
    sealed class Pilot { public Vector3 At, Vel; }

    sealed class Sim
    {
        public Bake B;
        public List<TandavaForm> Forms;
        public SwarmSortCore C;
        public TandavaDirectorCore D;
        public List<Plant> Plants;
        public readonly List<Pilot> Pilots = new();
        public readonly List<TandavaFood> Food = new();
        public readonly List<TandavaPilot> Sensed = new();
        public float Now, SinceBite = float.PositiveInfinity, SinceFed, Eaten, LastShed = -1e9f;
        public int Lost, BiteCursor, LastForm, NextPlantId = 1000;
        public Random Rng;
        // the log
        public readonly List<(float t, int form)> Commits = new();
        public int Meals, MealsBroken, LaidWhileFeeding, Sheds;
        public readonly int[] MealEnds = new int[5];
        public float MealSeconds, MealVolumeEaten, MealStart, MealEatenAtStart;
        public readonly int[] FormMeals = new int[4], CoilMeals = new int[3];
        public readonly float[] FormMealSeconds = new float[4], FormMealVolume = new float[4];
        public int MealForm, MealCoil = -1, CoilRepeats, SerpentStrikeMeals;
        public float MaxRadius, RiseAt = -1f, DanceAt = -1f;
        public Vector3 RiseAnchor;
        // the halo, placed when the drum starts
        public Vector3[] Post;
        public int GuardedSamples, HaloSamples;
    }

    static Sim MakeSim(Bake b, int[] picks, int seed)
    {
        var s = new Sim { B = b, Rng = new Random(seed) };
        s.Forms = BuildForms(b, picks);
        s.C = MakeCore(b, s.Forms[0].PlanIndex, TandavaArena.Hatch, seed, fed: false);
        s.D = new TandavaDirectorCore(s.Forms, DirectorSettings(), seed);
        s.Plants = LayPlants(seed);
        s.LastForm = 0;
        return s;
    }

    static TandavaSwarmState State(Sim s)
    {
        var c = s.C;
        float fill = (c.Stomach[0] + c.Stomach[1] + c.Stomach[2] + c.Stomach[3]) / StomachCapacity;
        return new TandavaSwarmState
        {
            Alive = Active(c), Lost = s.Lost, Anchor = c.Anchor * UnitScale, Forward = c.BX, Up = c.BY, Side = c.BZ,
            Stomach0 = c.Stomach[0], Stomach1 = c.Stomach[1], Stomach2 = c.Stomach[2], Stomach3 = c.Stomach[3],
            StomachFill = fill, SinceBite = s.SinceBite, EatenTotal = s.Eaten,
            Starving = s.SinceFed >= StarvationSeconds && fill < ForageBelow,
        };
    }

    /// <summary>One 10 Hz tick, in the order SwarmFauna runs it: feed, the plants, the metabolism, the pilots, the
    /// director, the plan the director wants, the levers, the swim, the core step.</summary>
    static void Tick(Sim s, Action<Sim> pilots = null)
    {
        const float dt = 1f / TickHz;
        var c = s.C;
        s.Now += dt;
        // feed: BitersPerStep members asked round-robin, each biting a plant it touches
        bool bit = false;
        float fill0 = (c.Stomach[0] + c.Stomach[1] + c.Stomach[2] + c.Stomach[3]) / StomachCapacity;
        if (fill0 < 1f)
            for (int b = 0, tries = 0; b < BitersPerStep && tries < c.Cap; tries++)
            {
                s.BiteCursor = (s.BiteCursor + 1) % c.Cap;
                int i = s.BiteCursor;
                if (!c.Active[i]) continue;
                b++;
                var at = c.Pos[i] * UnitScale;
                foreach (var p in s.Plants)
                {
                    if (!p.Alive || p.Store < p.Leaf) continue;
                    if (Vector3.DistanceSquared(at, p.At) > (PlantRadius + BiteRadius) * (PlantRadius + BiteRadius)) continue;
                    p.Store -= p.Leaf; p.LastBite = s.Now;
                    c.Stomach[p.Element] += p.Leaf; s.Eaten += p.Leaf; bit = true;
                    break;
                }
            }
        s.SinceBite = bit ? 0f : s.SinceBite + dt;
        float fill = (c.Stomach[0] + c.Stomach[1] + c.Stomach[2] + c.Stomach[3]) / StomachCapacity;
        s.SinceFed = bit || fill >= 1f ? 0f : s.SinceFed + dt;
        // the plants: regrow when left alone, die eaten down, replant elsewhere later
        foreach (var p in s.Plants)
        {
            if (!p.Alive)
            {
                if (s.Now >= p.ReplantAt)
                {
                    p.Alive = true; p.At = PlantSite(s.Rng, s.Plants); p.Store = 6 * p.Leaf; p.LastBite = -1e9f; p.Id = s.NextPlantId++;
                }
                continue;
            }
            if (s.Now - p.LastBite > RegrowPause) p.Store = MathF.Min(p.Prisms * p.Leaf, p.Store + RegrowPrismsPerSecond * p.Leaf * dt);
            if (p.Store <= DiesAtPrisms * p.Leaf) { p.Alive = false; p.ReplantAt = s.Now + ReplantSeconds; }
        }
        // the metabolism: unfed and hungry, it sheds a member every interval (the core picks who)
        var st = State(s);
        if (st.Starving && s.Now - s.LastShed >= ShedIntervalSeconds)
        {
            s.LastShed = s.Now;
            int v = c.StarvationVictim();
            if (v >= 0) { c.Kill(v); s.Lost++; s.Sheds++; }
        }
        // the pilots act (move, kill)
        pilots?.Invoke(s);
        foreach (var p in s.Pilots) p.At += p.Vel * dt;
        // what the director is told: every plant it can eat (outside the nucleus), every pilot
        s.Food.Clear();
        foreach (var p in s.Plants)
            if (p.Alive && p.At.Length() > TandavaArena.NucleusRadius) s.Food.Add(new TandavaFood { Id = p.Id, Position = p.At, Volume = p.Store });
        s.Sensed.Clear();
        foreach (var p in s.Pilots) s.Sensed.Add(new TandavaPilot { Position = p.At, Velocity = p.Vel });
        st = State(s);
        var d = s.D;
        d.Tick(dt, st, s.Food, s.Sensed);
        foreach (var e in d.Events)
        {
            if (e.Kind == TandavaEventKind.FormCommitted) s.Commits.Add((s.Now, e.B));
            if (e.Kind == TandavaEventKind.FeedBegan)
            {
                s.Meals++; s.MealStart = s.Now; s.MealEatenAtStart = s.Eaten;
                if (e.B >= 0) { s.CoilMeals[e.B]++; if (d.FormIx == s.MealForm && e.B == s.MealCoil) s.CoilRepeats++; }
                else if (d.FormIx < 2) s.SerpentStrikeMeals++;
                s.MealForm = d.FormIx; s.MealCoil = e.B;
            }
            if (e.Kind == TandavaEventKind.FeedEnded)
            {
                s.MealSeconds += s.Now - s.MealStart; s.MealVolumeEaten += s.Eaten - s.MealEatenAtStart;
                s.FormMeals[d.FormIx]++; s.FormMealSeconds[d.FormIx] += s.Now - s.MealStart;
                s.FormMealVolume[d.FormIx] += s.Eaten - s.MealEatenAtStart;
            }
            if (e.Kind == TandavaEventKind.FeedEnded) { s.MealEnds[e.B]++; if (e.B == (int)TandavaMealEnd.Broken) s.MealsBroken++; }
            if (e.Kind == TandavaEventKind.FeedEnded && e.B == (int)TandavaMealEnd.Bare && Environment.GetEnvironmentVariable("TANDAVA_DIAG") == "1")
            {
                var pl = s.Plants.FirstOrDefault(x => x.Id == e.A);
                if (pl != null)
                {
                    float nearest = float.MaxValue;
                    for (int i = 0; i < c.Cap; i++) if (c.Active[i]) nearest = MathF.Min(nearest, Vector3.Distance(c.Pos[i] * UnitScale, pl.At));
                    var mouthW = c.Anchor * UnitScale + TandavaDirectorCore.InBody(st, d.Form.FeedMouth);
                    Console.WriteLine($"      bare at {s.Now:F0} s: {d.Form.Name} plan {Keys[c.PlanIx]}, plant r {pl.At.Length():F0} store {pl.Store / pl.Leaf:F0} prisms, " +
                                      $"mouth-plant {Vector3.Distance(mouthW, pl.At):F0} u, nearest member {nearest:F0} u, goal r {d.Goal.Length():F0}, mood {d.Mood}");
                }
            }
            if (e.Kind == TandavaEventKind.Rising) { s.RiseAt = s.Now; s.RiseAnchor = c.Anchor * UnitScale; }
            if (e.Kind == TandavaEventKind.DanceBegan)
            {
                s.DanceAt = s.Now;
                // TandavaController.PlaceHalo: the dance ground plus the plan's ring offset in the body axes of the moment
                var ring = s.B.Ring[Keys[d.Form.PlanIndex]];
                var centre = d.DancePoint + c.BX * ring.centre.X + c.BY * ring.centre.Y + c.BZ * ring.centre.Z;
                s.Post = new Vector3[HaloCount];
                for (int k = 0; k < HaloCount; k++)
                {
                    float th = 2f * MathF.PI * k / HaloCount;
                    s.Post[k] = centre + (c.BY * MathF.Cos(th) + c.BZ * MathF.Sin(th)) * ring.orbit;
                }
            }
        }
        d.Events.Clear();
        if (d.Outcome != TandavaOutcome.Running) return;
        // the plan the director wants (TandavaController.ApplyPlan): a new FORM is a new body (RequestPlan, the lay ease
        // restarts); the same form's other pose is a re-arrangement (RequestPose, the lay ease kept)
        if (c.PlanIx != d.WantPlan)
        {
            if (d.FormIx != s.LastForm) c.RequestPlan(d.WantPlan);
            else c.RequestPose(d.WantPlan);
        }
        s.LastForm = d.FormIx;
        c.SetLevers(d.CruiseScale, d.TurnScale, d.HoldLaying);
        c.SwimTarget = d.Goal / UnitScale;
        c.Step(ReadOnlySpan<SwarmPredator>.Empty);
        foreach (var e in c.Events) if (e.Kind == SwarmEventKind.Laid && d.Feeding) s.LaidWhileFeeding++;
        c.Events.Clear();
        if ((c.Anchor * UnitScale).Length() > s.MaxRadius && Environment.GetEnvironmentVariable("TANDAVA_DIAG") == "2" && (c.Anchor * UnitScale).Length() > 630f)
            Console.WriteLine($"      r {(c.Anchor * UnitScale).Length():F0} at {s.Now:F1} s: {Keys[c.PlanIx]} phase {d.Phase} mood {d.Mood} goal r {d.Goal.Length():F0} coil {d.Coil}");
        s.MaxRadius = MathF.Max(s.MaxRadius, (c.Anchor * UnitScale).Length());
        if (d.InDance && s.Post != null && (int)MathF.Round(s.Now * TickHz) % 10 == 0)
            for (int k = 0; k < HaloCount; k++) { if (d.HaloIsOut(k)) continue; s.HaloSamples++; if (Guarded(s, k)) s.GuardedSamples++; }
    }

    static bool Guarded(Sim s, int k)
    {
        var c = s.C; int n = 0; float r2 = GuardRadius * GuardRadius;
        for (int i = 0; i < c.Cap; i++)
            if (c.Active[i] && c.Hatched[i] && c.EffectiveElement(i) == GuardElement && Vector3.DistanceSquared(c.Pos[i] * UnitScale, s.Post[k]) <= r2) n++;
        return n >= GuardMembers;
    }

    static void RunFor(Sim s, float seconds, Action<Sim> pilots = null)
    {
        int steps = (int)(seconds * TickHz);
        for (int q = 0; q < steps && s.D.Outcome == TandavaOutcome.Running; q++) Tick(s, pilots);
    }

    static string MealsByForm(Sim s) =>
        string.Join(", ", Enumerable.Range(0, 4).Where(f => s.FormMeals[f] > 0)
                                    .Select(f => $"{FormNames[f]} {s.FormMealSeconds[f] / s.FormMeals[f]:F1} s x {s.FormMeals[f]}"));

    static string Timeline(Sim s) =>
        string.Join(", ", s.Commits.Select(x => $"{FormNames[x.form]} {x.t:F0} s")) +
        (s.RiseAt >= 0 ? $", rose {s.RiseAt:F0} s" : "") + (s.DanceAt >= 0 ? $", drum {s.DanceAt:F0} s" : "");

    static bool PilotsWon(TandavaOutcome o) =>
        o is TandavaOutcome.Wiped or TandavaOutcome.Starved or TandavaOutcome.Shattered or TandavaOutcome.DanceBroken or TandavaOutcome.HeldOff;

    // ──────────────────────────────────────────────────────────────── the tests

    public static int Run(SwarmPlanData[] basePlans, string dir)
    {
        var b = Load(dir);
        Console.WriteLine($"tandava: {b.Plans.Length} plans at density {Density}: " +
                          string.Join(" / ", Variants.Select(v => string.Join(",", v.Select(k => b.Plans[b.Ix[k]].N)))) +
                          $" members; stomach {StomachCapacity:F0}; a meal {MealVolume:F0}");

        // ── T1: four forms x three variants, poses that are the same body, forms that only grow
        Console.WriteLine("T1 the plans");
        {
            bool twins = true, mouths = true, rings = true;
            foreach (var key in Variants[0].Concat(Variants[1]).Concat(Variants[3]))
            {
                var poses = new[] { "_feed" }.Concat(Variants[3].Contains(key) ? Array.Empty<string>() : Coils.Select(c => "_" + c));
                foreach (var pose in poses)
                {
                    twins &= Mix(b.Plans[b.Ix[key]]).SequenceEqual(Mix(b.Plans[b.Ix[key + pose]]));
                    mouths &= b.Mouth.ContainsKey(key + pose);
                }
                mouths &= b.Mouth.ContainsKey(key);
            }
            foreach (var key in Variants[2]) rings &= b.Ring.ContainsKey(key);
            Check(b.Plans.Length == 39, "39 plans: 4 forms x 3 variants, a strike pose for each variant of the three forms that eat, " +
                                        "and three coils for each serpent");
            Check(twins, "every pose carries exactly its travel plan's element counts (a pose commit is a re-sort, never a molt)");
            Check(mouths && rings, "every eating plan bakes its mouth, every dance plan its halo");
            bool grows = true;
            for (int f = 1; f < 4; f++)
                grows &= Variants[f].Min(k => b.Plans[b.Ix[k]].N) >= Variants[f - 1].Max(k => b.Plans[b.Ix[k]].N);
            Check(grows, "every variant of a form is at least as big as every variant of the one before (a commit only grows the body)");
            bool danger = true;
            foreach (var key in Keys.Where(k => k.EndsWith("_feed") || Coils.Any(c => k.EndsWith("_" + c))))
            {
                var p = b.Plans[b.Ix[key]];
                int charge = Enumerable.Range(0, p.N).Count(u => p.Elem[u] == 0);
                danger &= charge > 0 && Enumerable.Range(0, p.N).Count(u => p.Elem[u] == 0 && p.Tier[u] == 1) == charge;
            }
            Check(danger, "every strike pose's and coil's plates are ALL danger tier: the protectors");
        }

        // ── T2: it hatches WHOLE as the Great Serpent - no young serpent
        Console.WriteLine("T2 it hatches whole");
        foreach (var key in Variants[0])
        {
            var c = MakeCore(b, b.Ix[key], TandavaArena.Hatch, 7, fed: false);
            int n0 = Active(c);
            Step(c, 150);
            float cov = Coverage(c, 3.2f);
            Check(c.PlanIx == b.Ix[key] && n0 == c.Plan.N && cov >= 0.6f,
                  $"{key}: seeded at its full {n0}/{c.Plan.N} members, still {key}, shape coverage {cov:P0} after 15 s (>= 60%)");
        }

        // ── T3: the levers - the cruise scale is the body's speed, and the turn carry keeps a long body whole round a turn
        Console.WriteLine("T3 the levers");
        {
            float Speed(float scale)
            {
                var c = MakeCore(b, b.Ix["great_serpent_1"], new Vector3(-900, 0, 0), 11, fed: false);
                Step(c, 60);
                c.SetLevers(scale, 1f, false);
                c.SwimTarget = c.Anchor + Vector3.UnitX * 2000f / UnitScale;
                Step(c, 30);
                var a0 = c.Anchor; Step(c, 50);
                return Vector3.Distance(c.Anchor, a0) * UnitScale / 5f;
            }
            float v1 = Speed(1f), v2 = Speed(2.1f);
            float cfg = Cruise * UnitScale * TickHz;
            Check(MathF.Abs(v1 - cfg) < 0.15f * cfg, $"calm, the body swims at {v1:F0} u/s (the config's {cfg:F0})");
            Check(MathF.Abs(v2 / v1 - 2.1f) < 0.25f, $"fleeing (x2.1) it swims at {v2:F0} u/s - {v2 / v1:F2}x");
            float Turned(float carry)
            {
                var c = MakeCore(b, b.Ix["great_serpent_2"], new Vector3(-600, 0, 0), 13, fed: false);
                c.C.TurnCarry = carry;
                c.SwimTarget = c.Anchor + Vector3.UnitX * 2000f / UnitScale;
                Step(c, 120);
                c.SetLevers(2.1f, 2.5f, false);
                c.SwimTarget = c.Anchor + Vector3.UnitZ * 2000f / UnitScale;   // a hard 90 degree turn, fleeing
                Step(c, 40);
                return Coverage(c, 3.2f);
            }
            float with = Turned(1f), without = Turned(0f);
            Check(with >= 0.6f && with > without,
                  $"round a hard fleeing turn the body keeps {with:P0} of its shape with the turn carry ({without:P0} without)");
        }

        // ── T4: a form, once taken, is REMEMBERED - a cut regrows into the same form, fast
        Console.WriteLine("T4 it remembers its form");
        {
            _selfDeaths = 0;
            var c = MakeCore(b, b.Ix["great_serpent_1"], new Vector3(-600, 0, 0), 17, fed: true);
            Step(c, 120);
            int full = Active(c), plan = c.PlanIx;
            float stomach0 = c.Stomach.Sum();
            int cut = Cut(c, (int)(0.3f * full), c.BX);   // the tail third
            int t = 0;
            while (Active(c) < 0.93f * full && t < 200) { Step(c); t++; }
            Step(c, 100);
            float cov = Coverage(c, 3.2f);
            Check(t < 60, $"the tail cut off ({cut} of {full}) grows back to 93% in {t / TickHz:F1} s (< 6 s)");
            Check(c.PlanIx == plan && _selfDeaths == 0, $"still the same form, and nothing it did not lose died ({_selfDeaths})");
            Check(cov >= 0.6f, $"and it is the same animal again: shape coverage {cov:P0} 10 s later");
            Check(c.Stomach.Sum() < stomach0 - 0.5f * cut * EggVolume.Min(), $"the regrowth was PAID from the stomach ({stomach0 - c.Stomach.Sum():F0} volume)");
        }

        // ── T5: the laying hold (the director holds it while the swarm feeds): a cut sticks until it lets go
        Console.WriteLine("T5 the laying hold");
        {
            var c = MakeCore(b, b.Ix["great_serpent_3"], new Vector3(-600, 0, 0), 19, fed: true);
            Step(c, 120);
            c.SetLevers(1f, 1f, true);
            int full = Active(c);
            Cut(c, (int)(0.2f * full), c.BX);
            int held0 = Active(c);
            Step(c, 50);
            Check(Active(c) == held0, $"held, a 20% cut stays cut for 5 s ({Active(c)}/{full})");
            c.SetLevers(1f, 1f, false);
            int t = 0;
            while (Active(c) < 0.93f * full && t < 200) { Step(c); t++; }
            Check(t < 60, $"let go, it grows back in {t / TickHz:F1} s");
        }

        // ── T6: the feed pose - the plates go out to orbit the mouth, as DANGER plates; the lay ease is kept
        Console.WriteLine("T6 the feed pose");
        foreach (var key in new[] { "great_serpent_2", "many_headed_10", "sea_lion_1" })
        {
            var c = MakeCore(b, b.Ix[key], new Vector3(-500, 0, 0), 23, fed: true);
            Step(c, 200);
            var settings = new SwarmTickSettings
            {
                UnitScale = UnitScale, PrismScale = 1f, PlanDanger = true,
                DefaultHalf = new[] { Vector3.One, Vector3.One, Vector3.One, Vector3.One },
            };
            var job = new SwarmTickJob(c, settings, TickHz) { SwimTarget = c.SwimTarget };
            job.Prime();
            float ease = c.LayEase;
            job.RequestPose(b.Ix[key + "_feed"]);
            for (int q = 0; q < 100; q++) { job.Kick(true); job.Collect(); }
            var mouth = b.Mouth[key + "_feed"] / UnitScale;   // sim voxels, in the plan's own (centred) frame
            var m = c.Anchor + c.BX * mouth.X + c.BY * mouth.Y + c.BZ * mouth.Z;
            // the guard ring's radius, read off the feed plan itself: its plates' mean distance from its mouth
            var fp = b.Plans[b.Ix[key + "_feed"]];
            var ringR = Enumerable.Range(0, fp.N).Where(u => fp.Elem[u] == 0).Average(u => Vector3.Distance(fp.P[0][u], mouth));
            int charge = 0, near = 0, danger = 0;
            for (int i = 0; i < c.Cap; i++)
            {
                if (!c.Active[i] || c.EffectiveElement(i) != 0) continue;
                charge++;
                if (MathF.Abs(Vector3.Distance(c.Pos[i], m) - (float)ringR) <= 0.3f * (float)ringR + 3f) near++;
                if (job.Instances[i].Tier == 1) danger++;
            }
            Check(c.PlanIx == b.Ix[key + "_feed"] && c.LayEase >= ease - 1e-3f,
                  $"{key}: the pose commit keeps the lay ease ({ease:F2} -> {c.LayEase:F2}) - feeding is not a wound");
            Check(near >= 0.7f * charge, $"{key}: {near}/{charge} plates are on the guard ring (r {ringR * UnitScale:F0} u) round the mouth after 10 s");
            Check(danger >= 0.8f * charge, $"{key}: {danger}/{charge} of them wear DANGER plates (PlanDanger: the plan's tier, not a startle)");
            // and a census swarm never reads its plan's tier-1 marks: the same core behind a job without PlanDanger
            var quiet = new SwarmTickJob(c, new SwarmTickSettings
            {
                UnitScale = UnitScale, PrismScale = 1f,
                DefaultHalf = new[] { Vector3.One, Vector3.One, Vector3.One, Vector3.One },
            }, TickHz) { SwimTarget = c.SwimTarget };
            quiet.Prime();
            int quietDanger = Enumerable.Range(0, c.Cap).Count(i => c.Active[i] && quiet.Instances[i].Tier == 1);
            Check(quietDanger == 0, $"{key}: without PlanDanger (every shipped swarm) the same plates show no danger ({quietDanger})");
        }

        // ── T7: a free run - nobody stops it: through all four forms and its last feast, inside the cell
        Console.WriteLine("T7 a free run");
        foreach (var (picks, seed) in new[] { (new[] { 0, 0, 0, 0 }, 31), (new[] { 1, 2, 1, 2 }, 37), (new[] { 2, 1, 2, 1 }, 41) })
        {
            var s = MakeSim(b, picks, seed);
            RunFor(s, RunCap);
            var d = s.D;
            string tag = string.Join("", picks);
            Console.WriteLine($"    picks {tag}: {d.Outcome} at {d.Clock:F0} s - {Timeline(s)}; {s.Meals} meals " +
                              $"(fed/bare/full/broken/long {string.Join("/", s.MealEnds)}, {s.MealSeconds / Math.Max(1, s.Meals):F1} s and " +
                              $"{s.MealVolumeEaten / Math.Max(1f, s.MealSeconds):F0} volume/s a meal), max radius {s.MaxRadius:F0}, ate {s.Eaten:F0}");
            Console.WriteLine($"      a meal by form {MealsByForm(s)}; coils/wraps/eights {string.Join("/", s.CoilMeals)}");
            Check(d.Outcome == TandavaOutcome.Completed && s.Commits.Select(x => x.form).SequenceEqual(new[] { 1, 2, 3 }),
                  $"picks {tag}: Great Serpent -> Many-Headed -> Lord of the Dance -> Sea Lion -> the cycle completes");
            Check(d.Clock < 200f, $"unopposed it completes in {d.Clock:F0} s (< 200 s): the forms come fast, so the pilots must stop it, not wait it out");
            Check(s.Commits.Count == 3 && s.Commits[0].t < 60f, $"its first change comes at {s.Commits[0].t:F0} s (< 60 s)");
            Check(s.LaidWhileFeeding == 0, "it never laid an egg while it ate (the hold: it cannot heal at the table)");
            float roam = MathF.Max(DirectorSettings().RoamRadius, s.Forms.Max(f => f.CoilRoamRadius));   // rolled up, it reaches further
            Check(s.MaxRadius <= roam + 40f, $"it never left the cell (max radius {s.MaxRadius:F0} of {roam:F0})");
            if (s.RiseAt >= 0f)
                Check(Vector3.Distance(s.RiseAnchor, d.DancePoint) <= DirectorSettings().DanceReach + 20f,
                      $"it rose where it stood ({Vector3.Distance(s.RiseAnchor, d.DancePoint):F0} u from the dance ground)");
            if (seed == 31) Dump(s.C, "free_run_end");
        }

        // ── T8: threat and mood - HEALTHY, a pilot coming at it is LUNGED at; HURT, it bolts AWAY, faster; alone it calms
        Console.WriteLine("T8 threat and mood");
        foreach (bool hurt in new[] { false, true })
        {
            var s = MakeSim(b, new[] { 0, 0, 0, 0 }, 43);
            s.C.Stomach[1] = 0.15f * StomachCapacity;   // fed once: the test is the mood, not the hatchling
            RunFor(s, 12f);
            var c = s.C;
            if (hurt) { int cut = Cut(c, (int)(0.4f * Active(c)), c.BX); s.Lost += cut; Array.Clear(c.Stomach); s.Plants.Clear(); }   // and nothing to regrow from, nor to eat
            var pilot = new Pilot { At = c.Anchor * UnitScale + Vector3.Normalize(new Vector3(0.3f, 0.2f, 1f)) * 600f };
            s.Pilots.Add(pilot);
            float wary = -1f, flee = -1f, lunge = -1f, fast = 0f; bool away = true, toward = true; int awaySamples = 0, towardSamples = 0; string why = null;
            for (int q = 0; q < 150 && s.D.Outcome == TandavaOutcome.Running; q++)
            {
                var a0 = c.Anchor;
                Tick(s, x =>
                {
                    var to = x.C.Anchor * UnitScale - pilot.At; float dd = to.Length();
                    pilot.Vel = dd > 200f ? to / dd * 110f : Vector3.Zero;   // it comes in, and sits 200 u off the body
                });
                var me = c.Anchor * UnitScale;
                if (wary < 0 && s.D.Mood >= TandavaMood.Wary) wary = s.Now;
                if (s.D.Mood == TandavaMood.Fleeing)
                {
                    if (flee < 0) flee = s.Now;
                    away &= Vector3.Dot(s.D.Goal - me, pilot.At - me) < 0f; awaySamples++;
                }
                if (s.D.Mood == TandavaMood.Lunging)
                {
                    if (lunge < 0) lunge = s.Now;
                    // it aims its JAWS (the feed pose's mouth) at the pilot, led by the pilot's velocity
                    var jawsAim = s.D.Goal + TandavaDirectorCore.InBody(State(s), s.D.Form.FeedMouth);
                    bool at = Vector3.Distance(jawsAim, pilot.At) < 80f, bared = c.PlanIx == s.D.Form.FeedPlanIndex || s.Now - lunge <= 0.3f;
                    if ((!at || !bared) && why == null) why = $" (at {s.Now:F1} s: jaws aimed {Vector3.Distance(jawsAim, pilot.At):F0} u from it, plan {Keys[c.PlanIx]}, pilot {Vector3.Distance(pilot.At, me):F0} u off)";
                    toward &= at && bared; towardSamples++;
                }
                fast = MathF.Max(fast, Vector3.Distance(c.Anchor, a0) * UnitScale * TickHz);
            }
            if (!hurt)
            {
                Check(lunge >= 0f && towardSamples > 0 && toward, $"healthy, it LUNGED at the pilot at {lunge:F1} s, guards out, jaws always aimed at it{why}");
                Check(fast >= 2f * Cruise * UnitScale * TickHz, $"lunging, it swims at up to {fast:F0} u/s");
                Check(flee < 0f, "and it never fled");
            }
            else
            {
                Check(flee >= 0f && awaySamples > 0 && away, $"hurt (40% cut), it bolted at {flee:F1} s, always AWAY from the pilot");
                Check(lunge < 0f, "hurt, it never lunged");
                Check(fast >= 1.6f * Cruise * UnitScale * TickHz, $"fleeing, it swims at up to {fast:F0} u/s");
                float left = s.Now;
                pilot.Vel = Vector3.Zero; pilot.At = new Vector3(5000f, 0f, 0f);   // the pilot goes away
                float calm = -1f;
                for (int q = 0; q < 300 && calm < 0f && s.D.Outcome == TandavaOutcome.Running; q++)
                {
                    Tick(s);
                    if (s.D.Mood == TandavaMood.Calm) calm = s.Now - left;
                }
                Check(calm >= 0f && calm < 20f, $"alone again, it calmed in {calm:F1} s");
            }
        }

        // ── T9: where it chooses to eat - away from a pilot parked at a plant
        Console.WriteLine("T9 where it eats");
        {
            var forms = BuildForms(b, new[] { 0, 0, 0, 0 });
            var st = new TandavaSwarmState { Alive = forms[0].PlanCount, Anchor = Vector3.Zero, Forward = Vector3.UnitX, Up = Vector3.UnitY, Side = Vector3.UnitZ };
            var food = new List<TandavaFood>
            {
                new() { Id = 1, Position = new Vector3(0, 0, 400), Volume = 4000 },
                new() { Id = 2, Position = new Vector3(0, 0, -420), Volume = 4000 },
            };
            int Pick(List<TandavaPilot> pilots)
            {
                var d = new TandavaDirectorCore(forms, DirectorSettings(), 1);
                for (int q = 0; q < 5; q++) d.Tick(0.1f, st, food, pilots);
                return d.TargetFood;
            }
            int alone = Pick(new List<TandavaPilot>());
            int guarded = Pick(new List<TandavaPilot> { new() { Position = new Vector3(0, 0, 440) } });
            Check(alone == 1, $"alone it goes to the nearer plant (food {alone})");
            Check(guarded == 2, $"with a pilot parked by the nearer plant it goes to the far one (food {guarded})");
        }

        // ── T10: the broken meal - hurt at the table it bolts; it did not heal while it ate, and it regrows after
        Console.WriteLine("T10 the broken meal");
        {
            var s = MakeSim(b, new[] { 1, 1, 1, 1 }, 47);
            s.C.Stomach[1] = 0.25f * StomachCapacity;   // it has eaten before: the test is the meal, not the hatchling
            while (!s.D.Feeding && s.D.Outcome == TandavaOutcome.Running && s.Now < 120f) Tick(s);
            Check(s.D.Feeding, $"it reached a plant and began to eat at {s.Now:F0} s");
            float began = s.Now;
            int plan = s.D.Form.PlanCount;
            int full = Active(s.C);
            Tick(s); Tick(s);
            int target = (int)(0.22f * plan), killed = 0;
            float brokenAt = -1f;
            s.Pilots.Add(new Pilot { At = s.C.Anchor * UnitScale - s.C.BX * 60f });
            for (int q = 0; q < 40 && brokenAt < 0f; q++)
            {
                Tick(s, x =>
                {
                    if (killed >= target) return;
                    // strike the BODY, not the head: cut from the tail, sparing the plates round the mouth
                    int n = Cut(x.C, Math.Min(12, target - killed), x.C.BX, i => x.C.EffectiveElement(i) == 0);
                    killed += n; x.Lost += n;
                });
                if (!s.D.Feeding) brokenAt = s.Now;
            }
            Check(brokenAt > 0f && s.MealsBroken == 1 && s.D.Mood == TandavaMood.Fleeing,
                  $"cut by {killed} ({killed / (float)plan:P0}) mid-meal, the meal broke {brokenAt - began:F1} s in and it bolted");
            Check(s.LaidWhileFeeding == 0, "it laid nothing while it ate");
            float t0 = s.Now;
            s.Pilots.Clear();
            while (Active(s.C) < 0.9f * full && s.Now - t0 < 30f) Tick(s);
            Check(Active(s.C) >= 0.9f * full, $"away from the table it regrew to 90% in {s.Now - t0:F1} s - the same form, remembered");
        }

        // ── T11: denial - break every meal and it starves to pieces (there is no clock to run out)
        Console.WriteLine("T11 denial");
        {
            var s = MakeSim(b, new[] { 0, 1, 2, 0 }, 53);
            float strikeAt = -1f; int killedThisMeal = 0;
            RunFor(s, RunCap, x =>
            {
                if (!x.D.Feeding || x.Now < 25f) { strikeAt = -1f; killedThisMeal = 0; return; }   // they spawn across the cell
                if (strikeAt < 0f) strikeAt = x.Now + 3f;   // the pilots arrive three seconds into every meal
                if (x.Now < strikeAt) return;
                int target = (int)(0.21f * x.D.Form.PlanCount);
                if (killedThisMeal >= target) return;
                int n = Cut(x.C, Math.Min(12, target - killedThisMeal), x.C.BX, i => x.C.EffectiveElement(i) == 0);
                killedThisMeal += n; x.Lost += n;
            });
            var d = s.D;
            Console.WriteLine($"    {d.Outcome} at {d.Clock:F0} s as the {d.Form.Name}: {s.Meals} meals, {s.MealsBroken} broken; {Timeline(s)}");
            Check(PilotsWon(d.Outcome), $"every meal broken, the pilots win ({d.Outcome})");
            Check(d.FormIx <= 1, $"it never reached the dance ({d.Form.Name})");
        }

        // ── T12: the dance - it rises in place; break the halo and the dance is broken; let the drum end and it is the Sea Lion
        Console.WriteLine("T12 the dance");
        foreach (bool pilotsThread in new[] { false, true })
        {
            var s = MakeSim(b, new[] { 2, 2, pilotsThread ? 1 : 0, 0 }, pilotsThread ? 59 : 61);
            s.C.Stomach[1] = 0.95f * StomachCapacity;   // a stuffed stomach: it banks its way straight up to the dance
            for (int e = 0; e < 4; e++) if (e != 1) s.C.Stomach[e] = 0.05f * StomachCapacity;
            float nextTry = 0f; int tries = 0, repelled = 0;
            RunFor(s, 260f, x =>
            {
                if (!pilotsThread || !x.D.InDance || x.Post == null || x.Now < nextTry) return;
                nextTry = x.Now + 1.5f;
                var open = Enumerable.Range(0, HaloCount).Where(k => !x.D.HaloIsOut(k)).ToList();
                if (open.Count == 0) return;
                int k = open[x.Rng.Next(open.Count)];
                tries++;
                if (Guarded(x, k)) repelled++;
                else x.D.BreakHalo(k);
            });
            var d = s.D;
            float guarded = s.HaloSamples > 0 ? s.GuardedSamples / (float)s.HaloSamples : 0f;
            Console.WriteLine($"    {(pilotsThread ? "pilots threading" : "nobody threading")}: {d.Outcome} at {d.Clock:F0} s - {Timeline(s)}; " +
                              $"halo {d.HaloOut}/{HaloToBreak} ({tries} tries, {repelled} held by the attendants), guarded {guarded:P0} of the time");
            Check(s.RiseAt >= 0f && s.DanceAt - s.RiseAt >= DirectorSettings().RiseSeconds - 0.2f,
                  $"it rose into the Lord of the Dance and the halo lit {s.DanceAt - s.RiseAt:F1} s later (the figure assembles first)");
            if (pilotsThread) Check(d.Outcome == TandavaOutcome.DanceBroken, $"nine rings broken before the drum stops: the dance is broken ({d.Outcome})");
            else Check(s.Commits.Any(x => x.form == 3) && s.Commits.Last(x => x.form == 3).t - s.DanceAt >= DirectorSettings().DrumSeconds - 0.2f,
                       "nobody threads: when the drum stops it is the Sea Lion");
            Check(guarded > 0.05f, $"the attendant packs guard the halo some of the time ({guarded:P0})");
        }

        // ── T13: the shatter - the pilots' strike: a body cut below a third of its form, once armed
        Console.WriteLine("T13 the shatter");
        {
            var forms = BuildForms(b, new[] { 0, 0, 0, 0 });
            int n = forms[0].PlanCount;
            TandavaOutcome After(int alive, bool starving)
            {
                var d = new TandavaDirectorCore(forms, DirectorSettings(), 1);
                var st = new TandavaSwarmState { Alive = n, Anchor = Vector3.Zero, Forward = Vector3.UnitX, Up = Vector3.UnitY, Side = Vector3.UnitZ };
                d.Tick(0.1f, st, null, null);
                st.Alive = alive; st.Starving = starving;
                d.Tick(0.1f, st, null, null);
                return d.Outcome;
            }
            Check(After((int)(0.3f * n), false) == TandavaOutcome.Shattered, "cut to 30% of its body: shattered");
            Check(After((int)(0.3f * n), true) == TandavaOutcome.Starved, "starving and down to 30%: starved");
            Check(After((int)(0.5f * n), false) == TandavaOutcome.Running, "at half its body it fights on (it regrows if it can eat)");
            Check(After(0, false) == TandavaOutcome.Wiped, "every member dead: wiped out");
        }

        // ── T14: every match draws its own animals
        Console.WriteLine("T14 the variants");
        {
            var seen = new int[4, 3];
            bool round = true;
            for (int seed = 1; seed <= 300; seed++)
            {
                var picks = TandavaDirectorCore.DrawVariants(seed, new[] { 3, 3, 3, 3 });
                for (int f = 0; f < 4; f++) seen[f, picks[f]]++;
                int packed = TandavaDirectorCore.Pack(picks);
                for (int f = 0; f < 4; f++) round &= TandavaDirectorCore.Unpack(packed, f) == picks[f];
            }
            bool all = true;
            for (int f = 0; f < 4; f++) for (int v = 0; v < 3; v++) all &= seen[f, v] > 40;
            Check(all, "over 300 matches every variant of every form is drawn (each at least 40 times)");
            Check(round, "the picks pack into one int and back (replicated once)");
            Check(TandavaDirectorCore.DrawVariants(99, new[] { 3, 3, 3, 3 }).SequenceEqual(TandavaDirectorCore.DrawVariants(99, new[] { 3, 3, 3, 3 })),
                  "the same seed draws the same animals (every peer agrees)");
        }

        // ── T15: no clock - the hunt ends when the creature is broken or its cycle is complete, never on time
        Console.WriteLine("T15 no clock");
        {
            Check(DirectorSettings().MatchSeconds == 0f, "the shipped settings carry no match clock (MatchSeconds 0)");
            var forms = BuildForms(b, new[] { 0, 0, 0, 0 });
            var d = new TandavaDirectorCore(forms, DirectorSettings(), 1);
            var st = new TandavaSwarmState { Alive = forms[0].PlanCount, Anchor = Vector3.Zero, Forward = Vector3.UnitX, Up = Vector3.UnitY, Side = Vector3.UnitZ };
            for (int q = 0; q < 12000; q++) d.Tick(0.1f, st, null, null);
            Check(d.Outcome == TandavaOutcome.Running && d.TimeRemaining < 0f, $"twenty minutes with nothing to eat and nobody hunting: still running ({d.Outcome})");
        }

        // ── T16: a form change READS quickly - the re-sort into the new body (SortVMaxScale, SortWellClip)
        Console.WriteLine("T16 the form change");
        {
            float Reform(bool quick)
            {
                var c = MakeCore(b, b.Ix["great_serpent_1"], new Vector3(-300, 0, 0), 29, fed: true);
                if (!quick) { c.C.WellClip = 0.52463809f; for (int e = 0; e < 4; e++) c.C.VMax[e] /= SortVMaxScale; }
                Step(c, 80);
                c.Stomach[1] = 0.9f * StomachCapacity;
                c.RequestPlan(b.Ix["many_headed_7"]);
                for (int t = 1; t <= 300; t++) { Step(c); if (t % 5 == 0 && Coverage(c, 3.2f) >= 0.6f) return t / TickHz; }
                return 30f;
            }
            float quickT = Reform(true), slowT = Reform(false);
            Check(quickT <= 4f && quickT < 0.6f * slowT,
                  $"Great Serpent -> Seven-Headed: the new body reads (60% coverage) in {quickT:F1} s ({slowT:F1} s at the research's member speeds)");
        }

        // ── T17: aggression does not starve it - a pilot loitering in lunge range is charged, and between charges it eats
        Console.WriteLine("T17 pressed, it still eats");
        {
            var s = MakeSim(b, new[] { 0, 0, 0, 0 }, 67);
            var pilot = new Pilot { At = TandavaArena.Hatch + new Vector3(0f, 250f, 0f) };
            s.Pilots.Add(pilot);
            int lunges = 0; var was = TandavaMood.Calm;
            RunFor(s, 120f, x =>
            {
                var me = x.C.Anchor * UnitScale;
                var want = me + Vector3.Normalize(pilot.At - me + new Vector3(0f, 1f, 0f)) * 250f;   // it hangs 250 u off the body
                var to = want - pilot.At; float dd = to.Length();
                pilot.Vel = dd > 5f ? to / dd * MathF.Min(150f, dd * 4f) : Vector3.Zero;
                if (x.D.Mood == TandavaMood.Lunging && was != TandavaMood.Lunging) lunges++;
                was = x.D.Mood;
            });
            Check(lunges >= 3 && s.Meals >= 2,
                  $"a pilot hanging 250 u off it for two minutes was charged {lunges} times, and it still ate {s.Meals} meals (form {s.D.Form.Name})");
        }

        // ── T18: a serpent eats ROLLED UP - a formation drawn per meal, never the same one twice running, and the body
        // round the plant eats faster than the strike pose's head did (the prompter, 2026-10-06: "make the serpent roll up
        // like a snake in different formations when eating ... it takes a long time to eat")
        Console.WriteLine("T18 it eats rolled up");
        {
            var runs = new[] { (new[] { 0, 0, 0, 0 }, 31), (new[] { 1, 2, 1, 2 }, 37), (new[] { 2, 1, 2, 1 }, 41) };
            var secs = new float[2, 2]; var vol = new float[2, 2]; var meals = new int[2, 2];
            var used = new int[3]; int repeats = 0, strike = 0; float clockOn = 0f, clockOff = 0f;
            bool was = NoCoils;
            for (int mode = 0; mode < 2; mode++)
            {
                NoCoils = mode == 1;
                foreach (var (picks, seed) in runs)
                {
                    var s = MakeSim(b, picks, seed);
                    RunFor(s, RunCap);
                    for (int f = 0; f < 2; f++) { secs[mode, f] += s.FormMealSeconds[f]; vol[mode, f] += s.FormMealVolume[f]; meals[mode, f] += s.FormMeals[f]; }
                    if (mode == 0) { for (int k = 0; k < 3; k++) used[k] += s.CoilMeals[k]; repeats += s.CoilRepeats; strike += s.SerpentStrikeMeals; clockOn += s.D.Clock; }
                    else clockOff += s.D.Clock;
                }
            }
            NoCoils = was;
            for (int f = 0; f < 2; f++)
                Console.WriteLine($"    {FormNames[f]}: rolled up {secs[0, f] / Math.Max(1, meals[0, f]):F1} s a meal at {vol[0, f] / Math.Max(1f, secs[0, f]):F0} volume/s; " +
                                  $"in the strike pose {secs[1, f] / Math.Max(1, meals[1, f]):F1} s at {vol[1, f] / Math.Max(1f, secs[1, f]):F0} volume/s");
            Console.WriteLine($"    free runs {clockOn / runs.Length:F0} s rolled up, {clockOff / runs.Length:F0} s in the strike pose");
            Check(strike == 0 && used.Sum() == meals[0, 0] + meals[0, 1],
                  $"every serpent meal rolled up ({used.Sum()} of {meals[0, 0] + meals[0, 1]}; {strike} in the strike pose)");
            Check(used.All(n => n > 0), $"in every formation: coil {used[0]}, wrap {used[1]}, eight {used[2]}");
            Check(repeats == 0, $"never the same formation twice running ({repeats} repeats)");
            float rateOn = vol[0, 0] / Math.Max(1f, secs[0, 0]), rateOff = vol[1, 0] / Math.Max(1f, secs[1, 0]);
            Check(rateOn >= 2f * rateOff, $"the Great Serpent coiled round its plant eats {rateOn / Math.Max(1f, rateOff):F1}x as fast as its strike pose (>= 2x)");
            float mealOn = secs[0, 0] / Math.Max(1, meals[0, 0]), mealOff = secs[1, 0] / Math.Max(1, meals[1, 0]);
            Check(mealOn <= 6f && mealOn < mealOff, $"a Great Serpent meal takes {mealOn:F1} s (<= 6 s; {mealOff:F1} s in the strike pose)");
        }

        Console.WriteLine(_fail == 0 ? "\ntandava: OK" : $"\ntandava: {_fail} FAILED");
        return _fail;
    }
}
