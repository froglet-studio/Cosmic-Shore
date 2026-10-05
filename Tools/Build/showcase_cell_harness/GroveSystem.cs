// The threat-flora grove in the showcase cell (round 11c, Docs/THREAT_FLORA.md): SnapTrapCore + PhysarumCore on the
// SHARED world, mirroring ThreatGrove.cs - its IsEdible (ordinary cell mass only: never shielded, never a living body,
// never grove tissue, never built or carried), its slot / tube laying (from the colony's reserve, so the prisms are
// world mass), PickTrapSite / RootAtRim, PickSclerotiumSite, and the threat harness's contact rules (teeth burn,
// a closing jaw snaps, a pulsing tube or a beating heart stings, a resting tube or a plain slot is rammed).
// Compiled by run.sh only when Assets/.../ThreatFlora exists (-define:THREAT_FLORA).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CosmicShore.Gameplay;

sealed class GroveSystem : ICellSystem
{
    public string Name => "grove";
    public double StepHz => _snapHz;   // the snap traps' rate; the physarum's 10 Hz is billed inside the same LastMs
    public double LastMs { get; private set; }
    public readonly ThreatGroveShape Shape;
    public readonly SnapTrapCore Snap;
    public readonly PhysarumCore Phys;
    readonly JsonElement _cfg, _def;
    readonly float _snapHz, _budRetry, _foodRefresh;
    readonly int _rootBites, _snapCap, _clumps;
    readonly int[] _clumpCount;
    int[] _slot = new int[SnapTrapCore.SlotCount * 16];
    readonly Dictionary<int, int> _tube = new();
    float _snapAcc, _budRetryAt, _foodAt;
    ThreatVessel[] _vessels = new ThreatVessel[8];
    readonly Dictionary<int, Vector3> _lastPos = new();
    readonly Dictionary<(int wave, int pilot), float> _waveSeen = new();
    public double Planted;
    public int Snaps, ToothBurns, TubeBurns, BeatBurns, SlotsRammed, TubesRammed, Buds;
    readonly Cell _c;

    static Vector3 V(JsonElement e) => new((float)e[0].GetDouble(), (float)e[1].GetDouble(), (float)e[2].GetDouble());
    static float Prod(Vector3 v) => v.X * v.Y * v.Z;

    public GroveSystem(Cell c)
    {
        _c = c;
        var g = c.L.GetProperty("grove");
        _cfg = g.GetProperty("config");
        _def = g.GetProperty("defaults");
        Shape = ThreatGroveShape.MakeShellSector(Vector3.Zero, Vector3.Normalize(V(_cfg.GetProperty("Axis"))),
            Cell.F(_cfg, "InnerRadius"), Cell.F(_cfg, "OuterRadius"), Cell.F(_cfg, "HalfAngleDegrees"));
        _snapHz = Cell.F(_cfg, "SnapTrapHz");
        _budRetry = Cell.F(_cfg, "BudRetrySeconds");
        _foodRefresh = Cell.F(_cfg, "FoodRefreshSeconds");
        _rootBites = (int)Cell.F(_cfg, "RootBitesPerAbsorb");
        _snapCap = (int)Cell.F(_def, "SnapTrapCap");
        _clumps = (int)Cell.F(_cfg, "SnapTrapClumps");
        _clumpCount = new int[Math.Max(1, _clumps)];
        Array.Fill(_slot, -1);

        // ThreatGroveConfigSO.BuildSnapTrap / BuildPhysarum (the element scalings are the cores' own ForElement)
        var sp = new SnapTrapParams
        {
            StalkVolume = Prod(V(_cfg.GetProperty("StalkLeaf"))), LobeVolume = Prod(V(_cfg.GetProperty("LobeLeaf"))),
            ToothVolume = Prod(V(_cfg.GetProperty("ToothLeaf"))), HelioConeDegrees = Cell.F(_cfg, "HelioConeDegrees"),
        };
        Snap = new SnapTrapCore(sp.ForElement((int)Cell.F(_def, "SnapTrapElement")), (ulong)(c.Seed * 7919 + 17));
        float tubeV = Prod(V(_cfg.GetProperty("TubeLeaf")));
        float shellV = Cell.F(_def, "ShellPrisms") * Prod(V(_cfg.GetProperty("ShellLeaf")));
        var pp = new PhysarumParams
        {
            TubeVolume = tubeV, ShellVolume = shellV, MaxTubes = (int)Cell.F(_cfg, "MaxTubes"),
            StepSeconds = 1f / MathF.Max(1f, Cell.F(_cfg, "PhysarumHz")),
        };
        Phys = new PhysarumCore(pp.ForElement((int)Cell.F(_def, "PhysarumElement")), Shape, (ulong)(c.Seed * 7919 + 17) ^ 0x9E3779B9UL);

        // the seeder: the snap-trap floor across the clumps, and the sclerotia (their shells are not laid here - the
        // shell's volume stays in the network's reserve; their sting is the beat's HeartGuard)
        int floor = (int)Cell.F(_def, "SnapTrapSeedFloor");
        for (int k = 0; k < floor; k++)
        {
            var at = PickTrapSite(out var axis);
            Snap.AddTrap(at, axis, false);
            ProcessSnapEvents();
        }
        float planted = Cell.F(_cfg, "PlantedTubesPerSclerotium") * tubeV + shellV;
        var rng = new ThreatRng((ulong)(c.Seed * 131 + 7));
        for (int k = 0; k < (int)Cell.F(_def, "Sclerotia"); k++) Phys.AddHeart(Shape.Sample(ref rng, 40f), planted);
        Planted = Snap.Planted + Phys.Planted;
        // the grove before you arrive: the research warm-up, eating what it digests, then the network it settled on
        RefreshFood();
        Phys.RunWarmup();
        ProcessPhysarumEvents(warm: true);
        foreach (int v in Phys.TubeVoxels()) LayTube(v);
    }

    Vector3 ClumpCentre(int k)
    {
        int n = Math.Max(1, _clumps);
        var axis = Shape.Axis;
        ThreatFloraMath.Frame(axis, out var e1, out var e2);
        float ang = n == 1 ? 0f : ThreatFloraMath.Deg2Rad(Cell.F(_cfg, "ClumpSpreadDegrees"));
        float phi = 2f * MathF.PI * k / n;
        var dir = Vector3.Normalize(axis + (e1 * MathF.Cos(phi) + e2 * MathF.Sin(phi)) * MathF.Tan(ang));
        return Shape.CellCentre + dir * (0.5f * (Shape.InnerRadius + Shape.OuterRadius));
    }

    /// <summary>ThreatGrove.PickTrapSite + RootAtRim.</summary>
    Vector3 PickTrapSite(out Vector3 axis)
    {
        int c = 0;
        for (int k = 1; k < _clumpCount.Length; k++) if (_clumpCount[k] < _clumpCount[c]) c = k;
        _clumpCount[c]++;
        var inBall = _c.Rng.OnUnitSphere() * MathF.Cbrt(_c.Rng.NextFloat());
        var p = RootAtRim(ClumpCentre(c) + inBall * Cell.F(_cfg, "ClumpRadius"));
        axis = Vector3.Normalize(Shape.CellCentre - p);
        return p;
    }

    Vector3 RootAtRim(Vector3 at)
    {
        var p = Shape.Clamp(at, 20f);
        var off = p - Shape.CellCentre;
        float d = off.Length();
        if (d < 1e-3f) return p;
        float r = Shape.OuterRadius - _c.Rng.Range(Cell.F(_def, "TrapRootDepthMin"), Cell.F(_def, "TrapRootDepthMax"));
        return Shape.CellCentre + off / d * r;
    }

    /// <summary>ThreatGrove.Eat: up to <paramref name="bites"/> edible prisms within radius of at, nearest first.</summary>
    float Eat(Vector3 at, float radius, int bites)
    {
        var q = new List<int>();
        _c.World.QuerySphere(at, radius, q);
        float eaten = 0f;
        for (int n = 0; n < bites; n++)
        {
            int best = -1; float bd = float.MaxValue;
            foreach (int h in q)
            {
                if (!_c.World.GroveEdible(h)) continue;
                float d = Vector3.DistanceSquared(_c.World.Pos[h], at);
                if (d < bd) { bd = d; best = h; }
            }
            if (best < 0) break;
            eaten += _c.World.Eat(best, "grove");
        }
        return eaten;
    }

    void ProcessSnapEvents()
    {
        var ev = Snap.Events;
        for (int e = 0; e < ev.Count; e++)
        {
            var x = ev[e];
            switch (x.Kind)
            {
                case SnapTrapEventKind.Lay:
                {
                    Snap.PosedSlot(x.Trap, x.Slot, out var pos, out _, out _);
                    int o = x.Trap * SnapTrapCore.SlotCount + x.Slot;
                    if (_slot.Length <= o) { int old = _slot.Length; Array.Resize(ref _slot, Math.Max(o + 1, old * 2)); Array.Fill(_slot, -1, old, _slot.Length - old); }
                    int h = _c.World.LayFrom(pos, Snap.SlotVolume(x.Slot), MassKind.TrapTissue, "grove", 2);
                    _c.World.Element[h] = 3;   // a Time trap's tissue, as a grazer's IsFood would bank it
                    _slot[o] = h;
                    break;
                }
                case SnapTrapEventKind.Pose:
                    for (int s = 0; s < SnapTrapCore.SlotCount; s++)
                    {
                        int h = _slot[x.Trap * SnapTrapCore.SlotCount + s];
                        if (h < 0 || !_c.World.AliveL[h]) continue;
                        Snap.PosedSlot(x.Trap, s, out var pos, out _, out _);
                        _c.World.Pos[h] = pos;
                    }
                    break;
                case SnapTrapEventKind.Snap:
                    if (x.Vessel >= 0 && x.Vessel < _c.Pilots.Count)
                    {
                        Snaps++;
                        _c.AddContact(x.Vessel, "snaptrap", x.Position);
                    }
                    break;
                case SnapTrapEventKind.AbsorbRequest:
                {
                    float v = Eat(x.Position, Snap.P.Root, _rootBites);
                    if (v > 0f) Snap.Deposit(x.Trap, v);
                    break;
                }
                case SnapTrapEventKind.MouthAbsorbRequest:
                {
                    float v = Eat(x.Position, 0.5f * Snap.P.MouthLength * Snap.P.GeometryScale, 64);
                    if (v > 0f) Snap.Deposit(x.Trap, v);
                    break;
                }
                case SnapTrapEventKind.BudRequest:
                    if (_c.T >= _budRetryAt && Snap.LiveCount < _snapCap)
                    {
                        _budRetryAt = _c.T + _budRetry;
                        Snap.AddTrap(RootAtRim(x.Position), x.Axis, true);
                        Buds++;
                    }
                    break;
            }
        }
        ev.Clear();
    }

    /// <summary>ThreatGrove.RefreshFood: edible prisms inside the grove are the network's food.</summary>
    readonly List<int> _food = new();

    void RefreshFood()
    {
        _food.Clear();
        var q = new List<int>();
        Shape.Bounds(out var lo, out var hi);
        _c.World.QuerySphere((lo + hi) * 0.5f, (hi - lo).Length() * 0.5f, q);
        foreach (int h in q)
            if (_c.World.GroveEdible(h) && Shape.Contains(_c.World.Pos[h]) && _food.Count < (int)Cell.F(_cfg, "MaxFood")) _food.Add(h);
        Phys.EnsureFood(_food.Count);
        for (int f = 0; f < _food.Count; f++)
        {
            Phys.FoodPos[f] = _c.World.Pos[_food[f]];
            Phys.FoodVol[f] = _c.World.Vol[_food[f]];
            Phys.FoodAlive[f] = true;
        }
        Phys.FoodCount = _food.Count;
    }

    void ProcessPhysarumEvents(bool warm)
    {
        foreach (var x in Phys.Events)
        {
            switch (x.Kind)
            {
                case PhysarumEventKind.Digest:
                    if (x.Food >= 0 && x.Food < _food.Count && _food[x.Food] >= 0 && _c.World.AliveL[_food[x.Food]])
                    {
                        Phys.CreditDigest(_c.World.Eat(_food[x.Food], "grove"));
                        _food[x.Food] = -1;
                    }
                    break;
                case PhysarumEventKind.Lay:
                    if (!warm) LayTube(x.Voxel);
                    break;
                case PhysarumEventKind.Resorb:
                    if (!warm && _tube.TryGetValue(x.Voxel, out int h)) { _tube.Remove(x.Voxel); _c.World.Eat(h, "grove"); }
                    break;
            }
        }
        Phys.Events.Clear();
    }

    void LayTube(int v)
    {
        if (!Phys.Tube[v] || _tube.ContainsKey(v)) return;
        _tube[v] = _c.World.LayFrom(Phys.Centre(v), Phys.P.TubeVolume, MassKind.Tube, "grove", 2);
    }

    public void Tick(Cell c)
    {
        int n = c.Pilots.Count;
        if (_vessels.Length < n) _vessels = new ThreatVessel[n];
        for (int k = 0; k < n; k++)
            _vessels[k] = new ThreatVessel { Position = c.Pilots[k].Pos, Previous = c.Pilots[k].Prev, Radius = 6f };   // ThreatGrove.VesselRadius
        long t0 = Stopwatch.GetTimestamp();
        // snap traps at SnapTrapHz (two steps per 0.1 s tick); the trigger samples each vessel's path
        float tick = 1f / _snapHz;
        _snapAcc += Cell.Dt;
        while (_snapAcc >= tick - 1e-6f)
        {
            _snapAcc -= tick;
            if (Snap.LiveCount > 0) Snap.Step(tick, _vessels, n);
            ProcessSnapEvents();
        }
        // physarum at 10 Hz
        if (c.T >= _foodAt) { _foodAt = c.T + _foodRefresh; RefreshFood(); }
        Phys.SetVessels(_vessels, n);
        Phys.Advance(Cell.Dt);
        ProcessPhysarumEvents(warm: false);
        LastMs = Ms.Since(t0);
        Contacts(c);
        PollTubes();
    }

    /// <summary>ThreatGrove.PollTubes: a tube whose prism left by an active force leaves the network's ledger.</summary>
    void PollTubes()
    {
        var gone = new List<int>();
        foreach (var kv in _tube) if (!_c.World.AliveL[kv.Value]) gone.Add(kv.Key);
        foreach (int v in gone) { _tube.Remove(v); Phys.TubeLost(v); }
        for (int o = 0; o < Math.Min(_slot.Length, Snap.Count * SnapTrapCore.SlotCount); o++)
        {
            int h = _slot[o];
            if (h < 0 || _c.World.AliveL[h]) continue;
            _slot[o] = -1;
            Snap.SlotLost(o / SnapTrapCore.SlotCount, o % SnapTrapCore.SlotCount);   // ThreatGrove.TrapSlotRemoved
        }
    }

    /// <summary>The threat harness's contact rules (SnapTrapTests / PhysarumTests), on the shared world.</summary>
    void Contacts(Cell c)
    {
        // a pulse is telegraphed from the moment the pilot could see it (within 450 u, the research's VIEW)
        for (int v = 0; v < Phys.VoxelCount; v++)
        {
            if (!Phys.Danger[v]) continue;
            for (int k = 0; k < c.Pilots.Count; k++)
                if (!_waveSeen.ContainsKey((Phys.Wave[v], k)) && Vector3.Distance(Phys.Centre(v), c.Pilots[k].Pos) < 450f)
                    _waveSeen[(Phys.Wave[v], k)] = c.T;
        }
        for (int k = 0; k < c.Pilots.Count; k++)
        {
            var p = c.Pilots[k];
            Shape.Bounds(out var lo, out var hi);
            var mid = (lo + hi) * 0.5f;
            if (Vector3.Distance(p.Pos, mid) > (hi - lo).Length() * 0.5f + 60f) continue;
            // teeth always burn; any other slot is rammed
            for (int i = 0; i < Snap.Count; i++)
            {
                if (!Snap.Used[i] || !Snap.Alive[i] || Vector3.Distance(Snap.Heart[i], p.Pos) > 140f) continue;
                for (int s = 0; s < SnapTrapCore.SlotCount; s++)
                {
                    int h = _slot[i * SnapTrapCore.SlotCount + s];
                    if (h < 0 || !c.World.AliveL[h]) continue;
                    float d = Cell.SegDist(p.Prev, p.Pos, c.World.Pos[h]);
                    if (SnapTrapCore.SlotTooth[s]) { if (d < 3.5f + 6f && !Snap.Caught[i]) { ToothBurns++; c.AddContact(k, "snaptrap", c.World.Pos[h]); } }
                    else if (d < 4f + p.Radius) { c.World.Destroy(h, "ram"); SlotsRammed++; }
                }
            }
            // tubes along the path: a pulsing one stings, a resting one is rammed
            var seg = p.Pos - p.Prev;
            int m = Math.Max(2, (int)(seg.Length() / 4f) + 1);
            int hot = -1;
            for (int j = 0; j < m; j++)
            {
                var q = p.Prev + seg * (j / (float)(m - 1));
                if (!Shape.Contains(q, -Phys.H)) continue;
                int v = Phys.Vox(q);
                if (!Phys.Tube[v]) continue;
                if (Phys.Danger[v]) { if (hot < 0) hot = v; }
                else if (_tube.TryGetValue(v, out int th)) { c.World.Destroy(th, "ram"); TubesRammed++; }
            }
            if (hot >= 0)
            {
                TubeBurns++;
                float seen = _waveSeen.TryGetValue((Phys.Wave[hot], k), out float s0) ? s0 : c.T;
                c.AddContact(k, "physarum", Phys.Centre(hot), c.T - seen >= 0.25f);
            }
            for (int h = 0; h < Phys.HeartPosition.Count; h++)
            {
                if (!Phys.Beating(h) || Cell.SegDist(p.Prev, p.Pos, Phys.HeartPosition[h]) >= Phys.P.HeartGuard) continue;
                BeatBurns++;
                float last = Phys.NextBeat[h] - Phys.P.Period;
                c.AddContact(k, "physarum", Phys.HeartPosition[h], Phys.P.BeatGlow + (c.T - last) >= 0.25f);
                break;
            }
        }
    }

    /// <summary>Flora hearts the grove adds to the cell (FloraHeartRegistry): snap traps (Time) and sclerotia (Space).</summary>
    public IEnumerable<(Vector3 at, int element)> Hearts()
    {
        for (int i = 0; i < Snap.Count; i++) if (Snap.Used[i] && Snap.Alive[i]) yield return (Snap.HeartCrystalPosed(i), 3);
        for (int k = 0; k < Phys.HeartPosition.Count; k++) if (Phys.HeartAlive[k]) yield return (Phys.HeartPosition[k], 2);
    }

    /// <summary>Always-on heart colliders: one crystal per live trap and per sclerotium (author_threat_flora's gate).</summary>
    public int Colliders(Cell c) => Snap.LiveCount + Phys.LiveHearts;

    public void Census(List<(string, int, int)> into)
    {
        into.Add(("grove/snaptraps", Snap.LiveCount, _snapCap));
        into.Add(("grove/sclerotia", Phys.LiveHearts, (int)Cell.F(_def, "Sclerotia")));
        into.Add(("grove/tubes", Phys.TubeCount, Phys.P.MaxTubes));
    }

    public void Strikers(Cell c, List<Striker> into)
    {
        for (int i = 0; i < Snap.Count; i++)
            if (Snap.Used[i] && Snap.Alive[i] && c.NearAnyPilot(Snap.Heart[i], 400f))
                into.Add(new Striker { Cls = "snaptrap", Key = i, Pos = Snap.HeartCrystalPosed(i), Intent = Snap.Intent[i] });
        for (int k = 0; k < Phys.HeartPosition.Count; k++)
            if (Phys.HeartAlive[k] && c.NearAnyPilot(Phys.HeartPosition[k], 400f))
                into.Add(new Striker { Cls = "physarum", Key = 1000 + k, Pos = Phys.HeartPosition[k],
                                       Intent = Phys.Glowing(k) || Phys.Beating(k) ? 1f : 0f });
    }

    /// <summary>The grove's account: what the rhizome and the network hold in reserve (their laid prisms are world mass).</summary>
    public double Held() => Snap.Reserve + Phys.Reserve + Phys.HeartBodies;

    public void Ledger(List<(string, double)> into)
    {
        into.Add(("grove: planted (source)", Planted));
        into.Add(("grove: reserves (rhizome + network)", Snap.Reserve + Phys.Reserve));
        into.Add(("grove: snap-trap core self-audit", Snap.Audit()));
        into.Add(("grove: physarum core self-audit", Phys.Audit()));
    }

    public string Report() =>
        $"snap traps {Snap.LiveCount}/{_snapCap} (buds {Buds}, snaps {Snaps}, tooth burns {ToothBurns}, slots rammed {SlotsRammed}); " +
        $"physarum {Phys.TubeCount} tubes / {Phys.LiveHearts} hearts (tube burns {TubeBurns}, beat burns {BeatBurns}, tubes rammed {TubesRammed})";

    public void Snapshot(Utf8JsonWriter w)
    {
        w.WriteStartObject("grove");
        w.WriteStartArray("traps");
        for (int i = 0; i < Snap.Count; i++)
            if (Snap.Used[i] && Snap.Alive[i]) { var h = Snap.HeartCrystalPosed(i); W3(w, h); }
        w.WriteEndArray();
        w.WriteStartArray("slots");
        foreach (int h in _slot) if (h >= 0 && _c.World.AliveL[h]) W3(w, _c.World.Pos[h]);
        w.WriteEndArray();
        w.WriteStartArray("tubes");
        foreach (var kv in _tube) if (_c.World.AliveL[kv.Value]) W3(w, _c.World.Pos[kv.Value]);
        w.WriteEndArray();
        w.WriteStartArray("sclerotia");
        for (int k = 0; k < Phys.HeartPosition.Count; k++) if (Phys.HeartAlive[k]) W3(w, Phys.HeartPosition[k]);
        w.WriteEndArray();
        w.WriteEndObject();
    }

    static void W3(Utf8JsonWriter w, Vector3 p)
    {
        w.WriteStartArray();
        w.WriteNumberValue(MathF.Round(p.X, 1)); w.WriteNumberValue(MathF.Round(p.Y, 1)); w.WriteNumberValue(MathF.Round(p.Z, 1));
        w.WriteEndArray();
    }
}
