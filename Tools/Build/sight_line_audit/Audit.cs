// Sight-line audit: in every race mode whose layout is pure code, can a pilot leaving a ring see the next one?
// Garrett, 2026-11-14 (after Scarab Rally I4): "respect sight lines from one ring to another so that prisms are not
// occluding which ring is lime green from the perspective of leaving a ring" - and "there are probably other intensity
// levels of other race games that make that mistake". Run: bash Tools/Build/sight_line_audit/run.sh [mode ...]
//
// For each mode, intensity I1-I4 and seed, it builds the SHIPPED course and the prisms the mode lays near it (the same
// generators the game calls, compiled against the card-art harness's Unity shim), then for every leg casts rays from
// the ring you leave (its centre and four points at half its radius) to sixteen points on the next ring's rim. A ray is
// blocked when it passes through any prism box (shrunk 0.5 u, the ray's first and last 3% ignored so a rail threading
// both mouths is not counted). Moving prisms are posed at four times. Per mode and intensity it prints the share of
// blocked rays and how many legs have the next ring mostly hidden (> 50% of its rays blocked), the share blocked by prisms
// off the leg's line (what a sight bore like Scarab Rally I4's can clear), and for Blast Run which part blocked first
// (door, gauntlet tunnel or field). Not counted as occluders: the fittings of the ring you leave and of the next ring
// (anything within its radius + 20 u and 15 u of its plane; a Breakwater station; a Thresher nest), which ARE the ring.
// Not covered (their layouts are built inside the controller): Serpent Time, Waystation, Lantern Run. Redline, Grizzly
// Time, Wrasse Run, Brittle Star Time, Warpline and Burrow Run lay no prisms near the line. --field-only counts only
// Blast Run field prisms. A READER tool: it writes nothing but its report. First run: 2026-11-14 (Docs/Reports/
// SIGHT_LINES.md).
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Audit
{
    struct Box { public Vector3 P; public Quaternion R; public Vector3 S; public int Ring; public int Tag; } // Tag (Blast Run): 1 tunnel, 2 field, 3 door, 0 anything else // Ring: 1 + the gate whose own target this box is (0: none)

    const int Seeds = 16;
    // --field-only: count only Blast Run FIELD prisms (not doors or gauntlet tunnels), to see what a sight bore can fix
    static bool FieldOnly;
    static readonly HashSet<string> Only = new();

    public static int Main(string[] args)
    {
        foreach (var a in args) { if (a == "--field-only") FieldOnly = true; else Only.Add(a.ToLowerInvariant()); }
        Console.WriteLine("mode            I  legs  blocked%  hidden-legs  prisms/course  off-line%  note");
        Run("scarabrally", ScarabRally);
        Run("switchback", Switchback);
        Run("slingshot", Slingshot);
        Run("headlong", Headlong);
        Run("threshertime", Thresher);
        Run("breakwater", Breakwater);
        Run("breach", Breach);
        Run("thornline", Thornline);
        Run("perchrun", PerchRun);
        Run("slipline", SlipLine);
        Run("decoyrun", DecoyRun);
        Run("regatta", Regatta);
        Run("skein", Skein);
        return 0;
    }

    delegate bool Builder(int seed, int intensity, List<RaceGate> gates, List<Box> boxes, out bool closed);

    static void Run(string name, Builder build)
    {
        if (Only.Count > 0 && !Only.Contains(name)) return;
        for (int i = 1; i <= 4; i++)
        {
            long rays = 0, blocked = 0, offBlocked = 0; int legs = 0, hidden = 0, courses = 0; long prisms = 0; string note = "";
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var gates = new List<RaceGate>(); var boxes = new List<Box>();
                bool ok;
                try { ok = build(seed * 7919 + i, i, gates, boxes, out bool closed) && Measure(gates, boxes, closed, ref rays, ref blocked, ref legs, ref hidden, ref offBlocked); }
                catch (Exception e) { note = e.GetType().Name + ": " + e.Message; ok = false; }
                if (!ok) continue;
                courses++; prisms += boxes.Count;
            }
            if (courses == 0) { Console.WriteLine($"{name,-15} {i}  no course built {note}"); continue; }
            Console.WriteLine($"{name,-15} {i} {legs,5}  {100.0 * blocked / Math.Max(1, rays),7:0.0}  {hidden,5} ({100.0 * hidden / Math.Max(1, legs),4:0}%)  {prisms / (double)courses,10:0}  {100.0 * offBlocked / Math.Max(1, rays),7:0.0}  {Parts()}{note}");
        }
    }

    static bool Measure(List<RaceGate> g, List<Box> boxes, bool closed, ref long rays, ref long blocked, ref int legs, ref int hidden, ref long offBlocked)
    {
        if (g.Count < 2) return false;
        int n = closed ? g.Count : g.Count - 1;
        // a coarse grid of box bounding spheres keeps the test fast
        var bs = boxes.Select(b => (b, r: b.S.magnitude * 0.5f)).ToList();
        for (int k = 0; k < n; k++)
        {
            RaceGate from = g[k], to = g[(k + 1) % g.Count];
            Frame(from.Axis, out var a1, out var a2);
            Frame(to.Axis, out var b1, out var b2);
            Vector3 lo = Vector3.Min(from.Position, to.Position) - Vector3.one * (Mathf.Max(from.Radius, to.Radius) + 60f);
            Vector3 hi = Vector3.Max(from.Position, to.Position) + Vector3.one * (Mathf.Max(from.Radius, to.Radius) + 60f);
            int target = (k + 1) % g.Count + 1, leaving = k + 1;   // a gate's own fittings never occlude its own leg
            // the next ring's own fittings (a door, a breach wall, a collar: anything within its radius + 20 u of its
            // centre and 15 u of its plane) are the target itself, not an occluder
            Vector3 tAxis = to.Axis.normalized; float tReach = to.Radius + 20f;
            var near = bs.Where(x => x.b.Ring != target && x.b.Ring != leaving && (!FieldOnly || x.b.Tag == 2)
                                   && !((x.b.P - to.Position).sqrMagnitude < tReach * tReach && Mathf.Abs(Vector3.Dot(x.b.P - to.Position, tAxis)) < 15f) && x.b.P.x + x.r > lo.x && x.b.P.x - x.r < hi.x && x.b.P.y + x.r > lo.y && x.b.P.y - x.r < hi.y
                                   && x.b.P.z + x.r > lo.z && x.b.P.z - x.r < hi.z).Select(x => x.b).ToList();
            // boxes ON the leg's line (within 20 u: a tunnel, a plug, a wall you are meant to fly through or round)
            // against the rest (a field beside the line); the off-line share is what a sight bore can clear
            var off = near.Where(b => SwitchbackFeatures.DistanceToSegment(b.P, from.Position, to.Position) > 20f + b.S.magnitude * 0.5f).ToList();
            int legRays = 0, legBlocked = 0;
            for (int e = 0; e < 5; e++)
            {
                float ae = e * 0.4f * Mathf.PI;
                Vector3 eye = e == 4 ? from.Position : from.Position + (a1 * Mathf.Cos(ae) + a2 * Mathf.Sin(ae)) * (0.5f * from.Radius);
                for (int j = 0; j < 16; j++)
                {
                    float bj = j * Mathf.PI / 8f;
                    Vector3 rim = to.Position + (b1 * Mathf.Cos(bj) + b2 * Mathf.Sin(bj)) * to.Radius;
                    legRays++;
                    foreach (var b in near)
                        if (Hits(eye, rim, b)) { legBlocked++; TagHits[b.Tag]++; break; }
                    foreach (var b in off)
                        if (Hits(eye, rim, b)) { offBlocked++; break; }
                }
            }
            rays += legRays; blocked += legBlocked; legs++;
            if (legBlocked * 2 > legRays) hidden++;
        }
        return true;
    }

    static bool Hits(Vector3 a, Vector3 b, in Box p)
    {
        Quaternion inv = Quaternion.Inverse(p.R);
        Vector3 la = inv * (a - p.P), lb = inv * (b - p.P), d = lb - la;
        Vector3 h = p.S * 0.5f - Vector3.one * 0.5f;
        if (h.x <= 0f || h.y <= 0f || h.z <= 0f) return false;
        float t0 = 0.03f, t1 = 0.97f;
        for (int k = 0; k < 3; k++)
        {
            if (Mathf.Abs(d[k]) < 1e-6f) { if (Mathf.Abs(la[k]) > h[k]) return false; continue; }
            float u = (-h[k] - la[k]) / d[k], v = (h[k] - la[k]) / d[k];
            if (u > v) (u, v) = (v, u);
            t0 = Mathf.Max(t0, u); t1 = Mathf.Min(t1, v);
            if (t0 > t1) return false;
        }
        return true;
    }

    static void Frame(Vector3 axis, out Vector3 s1, out Vector3 s2)
    {
        axis = axis.normalized;
        s1 = Vector3.ProjectOnPlane(Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right, axis).normalized;
        s2 = Vector3.Cross(axis, s1);
    }

    public static readonly long[] TagHits = new long[8];
        // which part of a station a prism is: its doors, then its world (the tunnel's first TunnelPrisms, then the field)
    static int TagOf(SwitchbackFeatureLayout L, int index)
    {
        if (L.Stations.Count == 0) return 0;
        int world = 0; foreach (var st in L.Stations) world += st.WorldPrisms;
        int at = L.Prisms.Count - world;            // every door first, then each station's tunnel and field
        if (index < at) return 3;
        foreach (var st in L.Stations)
        {
            if (index < at + st.WorldPrisms) return index - at < st.TunnelPrisms ? 1 : 2;
            at += st.WorldPrisms;
        }
        return 0;
    }

    static string Parts()
    {
        long t = TagHits[1] + TagHits[2] + TagHits[3];
        string r = t == 0 ? "" : $"first blocker: door {TagHits[3]} tunnel {TagHits[1]} field {TagHits[2]}  ";
        Array.Clear(TagHits, 0, TagHits.Length);
        return r;
    }

    static void AddLayout(SwitchbackFeatureLayout L, List<Box> boxes)
    {
        for (int pi = 0; pi < L.Prisms.Count; pi++) { var p = L.Prisms[pi]; boxes.Add(new Box { P = p.Position, R = p.Rotation, S = p.Scale, Tag = TagOf(L, pi) }); }
        // movers: their prisms are in L.Prisms at the laid pose; add three more poses of each
        foreach (var m in L.Movers)
            for (int s = 1; s <= 3; s++)
            {
                m.PoseAt(s * 0.61f, out Quaternion spin, out Vector3 offset);
                for (int j = 0; j < m.Count; j++)
                {
                    var q = L.Prisms[m.FirstPrism + j];
                    m.Apply(spin, offset, q.Position, q.Rotation, out Vector3 pos, out Quaternion rot);
                    boxes.Add(new Box { P = pos, R = rot, S = q.Scale, Tag = 2 });
                }
            }
    }

    // ── the modes ──────────────────────────────────────────────────────────────────────────

    static SwitchbackCourseSettings Shell(SwitchbackCourseSettings s, int gates)
    {
        s.GateCount = gates; s.InnerRadius = 480f; s.OuterRadius = 1080f;
        s.FirstGateDirection = Vector3.up; s.FirstGateDistance = 620f;
        return s;
    }

    static bool ScarabRally(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = false;
        var c = SwitchbackCourse.Generate(seed, Shell(ScarabRallyCourse.ForIntensity(i), 24));
        if (c == null) return false; g.AddRange(c);
        if (ScarabRallyCourse.LaysBlastRun(i)) AddLayout(SwitchbackBlastRun.Layout(c, i, Vector3.zero, 480f / 1.22f + 30f, false), b);
        return true;
    }

    static bool Switchback(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = false;
        var c = SwitchbackCourse.Generate(seed, Shell(SwitchbackCourseSettings.ForIntensity(i), 20));
        if (c == null) return false; g.AddRange(c);
        AddLayout(SwitchbackBlastRun.Layout(c, i, Vector3.zero, 480f / 1.22f + 30f, true), b);
        return true;
    }

    static bool Slingshot(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = true;
        var c = HeadlongCircuit.Generate(seed, SlingshotCourse.ForIntensity(i));
        if (c == null) return false; g.AddRange(c);
        AddLayout(SlingshotOrrery.Layout(c, i, Vector3.zero, 0f, null), b);
        return true;
    }

    static bool Headlong(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = true;
        var c = HeadlongCircuit.Generate(seed, HeadlongCircuitSettings.ForIntensity(i));
        if (c == null) return false; g.AddRange(c);
        var hedges = new List<HeadlongHedges.Hedge>(); var parts = new List<HeadlongHedges.Part>();
        HeadlongHedges.Place(c, i, hedges);
        foreach (var h in hedges) { parts.Clear(); HeadlongHedges.Build(h, parts); foreach (var p in parts) b.Add(new Box { P = p.Position, R = p.Rotation, S = p.Scale }); }
        return true;
    }

    static bool Thresher(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = true;
        var c = HeadlongCircuit.Generate(seed, ThresherTimeCourse.ForIntensity(i));
        if (c == null) return false; g.AddRange(c);
        var parts = new List<ThresherTimeNest.Part>();
        for (int k = 0; k < c.Count; k++)
        {
            parts.Clear(); ThresherTimeNest.Build(c[k], k, i, parts);
            // a nest IS its gate's target (the ring is hidden behind it): only OTHER gates' nests count as occluders
            foreach (var p in parts) b.Add(new Box { P = p.Position, R = p.Rotation, S = p.Scale, Ring = k + 1 });
        }
        return true;
    }

    static bool Breakwater(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = true;
        var bset = BreakwaterCourseSettings.ForIntensity(i); bset.StationCount = 12;
        var st = BreakwaterCourse.Generate(seed, bset);
        if (st == null || st.Count == 0) return false;
        // a station (dish, collar, plug) IS its gate's target: only OTHER stations count as occluders
        for (int k = 0; k < st.Count; k++)
        {
            var s = st[k]; int ring = k + 1;
            g.Add(new RaceGate(s.Position, s.Axis, s.PortRadius));
            BreakwaterStationBuilder.Build(s, (pos, rot, scl, kind) => b.Add(new Box { P = pos, R = rot, S = scl, Ring = ring }));
        }
        return true;
    }

    static bool Breach(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = false;
        var s = BreachCourse.ForIntensity(i);
        var c = BreachCourse.Generate(seed, s);
        if (c == null) return false; g.AddRange(c);
        s.RingCount = c.Count;
        var wall = new List<(Vector3 position, int ring)>();
        BreachCourse.WallsFor(c, s, wall);
        var scl = new Vector3(BreachCourse.WallPrismSize, BreachCourse.WallPrismSize, BreachCourse.WallPrismDepth);
        foreach (var (p, r) in wall) b.Add(new Box { P = p, R = Quaternion.LookRotation(c[r].Axis), S = scl });
        return true;
    }

    static bool Thornline(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = false;
        var thorns = new List<Vector3>();
        var s = ThornlineCourse.ForIntensity(i);
        var c = ThornlineCourse.Generate(seed, s, thorns);
        if (c == null) return false; g.AddRange(c);
        var laid = new List<Thorn>();
        ThornlineCourse.Lay(thorns, s, laid);
        foreach (var t in laid) b.Add(new Box { P = t.Position, R = Quaternion.identity, S = Vector3.one * t.Size });
        return true;
    }

    static bool PerchRun(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = false;
        var s = PerchRunCourse.ForIntensity(i);
        var c = PerchRunCourse.Generate(seed, s);
        if (c == null) return false; g.AddRange(c);
        var coral = new List<Coral>();
        PerchRunCourse.CoralFor(c, s, coral);
        foreach (var k in coral) b.Add(new Box { P = k.Position, R = k.Rotation, S = Vector3.one * k.Size });
        return true;
    }

    static bool SlipLine(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = false;
        var s = SlipLineCourse.ForIntensity(i);
        var c = SlipLineCourse.Generate(seed, s);
        if (c == null) return false; g.AddRange(c);
        s.RingCount = c.Count;
        var rails = new List<Rail>(); var pts = new List<Vector3>();
        SlipLineCourse.RailsFor(c, s, rails);
        foreach (var r in rails)
        {
            var rot = Quaternion.LookRotation(r.Direction);
            var scl = new Vector3(SlipLineCourse.PrismWidth, SlipLineCourse.PrismWidth, SlipLineCourse.PrismLength);
            SlipLineCourse.PrismsOf(r, pts);
            foreach (var q in pts) b.Add(new Box { P = q, R = rot, S = scl });
        }
        return true;
    }

    static bool DecoyRun(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = false;
        var s = DecoyRunCourse.ForIntensity(i);
        var c = DecoyRunCourse.Generate(seed, s);
        if (c == null) return false; g.AddRange(c);
        var chalk = new List<Vector3>();
        DecoyRunCourse.ChalkFor(c, s, DecoyRunCourse.StartStandoff, chalk);
        foreach (var p in chalk) b.Add(new Box { P = p, R = Quaternion.identity, S = Vector3.one * DecoyRunCourse.ChalkSize });
        return true;
    }

    static bool Skein(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = false;
        var B = SkeinCourse.BuildAll(seed, SkeinCourseSettings.ForIntensity(i));
        if (B == null || B.Gates == null) return false;
        foreach (var q in B.Gates) g.Add(new RaceGate(q.Position, q.Axis, q.Radius));
        for (int l = 0; l < B.Positions.Count; l++)
            for (int k = 0; k < B.Positions[l].Length; k++)
                b.Add(new Box { P = B.Positions[l][k], R = B.Rotations[l][k], S = new Vector3(6f, 6f, 8f) });
        return true;
    }

    static bool Regatta(int seed, int i, List<RaceGate> g, List<Box> b, out bool closed)
    {
        closed = true;
        var c = RegattaCourse.BuildGates(seed, i);
        if (c == null) return false; g.AddRange(c);
        var L = RegattaCourse.BuildRails(c, RegattaCourse.DefaultRails);
        for (int l = 0; l < L.LanePositions.Count; l++)
            for (int k = 0; k < L.LanePositions[l].Length; k++)
                b.Add(new Box { P = L.LanePositions[l][k], R = L.LaneRotations[l][k], S = new Vector3(6f, 6f, 8f) });
        return true;
    }
}
