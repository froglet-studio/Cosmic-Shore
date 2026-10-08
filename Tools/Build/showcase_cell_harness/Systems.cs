// The showcase cell's creature systems (Docs/SWARM_FAUNA.md §26). Each wraps a SHIPPED pure core (compiled from
// Assets/ by run.sh) in a small stand-in for its Unity glue, mirroring the glue's rules line for line where they
// touch another system: what is food (flora tissue only, never shielded, inside the eater's band), what is loot
// (loose mass only - never living tissue, grove tissue, built or carried prisms), how a death leaves its mass
// (a starved body stands as a skeleton; a rammed body explodes out of the cell), and how a vessel is sensed.
// The glue each method mirrors is named in its comment. Nothing here re-types a number: they all come from
// layout.json (layout.py reads them from the authored assets and the author scripts).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CosmicShore.Gameplay;

static class Ms
{
    public static double Since(long t0) => (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
}

// ══════════════════════════════════════════════════════════════════════════════════════════════ flora

/// <summary>The Swarm cell's Borromean feeding grounds: per region, plants at the floor, one plate per growPeriod up to
/// the region's canonical budget, an offspring per GrowthPerOffspring plates (cooldown) up to the region's cap. Plates
/// are the shared food every grazer eats from. Not modelled: the element time-law on the grow period, plant death.</summary>
sealed class FloraSystem : ICellSystem, IOccupancy
{
    public string Name => "flora";
    public double StepHz => 10;
    public double LastMs { get; private set; }
    readonly float _growPeriod;
    readonly List<(int floor, int cap, float inner, float outer, int food, float leaf, int budget, float perOffspring, float cooldown)> _reg = new();
    readonly List<int> _sinceBirth = new();
    /// <summary>Per region: its planting pens (axis, cos of the half-angle, inner, outer) - FloraConfigurationSO.PlantingPens.</summary>
    readonly List<List<(Vector3 axis, float cos, float inner, float outer)>> _pens = new();

    public FloraSystem(Cell c)
    {
        _growPeriod = Cell.F(c.L, "grow_period");
        int ri = 0;
        foreach (var r in c.L.GetProperty("regions").EnumerateArray())
        {
            var band = r.GetProperty("flora_band");   // where the flora is planted (author_swarm_fauna.flora_band)
            var leaf = r.GetProperty("leaf");
            float lv = (float)(leaf[0].GetDouble() * leaf[1].GetDouble() * leaf[2].GetDouble());
            _reg.Add((r.GetProperty("floor").GetInt32(), r.GetProperty("cap").GetInt32(), (float)band[0].GetDouble(),
                      (float)band[1].GetDouble(), r.GetProperty("food").GetInt32(), lv, r.GetProperty("budget").GetInt32(),
                      Cell.F(r, "growth_per_offspring"), Cell.F(r, "reproduction_cooldown")));
            var pens = new List<(Vector3, float, float, float)>();
            foreach (var pen in r.GetProperty("pens").EnumerateArray())
            {
                var a = pen.GetProperty("axis");
                var ax = new Vector3((float)a[0].GetDouble(), (float)a[1].GetDouble(), (float)a[2].GetDouble());
                pens.Add((ax == Vector3.Zero ? ax : Vector3.Normalize(ax), MathF.Cos(Cell.F(pen, "half") * MathF.PI / 180f),
                          Cell.F(pen, "inner"), Cell.F(pen, "outer")));
            }
            _pens.Add(pens);
            for (int k = 0; k < _reg[ri].floor; k++) Plant(c, ri, DispersalPoint(c, ri), 0.5f);
            ri++;
        }
    }

    /// <summary>Flora.ResolveDispersalPoint with SpreadPlanting on (the Swarm cell's flora, author_swarm_fauna): the best
    /// of 8 random points - the farthest in direction from every living plant of the region - of the band, or, when the
    /// region has planting pens, of the pen holding the fewest of its living plants (ties at random).</summary>
    Vector3 DispersalPoint(Cell c, int ri)
    {
        var g = _reg[ri];
        int pen = EmptiestPen(c, ri);
        Vector3 best = default; float bestGap = -1f;
        for (int k = 0; k < 8; k++)
        {
            var p = pen >= 0 ? PenPoint(c, _pens[ri][pen]) : c.RandomInShell(g.inner + 15f, g.outer - 15f);
            float gap = float.MaxValue;
            var u = Vector3.Normalize(p);
            foreach (var pl in c.World.Plants) if (pl.Alive && pl.Region == ri) gap = MathF.Min(gap, Vector3.DistanceSquared(Vector3.Normalize(pl.Heart), u));
            if (gap > bestGap) { bestGap = gap; best = p; }
        }
        return best;
    }

    int EmptiestPen(Cell c, int ri)
    {
        var pens = _pens[ri];
        if (pens.Count == 0) return -1;
        int start = c.Rng.Range(0, pens.Count), best = -1, bestCount = int.MaxValue;
        for (int k = 0; k < pens.Count; k++)
        {
            int j = (start + k) % pens.Count, n = 0;
            foreach (var pl in c.World.Plants) if (pl.Alive && pl.Region == ri && InPen(pens[j], pl.Heart)) n++;
            if (n < bestCount) { bestCount = n; best = j; }
        }
        return best;
    }

    static bool InPen((Vector3 axis, float cos, float inner, float outer) pen, Vector3 p)
    {
        float r = p.Length();
        if (r < pen.inner || r > pen.outer) return false;
        return pen.axis == Vector3.Zero || r <= 0f || Vector3.Dot(p / r, pen.axis) >= pen.cos;
    }

    /// <summary>Flora.PenPoint: volume-uniform between the pen's radii (the harness's 15 u rim margin, as the band draw),
    /// uniform over its cone of directions.</summary>
    static Vector3 PenPoint(Cell c, (Vector3 axis, float cos, float inner, float outer) pen)
    {
        var p = c.RandomInShell(pen.inner + 15f, pen.outer - 15f);
        if (pen.axis == Vector3.Zero) return p;
        float r = p.Length();
        float cosT = 1f + (pen.cos - 1f) * c.Rng.NextFloat();
        float sinT = MathF.Sqrt(MathF.Max(0f, 1f - cosT * cosT));
        float phi = c.Rng.NextFloat() * 2f * MathF.PI;
        var u = Vector3.Normalize(Vector3.Cross(pen.axis, MathF.Abs(pen.axis.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
        var v = Vector3.Cross(pen.axis, u);
        return r * (cosT * pen.axis + sinT * (MathF.Cos(phi) * u + MathF.Sin(phi) * v));
    }

    void Plant(Cell c, int region, Vector3 at, float startFill)
    {
        var g = _reg[region];
        var pl = new Plant { Id = c.World.Plants.Count, Region = region, Element = g.food, Heart = at, Budget = g.budget,
                             NextGrow = c.T + _growPeriod * c.Rng.NextFloat(), LastBirth = c.T };
        c.World.Plants.Add(pl);
        _sinceBirth.Add(0);
        int n = (int)(g.budget * startFill);
        for (int k = 0; k < n; k++) c.World.GrowPlate(pl, g.leaf, PlateAt(c, pl));
    }

    static Vector3 PlateAt(Cell c, Plant pl) => pl.Heart + c.Rng.OnUnitSphere() * c.Rng.Range(6f, 30f);

    public int LivePlants(int region) => _count(region);
    int _count(int region) { int n = 0; foreach (var p in _plantsOf(region)) n++; return n; }
    IEnumerable<Plant> _plantsOf(int region) { foreach (var p in _world.Plants) if (p.Alive && p.Region == region) yield return p; }
    CellWorld _world = null!;

    public void Tick(Cell c)
    {
        long t0 = Stopwatch.GetTimestamp();
        _world = c.World;
        var plants = c.World.Plants;
        for (int i = 0; i < plants.Count; i++)
        {
            var pl = plants[i];
            if (!pl.Alive) continue;
            var g = _reg[pl.Region];
            while (pl.NextGrow <= c.T)
            {
                pl.NextGrow += _growPeriod;
                if (pl.Plates >= pl.Budget) continue;
                c.World.GrowPlate(pl, g.leaf, PlateAt(c, pl));
                _sinceBirth[i]++;
            }
            if (_sinceBirth[i] >= g.perOffspring && c.T - pl.LastBirth >= g.cooldown && _count(pl.Region) < g.cap)
            {
                _sinceBirth[i] = 0;
                pl.LastBirth = c.T;
                var at = pl.Heart + c.Rng.OnUnitSphere() * 81f;
                float r = Math.Clamp(at.Length(), g.inner + 10f, g.outer - 10f);
                Plant(c, pl.Region, Vector3.Normalize(at) * r, 0f);
            }
        }
        for (int ri = 0; ri < _reg.Count; ri++)
            while (_count(ri) < _reg[ri].floor) Plant(c, ri, DispersalPoint(c, ri), 0f);
        LastMs = Ms.Since(t0);
    }

    /// <summary>One always-on heart collider per live plant (author_swarm_fauna's "hearts").</summary>
    public int Colliders(Cell c) { int n = 0; foreach (var p in c.World.Plants) if (p.Alive) n++; return n; }

    public void Census(List<(string, int, int)> into)
    {
        for (int ri = 0; ri < _reg.Count; ri++) into.Add(($"plants/{RegionName(ri)}", _count(ri), _reg[ri].cap));
    }

    static string RegionName(int ri) => ri switch { 0 => "inner", 1 => "middle", _ => "outer" };

    /// <summary>C8: each planting pen, 1 when it holds a living plant of its region.</summary>
    public void Occupancy(Cell c, List<(string, int, int)> into)
    {
        for (int ri = 0; ri < _pens.Count; ri++)
            for (int k = 0; k < _pens[ri].Count; k++)
            {
                var pen = _pens[ri][k];
                bool any = c.World.Plants.Any(pl => pl.Alive && pl.Region == ri && InPen(pen, pl.Heart));
                into.Add(($"plant pen {RegionName(ri)}/{k}", any ? 1 : 0, 1));
            }
    }
    public void Strikers(Cell c, List<Striker> into) { }
    public double Held() => 0;
    public void Ledger(List<(string, double)> into) { }

    public void Snapshot(Utf8JsonWriter w)
    {
        w.WriteStartArray("plants");
        foreach (var p in _world.Plants)
        {
            if (!p.Alive) continue;
            w.WriteStartObject();
            w.WriteNumber("region", p.Region); w.WriteNumber("element", p.Element); w.WriteNumber("plates", p.Plates);
            Program.WriteVec(w, "at", p.Heart);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }
}

// ══════════════════════════════════════════════════════════════════════════════════════════════ swarms

/// <summary>The three Sort swarms (SwarmFauna.cs: BuildSortCore, BuildTickSettings, SenseVessels, Feed, ResolveGoal,
/// Starvation), each a SwarmTickJob run inline.</summary>
sealed class SwarmSystem : ICellSystem, IOccupancy
{
    sealed class One
    {
        public int Region;
        public float Inner, Outer, StomachCap;
        public SwarmTickJob Job = null!;
        public ProxyPool Proxies = null!;
        public Vector3 Goal;
        public bool Foraging;
        public Plant? GoalPlant;
        public float GoalSince, AtPlantSince = -1f, LastGoalBite = -1f, LastFed, LastShed = -1e9f, GoalBest = float.MaxValue, GoalProgressAt;
        public readonly Dictionary<Plant, float> Rested = new();
        public int BiteCursor;
        public readonly bool[] Gone;
        public readonly List<int> GoneSlots = new();
        public readonly int[] Hits;
        public int Rammed, Starved, PeakAlive;
        // round 11f (Docs/ECOLOGY_LOD.md §5): the swarm's macro body and its IMacroPopulation, as SwarmFauna builds them
        public SwarmMacroBody Macro = null!;
        public MacroSwarm Pop = null!;
        public long CollapsedTicks;
        // round 11-10: the spawner's extinction recovery (SwarmFauna.Extinction + RandomLifeSpawner)
        public int Start;
        public float ExtinctSince = -1f;
        public bool AnchorGone;
        public One(int cap) { Gone = new bool[cap]; Hits = new int[cap]; }
    }

    /// <summary>SwarmFauna's IMacroPopulation members, minus Unity: collapse only with nothing that needs individuals
    /// pending (no proxy, no kill waiting, no starvation shed due); a collapsed swarm drifts rigidly toward its goal at
    /// cruise and grazes BitersPerStep x TickHz x dt bites a macro tick into the same stomach.</summary>
    sealed class MacroSwarm : IMacroPopulation
    {
        readonly SwarmSystem _s; readonly One _o; readonly Cell _c;
        public MacroSwarm(SwarmSystem s, One o, Cell c) { _s = s; _o = o; _c = c; }
        public Vector3 MacroCentre => _o.Job.Anchor;
        public float MacroExtent => _s.BodyRadius(_o);
        public bool IsCollapsed => _o.Macro.Collapsed;
        public bool CanCollapse => !_o.Macro.Collapsed && _o.Job.Error == null && _o.Proxies.Count == 0 && _o.GoneSlots.Count == 0 && !_s.StarvationDue(_c, _o);
        public bool NeedsIndividuals => _o.Macro.Collapsed && (_o.Proxies.Count > 0 || _o.GoneSlots.Count > 0 || _s.StarvationDue(_c, _o));
        public MacroPopulationTotals Totals => _o.Macro.Totals;
        public bool Collapse() => CanCollapse && _o.Macro.TryCollapse();   // inline job: always Idle between ticks
        public void Expand() => _o.Macro.Expand();
        public void MacroTick(float dt)
        {
            if (!_o.Macro.Collapsed) return;
            _o.Macro.MacroTick(dt, _o.Goal, _s.CruiseWorld, _s._us);
            _s.Feed(_c, _o, Math.Max(1, (int)MathF.Round(_s._biters * _s._hz * dt)));
        }
    }

    public string Name => "swarm";
    public double StepHz => _hz;
    public double LastMs { get; private set; }
    readonly List<One> _sw = new();
    readonly JsonElement _cfg;
    readonly float _us, _hz, _bite, _vesselR, _senseMargin, _engage, _linger;
    readonly int _biters;
    readonly bool _lodOn;
    long _ticks;
    float BodyRadius(One o) => (_plans[Math.Clamp(o.Job.PlanIx, 0, 3)].Radius * 3f + 20f) * _us;
    float CruiseWorld => Cell.F(_cfg, "Cruise") * _us * _hz;
    bool StarvationDue(Cell c, One o) => Starving(c, o) && c.T - o.LastShed >= Cell.F(_cfg, "ShedIntervalSeconds");
    /// <summary>SwarmFauna.Starving (round 11-14): hungry (below ForageBelow) AND unfed StarvationSeconds - a sated swarm
    /// takes no bite, and on the clock alone it starved with a full stomach.</summary>
    bool Starving(Cell c, One o) => c.T - o.LastFed >= Cell.F(_cfg, "StarvationSeconds") && Fill(o) < Cell.F(_cfg, "ForageBelow");
    readonly SwarmPlanData[] _plans;
    readonly string[] _elemName = { "charge", "mass", "space", "time" };
    public readonly int Density;

    static float V4(JsonElement cfg, string k, int e) => (float)cfg.GetProperty(k)[e].GetDouble();

    public SwarmSystem(Cell c, SwarmPlanData[] plans, int density)
    {
        _cfg = c.L.GetProperty("swarm_config");
        _plans = plans;
        Density = density;
        _us = Cell.F(_cfg, "UnitScale"); _hz = Cell.F(_cfg, "TickHz");
        _bite = Cell.F(_cfg, "BiteRadius"); _biters = (int)Cell.F(_cfg, "BitersPerStep");
        _vesselR = Cell.F(_cfg, "VesselRadius"); _senseMargin = Cell.F(_cfg, "SenseMargin"); _engage = Cell.F(_cfg, "EngageRadius");
        _linger = Cell.F(_cfg, "ProxyLingerSeconds");
        // SwarmFaunaConfigSO.MacroLod (field default true when the asset has no key); SHOWCASE_LOD=0 runs the cell without it
        _lodOn = !_cfg.TryGetProperty("MacroLod", out _) || Cell.F(_cfg, "MacroLod") > 0.5f;
        _planCap = plans.Max(p => p.N);
        _membraneSim = c.Membrane * 0.97f / _us;
        int ri = 0;
        foreach (var r in c.L.GetProperty("regions").EnumerateArray())
        {
            var band = r.GetProperty("band");
            for (int s = 0; s < r.GetProperty("swarms").GetInt32(); s++)
            {
                var one = Hatch(c, ri, r.GetProperty("start").GetInt32(), (float)band[0].GetDouble(), (float)band[1].GetDouble(), c.Seed * 101 + ri * 7 + s);
                c.RegisterLod($"swarm/{Regions[ri]}", one.Pop, _lodOn);
                _sw.Add(one);
            }
            ri++;
        }
    }

    readonly int _planCap;
    readonly float _membraneSim;

    /// <summary>SwarmFauna.Seed: one swarm hatched in region <paramref name="ri"/>'s band (the constructor, and the
    /// spawner's re-hatch after an extinction).</summary>
    One Hatch(Cell c, int ri, int start, float inner, float outer, int seed)
    {
        var core = BuildSortCore(_planCap, _membraneSim, inner / _us, outer / _us, seed);
        var one = new One(core.Cap) { Region = ri, Inner = inner, Outer = outer, Start = start };
        // SwarmFauna.StomachCapacity: StomachEggs x Density x mean(EggVolume)
        float meanEgg = 0f; for (int e = 0; e < 4; e++) meanEgg += V4(_cfg, "EggVolume", e) * 0.25f;
        one.StomachCap = Cell.F(_cfg, "StomachEggs") * Density * meanEgg;
        var anchorW = HatchPoint(c, inner, outer);
        var tangent = Vector3.Cross(Vector3.Normalize(anchorW), c.Rng.OnUnitSphere());
        if (tangent.LengthSquared() < 1e-4f) tangent = Vector3.UnitX;
        core.Seed(start, (int)Cell.F(_cfg, "SeedMembers") * Density, anchorW / _us, Vector3.Normalize(tangent));
        core.SwimTarget = anchorW / _us;
        one.Job = new SwarmTickJob(core, TickSettings(), _hz) { SwimTarget = anchorW / _us };
        one.Job.Prime();
        one.Proxies = new ProxyPool((int)Cell.F(_cfg, "MaxProxies"), _linger);
        one.Goal = anchorW;
        one.LastFed = c.T;
        one.Macro = new SwarmMacroBody(one.Job);
        one.Pop = new MacroSwarm(this, one, c);
        return one;
    }

    /// <summary>SwarmFauna.Seed (round 11-10): the spawner's random point in the band (RandomPointInBand), moved to the
    /// living plant of the band nearest it when there is one.</summary>
    static Vector3 HatchPoint(Cell c, float inner, float outer)
    {
        var at = c.RandomInShell(inner + 20f, outer - 20f);
        Plant? best = null; float bd = float.MaxValue;
        foreach (var f in c.World.Plants)
        {
            if (!f.Alive) continue;
            float r = f.Heart.Length();
            if (r < inner || r > outer) continue;
            float d = Vector3.DistanceSquared(f.Heart, at);
            if (d < bd) { bd = d; best = f; }
        }
        return best != null ? best.Heart : at;
    }

    /// <summary>SwarmFauna.BuildSortCore, field for field.</summary>
    ISwarmCore BuildSortCore(int planCap, float membrane, float bandInner, float bandOuter, int seed)
    {
        var a = _cfg.GetProperty("SortAdhesion");
        var fp = _cfg.GetProperty("SortFramePeriod");
        var p = new SwarmSortParams
        {
            K = (int)Cell.F(_cfg, "SortWellsPerType"), PerWell = (int)Cell.F(_cfg, "SortUnitsPerWell"), CovScale = Cell.F(_cfg, "SortWellWidth"),
            KWell = Cell.F(_cfg, "SortWellGain"), WellClip = Cell.F(_cfg, "SortWellClip"),
            R0 = Cell.F(_cfg, "SortSpacing"), KRep = Cell.F(_cfg, "SortRepulsion"), RAdh = Cell.F(_cfg, "SortAdhesionRadius"),
            ASame = (float)a[0].GetDouble(), AElem = (float)a[1].GetDouble(), ARole = (float)a[2].GetDouble(), AOther = (float)a[3].GetDouble(),
            Swap = Cell.F(_cfg, "SortSwap"), RSwap = Cell.F(_cfg, "SortSwapRadius"),
            Inertia = Cell.F(_cfg, "SortInertia"), Noise = Cell.F(_cfg, "SortNoise"), KWellFF = Cell.F(_cfg, "SortFeedForward"),
            Dwell = (int)Cell.F(_cfg, "SortDwell"), LayRate = Cell.F(_cfg, "SortLayRate"), LayMax = (int)Cell.F(_cfg, "SortLayMax") * Density,
            PCross = Cell.F(_cfg, "SortCrossChance"), FillTol = Cell.F(_cfg, "SortFillTolerance"), Over = Cell.F(_cfg, "SortBodyFill"),
            Molt = true, Transfer = true, MoltRate = Cell.F(_cfg, "SortMoltRate"), MoltSteps = (int)Cell.F(_cfg, "SortMoltSteps"), MoltWindow = -1,
            KillLayHoldSteps = (int)MathF.Round(Cell.F(_cfg, "KillLayHoldSeconds") * _hz),
            Periods = new[]
            {
                Math.Max(1, (int)MathF.Round((float)fp[0].GetDouble())), Math.Max(1, (int)MathF.Round((float)fp[1].GetDouble())),
                Math.Max(1, (int)MathF.Round((float)fp[2].GetDouble())), Math.Max(1, (int)MathF.Round((float)fp[3].GetDouble())),
            },
            DomainSlots = Lineages, Lineages = Lineages, Drift = Cell.F(_cfg, "LineageDrift"), Funded = true, Animate = true, WellLook = true, Oriented = true,
            Cruise = Cell.F(_cfg, "Cruise"), Turn = Cell.F(_cfg, "TurnPerStep"),
            Membrane = membrane, CrossCost = Cell.F(_cfg, "CrossElementCost"), Cap = planCap,
            WellDead = Cell.F(_cfg, "SortWellDead"), WellDeadTime = Cell.F(_cfg, "SortWellDeadTime"),
            Wander = Cell.F(_cfg, "SortWander"), WanderTau = Cell.F(_cfg, "SortWanderTau"),
            Frac = Math.Max(1, (int)Cell.F(_cfg, "SortUpdateFraction")),
            // round 11d (§22): regrow from the wound, at a pace that eases back in
            BudAtWound = Cell.F(_cfg, "SortBudAtWound") > 0.5f, FateNear = Cell.F(_cfg, "SortFateNear") > 0.5f,
            LayRamp = (int)MathF.Round(Cell.F(_cfg, "SortLayRampSeconds") * _hz),
            ThreatGain = 3f * MathF.Pow(Density, 2f / 3f),
        };
        for (int e = 0; e < 4; e++) p.EggCost[e] = V4(_cfg, "EggVolume", e);
        p.BandInner = bandInner; p.BandOuter = bandOuter;
        return new SwarmSortCore(_plans, p, seed);
    }

    bool Lineages => Cell.F(_cfg, "MultiDomain") > 0.5f && Cell.F(_cfg, "Model") == 2f;

    /// <summary>SwarmFauna.BuildTickSettings (Bestiary / HuntEnter / LurkCalm / LocustPhaseSeconds are not in the asset:
    /// the SO's own field defaults apply, as Unity does for a missing key - SwarmFaunaConfigSO.cs).</summary>
    SwarmTickSettings TickSettings()
    {
        var half = TypicalHalfExtents(_plans);
        var s = new SwarmTickSettings
        {
            Centre = Vector3.Zero, UnitScale = _us, PrismScale = Cell.F(_cfg, "PrismScale"),
            HeartPrismGap = Cell.F(_cfg, "HeartPrismGap"),
            DangerEnter = Cell.F(_cfg, "DangerEnter"), DangerExit = Cell.F(_cfg, "DangerExit"),
            Bestiary = true, HuntEnter = 0.2f, LurkCalm = 0.05f,
            LocustPhaseTicks = Math.Max(1, (int)MathF.Round(2f * _hz)),
            EngageRadius = _engage, MaxEngaged = (int)Cell.F(_cfg, "MaxProxies"),
            MultiDomain = Lineages,
        };
        for (int e = 0; e < 4; e++) { s.HeartWorldScale[e] = V4(_cfg, "HeartWorldScale", e); s.DefaultHalf[e] = half[e]; }
        return s;
    }

    /// <summary>SwarmPlanLibrary.TypicalHalfExtents.</summary>
    static Vector3[] TypicalHalfExtents(SwarmPlanData[] plans)
    {
        var result = new Vector3[4];
        for (int e = 0; e < 4; e++)
        {
            var xs = new List<float>(); var ys = new List<float>(); var zs = new List<float>();
            foreach (var p in plans)
                for (int k = 0; k < p.N; k++)
                    if (p.Elem[k] == e) { xs.Add(p.Half[k].X); ys.Add(p.Half[k].Y); zs.Add(p.Half[k].Z); }
            result[e] = xs.Count == 0 ? new Vector3(1f, 0.6f, 0.6f) : new Vector3(Med(xs), Med(ys), Med(zs));
        }
        return result;
    }

    static float Med(List<float> v) { v.Sort(); return v[v.Count / 2]; }

    public int Cap(int k) => _sw[k].Job.Core.Cap;

    public int Reseeds;
    /// <summary>The fullest stomach (0..1) any swarm held when its starvation shed was asked for (round 11-14: must be
    /// below ForageBelow - a sated swarm never starves).</summary>
    public float ShedFillMax;

    /// <summary>SwarmFauna.Extinction, every tick: no member and no proxy for ExtinctLingerSeconds and the anchor leaves.</summary>
    public void TrackExtinction(Cell c)
    {
        foreach (var one in _sw)
        {
            if (one.AnchorGone) continue;
            if (one.Job.AliveCount > 0 || one.Proxies.Count > 0) { one.ExtinctSince = -1f; continue; }
            if (one.ExtinctSince < 0f) { one.ExtinctSince = c.T; continue; }
            if (c.T - one.ExtinctSince >= Cell.F(_cfg, "ExtinctLingerSeconds")) one.AnchorGone = true;
        }
    }

    /// <summary>RandomLifeSpawner.SpawnFaunaTypeLoop_Random's tick for each region's swarm config: a region whose anchor
    /// has left is below its PopulationSize, so the seeder hatches a fresh swarm (SwarmFauna.Seed: at the band's plant
    /// nearest a random point of the band). Its counters carry on so the run's report is the region's.</summary>
    public void SpawnerTick(Cell c)
    {
        for (int k = 0; k < _sw.Count; k++)
        {
            var old = _sw[k];
            if (!old.AnchorGone) continue;
            Reseeds++;
            var one = Hatch(c, old.Region, old.Start, old.Inner, old.Outer, c.Seed * 101 + old.Region * 7 + 1000 * Reseeds);
            one.Rammed = old.Rammed; one.Starved = old.Starved; one.PeakAlive = old.PeakAlive; one.CollapsedTicks = old.CollapsedTicks;
            c.ReplaceLod($"swarm/{Regions[old.Region]}", old.Pop, one.Pop);
            _sw[k] = one;
        }
    }

    public void Tick(Cell c)
    {
        double ms = 0;
        _ticks++;
        foreach (var one in _sw)
        {
            var job = one.Job;
            if (one.Macro.Collapsed)
            {
                one.CollapsedTicks++;
                one.Goal = ResolveGoal(c, one);
                continue;
            }
            Sense(c, one);
            job.SwimTarget = one.Goal / _us;
            job.Kick(inline: true);
            ms += job.LastTickMs;
            job.Collect();
            if (job.Error != null) throw new Exception($"swarm {one.Region} tick threw: {job.Error}");
            MaskGone(one);
            Feed(c, one);
            Starvation(c, one);
            Contacts(c, one);
            for (int q = 0; q < job.EngagedCount; q++) one.Proxies.Want(job.Engaged[q], c.T);
            one.Proxies.Retire(c.T);
            one.Goal = ResolveGoal(c, one);
            one.PeakAlive = Math.Max(one.PeakAlive, job.AliveCount);
        }
        if (s_swarmTrace && _ticks % 100 == 0)
            foreach (var one in _sw)
                Console.WriteLine($"   SWARM t={c.T:F0} {Regions[one.Region]} alive {one.Job.AliveCount} fill {Fill(one):F2} foraging {one.Foraging} collapsed {one.Macro.Collapsed} " +
                                  $"anchor r={one.Job.Anchor.Length():F0} goal {(one.GoalPlant == null ? "-" : $"plant {one.GoalPlant.Id} d={Vector3.Distance(one.GoalPlant.Heart, one.Job.Anchor):F0} plates {one.GoalPlant.Plates} at {(one.AtPlantSince >= 0 ? c.T - one.AtPlantSince : -1):F0}s")} unfed {c.T - one.LastFed:F0}s starved {one.Starved}");
        LastMs = ms;
    }

    /// <summary>SwarmFauna.SenseVessels: every vessel within the body's reach + max(SenseMargin, EngageRadius) of the anchor.</summary>
    void Sense(Cell c, One one)
    {
        var job = one.Job;
        job.PredCount = 0;
        float radius = _plans[Math.Clamp(job.PlanIx, 0, 3)].Radius * _us * 1.6f + MathF.Max(_senseMargin, _engage);
        for (int k = 0; k < c.Pilots.Count && job.PredCount < job.Preds.Length; k++)
        {
            var p = c.Pilots[k];
            if (Vector3.Distance(p.Pos, job.Anchor) > radius) continue;
            job.Preds[job.PredCount++] = new SwarmPredator { C = p.Pos / _us, V = p.Vel / (_us * _hz), R = _vesselR / _us };
        }
    }

    static void MaskGone(One one)
    {
        for (int q = one.GoneSlots.Count - 1; q >= 0; q--)
        {
            int i = one.GoneSlots[q];
            if (!one.Job.Instances[i].Alive || one.Job.BornThisTick(i)) { one.Gone[i] = false; one.Hits[i] = 0; one.GoneSlots.RemoveAt(q); }
        }
    }

    void Die(One one, int i)
    {
        one.Job.QueueKill(i);
        one.Proxies.Forget(i);
        if (!one.Gone[i]) { one.Gone[i] = true; one.GoneSlots.Add(i); }
    }

    float Fill(One one)
    {
        var st = one.Job.Stomach;
        float held = one.Macro is { Collapsed: true } ? (float)one.Macro.State.StomachTotal : st[0] + st[1] + st[2] + st[3];
        return held / MathF.Max(1e-3f, one.StomachCap);
    }

    readonly List<int> _q = new();
    static readonly bool s_biteTrace = Environment.GetEnvironmentVariable("SHOWCASE_BITE_TRACE") == "1";

    /// <summary>SwarmFauna.Feed + IsFood: BitersPerStep members a tick, the nearest... first edible FLORA prism within
    /// BiteRadius (flora tissue, not shielded, inside this swarm's band), volume banked under the plant's element.</summary>
    void Feed(Cell c, One one) => Feed(c, one, _biters);

    void Feed(Cell c, One one, int biters)
    {
        if (Fill(one) >= 1f) { one.LastFed = c.T; return; }
        var inst = one.Job.Instances;
        int cap = inst.Length, bitten = 0;
        for (int tries = 0; tries < cap && bitten < biters; tries++)
        {
            one.BiteCursor = (one.BiteCursor + 1) % cap;
            int i = one.BiteCursor;
            if (!inst[i].Alive || one.Gone[i]) continue;
            bitten++;
            int found = c.World.QuerySphere(inst[i].CurPos, _bite, _q);
            for (int k = 0; k < found; k++)
            {
                int h = _q[k];
                if (!c.World.IsFloraTissue(h) || c.World.ShieldedL[h]) continue;
                float r = c.World.Pos[h].Length();
                if (r < one.Inner || r > one.Outer) continue;   // IsPreyForMe: this species' band
                int e = c.World.Element[h];
                if (e < 0) continue;
                bool fromGoal = one.GoalPlant != null && c.World.Kind[h] == MassKind.Leaf && c.World.PlantOf[h] == one.GoalPlant.Id;
                float v = c.World.Eat(h, "swarm");
                if (one.Macro.Collapsed) one.Macro.Bank(e, v);
                else one.Job.QueueDeposit(e, v);
                one.LastFed = c.T;
                if (fromGoal) one.LastGoalBite = c.T;
                break;
            }
        }
    }

    /// <summary>SwarmFauna.Starvation: unfed StarvationSeconds -> the core picks a victim each ShedInterval; it withers
    /// through its proxy and its body prism stands as a skeleton.</summary>
    void Starvation(Cell c, One one)
    {
        var job = one.Job;
        int v = job.StarvationVictim;
        if (v >= 0 && job.Instances[v].Alive && !one.Gone[v])
        {
            var s = job.Instances[v].Scale;
            c.World.LaySkeleton(job.BodyAt(v, 1f), s.X * s.Y * s.Z, "swarm");
            one.Proxies.Want(v, c.T, force: true);
            Die(one, v);
            one.Starved++;
        }
        if (!Starving(c, one)) return;
        if (c.T - one.LastShed < Cell.F(_cfg, "ShedIntervalSeconds")) return;
        one.LastShed = c.T;
        ShedFillMax = MathF.Max(ShedFillMax, Fill(one));
        job.WantStarvationVictim = true;
    }

    string Cls(int eff) => "swarm/" + _elemName[Math.Clamp(eff, 0, 3)];

    /// <summary>A vessel's path through a member: a DANGER plate (tier 1) is a hostile danger prism - a burn; any other
    /// body prism is rammed (it explodes, the member dies and drops its crystal; a shield plate takes two hits).</summary>
    void Contacts(Cell c, One one)
    {
        var job = one.Job;
        var inst = job.Instances;
        for (int k = 0; k < c.Pilots.Count; k++)
        {
            var p = c.Pilots[k];
            if (Vector3.Distance(p.Pos, job.Anchor) > _plans[Math.Clamp(job.PlanIx, 0, 3)].Radius * _us * 1.6f + 60f) continue;
            for (int i = 0; i < inst.Length; i++)
            {
                if (!inst[i].Alive || one.Gone[i]) continue;
                var body = job.BodyAt(i, 1f);
                var sc = inst[i].Scale;
                float reach = p.Radius + 0.5f * MathF.Max(sc.X, MathF.Max(sc.Y, sc.Z));
                if (Cell.SegDist(p.Prev, p.Pos, body) > reach) continue;
                if (inst[i].Tier == 1) { c.AddContact(k, Cls(job.Core.EffectiveElement(i)), body); continue; }
                if (inst[i].Tier == 2 && ++one.Hits[i] < 2) continue;
                one.Rammed++;
                Die(one, i);
            }
        }
    }

    /// <summary>SwarmFauna.ResolveGoal: hungry -> the nearest plant in band it can eat and has not rested; graze until
    /// sated or GiveUpSeconds without a bite from it; sated -> roam the band.</summary>
    Vector3 ResolveGoal(Cell c, One one)
    {
        var here = one.Job.Anchor;
        float now = c.T, fill = Fill(one);
        float forage = Cell.F(_cfg, "ForageBelow"), sated = Cell.F(_cfg, "SatedAbove"), giveUp = Cell.F(_cfg, "GiveUpSeconds");
        if (!one.Foraging && fill < forage) one.Foraging = true;
        else if (one.Foraging && fill >= sated) one.Foraging = false;
        if (one.GoalPlant != null)
        {
            bool bare = one.AtPlantSince >= 0f && now - MathF.Max(one.AtPlantSince, one.LastGoalBite) > giveUp;
            bool unreached = one.AtPlantSince < 0f && now - one.GoalProgressAt > 6f * giveUp;   // no progress (round 11-10)
            if (!one.Foraging || bare || unreached || !one.GoalPlant.Alive)
            {
                one.Rested[one.GoalPlant] = now;
                one.GoalPlant = null;
                one.AtPlantSince = -1f;
            }
        }
        if (one.Foraging)
        {
            var plant = one.GoalPlant;
            if (plant == null)
            {
                float bd = float.MaxValue;
                foreach (var f in c.World.Plants)
                {
                    if (!f.Alive) continue;
                    float r = f.Heart.Length();
                    if (r < one.Inner || r > one.Outer) continue;
                    if (one.Rested.TryGetValue(f, out float t) && now - t < Cell.F(_cfg, "PlantRestSeconds")) continue;
                    float d = Vector3.DistanceSquared(f.Heart, here);
                    if (d < bd) { bd = d; plant = f; }
                }
            }
            if (plant != null)
            {
                if (plant != one.GoalPlant)
                {
                    one.GoalPlant = plant; one.GoalSince = now; one.AtPlantSince = -1f; one.LastGoalBite = -1f;
                    one.GoalBest = float.MaxValue; one.GoalProgressAt = now;
                }
                float d = Vector3.Distance(plant.Heart, here);
                if (d < one.GoalBest - 1f) { one.GoalBest = d; one.GoalProgressAt = now; }
                if (one.AtPlantSince < 0f && d < 60f) one.AtPlantSince = now;
                // SwarmFauna.ResolveGoal: along the shell (SwarmShellPath), the Goal setter clamps into the band
                return ClampBand(one, SwarmShellPath.Toward(Vector3.Zero, here, plant.Heart, Ride(one, here.Length()), Cell.F(_cfg, "WanderReach")));
            }
        }
        if (Vector3.DistanceSquared(one.Goal, here) > 40f * 40f) return one.Goal;
        var wander = here + c.Rng.OnUnitSphere() * Cell.F(_cfg, "WanderReach");
        float rr = here.Length();
        if (rr > 1f) wander = Vector3.Normalize(wander) * Ride(one, rr);
        return ClampBand(one, wander);
    }

    /// <summary>SwarmFauna.RideRadius: the whole body inside the band (SwarmShellPath.Ride, round 11-14).</summary>
    float Ride(One one, float r) => SwarmShellPath.Ride(r, one.Inner, one.Outer, BodyRadius(one));

    static Vector3 ClampBand(One one, Vector3 p)
    {
        float r = p.Length();
        if (r < 1e-3f) return p;
        return p * (Math.Clamp(r, one.Inner, one.Outer) / r);
    }

    public int Colliders(Cell c) { int n = 0; foreach (var one in _sw) n += 2 * one.Proxies.Count; return n; }

    static readonly string[] Regions = { "inner", "middle", "outer" };
    static readonly bool s_swarmTrace = Environment.GetEnvironmentVariable("SHOWCASE_SWARM_TRACE") == "1";

    public void Census(List<(string, int, int)> into)
    {
        foreach (var one in _sw) into.Add(($"swarm/{Regions[one.Region]}", one.Job.AliveCount, one.Job.Core.Cap));
    }

    /// <summary>C8: members inside the swarm's band (a collapsed swarm's members are its macro state - not counted).</summary>
    public void Occupancy(Cell c, List<(string, int, int)> into)
    {
        foreach (var one in _sw)
        {
            if (one.Macro is { Collapsed: true }) continue;
            int n = 0, inPen = 0;
            var inst = one.Job.Instances;
            for (int i = 0; i < inst.Length; i++)
            {
                if (!inst[i].Alive) continue;
                n++;
                float r = inst[i].CurPos.Length();
                if (r >= one.Inner && r <= one.Outer) inPen++;
            }
            into.Add(($"swarm/{Regions[one.Region]}", inPen, n));
        }
    }

    public void Strikers(Cell c, List<Striker> into)
    {
        for (int s = 0; s < _sw.Count; s++)
        {
            var job = _sw[s].Job;
            if (!c.NearAnyPilot(job.Anchor, _plans[Math.Clamp(job.PlanIx, 0, 3)].Radius * _us * 1.6f + 400f)) continue;
            var inst = job.Instances;
            for (int i = 0; i < inst.Length; i++)
            {
                if (!inst[i].Alive) continue;
                var at = inst[i].CurPos;
                if (!c.NearAnyPilot(at, 400f)) continue;
                // a swarm member's strike is its DANGER plate: the plate it shows is its intent (SwarmTickJob Build)
                into.Add(new Striker { Cls = Cls(job.Core.EffectiveElement(i)), Key = s * 100000L + i, Pos = at, Intent = inst[i].Tier == 1 ? 1f : 0f });
            }
        }
    }

    /// <summary>The swarms are a ledger BOUNDARY: bites go into the stomach (and become eggs whose bodies the plan, not the
    /// meal, sizes), starved bodies come out as skeletons. Account = bites - skeletons.</summary>
    double _eaten, _laid;
    public double Held() => _eaten - _laid;
    public void SyncBook(CellWorld w)
    {
        _eaten = w.EatenBy.TryGetValue("swarm", out var e) ? e : 0;
        _laid = w.LaidBy.TryGetValue("swarm", out var l) ? l : 0;
    }

    public void Ledger(List<(string, double)> into)
    {
        double st = 0; foreach (var one in _sw) for (int e = 0; e < 4; e++) st += one.Job.Stomach[e];
        into.Add(("swarm: bites eaten (boundary in)", _eaten));
        into.Add(("swarm: starved bodies laid as skeletons (boundary out)", _laid));
        into.Add(("swarm: stomachs banked now (informational)", st));
    }

    public string Report() => string.Join(", ", _sw.Select(o =>
        $"{Regions[o.Region]} {o.Job.AliveCount}/{o.Job.Core.Cap} (peak {o.PeakAlive}, rammed {o.Rammed}, starved {o.Starved}, proxies peak {o.Proxies.Peak}, " +
        $"collapsed {o.CollapsedTicks}/{_ticks} ticks)"));


    public void Snapshot(Utf8JsonWriter w)
    {
        w.WriteStartArray("swarm");
        foreach (var one in _sw)
        {
            var job = one.Job;
            for (int i = 0; i < job.Instances.Length; i++)
            {
                var s = job.Instances[i];
                if (!s.Alive) continue;
                w.WriteStartArray();
                w.WriteNumberValue(MathF.Round(s.CurPos.X, 1)); w.WriteNumberValue(MathF.Round(s.CurPos.Y, 1)); w.WriteNumberValue(MathF.Round(s.CurPos.Z, 1));
                w.WriteNumberValue(one.Region); w.WriteNumberValue(job.Core.EffectiveElement(i)); w.WriteNumberValue(s.Tier); w.WriteNumberValue(s.DomainSlot);
                w.WriteEndArray();
            }
        }
        w.WriteEndArray();
    }
}

// ══════════════════════════════════════════════════════════════════════════════════════════════ substrate

/// <summary>The cell's substrate (SubstrateCellHost + SubstrateFauna): one SubstrateCore, seven populations, one
/// SubstrateTickJob run inline. Feed / Hunt / ShedStarving mirror SubstrateFauna; vessels are sensed cell-wide.</summary>
sealed class SubstrateSystem : ICellSystem, IOccupancy
{
    sealed class Pop
    {
        public string Key = "";
        public int Index, Start, Cap, MaxBites, MaxProxies;
        public float Inner, Outer, BiteRadius, Engage;
        public ProxyPool Proxies = null!;
        public int BiteCursor, Rammed, Starved, Preyed, Peak, Asked, Bitten;
        // round 11f-2 (Docs/ECOLOGY_LOD.md §6.1): the freeze route, as SubstrateFauna implements IMacroPopulation
        public MacroSub Lod = null!;
        public float Thaw, ExtentPad, ReserveS = float.PositiveInfinity;
        public Vector3 Centre;
        public float Extent;
        public bool Collapsed;
        // round 11-10: the spawner's extinction recovery (SubstrateFauna.Extinction + RandomLifeSpawner)
        public Action SeedNow = null!;
        public float Linger, ExtinctSince = -1f;
        public bool AnchorGone;
        public int Reseeds;
    }

    /// <summary>SubstrateFauna's IMacroPopulation, minus Unity: freeze the block (SubstratePopulation.Frozen, set between
    /// ticks) when nothing only individuals resolve is pending and the hungriest agent has twice the thaw margin of
    /// reserve; thaw on approach, on a hunt, or before the reserve runs out. MacroTick is a no-op (the frozen pass burns
    /// metabolism every tick; a band population has no goal to drift to).</summary>
    sealed class MacroSub : IMacroPopulation
    {
        readonly SubstrateSystem _s; readonly Pop _p;
        public MacroSub(SubstrateSystem s, Pop p) { _s = s; _p = p; }
        public Vector3 MacroCentre => _p.Centre;
        public float MacroExtent => _p.Extent;
        public bool IsCollapsed => _p.Collapsed;
        public bool CanCollapse => !_p.Collapsed && _s.Job.Error == null && _p.Proxies.Count == 0 && !_s.AnyGone(_p)
                                   && _s.Job.EngagedCount[_p.Index] == 0 && !_s.AnyStarving(_p) && _p.ReserveS > 2f * _p.Thaw;
        public bool NeedsIndividuals => _p.Collapsed && (_p.Proxies.Count > 0 || _s.AnyGone(_p) || _s.AnyStarving(_p) || _p.ReserveS < _p.Thaw);
        public MacroPopulationTotals Totals => new() { Individuals = _s.Job.PopAlive[_p.Index], BodyVolume = _s.Job.PopVolume[_p.Index], Stomach = _s.Core.ReserveVolume(_p.Index) };
        public bool Collapse()
        {
            if (!CanCollapse) return false;
            _p.Collapsed = true;
            _s.Core.Pops[_p.Index].Frozen = true;   // SubstrateCellHost.Freeze, applied between ticks (the harness ticks inline)
            return true;
        }
        public void Expand() { if (!_p.Collapsed) return; _p.Collapsed = false; _s.Core.Pops[_p.Index].Frozen = false; }
        public void MacroTick(float dt) { }
    }

    bool AnyGone(Pop p) { foreach (int i in _goneSlots) if (i >= p.Start && i < p.Start + p.Cap) return true; return false; }
    bool AnyStarving(Pop p) { for (int i = p.Start; i < p.Start + p.Cap; i++) if (_starving[i]) return true; return false; }

    /// <summary>SubstrateFauna.ReadMacroState: centroid, spread (+ a quarter of the engage radius) and reserve, read
    /// between ticks for the director and the thaw rule.</summary>
    void ReadMacroState(Pop p)
    {
        var cen = Vector3.Zero; int n = 0;
        for (int i = p.Start; i < p.Start + p.Cap; i++) if (Job.Instances[i].Alive) { cen += Job.Instances[i].CurPos; n++; }
        if (n > 0) cen /= n;
        float ext = 0f;
        for (int i = p.Start; i < p.Start + p.Cap; i++) if (Job.Instances[i].Alive) ext = MathF.Max(ext, Vector3.Distance(cen, Job.Instances[i].CurPos));
        if (n > 0) p.Centre = cen;
        p.Extent = ext + p.ExtentPad;
        p.ReserveS = Core.ReserveSeconds(p.Index);
    }

    public string Name => "substrate";
    public double StepHz => 10;
    public double LastMs { get; private set; }
    public readonly SubstrateCore Core;
    public readonly SubstrateTickJob Job;
    readonly List<Pop> _pops = new();
    readonly bool[] _gone;
    readonly List<int> _goneSlots = new();
    readonly bool[] _starving;
    /// <summary>What the glue booked for a death it queued (the published body) and how it booked it (ram = the body left
    /// the cell; otherwise it stands as a skeleton or went to a hunter). The job's Killed list says what the agent really
    /// held when the kill landed; a meal queued in the same pass is the difference, booked the same way.</summary>
    readonly float[] _booked;
    readonly bool[] _bookedRam;
    public double LateMeals;
    public int LateMealCount;
    readonly float _vesselR;
    public double Seeded, Rammed, PreyQueued, StarvedLaid;
    /// <summary>Flora hearts from other systems (FloraHeartRegistry is every Flora: the grove's traps and sclerotia too).</summary>
    public Func<IEnumerable<(Vector3 at, int element)>>? ExtraHearts;

    public SubstrateSystem(Cell c, FloraSystem flora)
    {
        var rows = c.L.GetProperty("substrate").EnumerateArray().ToList();
        var first = rows[0].GetProperty("asset");
        Core = new SubstrateCore(c.L.GetProperty("substrate_capacity").GetInt32(), c.Membrane, Cell.Dt, 40, c.Seed * 31 + 5);
        Job = new SubstrateTickJob(Core, new SubstrateTickSettings
        {
            Centre = Vector3.Zero, HeartPrismGap = Cell.F(first, "heartPrismGap"), Thin = Cell.F(first, "bodyThin"),
            // SubstrateCellHost: the FIRST population's engage numbers are the job's defaults; each population's own
            // (SubstratePopulation.EngageRadius / MaxEngaged, set by SubstrateFauna.ClaimBlock) win - QA-SWARM-ROUND11-9
            EngageRadius = Cell.F(first, "engageRadius"), MaxEngaged = (int)Cell.F(first, "maxProxies"),
        });
        var hs = first.GetProperty("heartWorldScale");
        Job.S.HeartWorldScale = new[] { (float)hs[0].GetDouble(), (float)hs[1].GetDouble(), (float)hs[2].GetDouble(), (float)hs[3].GetDouble() };
        _vesselR = Cell.F(first, "vesselRadius");
        _leafVolume = c.L.GetProperty("regions").EnumerateArray().Select(rg =>
            (float)(rg.GetProperty("leaf")[0].GetDouble() * rg.GetProperty("leaf")[1].GetDouble() * rg.GetProperty("leaf")[2].GetDouble())).ToArray();
        var only = Environment.GetEnvironmentVariable("SHOWCASE_SUBSTRATE");   // diagnostics: a subset of the species
        foreach (var r in rows)
        {
            string key = r.GetProperty("key").GetString()!;
            if (!string.IsNullOrEmpty(only) && !only.Split(',').Contains(key)) continue;
            var a = r.GetProperty("asset");
            var band = r.GetProperty("band");
            // the SHIPPED species asset's params (what SubstrateFauna reads), not the C# port: a demo override such as the
            // pack's RingHoldSeconds 6 lives only in the asset (author_substrate_fauna.DEMO_OVERRIDES)
            var P = ShippedSpecies(key, r.GetProperty("species"));
            float inner = (float)band[0].GetDouble(), outer = (float)band[1].GetDouble();
            int q = Core.AddPopulation(P, r.GetProperty("element").GetInt32(), inner, outer);
            if (q < 0) throw new Exception($"substrate: no room for {key}");
            var block = Core.Pops[q];
            ApplyEngage(block, Cell.F(a, "engageRadius"), (int)Cell.F(a, "maxProxies"));
            // round 11-11 (SubstrateFauna.ClaimBlock): the species' sector pen inside its band
            float half = a.TryGetProperty("sectorHalfAngle", out var ha) ? (float)ha.GetDouble() : 0f;
            var axis = Vector3.UnitX;
            if (a.TryGetProperty("sectorAxis", out var ax))
                axis = new Vector3((float)ax[0].GetDouble(), (float)ax[1].GetDouble(), (float)ax[2].GetDouble());
            bool sector = half > 0f && axis.LengthSquared() > 1e-6f;
            if (sector) Core.SetSector(q, axis, half);
            float sectorCos = sector ? MathF.Cos(half * MathF.PI / 180f) : -2f;
            var axisU = sector ? Vector3.Normalize(axis) : Vector3.UnitX;
            bool InSector(Vector3 p) => !sector || p.LengthSquared() < 1e-6f || Vector3.Dot(Vector3.Normalize(p), axisU) >= sectorCos;
            int clusters = a.TryGetProperty("seedClusters", out var sc) ? (int)sc.GetDouble() : 0;
            var pop = new Pop
            {
                Key = key, Index = q, Start = block.Start, Cap = block.Cap, Inner = inner, Outer = outer,
                MaxBites = (int)Cell.F(a, "maxBitesPerTick"), BiteRadius = Cell.F(a, "biteRadius"),
                Engage = Cell.F(a, "engageRadius"), MaxProxies = (int)Cell.F(a, "maxProxies"),
                Proxies = new ProxyPool((int)Cell.F(a, "maxProxies"), Cell.F(a, "proxyLingerSeconds")),
                Thaw = Cell.F(a, "thawReserveSeconds"), ExtentPad = MathF.Max(0f, Cell.F(a, "engageRadius") * 0.25f),
            };
            pop.Lod = new MacroSub(this, pop);
            _pops.Add(pop);
            c.RegisterLod($"substrate/{key}", pop.Lod, Cell.F(a, "macroLod") > 0.5f);
            // SubstrateFauna.Seed: an ambusher among the flora hearts in its band, else around the anchor. A local
            // function: the spawner's re-hatch after an extinction (SpawnerTick) seeds a population exactly this way.
            void SeedNow()
            {
            int n = Math.Min(r.GetProperty("seed").GetInt32(), pop.Cap);
            double before = Core.MassIn;
            if (r.GetProperty("at_flora").GetInt32() != 0)
            {
                var at = new List<Vector3>();
                foreach (var f in c.World.Plants)
                {
                    if (!f.Alive || at.Count >= n) continue;
                    float rr = f.Heart.Length();
                    if (rr >= inner && rr <= outer && InSector(f.Heart)) at.Add(f.Heart);
                }
                for (int k = 0; at.Count > 0 && at.Count < n; k++) at.Add(at[k % at.Count]);
                if (at.Count > 0) Core.SeedAt(q, at.ToArray(), 12f);
            }
            if (Core.MassIn == before && (clusters > 1 || sector))
            {
                // SubstrateFauna.Seed (round 11-11): herds, roosts, a school - clusters at points of the band and sector,
                // a little inside the band's walls
                float spread = Cell.F(a, "seedSpread"), pad = MathF.Min(spread, 0.25f * (outer - inner));
                var centres = new Vector3[Math.Max(1, clusters)];
                for (int k = 0; k < centres.Length; k++)
                {
                    var pt = c.RandomInShell(inner + pad, outer - pad);
                    for (int t = 0; t < 64 && !InSector(pt); t++) pt = c.RandomInShell(inner + pad, outer - pad);
                    if (!InSector(pt)) pt = axisU * (0.5f * (inner + outer));
                    centres[k] = pt;
                }
                var seats = new Vector3[n];
                for (int k = 0; k < n; k++) seats[k] = centres[k % centres.Length];
                Core.SeedAt(q, seats, spread);
            }
            if (Core.MassIn == before)
            {
                var anchor = c.RandomInShell(inner + 20f, outer - 20f);
                if (Environment.GetEnvironmentVariable("SHOWCASE_SEED_AT_FLORA") == "1")
                {
                    Plant? best = null; float bd = float.MaxValue;
                    foreach (var f in c.World.Plants)
                    {
                        float rr = f.Heart.Length();
                        if (!f.Alive || rr < inner || rr > outer) continue;
                        float d = Vector3.DistanceSquared(f.Heart, anchor);
                        if (d < bd) { bd = d; best = f; }
                    }
                    if (best != null) anchor = best.Heart;
                }
                Core.Seed(q, n, anchor, Cell.F(a, "seedSpread"));
            }
            Seeded += Core.MassIn - before;
            }
            pop.SeedNow = SeedNow;
            pop.Linger = Cell.F(a, "extinctLingerSeconds");
            SeedNow();
        }
        _gone = new bool[Core.Capacity];
        _starving = new bool[Core.Capacity];
        _booked = new float[Core.Capacity];
        _bookedRam = new bool[Core.Capacity];
        Job.Prime();
    }

    public int Reseeds;

    /// <summary>SubstrateFauna.Extinction, every tick: no agent and no proxy for the species' ExtinctLingerSeconds and the
    /// anchor leaves (its block is freed).</summary>
    public void TrackExtinction(Cell c)
    {
        foreach (var p in _pops)
        {
            if (p.AnchorGone) continue;
            if (Job.PopAlive[p.Index] > 0 || p.Proxies.Count > 0) { p.ExtinctSince = -1f; continue; }
            if (p.ExtinctSince < 0f) { p.ExtinctSince = c.T; continue; }
            if (c.T - p.ExtinctSince >= p.Linger) p.AnchorGone = true;
        }
    }

    /// <summary>RandomLifeSpawner's tick for each substrate species config: a species whose anchor left is below its
    /// PopulationSize (1), so the seeder hatches a fresh anchor, which seeds its population by SubstrateFauna.Seed's
    /// rules (at the band's flora, in clusters, or at a random point of the band).</summary>
    public void SpawnerTick(Cell c)
    {
        foreach (var p in _pops)
        {
            if (!p.AnchorGone) continue;
            if (p.Collapsed) p.Lod.Expand();
            p.SeedNow();
            p.AnchorGone = false; p.ExtinctSince = -1f; p.Reseeds++; Reseeds++;
        }
    }

    /// <summary>Fields whose shipped value differs from <see cref="SubstrateResearch.ByName"/>'s game port, per species
    /// (reported, so a demo override is visible in the run).</summary>
    public readonly List<string> AssetOverrides = new();

    /// <summary>A SubstrateSpeciesParams filled from the species asset's serialized `species:` block (layout.py
    /// yaml_species): every serialized field must name a field of the class (an unknown one fails the run - the asset
    /// and the class have drifted), and every field of the class must be serialized.</summary>
    SubstrateSpeciesParams ShippedSpecies(string key, JsonElement sp)
    {
        var port = SubstrateResearch.ByName(key, game: true);
        var P = port.Clone();
        foreach (var f in typeof(SubstrateSpeciesParams).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (f.IsInitOnly || f.IsLiteral) continue;
            if (!sp.TryGetProperty(f.Name, out var v)) throw new Exception($"substrate {key}: the species asset has no {f.Name}");
            if (f.FieldType == typeof(SubstrateRegime))
            {
                object reg = f.GetValue(P)!;
                foreach (var g in typeof(SubstrateRegime).GetFields())
                {
                    if (!v.TryGetProperty(g.Name, out var gv)) throw new Exception($"substrate {key}: {f.Name}.{g.Name} missing");
                    g.SetValue(reg, ToField(g.FieldType, gv));
                }
                f.SetValue(P, reg);
                continue;
            }
            f.SetValue(P, ToField(f.FieldType, v));
        }
        foreach (var prop in sp.EnumerateObject())
            if (typeof(SubstrateSpeciesParams).GetField(prop.Name) == null)
                throw new Exception($"substrate {key}: the species asset's {prop.Name} is not a SubstrateSpeciesParams field");
        // what the asset changes from the port (a demo override, or drift author_substrate_fauna --check would catch)
        var a = new Dictionary<string, float>(); var b = new Dictionary<string, float>();
        port.Visit((n, x) => a[n] = x); port.VisitPrimitives((n, x) => a[n] = x);
        P.Visit((n, x) => b[n] = x); P.VisitPrimitives((n, x) => b[n] = x);
        foreach (var kv in b) if (a.TryGetValue(kv.Key, out float pv) && pv != kv.Value) AssetOverrides.Add($"{key}.{kv.Key} {pv:G4}->{kv.Value:G4}");
        return P;
    }

    static object? ToField(Type t, JsonElement v)
    {
        if (t == typeof(float)) return (float)v.GetDouble();
        if (t == typeof(int)) return (int)v.GetDouble();
        if (t == typeof(bool)) return v.GetDouble() != 0;
        if (t == typeof(string)) return v.ValueKind == JsonValueKind.String ? (v.GetString() == "''" ? "" : v.GetString()) : v.GetRawText();
        if (t == typeof(float[]))
            return v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray() : Array.Empty<float>();
        throw new Exception("species field of unsupported type " + t);
    }

    /// <summary>The food a plant heart stands for in the field: SubstrateCellHost.SenseFood's one unit per heart. Env
    /// SHOWCASE_FOOD_MASS=1 (diagnostic) weighs each heart by the leaf volume it holds instead.</summary>
    float FoodVolume(Plant pl) => _massFood ? pl.Plates * _leafVolume[pl.Region] : 1f;
    readonly bool _massFood = Environment.GetEnvironmentVariable("SHOWCASE_FOOD_MASS") == "1";
    float[] _leafVolume = Array.Empty<float>();

    /// <summary>One population's engage numbers on its block (the fix for the shared-first-species engagement).</summary>
    static void ApplyEngage(SubstratePopulation block, float radius, int max)
    {
        block.EngageRadius = radius;
        block.MaxEngaged = max;
    }

    static readonly bool BandOnly = Environment.GetEnvironmentVariable("SHOWCASE_FOOD_BAND") == "1";
    static readonly bool FoodPrisms = Environment.GetEnvironmentVariable("SHOWCASE_FOOD_PRISMS") == "1";

    Pop PopOf(int i) { foreach (var p in _pops) if (i >= p.Start && i < p.Start + p.Cap) return p; return _pops[0]; }

    public void Tick(Cell c)
    {
        int n = 0;
        foreach (var p in c.Pilots)
            if (n < Job.Pilots.Length) Job.Pilots[n++] = new SubstratePilot { Pos = p.Pos, Vel = p.Vel, Radius = _vesselR, Id = p.Id };
        Job.PilotCount = n;
        // SubstrateCellHost.SenseFood: one unit of food per living, non-Charge plant heart in the cell
        int f = 0;
        foreach (var pl in c.World.Plants)
        {
            if (!pl.Alive || pl.Element == 0) continue;
            if (f >= Job.Food.Length) Array.Resize(ref Job.Food, Job.Food.Length * 2);
            Job.Food[f++] = new SubstrateFood { Pos = pl.Heart, Volume = FoodVolume(pl) };
        }
        // every OTHER flora heart in the registry is food-field too - the grove's traps and sclerotia
        if (ExtraHearts != null)
            foreach (var (at, element) in ExtraHearts())
            {
                if (element == 0 || at.Length() > c.Membrane) continue;
                if (f >= Job.Food.Length) Array.Resize(ref Job.Food, Job.Food.Length * 2);
                Job.Food[f++] = new SubstrateFood { Pos = at, Volume = 1f };
            }
        if (FoodPrisms)
        {
            // DIAGNOSTIC ONLY (SHOWCASE_FOOD_PRISMS=1): the substrate harness's food - one point per edible leaf prism
            f = 0;
            for (int h = 0; h < c.World.Count; h++)
            {
                if (!c.World.AliveL[h] || c.World.Kind[h] != MassKind.Leaf || c.World.ShieldedL[h]) continue;
                if (BandOnly && (c.World.Pos[h].Length() < _pops[0].Inner || c.World.Pos[h].Length() > _pops[0].Outer)) continue;
                if (f >= Job.Food.Length) Array.Resize(ref Job.Food, Job.Food.Length * 2);
                Job.Food[f++] = new SubstrateFood { Pos = c.World.Pos[h], Volume = c.World.Vol[h] };
            }
        }
        Job.FoodCount = f;
        _inFlight = 0;   // every feed queued so far lands in this tick
        Job.Kick(inline: true);
        LastMs = Job.LastTickMs;
        Job.Collect();
        if (Job.Error != null) throw new Exception($"substrate tick threw: {Job.Error}");
        foreach (var k in Job.Killed)
        {
            _pendingKill -= _booked[k.Index];   // the kill landed: the core no longer holds what was booked for it
            float late = k.Volume - _booked[k.Index];
            if (late <= 1e-5f) continue;
            LateMeals += late; LateMealCount++;
            if (_bookedRam[k.Index]) Rammed += late;
            else c.World.LaySkeleton(Core.Pos[k.Index], late, "substrate");
        }

        MaskGone();
        ReadEvents(c);
        Feed(c);
        Hunt(c);
        ShedStarving(c);
        Rams(c);
        foreach (var pop in _pops)
        {
            int eng = Job.EngagedCount[pop.Index];
            for (int q = 0; q < eng; q++) pop.Proxies.Want(Job.Engaged[pop.Start + q], c.T);
            pop.Proxies.Retire(c.T);
            pop.Peak = Math.Max(pop.Peak, Job.PopAlive[pop.Index]);
            ReadMacroState(pop);
        }
    }

    void MaskGone()
    {
        for (int q = _goneSlots.Count - 1; q >= 0; q--)
        {
            int i = _goneSlots[q];
            if (!Job.Instances[i].Alive || Job.BornThisTick(i)) { _gone[i] = false; _goneSlots.RemoveAt(q); }
        }
    }

    /// <summary>Meals queued since the last tick began: eaten from the world, not yet in a body (they land next tick).</summary>
    double _inFlight;
    void QueueFeed(int i, float v)
    {
        if (!(v > 0f)) return;
        Job.QueueFeed(i, v);
        _inFlight += v;
    }

    void Die(int i, bool ram = false)
    {
        if (!_gone[i]) { _booked[i] = BodyVolume(i); _bookedRam[i] = ram; _pendingKill += _booked[i]; }
        Job.QueueKill(i);
        PopOf(i).Proxies.Forget(i);
        _starving[i] = false;
        if (!_gone[i]) { _gone[i] = true; _goneSlots.Add(i); }
    }

    float BodyVolume(int i) { var b = Job.Body[i]; return b.X * b.Y * b.Z; }

    void ReadEvents(Cell c)
    {
        foreach (var e in Job.Events)
        {
            if (e.Kind == SubstrateEventKind.Starving) _starving[e.Index] = true;
            else if (e.Kind == SubstrateEventKind.Born) _starving[e.Index] = false;
            else if (e.Kind == SubstrateEventKind.Bite && e.Other >= 0 && e.Other < c.Pilots.Count)
            {
                // a contact with the danger plate at the species' weight (round 11-11: a mobber's peck is a 0.25 drain;
                // a weight-0 plate - the leech - is never dangerous, SubstrateAgentFauna.SetDanger)
                float w = Core.Pops[PopOf(e.Index).Index].P.ContactWeight;
                // rated on the agent that BIT (the event's first biter), not on whichever of its class is nearest the pilot:
                // a herd member grazing beside you is not the bull that charged you
                if (w > 0f) c.AddContact(e.Other, PopOf(e.Index).Key, Job.Instances[e.Index].CurPos,
                                         telegraphed: c.ArmedFor(PopOf(e.Index).Key, e.Index), weight: w);
            }
            else if (e.Kind == SubstrateEventKind.Sip && e.Other >= 0 && e.Other < c.Pilots.Count)
                // SubstrateFauna.Sip: a rider drains its host through the danger effect's ApplyContact, no collision
                c.AddContact(e.Other, PopOf(e.Index).Key, Job.Instances[e.Index].CurPos, weight: e.Value);
        }
    }

    readonly List<int> _q = new();

    /// <summary>SubstrateFauna.Feed + IsFood: per population up to MaxBitesPerTick of the agents the core asked for, the
    /// first edible flora prism within BiteRadius (flora tissue, not shielded, inside the species' band), paid 1:1.</summary>
    static readonly bool s_biteTrace = Environment.GetEnvironmentVariable("SHOWCASE_BITE_TRACE") == "1";
    void Feed(Cell c)
    {
        var reqs = Job.EatRequests;
        if (reqs.Count == 0) return;
        foreach (var pop in _pops)
        {
            int bitten = 0;
            for (int t = 0; t < reqs.Count && bitten < pop.MaxBites; t++)
            {
                pop.BiteCursor = (pop.BiteCursor + 1) % reqs.Count;
                int i = reqs[pop.BiteCursor];
                if (i < pop.Start || i >= pop.Start + pop.Cap || _gone[i] || !Job.Instances[i].Alive) continue;
                bitten++;
                pop.Asked++;
                int found = c.World.QuerySphere(Job.Instances[i].CurPos, pop.BiteRadius, _q);
                if (s_biteTrace && pop.Asked % 500 == 1)
                {
                    var me = Job.Instances[i].CurPos; float bd = 1e9f; int bh = -1;
                    for (int h = 0; h < c.World.Count; h++) { if (!c.World.AliveL[h] || !c.World.IsFloraTissue(h)) continue; float d = Vector3.Distance(c.World.Pos[h], me); if (d < bd) { bd = d; bh = h; } }
                    Console.WriteLine($"   BITE {pop.Key} t={c.T:F0} r={me.Length():F0} found {found} nearest leaf {bd:F0}u r={(bh>=0?c.World.Pos[bh].Length():0):F0} shield {(bh>=0&&c.World.ShieldedL[bh])} hunger {Core.Hunger[i]:F2}");
                }
                for (int k = 0; k < found; k++)
                {
                    int h = _q[k];
                    if (!c.World.IsFloraTissue(h) || c.World.ShieldedL[h]) continue;
                    float r = c.World.Pos[h].Length();
                    if (r < pop.Inner || r > pop.Outer) continue;
                    QueueFeed(i, c.World.Eat(h, "substrate"));
                    pop.Bitten++;
                    break;
                }
            }
        }
    }

    /// <summary>SubstrateFauna.Hunt: the prey dies through its proxy (Predated: its body goes into the hunter's mouth) and
    /// its stock - the published body's volume - is queued into the hunter.</summary>
    void Hunt(Cell c)
    {
        foreach (var r in Job.PreyRequests)
        {
            if (_gone[r.Predator] || !Job.Instances[r.Predator].Alive) continue;
            if (_gone[r.Prey] || !Job.Instances[r.Prey].Alive) continue;
            PopOf(r.Prey).Lod.Expand();   // MaterialiseForHit thaws first: a hunt is resolved by individuals
            float stock = BodyVolume(r.Prey);
            Die(r.Prey);
            PopOf(r.Prey).Preyed++;
            QueueFeed(r.Predator, stock);
            PreyQueued += stock;
        }
    }

    /// <summary>SubstrateFauna.ShedStarving: a starving agent gets a proxy, withers through it, and its body (the
    /// published body prism) stands as a skeleton.</summary>
    static readonly bool s_subDeathTrace = Environment.GetEnvironmentVariable("SHOWCASE_DEATH_TRACE") == "1";
    void ShedStarving(Cell c)
    {
        for (int i = 0; i < _starving.Length; i++)
        {
            if (!_starving[i]) continue;
            if (!Job.Instances[i].Alive || _gone[i]) { _starving[i] = false; continue; }
            float v = BodyVolume(i);
            c.World.LaySkeleton(Job.BodyAt(i, 1f), v, "substrate");
            StarvedLaid += v;
            PopOf(i).Proxies.Want(i, c.T, force: true);
            PopOf(i).Starved++;
            if (s_subDeathTrace) Console.WriteLine($"   DEATH substrate/{PopOf(i).Key} t={c.T:F1} starved at r={Job.BodyAt(i, 1f).Length():F0}");
            Die(i);
        }
    }

    /// <summary>A vessel's path through an agent's body: a striking agent (tier 1) is a danger prism (its bite is the
    /// core's Bite event); a calm one is rammed - its body explodes out of the cell and it drops its crystal.</summary>
    void Rams(Cell c)
    {
        for (int i = 0; i < Job.Instances.Length; i++)
        {
            var s = Job.Instances[i];
            if (!s.Alive || _gone[i] || s.Tier == 1) continue;
            var b = Job.Body[i];
            for (int k = 0; k < c.Pilots.Count; k++)
            {
                var p = c.Pilots[k];
                if (Cell.SegDist(p.Prev, p.Pos, Job.BodyAt(i, 1f)) > p.Radius + 0.5f * b.X) continue;
                Rammed += BodyVolume(i);
                PopOf(i).Rammed++;
                if (s_subDeathTrace) Console.WriteLine($"   DEATH substrate/{PopOf(i).Key} t={c.T:F1} rammed by {p.Kind} at r={Job.BodyAt(i, 1f).Length():F0}");
                Die(i, ram: true);
                break;
            }
        }
    }

    public int Colliders(Cell c) { int n = 0; foreach (var p in _pops) n += 2 * p.Proxies.Count; return n; }

    public void Census(List<(string, int, int)> into)
    {
        foreach (var p in _pops) into.Add(($"substrate/{p.Key}", Job.PopAlive[p.Index], p.Cap));
    }

    /// <summary>SHOWCASE_OCC_TRACE=1 (diagnostic): per population, member-seconds below its band, above it, outside its
    /// sector, counted, and the summed distance outside the band (printed at the end of the run).</summary>
    static readonly bool s_occTrace = Environment.GetEnvironmentVariable("SHOWCASE_OCC_TRACE") == "1";
    public readonly Dictionary<string, (long below, long above, long sector, long n, double beyond)> OccWhy = new();
    /// <summary>C8: members inside the population's pen - its band, and its sector when it has one (SubstrateCore.SetSector).</summary>
    public void Occupancy(Cell c, List<(string, int, int)> into)
    {
        foreach (var p in _pops)
        {
            var pop = Core.Pops[p.Index];
            int n = 0, inPen = 0;
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
            {
                if (!Core.Alive[i]) continue;
                n++;
                var at = Core.Pos[i];
                float r = at.Length();
                bool below = r < pop.BandInner, above = r > pop.BandOuter;
                bool outSector = pop.HasSector && Vector3.Dot(at, pop.SectorAxis) < pop.SectorCos * r;
                if (s_occTrace)
                {
                    var (b0, a0, s0, n0, d0) = OccWhy.GetValueOrDefault(p.Key);
                    float beyond = below ? pop.BandInner - r : above ? r - pop.BandOuter : 0f;
                    OccWhy[p.Key] = (b0 + (below ? 1 : 0), a0 + (above ? 1 : 0), s0 + (outSector ? 1 : 0), n0 + 1, d0 + beyond);
                }
                if (below || above || outSector) continue;
                inPen++;
            }
            into.Add(($"substrate/{p.Key}", inPen, n));
        }
    }

    public void Strikers(Cell c, List<Striker> into)
    {
        foreach (var pop in _pops)
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
            {
                if (!Core.Alive[i]) continue;
                var at = Core.Pos[i];
                if (!c.NearAnyPilot(at, 400f)) continue;
                // intent: the gregarious phase - and, for a RAMPED role (a bull's head-down, a mobber's pull-up), the
                // windup itself, which is that species' telegraph (Docs/SUBSTRATE_FAUNA.md §9: RampS "the telegraph"); a
                // bull arms on sight, before its herd's phase has flipped, so its phase alone read its charge as unwarned
                float intent = Core.Ramp[i] > 0f ? 1f : Core.Phase[i];
                into.Add(new Striker { Cls = pop.Key, Key = i, Pos = at, Intent = intent });
            }
    }

    /// <summary>The substrate's account: the stock its living agents hold (SubstrateCore.MassHeld) plus the bodies rammed
    /// out of the cell. MassIn - MassOut == MassHeld is the core's own law.</summary>
    /// A death is booked (rammed out, laid as a skeleton, fed to a hunter) the moment the glue decides it, but the core
    /// keeps the agent's stock until the queued kill lands next tick: that stock is not counted twice (a ledger read
    /// between the two - a ram on a minute boundary - otherwise sees the body in both places).
    public double Held() => Core.MassHeld() - _pendingKill + Rammed + _inFlight;
    double _pendingKill;

    public void Ledger(List<(string, double)> into)
    {
        into.Add(("substrate: seeded stock (source)", Seeded));
        into.Add(("substrate: held by living agents", Core.MassHeld()));
        into.Add(("substrate: bodies rammed out of the cell", Rammed));
        into.Add(("substrate: prey stock queued to hunters", PreyQueued));
        into.Add(("substrate: core self-audit MassIn - MassOut - MassHeld", Core.MassIn - Core.MassOut - Core.MassHeld()));
        into.Add(($"substrate: meals queued in a death's own pass, booked with the body ({LateMealCount})", LateMeals));
    }

    public string Report() => string.Join(", ", _pops.Select(p =>
        $"{p.Key} {Job.PopAlive[p.Index]}/{p.Cap} (peak {p.Peak}, bites {p.Bitten} of {p.Asked} asked, births {Core.Pops[p.Index].Births}, rammed {p.Rammed}, starved {p.Starved}, preyed {p.Preyed}, proxies peak {p.Proxies.Peak})")) +
        (AssetOverrides.Count > 0 ? "; shipped assets differ from the C# ports in " + string.Join(", ", AssetOverrides) : "");

    public void Snapshot(Utf8JsonWriter w)
    {
        w.WriteStartArray("substrate");
        foreach (var pop in _pops)
            for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
            {
                if (!Core.Alive[i]) continue;
                w.WriteStartArray();
                w.WriteNumberValue(MathF.Round(Core.Pos[i].X, 1)); w.WriteNumberValue(MathF.Round(Core.Pos[i].Y, 1)); w.WriteNumberValue(MathF.Round(Core.Pos[i].Z, 1));
                w.WriteStringValue(pop.Key); w.WriteNumberValue(Core.Danger[i] ? 1 : 0);
                w.WriteEndArray();
            }
        w.WriteEndArray();
    }
}

// ══════════════════════════════════════════════════════════════════════════════════════════════ builders

/// <summary>The fortress colony and the thief nest (BuilderColonyFauna) on the shared world.</summary>
sealed class BuilderSystem : ICellSystem, IOccupancy
{
    public string Name => "builders";
    public double StepHz => _hz;
    public double LastMs { get; private set; }
    public readonly BuilderColonyCore Fort;
    // round 11-10: the thief nest and the wearer colony are re-founded by the spawner after an extinction (SpawnerTick),
    // so these are replaced, not readonly
    public ThiefNestCore Thief;
    /// <summary>Round 11-10's WEARER colony (BuilderColonyFauna species Wearers = 2): founded in its band, roams the cell.</summary>
    public WearerCore? Wear;
    public readonly Vector3 FortAnchor;
    public Vector3 Nest, WearAnchor;
    readonly ProxyPool? _wearProxies;
    readonly float _wearEngage;
    readonly int _maxHearts;
    readonly float _hz, _vesselR;
    readonly ProxyPool _fortProxies, _thiefProxies;
    readonly float _fortEngage, _thiefEngage;
    public double Founders, DeadStomachs;
    public int ReuseSameStep, Rammed;
    readonly int _maxWorkers;
    readonly (float lo, float hi) _fortBand;
    const int ColonyDomain = 2;

    public BuilderSystem(Cell c, Func<Vector3, float, Vector3> outsideGroves)
    {
        var rows = c.L.GetProperty("builders").EnumerateArray().ToList();
        var fort = rows.First(r => r.GetProperty("species").GetInt32() == 0);
        var thief = rows.First(r => r.GetProperty("species").GetInt32() == 1);
        var fc = fort.GetProperty("config");
        var tc = thief.GetProperty("config");
        _hz = Cell.F(fc, "TickHz");
        _vesselR = Cell.F(fc, "VesselRadius");
        var fb = fort.GetProperty("band");
        float sense = Cell.F(fc, "Sense");
        // BuilderColonyFauna: the band is where the colony LIVES; it forages a worker's sense beyond it either side
        float bandInner = MathF.Max(0f, (float)fb[0].GetDouble() - sense), bandOuter = (float)fb[1].GetDouble() + sense;
        _fortBand = (bandInner, bandOuter);
        FortAnchor = c.RandomInShell((float)fb[0].GetDouble(), (float)fb[1].GetDouble());
        _maxWorkers = (int)Cell.F(fc, "MaxWorkers");
        Fort = new BuilderColonyCore(c.World, ColonyParams(fc, c.Membrane, bandInner, bandOuter), FortAnchor, ColonyDomain, 1, c.Seed * 13 + 1);
        var tb = thief.GetProperty("band");
        var spawn = c.RandomInShell((float)tb[0].GetDouble(), (float)tb[1].GetDouble());
        Nest = FindNestPlant(c, spawn, Cell.F(tc, "ScoutRange"), outsideGroves);
        _tc = tc; _tb = tb; _outside = outsideGroves;
        Thief = new ThiefNestCore(c.World, ThiefParams(tc, c.Membrane), Nest, ColonyDomain, 2, c.Seed * 13 + 2);
        var wear = rows.FirstOrDefault(r => r.GetProperty("species").GetInt32() == 2);
        if (wear.ValueKind == JsonValueKind.Object)
        {
            var wc = wear.GetProperty("config");
            var wb = wear.GetProperty("band");
            _wc = wc; _wb = wb;
            WearAnchor = c.RandomInShell((float)wb[0].GetDouble(), (float)wb[1].GetDouble());
            _maxHearts = (int)Cell.F(wc, "MaxWearerHearts");
            Wear = new WearerCore(c.World, WearerParams(wc, c.Membrane), WearAnchor, ColonyDomain, 3, c.Seed * 13 + 3);
            _wearProxies = new ProxyPool((int)Cell.F(wc, "MaxProxies"), Cell.F(wc, "ProxyLingerSeconds"));
            _wearEngage = Cell.F(wc, "EngageRadius");
        }
        Founders = Fort.StomachTotal + Thief.StomachTotal + (Wear?.StomachTotal ?? 0f);
        // BuilderColonyFauna.SenseVessels: the radius each species senses vessels over, and from where
        float fortSense = Cell.F(fc, "ShellRadius") * 2f + MathF.Max(Cell.F(fc, "AlarmRadius"), Cell.F(fc, "EngageRadius")) + Cell.F(fc, "Sense");
        _fortProxies = new ProxyPool((int)Cell.F(fc, "MaxProxies"), Cell.F(fc, "ProxyLingerSeconds"));
        _thiefProxies = new ProxyPool((int)Cell.F(tc, "MaxProxies"), Cell.F(tc, "ProxyLingerSeconds"));
        _fortC = MakeColony(c, "fortress", fc, FortAnchor, fortSense, _fortProxies, () => Fort.CanRoost, () => Fort.RoostSecondsLeft, Fort.Roost, Fort.Pos, Fort.Alive);
        _thiefC = MakeColony(c, "thieves", tc, Nest, Cell.F(tc, "Territory") + Cell.F(tc, "ScoutRange"), _thiefProxies,
                             () => Thief.CanRoost, () => Thief.RoostSecondsLeft, Thief.Roost, Thief.Pos, Thief.Alive);
        if (Wear != null)
            _wearC = MakeColony(c, "wearers", wear.GetProperty("config"), Vector3.Zero, c.Membrane, _wearProxies!,
                                () => Wear.CanRoost, () => Wear.RoostSecondsLeft, Wear.Roost, Wear.Pos, Wear.Alive);
        _fortEngage = Cell.F(fc, "EngageRadius"); _thiefEngage = Cell.F(tc, "EngageRadius");
    }

    // ── round 11-10: extinction recovery (BuilderColonyFauna.Extinction + RandomLifeSpawner) ─────────────────────────
    readonly JsonElement _tc, _tb, _wc, _wb;
    readonly Func<Vector3, float, Vector3> _outside;
    float _thiefSince = -1f, _wearSince = -1f;
    bool _thiefGone, _wearGone;
    public int ThiefReseeds, WearReseeds;
    double _retiredHeld, _retiredEaten, _retiredMetab;
    int _retiredThiefKills, _retiredThiefBirths, _retiredWearKills, _retiredWearBirths;

    static float Linger(JsonElement cfg) => cfg.TryGetProperty("ExtinctLingerSeconds", out var v) ? (float)v.GetDouble() : 8f;

    /// <summary>BuilderColonyFauna.Extinction, every tick: no member, no proxy for ExtinctLingerSeconds and the anchor
    /// leaves. (The fortress has never gone extinct in a run; it is reported by C4 if it does.)</summary>
    public void TrackExtinction(Cell c)
    {
        static void Track(Cell c, bool empty, float linger, ref float since, ref bool gone)
        {
            if (gone) return;
            if (!empty) { since = -1f; return; }
            if (since < 0f) { since = c.T; return; }
            if (c.T - since >= linger) gone = true;
        }
        Track(c, Thief.AliveCount == 0 && _thiefProxies.Count == 0, Linger(_tc), ref _thiefSince, ref _thiefGone);
        if (Wear != null) Track(c, Wear.AliveCount == 0 && _wearProxies!.Count == 0, Linger(_wc), ref _wearSince, ref _wearGone);
    }

    /// <summary>RandomLifeSpawner's tick for each builder config: a colony whose anchor left is re-founded at a random
    /// point of its band (the nest perched on the nearest plant, BuilderColonyFauna.Seed). The old anchor's OnDestroy
    /// released its structure (hoard, lair) as loose prisms, and its books (metabolised, birth bills, eaten) stay in the
    /// ledger; the founders' stomachs are a new source.</summary>
    public void SpawnerTick(Cell c)
    {
        if (_thiefGone)
        {
            foreach (int h in Thief.Hoard) c.World.SetBuilt(h, Thief.ColonyId, -(h + 1), false);
            _retiredHeld += Thief.StomachTotal + Thief.Metabolised + 0.5 * Thief.BirthPaid;
            _retiredEaten += Thief.EatenVolume; _retiredMetab += Thief.Metabolised;
            _retiredThiefKills += Thief.Kills; _retiredThiefBirths += Thief.Births;
            ThiefReseeds++;
            var spawn = c.RandomInShell((float)_tb[0].GetDouble(), (float)_tb[1].GetDouble());
            Nest = FindNestPlant(c, spawn, Cell.F(_tc, "ScoutRange"), _outside);
            Thief = new ThiefNestCore(c.World, ThiefParams(_tc, c.Membrane), Nest, ColonyDomain, 2, c.Seed * 13 + 2 + 100 * ThiefReseeds);
            Founders += Thief.StomachTotal;
            var old = _thiefC;
            _thiefC = MakeColony(c, "thieves", _tc, Nest, Cell.F(_tc, "Territory") + Cell.F(_tc, "ScoutRange"), _thiefProxies,
                                 () => Thief.CanRoost, () => Thief.RoostSecondsLeft, Thief.Roost, Thief.Pos, Thief.Alive, register: false);
            c.ReplaceLod("thieves", old, _thiefC);
            _thiefGone = false; _thiefSince = -1f;
        }
        if (_wearGone && Wear != null)
        {
            foreach (int h in Wear.Lair) c.World.SetBuilt(h, Wear.ColonyId, -(h + 1), false);
            _retiredHeld += Wear.StomachTotal + Wear.Metabolised;
            _retiredEaten += Wear.EatenVolume; _retiredMetab += Wear.Metabolised;
            _retiredWearKills += Wear.Kills; _retiredWearBirths += Wear.Births;
            WearReseeds++;
            WearAnchor = c.RandomInShell((float)_wb[0].GetDouble(), (float)_wb[1].GetDouble());
            Wear = new WearerCore(c.World, WearerParams(_wc, c.Membrane), WearAnchor, ColonyDomain, 3, c.Seed * 13 + 3 + 100 * WearReseeds);
            Founders += Wear.StomachTotal;
            var old = _wearC!;
            var w = Wear;
            _wearC = MakeColony(c, "wearers", _wc, Vector3.Zero, c.Membrane, _wearProxies!,
                                () => w.CanRoost, () => w.RoostSecondsLeft, w.Roost, w.Pos, w.Alive, register: false);
            c.ReplaceLod("wearers", old, _wearC);
            _wearGone = false; _wearSince = -1f;
        }
    }

    /// <summary>BuilderColonyConfigSO.ToColonyParams.</summary>
    static BuilderColonyParams ColonyParams(JsonElement k, float membrane, float bandInner, float bandOuter) => new()
    {
        Founders = (int)Cell.F(k, "Founders"), MaxWorkers = (int)Cell.F(k, "MaxWorkers"), Speed = Cell.F(k, "WorkerSpeed"),
        Sense = Cell.F(k, "Sense"), ForageFraction = (int)Cell.F(k, "ForageFraction"),
        Spacing = Cell.F(k, "LatticeSpacing"), LatticeHalf = (int)Cell.F(k, "LatticeHalf"),
        Rc = Cell.F(k, "ShellRadius"), W = Cell.F(k, "ShellWidth"), KCement = Cell.F(k, "CementK"), Nucleate = Cell.F(k, "Nucleate"),
        Mend = (BuilderMendRule)(int)Cell.F(k, "Mend"), GapGain = Cell.F(k, "GapGain"), AlarmGain = Cell.F(k, "AlarmGain"), ScarGain = Cell.F(k, "ScarGain"),
        AlarmRadius = Cell.F(k, "AlarmRadius"), StrikeAt = Cell.F(k, "StrikeAt"), DefendCaste = Cell.F(k, "DefendCaste"),
        SettleSeconds = Cell.F(k, "SettleSeconds"),
        Containment = membrane * 0.95f, CellCentre = Vector3.Zero, BandInner = bandInner, BandOuter = bandOuter,
        Stomach = new BuilderStomachParams
        {
            Capacity = Cell.F(k, "WorkerStomach"), Metabolism = Cell.F(k, "WorkerMetabolism"), Torpor = Cell.F(k, "WorkerMetabolism"),
            HungryBelow = Cell.F(k, "WorkerHungryBelow"), BirthAbove = Cell.F(k, "WorkerBirthAbove"), BirthCost = Cell.F(k, "WorkerBirthCost"),
            OwnDomainBelow = Cell.F(k, "WorkerOwnDomainBelow"),
        },
    };

    /// <summary>BuilderColonyConfigSO.ToThiefParams.</summary>
    static ThiefParams ThiefParams(JsonElement k, float membrane) => new()
    {
        Founders = (int)Cell.F(k, "ThiefFounders"), MaxThieves = (int)Cell.F(k, "MaxThieves"), FreeSpeed = Cell.F(k, "ThiefSpeed"),
        LadenSpeed = Cell.F(k, "LadenSpeed"), Warm = Cell.F(k, "WarmSeconds"), Spot = Cell.F(k, "SpotRange"), Scout = Cell.F(k, "ScoutRange"),
        Territory = Cell.F(k, "Territory"), TimidRange = Cell.F(k, "TimidRange"), SettleSeconds = Cell.F(k, "SettleSeconds"),
        Containment = membrane * 0.92f, MembraneRadius = membrane, CellCentre = Vector3.Zero,
        Stomach = new BuilderStomachParams
        {
            Capacity = Cell.F(k, "ThiefStomach"), Metabolism = Cell.F(k, "ThiefMetabolism"), Torpor = Cell.F(k, "ThiefTorpor"),
            HungryBelow = Cell.F(k, "ThiefHungryBelow"), BirthAbove = Cell.F(k, "ThiefBirthAbove"), BirthCost = Cell.F(k, "ThiefBirthCost"),
            OwnDomainBelow = Cell.F(k, "ThiefOwnDomainBelow"),
        },
    };

    /// <summary>BuilderColonyConfigSO.ToWearerParams.</summary>
    static WearerParams WearerParams(JsonElement k, float membrane) => new()
    {
        Founders = (int)Cell.F(k, "WearerFounders"), MaxHearts = (int)Cell.F(k, "MaxWearerHearts"), Speed = Cell.F(k, "WearerSpeed"),
        HuntAt = (int)Cell.F(k, "WearHuntAt"), BodyCap = (int)Cell.F(k, "WearBodyCap"), WornCap = (int)Cell.F(k, "WornCap"),
        Sense = Cell.F(k, "WearSense"), KeepOff = Cell.F(k, "WearKeepOff"), Windup = Cell.F(k, "WearWindup"), Lunge = Cell.F(k, "WearLunge"),
        RearAt = Cell.F(k, "WearRearAt"), Contact = Cell.F(k, "WearContact"), Sight = Cell.F(k, "WearSight"),
        HurtFraction = Cell.F(k, "WearHurtFraction"), HurtShed = Cell.F(k, "WearHurtShed"), HurtWindow = Cell.F(k, "WearHurtWindow"),
        Containment = membrane * 0.95f, CellCentre = Vector3.Zero,
        Stomach = new BuilderStomachParams
        {
            Capacity = Cell.F(k, "WearerStomach"), FounderFill = 0.6f, Metabolism = Cell.F(k, "WearerMetabolism"), Torpor = Cell.F(k, "WearerMetabolism"),
            HungryBelow = Cell.F(k, "WearerHungryBelow"), BirthAbove = 0.9f, BirthCost = Cell.F(k, "WearerBirthCost"),
            OwnDomainBelow = Cell.F(k, "WearerOwnDomainBelow"),
        },
    };

    /// <summary>BuilderColonyFauna.FindNestPlant: the nearest living plant heart within ScoutRange of the spawn point (and
    /// inside 0.9 x membrane), perched 6 u outward; with none, the spawn point pushed clear of every grove.</summary>
    static Vector3 FindNestPlant(Cell c, Vector3 from, float scout, Func<Vector3, float, Vector3> outsideGroves)
    {
        Plant? best = null;
        float bd = scout * scout;
        foreach (var f in c.World.Plants)
        {
            if (!f.Alive || f.Heart.Length() > c.Membrane * 0.9f) continue;
            float d = Vector3.DistanceSquared(f.Heart, from);
            if (d <= bd) { bd = d; best = f; }
        }
        if (best == null) return outsideGroves(from, 40f);
        var outward = best.Heart.LengthSquared() > 1f ? Vector3.Normalize(best.Heart) : Vector3.UnitY;
        return best.Heart + outward * 6f;
    }

    /// <summary>
    /// One colony as BuilderColonyFauna hosts it: the vessels it SENSES (the glue's overlap - a fortress around its
    /// anchor, a thief nest over its territory + scout range, a wearer membrane-wide from the cell centre), and its
    /// IMacroPopulation (round 11f-2, Docs/ECOLOGY_LOD.md §6.2, the roost route): collapse only with no proxy, no vessel
    /// sensed, the core able to roost and twice the thaw margin of torpor left; a colony that is ready except for its
    /// carriers starts a 2.5 s wind-down; MacroTick = Roost(1); expand before a stomach empties.
    /// </summary>
    sealed class Colony : IMacroPopulation
    {
        public string Name = "";
        public Vector3 SenseFrom; public float SenseRadius;
        public BuilderVessel[] Vessels = new BuilderVessel[8];
        public int VesselCount;
        public ProxyPool Proxies = null!;
        public Func<bool> CoreCanRoost = null!; public Func<float> RoostLeft = null!; public Action<float> Roost = null!;
        public Vector3[] Pos = null!; public bool[] Alive = null!;
        public float Thaw, Pad, WindDownUntil = float.NegativeInfinity, Now;
        public bool Collapsed;
        public Vector3 Centre; public float Extent;
        public const float WindDownSeconds = 2.5f;

        public void Sense(Cell c, float vesselR)
        {
            VesselCount = 0;
            if (Vessels.Length < c.Pilots.Count) Vessels = new BuilderVessel[c.Pilots.Count];
            for (int k = 0; k < c.Pilots.Count; k++)
            {
                var p = c.Pilots[k];
                if (Vector3.Distance(p.Pos, SenseFrom) > SenseRadius) continue;
                Vessels[VesselCount++] = new BuilderVessel { Pos = p.Pos, Vel = p.Vel, Radius = vesselR, Id = k, Domain = p.Domain, Rams = true };
            }
        }

        /// <summary>BuildFrame's bounds: the alive members' box, padded (20 + body length + heart).</summary>
        public void Bounds()
        {
            var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue); int n = 0;
            for (int k = 0; k < Pos.Length; k++) if (Alive[k]) { lo = Vector3.Min(lo, Pos[k]); hi = Vector3.Max(hi, Pos[k]); n++; }
            if (n == 0) { Extent = 1f; return; }
            lo -= new Vector3(Pad); hi += new Vector3(Pad);
            Centre = (lo + hi) * 0.5f; Extent = ((hi - lo) * 0.5f).Length();
        }

        public Vector3 MacroCentre => Centre;
        public float MacroExtent => Extent;
        public bool IsCollapsed => Collapsed;
        public bool CanCollapse
        {
            get
            {
                if (Collapsed || Proxies.Count > 0 || VesselCount > 0) return false;
                if (!CoreCanRoost()) { WindDownUntil = Now + WindDownSeconds; return false; }
                return RoostLeft() > 2f * Thaw;
            }
        }
        public bool NeedsIndividuals => Collapsed && (Proxies.Count > 0 || RoostLeft() < Thaw);
        public MacroPopulationTotals Totals => default;
        public bool Collapse() { if (!CanCollapse) return false; Collapsed = true; return true; }
        public void Expand() => Collapsed = false;
        public void MacroTick(float dt) { if (Collapsed) Roost(dt); }
    }

    readonly Colony _fortC;
    Colony _thiefC;
    Colony? _wearC;

    Colony MakeColony(Cell c, string name, JsonElement cfg, Vector3 senseFrom, float senseRadius, ProxyPool pool,
                      Func<bool> canRoost, Func<float> left, Action<float> roost, Vector3[] pos, bool[] alive, bool register = true)
    {
        var bs = cfg.GetProperty("BodyScale");
        var col = new Colony
        {
            Name = name, SenseFrom = senseFrom, SenseRadius = senseRadius, Proxies = pool, CoreCanRoost = canRoost, RoostLeft = left,
            Roost = roost, Pos = pos, Alive = alive,
            Thaw = cfg.TryGetProperty("ThawReserveSeconds", out _) ? Cell.F(cfg, "ThawReserveSeconds") : 20f,
            Pad = 20f + (float)bs[2].GetDouble() + 2f,
        };
        bool on = !cfg.TryGetProperty("MacroLod", out _) || Cell.F(cfg, "MacroLod") > 0.5f;
        if (register) c.RegisterLod(name, col, on);
        return col;
    }

    /// <summary>False when the colony roosts this tick: it only draws (BuilderColonyFauna.Update), so its core's
    /// per-step lists (deaths, births, stings, strikes) still hold the LAST step's and must not be read again.</summary>
    bool StepColony(Colony col, Cell c, Action<BuilderVessel[], int> step)
    {
        col.Now = c.T;
        col.Sense(c, _vesselR);
        if (col.Collapsed) return false;
        step(col.Vessels, col.VesselCount);
        col.Bounds();
        return true;
    }

    public void Tick(Cell c)
    {
        int n = c.Pilots.Count;
        // a vessel flying through the fortress wall explodes the prisms it hits (the raider's cut)
        var q = new List<int>();
        foreach (var p in c.Pilots)
        {
            if (Vector3.Distance(p.Pos, FortAnchor) > 120f) continue;
            c.World.QuerySphere(p.Pos, p.Radius + 4f, q);
            foreach (int h in q) if (Fort.IsBuilt(h)) { c.World.Destroy(h, "ram"); Rammed++; }
        }
        long t0 = Stopwatch.GetTimestamp();
        c.World.Eater = "builders";
        float dt = 1f / _hz;
        bool fortStepped = StepColony(_fortC, c, (v, k) => { Fort.WindDown = k == 0 && c.T < _fortC.WindDownUntil; Fort.Step(dt, v, k); });
        // thieves and wearers sense vessels within their own sight sphere (BuilderColonyFauna.SenseVessels), not the
        // whole membrane, so a colony nobody is near can roost (round 11-10 target 5)
        (_thiefC.SenseFrom, _thiefC.SenseRadius) = Thief.SightSphere();
        if (Wear != null) (_wearC!.SenseFrom, _wearC.SenseRadius) = Wear.SightSphere();
        c.World.Stealer = "thieves";
        bool thiefStepped = StepColony(_thiefC, c, (v, k) => Thief.Step(dt, v, k));
        c.World.Stealer = "wearers";
        bool wearStepped = Wear != null && StepColony(_wearC!, c, (v, k) => Wear.Step(dt, v, k));
        LastMs = Ms.Since(t0);
        if (s_deathTrace && thiefStepped)
            foreach (var d in Thief.Deaths)
                Console.WriteLine($"   DEATH thief t={c.T:F1} by {(d.Vessel >= 0 && d.Vessel < c.Pilots.Count ? c.Pilots[d.Vessel].Kind : d.Vessel.ToString())} at r={d.At.Length():F0} nest {Vector3.Distance(d.At, Thief.Nest):F0}u carry {Thief.Carry[d.Agent]} claim {Thief.Claim[d.Agent]} pilotV {(d.Vessel >= 0 && d.Vessel < c.Pilots.Count ? c.Pilots[d.Vessel].Vel.Length() : 0):F0}");
        if (s_deathTrace && wearStepped)
            foreach (var d in Wear!.Deaths)
                Console.WriteLine($"   DEATH wearer t={c.T:F1} by {(d.Vessel >= 0 && d.Vessel < c.Pilots.Count ? c.Pilots[d.Vessel].Kind : d.Vessel.ToString())} at r={d.At.Length():F0} stomach {d.Stomach:F1} alive {Wear.AliveCount}");
        if (fortStepped) BookDeaths(Fort.Deaths, Fort.Born);
        if (thiefStepped) BookDeaths(Thief.Deaths, Thief.Born);
        if (wearStepped) BookDeaths(Wear!.Deaths, Wear.Born);
        if (fortStepped)
            foreach (int v in Fort.Stings)
                if (v >= 0 && v < n) c.AddContact(v, "fortress", c.Pilots[v].Pos);
        // a lunge that touched its pilot: the body's prisms are danger-tier for the lunge - a hostile contact (a burn)
        if (wearStepped)
            foreach (var (heart, v) in Wear!.Struck)
                // rated on the heart that LUNGED (its striker key), not the nearest heart: a creature 100 prisms wide puts another
                // colony member's heart nearer the pilot than the one whose rear and lunge it watched
                if (v >= 0 && v < n) c.AddContact(v, "wearer", Wear.Pos[heart], telegraphed: c.ArmedFor("wearer", 1_000_000 + heart));
        foreach (var (owner, by) in c.World.StolenFrom) c.Meet(owner, by, false);
        c.World.StolenFrom.Clear();
        Engage(c, Fort.Pos, Fort.Alive, _fortProxies, _fortEngage);
        Engage(c, Thief.Pos, Thief.Alive, _thiefProxies, _thiefEngage);
        if (Wear != null) Engage(c, Wear.Pos, Wear.Alive, _wearProxies!, _wearEngage);
    }

    /// <summary>A member's stomach at death leaves with it (Docs/BUILDERS_AND_THIEVES.md "unstated volume"); it is booked
    /// here from <see cref="BuilderDeath.Stomach"/>, recorded at the death - a birth in the SAME step can re-use the slot
    /// and overwrite the array (QA-SWARM-ROUND11-9). Those re-uses are counted to show the case is exercised.</summary>
    static readonly bool s_deathTrace = Environment.GetEnvironmentVariable("SHOWCASE_DEATH_TRACE") == "1";
    void BookDeaths(List<BuilderDeath> deaths, List<int> born)
    {
        foreach (var d in deaths)
        {
            if (born.Contains(d.Agent)) ReuseSameStep++;
            DeadStomachs += d.Stomach;
        }
    }

    static void Engage(Cell c, Vector3[] pos, bool[] alive, ProxyPool pool, float r)
    {
        for (int k = 0; k < pos.Length; k++) if (alive[k] && c.NearAnyPilot(pos[k], r)) pool.Want(k, c.T);
        pool.Retire(c.T);
    }

    public int Colliders(Cell c) => 2 * (_fortProxies.Count + _thiefProxies.Count + (_wearProxies?.Count ?? 0));

    /// <summary>C8: members inside the band each colony lives in, widened by a worker's forage sense either side (the
    /// band the core is given - BuilderColonyFauna). Thieves and wearers range the cell by design (a raid, a hunt): their
    /// pen is reported, not asserted (Program.Summary).</summary>
    public void Occupancy(Cell c, List<(string, int, int)> into)
    {
        void Count(string cls, int cap, Func<int, bool> alive, Func<int, Vector3> pos, float lo, float hi)
        {
            int n = 0, inPen = 0;
            for (int k = 0; k < cap; k++)
            {
                if (!alive(k)) continue;
                n++;
                float r = pos(k).Length();
                if (r >= lo && r <= hi) inPen++;
            }
            into.Add((cls, inPen, n));
        }
        Count("fortress/workers", Fort.Cap, k => Fort.Alive[k], k => Fort.Pos[k], _fortBand.lo, _fortBand.hi);
        Count("thieves", Thief.Cap, k => Thief.Alive[k], k => Thief.Pos[k], (float)_tb[0].GetDouble(), (float)_tb[1].GetDouble());
        if (Wear != null) Count("wearers/hearts", Wear.Cap, k => Wear.Alive[k], k => Wear.Pos[k], (float)_wb[0].GetDouble(), (float)_wb[1].GetDouble());
    }

    public void Census(List<(string, int, int)> into)
    {
        into.Add(("fortress/workers", Fort.AliveCount, _maxWorkers));
        into.Add(("thieves", Thief.AliveCount, Thief.Cap));
        if (Wear != null) into.Add(("wearers/hearts", Wear.AliveCount, _maxHearts));
    }

    public void Strikers(Cell c, List<Striker> into)
    {
        for (int k = 0; k < Fort.Cap; k++)
            if (Fort.Alive[k] && c.NearAnyPilot(Fort.Pos[k], 400f))
                into.Add(new Striker { Cls = "fortress", Key = k, Pos = Fort.Pos[k], Intent = Fort.Intent[k] });
        if (Wear == null) return;
        // a wearer's strike is its creature's: the leader's REAR (intent 0.5-1 over the 1 s windup) is the telegraph
        for (int k = 0; k < Wear.Cap; k++)
            if (Wear.IsLeader(k) && c.NearAnyPilot(Wear.Pos[k], 400f))
                into.Add(new Striker { Cls = "wearer", Key = 1_000_000 + k, Pos = Wear.Pos[k], Intent = Wear.Intent[k] });
    }

    /// <summary>Builders' account: living stomachs + stomachs that died with their owners + metabolised (the colony's one
    /// declared exit) + half of every birth bill (the newborn's body; the other half is its stomach).
    /// A wearer's birth is paid from EATEN moult volume straight into the newborn's stomach (the overflow is metabolised),
    /// so its account is stomachs + metabolised, with no body half.</summary>
    public double Held() => Fort.StomachTotal + Thief.StomachTotal + DeadStomachs + Fort.Metabolised + Thief.Metabolised
                            + 0.5 * (Fort.BirthPaid + Thief.BirthPaid) + (Wear != null ? Wear.StomachTotal + Wear.Metabolised : 0.0)
                            + _retiredHeld;

    public void Ledger(List<(string, double)> into)
    {
        into.Add(("builders: founders' stomachs (source)", Founders));
        into.Add(("builders: eaten (fortress + thieves + wearers, + re-founded colonies' predecessors)", Fort.EatenVolume + Thief.EatenVolume + (Wear?.EatenVolume ?? 0f) + _retiredEaten));
        into.Add(("builders: metabolised (declared exit)", Fort.Metabolised + Thief.Metabolised + (Wear?.Metabolised ?? 0f) + _retiredMetab));
        into.Add(("builders: stomachs dead with their owners", DeadStomachs));
        into.Add(("builders: same-step slot re-uses (booked from BuilderDeath.Stomach)", ReuseSameStep));
    }

    public string Report() =>
        $"fortress {Fort.AliveCount}/{_maxWorkers} built {Fort.Built} placed {Fort.Placed} repairs {Fort.Repairs} kills {Fort.Kills} starved {Fort.Starved} " +
        $"stings {Fort.StingCount} walls rammed {Rammed}; thieves {Thief.AliveCount}/{Thief.Cap} hoard {Thief.HoardCount} steals {Thief.Steals} " +
        $"raided {Thief.Raided} recaptured {Thief.Recaptured} kills {Thief.Kills} starved {Thief.Starved} births {Thief.Births}" +
        (ThiefReseeds > 0 ? $" (nest re-founded {ThiefReseeds}x; earlier nests: kills {_retiredThiefKills}, births {_retiredThiefBirths})" : "") + "; " +
        (Wear == null ? "" :
        $"wearers {Wear.AliveCount}/{_maxHearts} creatures {Wear.Creatures} largest {Wear.Largest} (max {Wear.MaxBody}) worn steals {Wear.WornSteals} " +
        $"(trail {Wear.WornTrail}) fusions {Wear.Fusions} lunges {Wear.Lunges} hits {Wear.Hits} stripped {Wear.Stripped} moults {Wear.SatiationMoults}+{Wear.HurtMoults} " +
        $"lair {Wear.LairCount} births {Wear.Births} kills {Wear.Kills} starved {Wear.Starved}" +
        (WearReseeds > 0 ? $" (re-founded {WearReseeds}x; earlier colonies: kills {_retiredWearKills}, births {_retiredWearBirths})" : "") + "; ") +
        $"proxies peak {_fortProxies.Peak}+{_thiefProxies.Peak}+{_wearProxies?.Peak ?? 0}";

    public void Snapshot(Utf8JsonWriter w)
    {
        w.WriteStartObject("builders");
        Program.WriteVec(w, "fort_anchor", FortAnchor);
        Program.WriteVec(w, "nest", Nest);
        WritePts(w, "workers", Enumerable.Range(0, Fort.Cap).Where(k => Fort.Alive[k]).Select(k => Fort.Pos[k]));
        WritePts(w, "walls", Fort.BuiltHandles.Select(h => FortWorld!.Pos[h]));
        WritePts(w, "thieves", Enumerable.Range(0, Thief.Cap).Where(k => Thief.Alive[k]).Select(k => Thief.Pos[k]));
        WritePts(w, "hoard", Thief.Hoard.Select(h => FortWorld!.Pos[h]));
        if (Wear != null)
        {
            WritePts(w, "wearer_hearts", Enumerable.Range(0, Wear.Cap).Where(k => Wear.Alive[k]).Select(k => Wear.Pos[k]));
            WritePts(w, "worn", Enumerable.Range(0, Wear.Cap).Where(k => Wear.IsLeader(k)).SelectMany(k => Wear.BodyOf(k)).Select(h => FortWorld!.Pos[h]));
        }
        w.WriteEndObject();
    }

    public CellWorld? FortWorld;

    static void WritePts(Utf8JsonWriter w, string name, IEnumerable<Vector3> pts)
    {
        w.WriteStartArray(name);
        foreach (var p in pts)
        {
            w.WriteStartArray();
            w.WriteNumberValue(MathF.Round(p.X, 1)); w.WriteNumberValue(MathF.Round(p.Y, 1)); w.WriteNumberValue(MathF.Round(p.Z, 1));
            w.WriteEndArray();
        }
        w.WriteEndArray();
    }
}
