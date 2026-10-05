// The showcase cell's shared context (Docs/SWARM_FAUNA.md §25): the world every system acts on, the pilots, the clock,
// and the shared bookkeeping - contacts, the telegraph tracker, proxies (the collider count) and the encounter table.
// Every system (Systems.cs) reads and writes the cell only through this.
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using CosmicShore.Gameplay;

/// <summary>One agent that can strike a pilot, as the telegraph rule and the skilled pilot read it.</summary>
struct Striker
{
    public string Cls;
    public long Key;
    public Vector3 Pos;
    public float Intent;
}

/// <summary>A danger contact: a creature's strike touched a pilot (burn-rules.md "Contact with danger prisms").</summary>
struct Contact
{
    public int Pilot;
    public string Cls;
    public Vector3 At;
    /// <summary>null = rate it by the shared rule (the nearest agent of the class); set = the system rated it itself.</summary>
    public bool? Telegraphed;
}

/// <summary>A creature system living in the cell. Ticked at 10 Hz in a fixed order (Program.Systems).</summary>
interface ICellSystem
{
    string Name { get; }
    /// <summary>How often the core steps (per second) - its cost is billed at this rate.</summary>
    double StepHz { get; }
    void Tick(Cell c);
    /// <summary>Wall ms of the core step(s) this tick (worker-side cost; the glue's main-thread work is not the game's).</summary>
    double LastMs { get; }
    /// <summary>Engaged colliders right now (proxies x 2, plus always-on hearts this system owns).</summary>
    int Colliders(Cell c);
    void Census(List<(string cls, int alive, int cap)> into);
    void Strikers(Cell c, List<Striker> into);
    /// <summary>Mass this system holds OUTSIDE the world's prisms (stomachs, bodies, reserves) - its ledger account.</summary>
    double Held();
    void Ledger(List<(string line, double value)> into);
    void Snapshot(Utf8JsonWriter w);
}

/// <summary>A proxy pool: members wanted (engaged) get a proxy up to the cap; one not wanted for Linger seconds retires.
/// The game's per-system SyncProxies, reduced to the count (two colliders each: heart + body prism).</summary>
sealed class ProxyPool
{
    readonly int _cap;
    readonly float _linger;
    readonly Dictionary<int, float> _wanted = new();
    readonly List<int> _drop = new();
    public int Peak;

    public ProxyPool(int cap, float linger) { _cap = cap; _linger = linger; }

    public int Count => _wanted.Count;

    public void Want(int slot, float now, bool force = false)
    {
        if (_wanted.ContainsKey(slot)) { _wanted[slot] = now; return; }
        if (!force && _wanted.Count >= _cap) return;
        _wanted[slot] = now;
    }

    public void Forget(int slot) => _wanted.Remove(slot);

    public void Retire(float now)
    {
        _drop.Clear();
        foreach (var kv in _wanted) if (now - kv.Value > _linger) _drop.Add(kv.Key);
        foreach (int k in _drop) _wanted.Remove(k);
        Peak = Math.Max(Peak, _wanted.Count);
    }
}

sealed class Pilot
{
    public int Id;
    public string Kind = "";
    public Vector3 Pos, Prev, Vel, Goal;
    public float Speed = 120f, Radius = 9f, Turn = 2.2f;
    public int Domain = 1;
    public float TrailT, Cool = -1f, GoalT;
    // raider script
    public int Phase;
    public float PhaseT;
    // stakes
    public int Contacts, Telegraphed, Landed;
    public int[] Petals = { 5, 5, 5, 5 };
    public readonly Dictionary<string, int> ByClass = new();
    public readonly List<Vector3> Path = new();
}

sealed class Cell
{
    public readonly JsonElement L;
    public readonly CellWorld World;
    public readonly List<Pilot> Pilots = new();
    public readonly ThreatRng Rng;
    public readonly int Seed;
    public float T;
    public const float Dt = 0.1f;
    public readonly float Membrane;

    public readonly List<Contact> Contacts = new();
    public readonly List<Striker> StrikersNow = new();
    /// <summary>Per minute, per pilot: the threat classes met (a contact, or an armed striker within MeetRange).</summary>
    public readonly List<Dictionary<string, int>[]> Met = new();
    public const float MeetRange = 150f;

    // telegraph tracker: (class, key) -> time intent rose past 0.5 (cleared below 0.2)
    readonly Dictionary<(string, long), float> _armed = new();
    readonly Dictionary<(string, long), float> _lastSeen = new();

    // ── the cell's ecology LOD (CellEcologyLod, Docs/ECOLOGY_LOD.md §4.2): ONE director for every population ──
    public readonly EcologyLodDirector Lod = new();
    /// <summary>SHOWCASE_LOD=0 runs the cell with every population expanded (the round-11 cell before the LOD).</summary>
    public readonly bool LodOn = Environment.GetEnvironmentVariable("SHOWCASE_LOD") != "0";
    readonly List<(string name, IMacroPopulation pop)> _lodPops = new();
    EcologyPilot[] _lodPilots = Array.Empty<EcologyPilot>();
    float _lodAcc;
    public long LodTicks;
    /// <summary>Per population: ticks spent collapsed; and the contract, counted - ticks a collapsed population was
    /// visible to a pilot, and ticks a pilot was inside its extent.</summary>
    public readonly Dictionary<string, long> LodCollapsedTicks = new();
    public long LodSeen, LodTouched;
    public double LodMs;
    public IEnumerable<(string name, IMacroPopulation pop)> LodPopulations => _lodPops;

    public void RegisterLod(string name, IMacroPopulation p, bool enabled)
    {
        if (!LodOn || !enabled) return;
        Lod.Register(p);
        _lodPops.Add((name, p));
        LodCollapsedTicks[name] = 0;
    }

    /// <summary>CellEcologyLod.Advance, once per 0.1 s tick before any population steps: the pilots, the guard (every
    /// frame in the game), Tick(1) at 1 Hz; then the contract's counters for whatever is collapsed now.</summary>
    public void AdvanceLod(Action<string>? trace = null)
    {
        if (_lodPops.Count == 0) return;
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_lodPilots.Length != Pilots.Count) _lodPilots = new EcologyPilot[Pilots.Count];
        for (int k = 0; k < Pilots.Count; k++)
        {
            var p = Pilots[k];
            _lodPilots[k] = new EcologyPilot { X = p.Pos.X, Y = p.Pos.Y, Z = p.Pos.Z, Vx = p.Vel.X, Vy = p.Vel.Y, Vz = p.Vel.Z, Speed = p.Vel.Length() };
        }
        Lod.SetPilots(_lodPilots);
        Lod.Guard();
        _lodAcc += Dt;
        if (_lodAcc >= 1f - 1e-4f)
        {
            _lodAcc -= 1f;
            if (trace != null && ((int)T) % 10 == 0)
                foreach (var (name, pop) in _lodPops)
                    trace($"   lod t={T:F0} {name}: extent {pop.MacroExtent:F0} want {Lod.WantsIndividuals(pop)} seen {Lod.SeenByAnyPilot(pop)} " +
                          $"can {pop.CanCollapse} needs {pop.NeedsIndividuals} collapsed {pop.IsCollapsed}");
            Lod.Tick(1f);
        }
        LodTicks++;
        foreach (var (name, pop) in _lodPops)
        {
            if (!pop.IsCollapsed) continue;
            LodCollapsedTicks[name]++;
            if (Lod.SeenByAnyPilot(pop)) LodSeen++;
            foreach (var p in Pilots)
                if (Vector3.Distance(p.Pos, pop.MacroCentre) < pop.MacroExtent + p.Radius) LodTouched++;
        }
        LodMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    public Cell(JsonElement layout, int seed)
    {
        L = layout;
        Seed = seed;
        Rng = new ThreatRng((ulong)seed * 2654435761UL + 11UL);
        Membrane = (float)L.GetProperty("membrane").GetDouble();
        World = new CellWorld(seed, Membrane);
    }

    public int Minute => (int)(T / 60f);

    public Vector3 RandomInShell(float inner, float outer)
    {
        var d = Rng.OnUnitSphere();
        float a = inner * inner * inner, b = outer * outer * outer;
        float r = MathF.Cbrt(a + (b - a) * Rng.NextFloat());
        return d * r;
    }

    Dictionary<string, int>[] MetNow()
    {
        while (Met.Count <= Minute)
        {
            var row = new Dictionary<string, int>[Pilots.Count];
            for (int k = 0; k < row.Length; k++) row[k] = new Dictionary<string, int>();
            Met.Add(row);
        }
        return Met[Minute];
    }

    /// <summary>A pilot met a threat class this minute (weight 1 = a contact, 0 = only seen armed nearby).</summary>
    public void Meet(int pilot, string cls, bool contact)
    {
        var row = MetNow()[pilot];
        row.TryGetValue(cls, out int v);
        row[cls] = Math.Max(v, contact ? 2 : 1);
    }

    /// <summary>Update the telegraph tracker from this tick's strikers (intent > 0.5 arms, < 0.2 clears).</summary>
    public void TrackIntent()
    {
        foreach (var s in StrikersNow)
        {
            var key = (s.Cls, s.Key);
            _lastSeen[key] = T;
            if (_armed.ContainsKey(key)) { if (s.Intent < 0.2f) _armed.Remove(key); }
            else if (s.Intent > 0.5f) _armed[key] = T;
            if (s.Intent > 0.5f)
                for (int p = 0; p < Pilots.Count; p++)
                    if (Vector3.Distance(Pilots[p].Pos, s.Pos) < MeetRange) Meet(p, s.Cls, false);
        }
        // an agent nobody reports any more (dead, or far from every pilot) loses its arming after a second
        var stale = new List<(string, long)>();
        foreach (var kv in _lastSeen) if (T - kv.Value > 1f) stale.Add(kv.Key);
        foreach (var k in stale) { _lastSeen.Remove(k); _armed.Remove(k); }
    }

    /// <summary>burn-rules.md: telegraphed when the striking class's agent NEAREST the pilot had intent > 0.5 for at
    /// least 0.25 s before the contact.</summary>
    public bool Telegraphed(string cls, Vector3 pilotPos)
    {
        long best = long.MinValue;
        float bd = float.MaxValue;
        foreach (var s in StrikersNow)
        {
            if (s.Cls != cls) continue;
            float d = Vector3.DistanceSquared(s.Pos, pilotPos);
            if (d < bd) { bd = d; best = s.Key; }
        }
        if (best == long.MinValue) return false;
        return _armed.TryGetValue((cls, best), out float since) && T - since >= 0.25f;
    }

    public void AddContact(int pilot, string cls, Vector3 at, bool? telegraphed = null) =>
        Contacts.Add(new Contact { Pilot = pilot, Cls = cls, At = at, Telegraphed = telegraphed });

    public static float SegDist(Vector3 a, Vector3 b, Vector3 p) => ThreatFloraMath.SegmentPointDistance(a, b, p);

    public bool NearAnyPilot(Vector3 p, float r)
    {
        float r2 = r * r;
        foreach (var pl in Pilots) if (Vector3.DistanceSquared(pl.Pos, p) <= r2) return true;
        return false;
    }

    public static float F(JsonElement e, string k) => (float)e.GetProperty(k).GetDouble();
}
