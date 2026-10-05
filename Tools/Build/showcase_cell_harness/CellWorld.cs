// The showcase cell's MASS: every prism of the Swarm cell that is not a creature's own body - flora plates, vessel
// trail, skeletons - in one store every system reads and eats from (Docs/SWARM_FAUNA.md §25). It is the harness's
// stand-in for PrismSpatialIndex + the prism lifecycle, and it implements the builders' IBuilderWorld so the fortress
// and the thief nest act on the SAME prisms the swarms and the substrate graze.
//
// Every volume that enters or leaves is booked: Grown (flora plates), Trail (vessel wake), Laid (a creature's body
// given back as a skeleton), EatenBy[system], Destroyed (a vessel cut through it). The global ledger in Program.cs
// closes this book against what each system says it holds.
using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Gameplay;

/// <summary>What a prism IS, as the platform's predicates ask it: Leaf = a Borromean plant's plate (flora tissue),
/// Trail = a vessel's wake, Skeleton = a dead creature's body, TrapTissue = a snap trap's slot (flora tissue - a trap is
/// a Flora), Tube = a physarum tube (grove tissue with no LifeForm - ThreatGrove.IsGroveTissue).</summary>
enum MassKind { Leaf = 0, Trail = 1, Skeleton = 2, TrapTissue = 3, Tube = 4 }

sealed class Plant
{
    public int Id, Region, Element;
    public Vector3 Heart;
    public int Plates, Grown, Budget;
    public float NextGrow, LastBirth;
    public bool Alive = true;
}

sealed class CellWorld : IWearWorld
{
    public readonly float R;
    public float T;
    public readonly BuilderRng Rng;

    // ── mass, struct of arrays ──
    public readonly List<Vector3> Pos = new();
    public readonly List<float> Vol = new();
    public readonly List<bool> AliveL = new(), ShieldedL = new();
    public readonly List<MassKind> Kind = new();
    public readonly List<int> PlantOf = new(), Dom = new(), PrevDom = new(), Element = new(), Owner = new();
    public readonly List<float> Born = new();
    public readonly Dictionary<int, int> Built = new();
    public readonly HashSet<int> Carried = new();
    public readonly List<Plant> Plants = new();

    // ── the book ──
    public double Grown, Trail, Laid, Destroyed, Reclaimed;
    public readonly Dictionary<string, double> EatenBy = new(), LaidBy = new(), DestroyedBy = new();
    public string Eater = "builders";   // who the IBuilderWorld.Consume calls are booked to (set before each colony step)
    public int Steals, Recaptures, Raids;
    /// <summary>(pilot, stealer) for every piece of a pilot's wake a colony took since the caller last cleared it (the
    /// encounter table); <see cref="Stealer"/> names the colony stepping now.</summary>
    public readonly List<(int pilot, string by)> StolenFrom = new();
    public string Stealer = "thieves";

    readonly float _h;
    readonly int _n;
    int[] _start = Array.Empty<int>(), _order = Array.Empty<int>(), _key = Array.Empty<int>(), _fill = Array.Empty<int>();

    public CellWorld(int seed, float r, float gridH = 40f)
    {
        Rng = new BuilderRng(seed);
        R = r; _h = gridH;
        _n = (int)MathF.Ceiling(2 * r / gridH) + 1;
        Rebuild();
    }

    public int Count => Pos.Count;

    public int Add(Vector3 p, float v, MassKind kind, int plant, int element, int dom)
    {
        Pos.Add(p); Vol.Add(v); AliveL.Add(true); ShieldedL.Add(false); Kind.Add(kind); PlantOf.Add(plant);
        Element.Add(element); Dom.Add(dom); PrevDom.Add(dom); Born.Add(T); Owner.Add(-1);
        return Pos.Count - 1;
    }

    public double LiveVolume()
    {
        double s = 0;
        for (int i = 0; i < Pos.Count; i++) if (AliveL[i]) s += Vol[i];
        return s;
    }

    public double LiveVolume(MassKind k)
    {
        double s = 0;
        for (int i = 0; i < Pos.Count; i++) if (AliveL[i] && Kind[i] == k) s += Vol[i];
        return s;
    }

    public double EatenTotal() { double s = 0; foreach (var v in EatenBy.Values) s += v; return s; }

    /// <summary>Everything ever put in the world minus everything in it or taken out of it. 0 = the store conserves.</summary>
    public double Audit() => Grown + Trail + Laid - (LiveVolume() + EatenTotal() + Destroyed + Reclaimed);

    /// <summary>Eat prism <paramref name="h"/> for <paramref name="who"/> (Prism.Consume): its volume leaves the world.</summary>
    public float Eat(int h, string who)
    {
        if (!AliveL[h] || ShieldedL[h]) return 0f;
        AliveL[h] = false; Carried.Remove(h); Built.Remove(h); Unwear(h);
        EatenBy.TryGetValue(who, out double e);
        EatenBy[who] = e + Vol[h];
        if (Kind[h] == MassKind.Leaf && PlantOf[h] >= 0) Plants[PlantOf[h]].Plates--;
        return Vol[h];
    }

    /// <summary>A vessel flew through it: the prism explodes (its volume leaves the cell).</summary>
    public void Destroy(int h, string what = "other")
    {
        if (!AliveL[h]) return;
        AliveL[h] = false; Carried.Remove(h); Built.Remove(h);
        Destroyed += Vol[h];
        DestroyedBy.TryGetValue(what, out double d);
        DestroyedBy[what] = d + Vol[h];
        if (Kind[h] == MassKind.Leaf && PlantOf[h] >= 0) Plants[PlantOf[h]].Plates--;
    }

    /// <summary>A creature's body left standing (starvation's wither): mass the creature's system gives the cell.</summary>
    public int LaySkeleton(Vector3 p, float v, string by)
    {
        if (!(v > 0f)) return -1;
        return LayFrom(p, v, MassKind.Skeleton, by, 0);
    }

    /// <summary>Mass a system puts INTO the cell from what it holds (a skeleton, a grove slot, a tube).</summary>
    public int LayFrom(Vector3 p, float v, MassKind kind, string by, int dom)
    {
        Laid += v;
        LaidBy.TryGetValue(by, out double l);
        LaidBy[by] = l + v;
        return Add(p, v, kind, -1, -1, dom);
    }

    public int LayTrail(Vector3 p, float v, int dom, int pilot)
    {
        Trail += v;
        int h = Add(p, v, MassKind.Trail, -1, -1, dom);
        Owner[h] = pilot;
        return h;
    }

    /// <summary>Flora tissue (a lifeform's prism that is a plant): what the swarms and the substrate graze.</summary>
    public bool IsFloraTissue(int h) => Kind[h] == MassKind.Leaf || Kind[h] == MassKind.TrapTissue;

    /// <summary>ThreatGrove.IsEdible: ordinary cell mass - never shielded, never a living body (any lifeform's or a
    /// creature's prism), never grove tissue, never a builder's structure or a carried prism.</summary>
    public bool GroveEdible(int h) =>
        AliveL[h] && !ShieldedL[h] && (Kind[h] == MassKind.Trail || Kind[h] == MassKind.Skeleton)
        && !Built.ContainsKey(h) && !Carried.Contains(h);

    public int GrowPlate(Plant pl, float v, Vector3 at)
    {
        Grown += v;
        pl.Plates++; pl.Grown++;
        return Add(at, v, MassKind.Leaf, pl.Id, pl.Element, 0);
    }

    // ── spatial hash, rebuilt once per tick (PrismSpatialIndex stand-in) ──
    public void Rebuild()
    {
        int cells = _n * _n * _n;
        if (_start.Length != cells + 2) { _start = new int[cells + 2]; _fill = new int[cells + 1]; }
        else { Array.Clear(_start, 0, _start.Length); Array.Clear(_fill, 0, _fill.Length); }
        int n = Pos.Count;
        if (_key.Length < n) _key = new int[Math.Max(n, _key.Length * 2)];
        for (int i = 0; i < n; i++)
        {
            _key[i] = AliveL[i] ? Key(Pos[i]) : cells;
            _start[_key[i] + 1]++;
        }
        for (int c = 0; c <= cells; c++) _start[c + 1] += _start[c];
        if (_order.Length < n) _order = new int[Math.Max(n, _order.Length * 2)];
        for (int i = 0; i < n; i++) _order[_start[_key[i]] + _fill[_key[i]]++] = i;
        _indexed = n;
    }

    int _indexed;
    int Cl(float x) => Math.Clamp((int)((x + R) / _h), 0, _n - 1);
    int Key(Vector3 p) => (Cl(p.X) * _n + Cl(p.Y)) * _n + Cl(p.Z);

    public int QuerySphere(Vector3 q, float r, List<int> results)
    {
        results.Clear();
        int x0 = Cl(q.X - r), x1 = Cl(q.X + r), y0 = Cl(q.Y - r), y1 = Cl(q.Y + r), z0 = Cl(q.Z - r), z1 = Cl(q.Z + r);
        float r2 = r * r;
        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        {
            int b = (x * _n + y) * _n;
            int s = _start[b + z0], e = _start[b + z1 + 1];
            for (int j = s; j < e; j++)
            {
                int i = _order[j];
                if (AliveL[i] && Vector3.DistanceSquared(Pos[i], q) <= r2) results.Add(i);
            }
        }
        // prisms created since the last rebuild (a trail laid this tick) are found by a scan - the index is live in the game
        for (int i = _indexed; i < Pos.Count; i++)
            if (AliveL[i] && Vector3.DistanceSquared(Pos[i], q) <= r2) results.Add(i);
        return results.Count;
    }

    // ── IBuilderWorld ──
    public bool Alive(int h) => AliveL[h];
    public Vector3 Position(int h) => Pos[h];
    public int Domain(int h) => Dom[h];
    public bool Shielded(int h) => ShieldedL[h];
    public bool IsTrail(int h) => Kind[h] == MassKind.Trail;
    public float Age(int h) => T - Born[h];
    public float Volume(int h) => Vol[h];
    /// <summary>Loose = not a living plant's tissue, not built, not carried (Fauna.IsStealableForMe's world half).</summary>
    public bool Loose(int h) => (Kind[h] == MassKind.Trail || Kind[h] == MassKind.Skeleton)
                                && !Built.ContainsKey(h) && !Carried.Contains(h);

    public bool Steal(int h, int domain)
    {
        if (!AliveL[h] || ShieldedL[h]) return false;
        if (Dom[h] == domain) return true;
        PrevDom[h] = Dom[h]; Dom[h] = domain; Steals++;
        if (Owner[h] >= 0) StolenFrom.Add((Owner[h], Stealer));
        return true;
    }

    public void Carry(int h, Vector3 p) { Pos[h] = p; Carried.Add(h); }

    // ── IWearWorld (BuilderPrismWorld's wearer surface): a worn prism hangs off ONE body; a PoseBody moves them all ──
    readonly Dictionary<int, Dictionary<int, Vector3>> _bodies = new();   // creature -> (handle -> local)
    readonly Dictionary<int, int> _wornIn = new();                         // handle -> creature
    /// <summary>Worn prisms turned to the danger tier by a lunge (SetBodyDanger).</summary>
    public readonly HashSet<int> DangerousWorn = new();
    public int WornCount => _wornIn.Count;
    public bool IsWorn(int h) => _wornIn.ContainsKey(h);

    public void Wear(int h, int creature, Vector3 local)
    {
        if (_wornIn.TryGetValue(h, out int was)) _bodies[was].Remove(h);
        if (!_bodies.TryGetValue(creature, out var b)) _bodies[creature] = b = new Dictionary<int, Vector3>();
        b[h] = local; _wornIn[h] = creature;
        Carried.Add(h);
    }

    public void Unwear(int h)
    {
        if (!_wornIn.TryGetValue(h, out int c)) return;
        _bodies[c].Remove(h); _wornIn.Remove(h);
        Carried.Remove(h); DangerousWorn.Remove(h);
    }

    public void PoseBody(int creature, Vector3 pos, Vector3 right, Vector3 up, Vector3 forward, float squash)
    {
        if (!_bodies.TryGetValue(creature, out var b)) return;
        foreach (var kv in b)
        {
            var l = kv.Value * squash;
            Pos[kv.Key] = pos + right * l.X + up * l.Y + forward * l.Z;
        }
    }

    public void SetBodyDanger(int creature, bool danger)
    {
        if (!_bodies.TryGetValue(creature, out var b)) return;
        foreach (int h in b.Keys) { if (danger) DangerousWorn.Add(h); else DangerousWorn.Remove(h); }
    }
    public void Drop(int h) { Carried.Remove(h); }

    public bool TryReserve(Vector3 site, float clear)
    {
        var tmp = _reserve;
        QuerySphere(site, clear, tmp);
        return tmp.Count == 0;
    }
    readonly List<int> _reserve = new();

    public void Settle(int h, Vector3 from, Vector3 site, float seconds) { Pos[h] = site; Carried.Remove(h); }
    public void SetBuilt(int h, int colony, int site, bool built) { if (built) Built[h] = colony; else Built.Remove(h); }
    public float Consume(int h, Vector3 mouth) => Eat(h, Eater);
    public void GiveBack(int h) { Dom[h] = PrevDom[h]; Carried.Remove(h); Recaptures++; }

    /// <summary>A raiding vessel took a hoarded prism back: it changes hands to the vessel (it leaves the cell's mass,
    /// booked as Reclaimed - the vessel carries it as wake).</summary>
    public void Reclaim(int h, int vesselId)
    {
        if (!AliveL[h]) return;
        AliveL[h] = false; Carried.Remove(h); Built.Remove(h);
        Reclaimed += Vol[h]; Raids++;
    }
}
