// A C# port of the research arena (Tools/Ecology/common/arena.py on the research branch) as an IBuilderWorld, so the
// SHIPPED builder / thief cores run headless against the same world the research measured them in: a 1200 u cell,
// prism "mass" points in plant-like clumps, scripted pilots that lay a conserved TRAIL, a uniform spatial hash
// rebuilt once per step, and the conservation audit (created - live - eaten - destroyed == 0).
using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Gameplay;

sealed class Pilot
{
    public string Policy = "wander", Name = "pilot";
    public Vector3 Pos, Vel, Goal;
    public float Speed = 120f, Turn = 2f, Radius = 6f;
    public int Domain = 1;
    public float TrailEvery;          // seconds between trail prisms (builders harness: 0.25 s, 6 vol)
    public float TrailVol = 6f;
    public float TrailT;
    public List<Vector3> Waypoints = new();
    public int Wp;
    public bool Ram;                  // destroys structure prisms it flies through and kills workers it touches
    public bool Thief;                // a stealing vessel: structure prisms it touches change hands instead
    public bool Present = true;       // false = not in the cell (an empty opening)
    public int Index;
}

sealed class Arena : IBuilderWorld
{
    public readonly BuilderRng Rng;
    public readonly float R;
    public float T;

    // mass (struct of arrays, grown by lay)
    public readonly List<Vector3> Pos = new();
    public readonly List<float> Vol = new();
    public readonly List<bool> AliveL = new(), ShieldedL = new(), TrailL = new();
    public readonly List<int> Dom = new(), Owner = new(), PrevDom = new();
    public readonly List<float> Laid = new();
    public readonly Dictionary<int, int> Built = new();   // handle -> colony (BuilderRegistry)
    public double Scattered, LaidVol, Eaten, Destroyed, Stolen;
    public int Steals, Moves, Reservations;
    public readonly List<Pilot> Pilots = new();

    // spatial hash (common/arena.py Grid)
    readonly float _h;
    readonly int _n;
    int[] _start = Array.Empty<int>(), _order = Array.Empty<int>();
    // spacing-mode trails (bestiary enable_trails)
    public float TrailSpacing, TrailSpacingVol = 10f;
    /// <summary>A fraction of spacing-mode trail prisms laid SHIELDED (the shield-law test).</summary>
    public float TrailShieldedFrac;
    readonly Dictionary<int, float> _trailAcc = new();

    public Arena(int seed, float r = 1200f, float gridH = 40f)
    {
        Rng = new BuilderRng(seed);
        R = r; _h = gridH;
        _n = (int)MathF.Ceiling(2 * r / gridH) + 1;
        Rebuild();
    }

    public int Count => Pos.Count;

    public Vector3 Ball(float lo, float hi)
    {
        var d = Rng.OnSphere();
        float u = Rng.Uniform();
        float rr = MathF.Cbrt(lo * lo * lo + u * (hi * hi * hi - lo * lo * lo));
        return d * rr;
    }

    public void Scatter(int n, float rlo = 0.3f, float rhi = 0.9f, float vlo = 8f, float vhi = 40f, int clumps = 24, float shieldedFrac = 0f)
    {
        var centres = new Vector3[clumps];
        for (int c = 0; c < clumps; c++) centres[c] = Ball(rlo * R, rhi * R);
        for (int i = 0; i < n; i++)
        {
            int w = Rng.Range(clumps);
            float v = vlo + (vhi - vlo) * Rng.Uniform();
            Add(centres[w] + Rng.Normal3(25f), v, 0, false, Rng.Uniform() < shieldedFrac, -1);
            Scattered += v;
        }
        Rebuild();
    }

    int Add(Vector3 p, float v, int dom, bool trail, bool shielded, int owner)
    {
        Pos.Add(p); Vol.Add(v); AliveL.Add(true); ShieldedL.Add(shielded); TrailL.Add(trail);
        Dom.Add(dom); PrevDom.Add(dom); Owner.Add(owner); Laid.Add(T);
        return Pos.Count - 1;
    }

    public int Lay(Vector3 p, float v, int dom, bool trail, int owner = -1)
    {
        LaidVol += v;
        return Add(p, v, dom, trail, false, owner);
    }

    public double LiveVolume()
    {
        double s = 0;
        for (int i = 0; i < Pos.Count; i++) if (AliveL[i]) s += Vol[i];
        return s;
    }

    /// <summary>Conservation residual: (everything created) - (live + eaten + destroyed). 0 means conserved.</summary>
    public double Audit() => Scattered + LaidVol - (LiveVolume() + Eaten + Destroyed);

    int[] _key = Array.Empty<int>(), _fill = Array.Empty<int>();

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
    }

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
        return results.Count;
    }

    // ── IBuilderWorld ────────────────────────────────────────────────────────────────────────────
    public bool Alive(int h) => AliveL[h];
    public Vector3 Position(int h) => Pos[h];
    public int Domain(int h) => Dom[h];
    public bool Shielded(int h) => ShieldedL[h];
    public bool IsTrail(int h) => TrailL[h] || Owner[h] >= 0;
    public float Age(int h) => T - Laid[h];
    public float Volume(int h) => Vol[h];
    public readonly HashSet<int> Carried = new();
    public bool Loose(int h) => !Built.ContainsKey(h) && !Carried.Contains(h);

    public bool Steal(int h, int domain)
    {
        if (!AliveL[h] || ShieldedL[h]) return false;
        if (Dom[h] == domain) return true;
        PrevDom[h] = Dom[h];
        Dom[h] = domain; Stolen += Vol[h]; Steals++;
        return true;
    }

    public void Carry(int h, Vector3 p) { Pos[h] = p; Moves++; Carried.Add(h); }

    public bool TryReserve(Vector3 site, float clear)
    {
        Reservations++;
        int x0 = Cl(site.X - clear), x1 = Cl(site.X + clear), y0 = Cl(site.Y - clear), y1 = Cl(site.Y + clear), z0 = Cl(site.Z - clear), z1 = Cl(site.Z + clear);
        float r2 = clear * clear;
        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        {
            int b = (x * _n + y) * _n;
            for (int j = _start[b + z0]; j < _start[b + z1 + 1]; j++)
            {
                int i = _order[j];
                if (AliveL[i] && Vector3.DistanceSquared(Pos[i], site) <= r2) return false;
            }
        }
        return true;
    }

    public void Settle(int h, Vector3 from, Vector3 site, float seconds) { Pos[h] = site; Moves++; Carried.Remove(h); }
    public void SetBuilt(int h, int colony, int site, bool built) { if (built) Built[h] = colony; else Built.Remove(h); }

    public float Consume(int h, Vector3 mouth)
    {
        if (!AliveL[h] || ShieldedL[h]) return 0f;
        AliveL[h] = false; Eaten += Vol[h]; Carried.Remove(h); Built.Remove(h);
        return Vol[h];
    }

    public void GiveBack(int h) { Dom[h] = PrevDom[h]; Carried.Remove(h); }

    public void Reclaim(int h, int vesselId)
    {
        Dom[h] = Pilots[vesselId].Domain; Carried.Remove(h);
    }

    public float Destroy(int h)
    {
        if (!AliveL[h] || ShieldedL[h]) return 0f;
        AliveL[h] = false; Destroyed += Vol[h]; Built.Remove(h); Carried.Remove(h);
        return Vol[h];
    }

    /// <summary>A carrier died: its prism falls loose where it is (the colony glue's Release).</summary>
    public void Drop(int h) => Carried.Remove(h);

    // ── pilots ────────────────────────────────────────────────────────────────────────────────────
    public Pilot AddPilot(Pilot p)
    {
        p.Pos = Ball(0.5f * R, 0.8f * R);
        p.Vel = Rng.OnSphere() * p.Speed;
        p.Goal = Ball(0.2f * R, 0.9f * R);
        p.Index = Pilots.Count;
        Pilots.Add(p);
        return p;
    }

    /// <summary>Targets a hunter chases (a species publishes its agents here); threats an evader flees.</summary>
    public List<Vector3> Targets = new();

    Vector3 PilotGoal(Pilot p)
    {
        if (p.Policy == "hunter" && Targets.Count > 0)
        {
            var best = Targets[0]; float bd = float.MaxValue;
            foreach (var t in Targets) { float d = Vector3.Distance(t, p.Pos); if (d < bd) { bd = d; best = t; } }
            return best;
        }
        if (p.Policy == "circuit" && p.Waypoints.Count > 0)
        {
            if (Vector3.Distance(p.Waypoints[p.Wp], p.Pos) < 60f) p.Wp = (p.Wp + 1) % p.Waypoints.Count;
            return p.Waypoints[p.Wp];
        }
        if (Vector3.Distance(p.Goal, p.Pos) < 60f) p.Goal = Ball(0.2f * R, 0.9f * R);
        return p.Goal;
    }

    public void Step(float dt)
    {
        foreach (var p in Pilots)
        {
            if (!p.Present) continue;
            var want = PilotGoal(p) - p.Pos;
            float n = want.Length();
            if (n > 1e-6f)
            {
                var v = BuilderMath.Unit(p.Vel); var w = want / n;
                float ang = MathF.Acos(Math.Clamp(Vector3.Dot(v, w), -1f, 1f));
                float k = MathF.Min(1f, p.Turn * dt / MathF.Max(ang, 1e-6f));
                var nd = v + (w - v) * k;
                p.Vel = BuilderMath.Unit(nd) * p.Speed;
            }
            p.Pos += p.Vel * dt;
            float r = p.Pos.Length();
            if (r > R * 0.97f) p.Pos *= R * 0.97f / r;
            if (TrailSpacing > 0f)
            {
                _trailAcc.TryGetValue(p.Index, out float acc);
                acc += p.Speed * dt;
                while (acc >= TrailSpacing)
                {
                    acc -= TrailSpacing;
                    var back = BuilderMath.Unit(p.Vel) * (p.Radius + 4f + acc);
                    int ti = Lay(p.Pos - back, TrailSpacingVol, 0, false, p.Index);
                    if (TrailShieldedFrac > 0f && Rng.Uniform() < TrailShieldedFrac) ShieldedL[ti] = true;
                }
                _trailAcc[p.Index] = acc;
            }
            if (p.TrailEvery > 0f)
            {
                p.TrailT += dt;
                while (p.TrailT >= p.TrailEvery)
                {
                    p.TrailT -= p.TrailEvery;
                    var back = BuilderMath.Unit(p.Vel) * (p.Radius * 2f);
                    Lay(p.Pos - back, p.TrailVol, p.Domain, true);
                }
            }
        }
        Rebuild();
        T += dt;
    }

    /// <summary>The vessels as a colony senses them (index = pilot index).</summary>
    public int Vessels(BuilderVessel[] into, bool everyoneRams = false)
    {
        int n = 0;
        foreach (var p in Pilots)
        {
            if (!p.Present) continue;
            into[n++] = new BuilderVessel
            {
                Pos = p.Pos, Vel = p.Vel, Radius = p.Radius, Id = p.Index, Domain = p.Domain,
                Rams = everyoneRams || p.Ram || p.Policy == "hunter",
            };
        }
        return n;
    }

    readonly List<int> _ramScratch = new();

    /// <summary>harness.ram: a ram-capable pilot destroys unshielded STRUCTURE prisms it flies through (reach 4);
    /// a stealing pilot takes them instead (they change hands, nothing removed).</summary>
    public void RamStructures(float reach = 4f)
    {
        foreach (var p in Pilots)
        {
            if (!p.Present || !(p.Ram || p.Thief)) continue;
            QuerySphere(p.Pos, p.Radius + reach, _ramScratch);
            foreach (int c in _ramScratch)
            {
                if (!AliveL[c] || !Built.ContainsKey(c)) continue;
                if (p.Thief) { if (Steal(c, p.Domain)) Built.Remove(c); }
                else Destroy(c);
            }
        }
    }
}
