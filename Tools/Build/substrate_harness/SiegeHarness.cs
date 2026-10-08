// Group `siege` (Docs/SUBSTRATE_FAUNA.md §10): the game's SIEGE port against the lab it came from.
//
//   S1 fidelity   - every number of the phase machine equals the lab's (siege_fixture.json, read out of 70_siege.js)
//   S2 parity     - given the lab's snapshot at the start of an encounter and its pilot track, the port reproduces the
//                   lab's encounter: the same phase events on the same steps, the same bites, members where the lab put them
//   S3 in the game's tick - the siege as a substrate population (0.1 s ticks, 3 sub-steps), against scripted pilots:
//                   the rhythm, the telegraph before every bite, the gap reader escaping, the danger tier
//   S4 lifecycle  - the siege feeds and breeds on real food (every life form completes its life cycle), the ledger closes
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CosmicShore.Gameplay;

static partial class SubstrateHarness
{
    static JsonElement SiegeFixture()
    {
        string path = Path.Combine(_repo, "Tools", "Build", "substrate_harness", "siege_fixture.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    static Vector3 V3(JsonElement a, int k = 0) => new Vector3(a[k].GetSingle(), a[k + 1].GetSingle(), a[k + 2].GetSingle());

    static void Siege()
    {
        var fx = SiegeFixture();
        SiegeFidelity(fx);
        SiegeParity(fx);
        SiegeInGame(fx);
        SiegeLifecycle();
    }

    // ───────────────────────────────────────────────────────────── S1

    static void SiegeFidelity(JsonElement fx)
    {
        Console.WriteLine("\nS1. siege fidelity: the phase machine's numbers vs the lab's 70_siege.js (siege_fixture.json)");
        var g = SubstrateResearch.GameSiege();
        var def = fx.GetProperty("defaults"); var inl = fx.GetProperty("inline");
        int n = 0; var bad = new List<string>();
        g.Siege.Visit((k, v) =>
        {
            var src = def.TryGetProperty(k, out var e) ? e : inl.TryGetProperty(k, out var e2) ? e2 : default;
            if (src.ValueKind != JsonValueKind.Number) { bad.Add($"{k}: not in the lab"); return; }
            n++;
            if (Math.Abs(src.GetDouble() - v) > 1e-5 * Math.Max(1.0, Math.Abs(src.GetDouble()))) bad.Add($"{k}: C# {v} vs lab {src.GetDouble()}");
        });
        // the lab's N and SIZE are the species' own numbers
        if (Math.Abs(def.GetProperty("N").GetDouble() - g.N0) > 0) bad.Add($"N: n0 {g.N0} vs lab {def.GetProperty("N").GetDouble()}");
        if (Math.Abs(def.GetProperty("SIZE").GetDouble() - g.Solitary.Size) > 1e-5) bad.Add($"SIZE: {g.Solitary.Size}");
        Check(bad.Count == 0 && n == 39, $"{n + 2} lab numbers match (e.g. R0 {g.Siege.R0} -> R1 {g.Siege.R1}, T_HOLD {g.Siege.THold} s, DIVE {g.Siege.Dive} u/s, " +
              $"glow {g.Siege.GlowDelay} s into CLOSE)" + (bad.Count > 0 ? " - " + string.Join("; ", bad.Take(5)) : ""));
        Check(g.Siege.Enabled && Math.Abs(Dt / g.Siege.Substeps - fx.GetProperty("scenarios")[0].GetProperty("dt").GetSingle()) < 1e-6,
              $"the port runs at the lab's step: {g.Siege.Substeps} sub-steps of the {Dt} s tick = 1/30 s");
    }

    // ───────────────────────────────────────────────────────────── S2

    /// <summary>A core holding exactly the lab's snapshot: one siege population of the lab's members.</summary>
    static (SubstrateCore c, SubstratePopulation pop) SiegeFromSnapshot(JsonElement snap)
    {
        int n = snap.GetProperty("n").GetInt32();
        var P = SubstrateResearch.GameSiege(); P.N0 = n; P.Capacity = n;
        var c = new SubstrateCore(n, snap.GetProperty("R").GetSingle(), Dt, 40, 1);
        int q = c.AddPopulation(P, 3);
        c.Seed(q, n, Vector3.Zero, 1f);
        var pop = c.Pops[q];
        var alive = snap.GetProperty("alive"); var pos = snap.GetProperty("pos"); var vel = snap.GetProperty("vel");
        int m = 0;
        for (int i = 0; i < n; i++)
        {
            c.Pos[i] = V3(pos, 3 * i); c.Vel[i] = V3(vel, 3 * i);
            c.Alive[i] = alive[i].GetInt32() != 0;
            if (c.Alive[i]) pop.Live[m++] = i;
        }
        pop.LiveCount = m;
        var S = pop.Siege;
        S.Phase = SubstrateSiegePhase.Roam; S.Tp = snap.GetProperty("tp").GetDouble(); S.Cool = snap.GetProperty("cool").GetDouble();
        S.C = V3(snap.GetProperty("C")); S.Axis = V3(snap.GetProperty("axis")); S.Rs = snap.GetProperty("Rs").GetSingle();
        S.Fill = snap.GetProperty("fill").GetSingle(); S.RWall = snap.GetProperty("rWall").GetSingle();
        S.Log = new List<(double, string)>();
        return (c, pop);
    }

    /// <summary>One lab encounter replayed through the port: (first step whose events differ or -1, lab events, our
    /// events, steps whose bite count differs, sorted member deviations at the lab's frames, the siege).</summary>
    static (int miss, List<string> lab, List<string> ours, int bitesDiff, List<float> dev, SubstrateSiegeState S) Replay(JsonElement sc, SubstrateSiegeAblation ablate)
    {
        var snap = sc.GetProperty("snap");
        var (c, pop) = SiegeFromSnapshot(snap);
        var S = pop.Siege;
        S.Ablate = ablate;
        double h = sc.GetProperty("dt").GetDouble(); float radius = snap.GetProperty("pilot_radius").GetSingle();
        var steps = sc.GetProperty("steps"); var frames = sc.GetProperty("frames");
        int fi = 0, bitesDiff = 0, firstEvMiss = -1;
        var labEv = new List<string>(); var ourEv = new List<string>();
        var dev = new List<float>();
        for (int k = 0; k < steps.GetArrayLength(); k++)
        {
            var st = steps[k];
            S.Clock = st.GetProperty("t").GetDouble();
            c._pilots[0] = new SubstratePilot { Pos = V3(st.GetProperty("p")), Vel = V3(st.GetProperty("v")), Radius = radius, Id = 1 };
            c._npil = 1;
            int l0 = S.Log.Count;
            SubstrateSiege.Substep(c, pop, h, 0.0);
            var ours = S.Log.Skip(l0).Select(e => $"{k}:{e.What}").ToList();
            var labs = st.GetProperty("ev").EnumerateArray().Select(e => $"{k}:{e.GetString()}").ToList();
            ourEv.AddRange(ours); labEv.AddRange(labs);
            if (firstEvMiss < 0 && !ours.SequenceEqual(labs)) firstEvMiss = k;
            if (S.Strikes != st.GetProperty("bites").GetInt32()) bitesDiff++;
            if (fi < frames.GetArrayLength() && frames[fi].GetProperty("k").GetInt32() == k)
            {
                var fp = frames[fi].GetProperty("pos");
                for (int i = 0; i < c.Capacity; i++) if (c.Alive[i]) dev.Add(Vector3.Distance(c.Pos[i], V3(fp, 3 * i)));
                fi++;
            }
        }
        dev.Sort();
        return (firstEvMiss, labEv, ourEv, bitesDiff, dev, S);
    }

    static void SiegeParity(JsonElement fx)
    {
        Console.WriteLine("\nS2. siege parity: the lab's own encounters replayed through the port (snapshot at GATHER + the lab's pilot track)");
        foreach (var sc in fx.GetProperty("scenarios").EnumerateArray())
        {
            string pol = sc.GetProperty("policy").GetString();
            var (miss, labEv, ourEv, bitesDiff, dev, S) = Replay(sc, SubstrateSiegeAblation.None);
            float med = dev[dev.Count / 2], p95 = dev[(int)(dev.Count * 0.95)];
            Check(miss < 0 && labEv.Count > 0,
                  $"{pol}: the same phase events on the same steps as the lab ({string.Join(" ", labEv)})" +
                  (miss >= 0 ? $" - ours {string.Join(" ", ourEv)}" : ""));
            Check(bitesDiff == 0, $"{pol}: the same bites on the same steps ({S.Strikes} bite(s), {S.DiveBites} in the dive, {S.WallBites} on the wall)");
            Check(med < 0.5f && p95 < 6f, $"{pol}: members where the lab put them (float vs the lab's double over {sc.GetProperty("steps").GetArrayLength()} steps: median {med:F3} u, p95 {p95:F2} u, max {dev[^1]:F1} u)");
        }
        // negative controls: the gate must see a port that drops the iris or the web
        var scs = fx.GetProperty("scenarios").EnumerateArray().ToList();
        var still = scs.First(x => x.GetProperty("policy").GetString() == "still");
        var hunter = scs.First(x => x.GetProperty("policy").GetString() == "hunter");
        var ni = Replay(still, SubstrateSiegeAblation.NoIris);
        var nb = Replay(hunter, SubstrateSiegeAblation.NoBreach);
        Check(ni.dev[ni.dev.Count / 2] > 0.5f, $"negative control: a port without the iris leaves the lab's track (median {ni.dev[ni.dev.Count / 2]:F1} u)");
        Check(nb.miss >= 0, $"negative control: a port without the web misses the lab's breach (ours {string.Join(" ", nb.ours)})");
    }

    // ───────────────────────────────────────────────────────────── S3

    /// <summary>A gap reader (the lab's BREAKER): during a shell it steers for the direction out of the shell's centre
    /// farthest from every member - the counterplay the design names, flown perfectly.</summary>
    static void Breaker(Pilot pl, SubstrateCore c, SubstratePopulation pop, Vector3[] dirs, float dt)
    {
        var S = pop.Siege;
        if (!S.InShell) { pl.Mode = "wander"; return; }
        pl.Mode = "seek";
        var us = new List<Vector3>();
        for (int i = pop.Start; i < pop.Start + pop.Cap; i++) if (c.Alive[i]) us.Add(SubstrateCore.Unit(c.Pos[i] - S.C));
        float best = -2f; Vector3 bd = dirs[0];
        var vn = SubstrateCore.Unit(pl.Vel);
        foreach (var d in dirs)
        {
            float mx = -1f; foreach (var u in us) mx = MathF.Max(mx, Vector3.Dot(d, u));
            float score = -mx + 0.05f * Vector3.Dot(d, vn);
            if (score > best) { best = score; bd = d; }
        }
        pl.Goal = S.C + bd * 1000f;
    }

    static void SiegeInGame(JsonElement fx)
    {
        Console.WriteLine("\nS3. the siege in the game's tick: one substrate population (0.1 s ticks x 3 sub-steps), scripted pilots, 6 seeds x 3 min");
        var dirs = SubstrateCore.FibDirs(96);
        var lab = fx.GetProperty("eval").GetProperty("rows");
        var rows = new Dictionary<string, (int enc, int esc, int breach, int dives, int bites, int teleBad, int armedEarly, float maxStep, int dangerMiss)>();
        foreach (var pol in new[] { "wander", "breaker", "still" })
        {
            int enc = 0, esc = 0, br = 0, dv = 0, bites = 0, teleBad = 0, early = 0, dmiss = 0; float maxStep = 0f;
            for (int seed = 0; seed < 6; seed++)
            {
                var w = new World(256, 300 + seed);
                var rng = new Random(300 + seed);
                w.Scatter(rng, 600, 0.3f, 0.9f, 16);
                var P = SubstrateResearch.GameSiege();
                int q = w.Core.AddPopulation(P, 3, 690f, 1080f);
                w.Core.Seed(q, P.N0, Ball(rng, 700f, 900f), 70f);
                var pop = w.Core.Pops[q];
                var pl = new Pilot(400 + seed, Ball(rng, 500f, 800f), Ball(rng, 1f, 1f)) { Id = 7, Speed = pol == "still" ? 0f : pol == "breaker" ? 140f : 120f, Turn = 2f, Mode = pol == "breaker" ? "wander" : pol };
                if (pol == "still") pl.Mode = "still";
                w.Pilots.Add(pl);
                float glowSince = -1f;
                for (int t = 0; t < 1800; t++)
                {
                    if (pol == "breaker" && pop.Siege.InShell) Breaker(pl, w.Core, pop, dirs, Dt);
                    int log0 = w.Log.Count;
                    w.Step();
                    var S = pop.Siege;
                    // the telegraph: every bite comes after its member's intent has been above 0.5 for >= 0.25 s
                    float itMax = 0f;
                    for (int i = pop.Start; i < pop.Start + pop.Cap; i++) if (w.Core.Alive[i]) itMax = MathF.Max(itMax, S.Intent[i - pop.Start]);
                    bool glow = (S.Phase == SubstrateSiegePhase.Close || S.Phase == SubstrateSiegePhase.Hold || S.Phase == SubstrateSiegePhase.Dive) && itMax > 0.5f;
                    if (glow && glowSince < 0f) glowSince = w.Core.T; else if (!glow && S.Phase != SubstrateSiegePhase.Scatter) glowSince = -1f;
                    for (int e = log0; e < w.Log.Count; e++)
                    {
                        var ev = w.Log[e];
                        if (ev.Kind != SubstrateEventKind.Bite || w.Core.PopOf[ev.Index] != q) continue;
                        bites++;
                        if (glowSince < 0f || w.Core.T - glowSince < 0.25f) teleBad++;
                    }
                    // the danger tier is exactly the members that may bite; none before the glow is up
                    for (int i = pop.Start; i < pop.Start + pop.Cap; i++)
                    {
                        if (!w.Core.Alive[i] || w.Core.Starving[i]) continue;
                        if (w.Core.Danger[i] != S.Dangerous[i - pop.Start]) dmiss++;
                        if (w.Core.Danger[i] && (S.Phase == SubstrateSiegePhase.Roam || S.Phase == SubstrateSiegePhase.Gather || S.Phase == SubstrateSiegePhase.Scatter)) early++;
                    }
                }
                var Sx = pop.Siege;
                enc += (int)Sx.Encounters; esc += (int)Sx.Escapes; br += (int)Sx.Breaches; dv += (int)Sx.Dives;
                maxStep = MathF.Max(maxStep, w.MaxStep);
            }
            rows[pol] = (enc, esc, br, dv, bites, teleBad, early, maxStep, dmiss);
            Console.WriteLine($"    {pol,-8} encounters {enc / 18f:F2}/min, escape share {(enc > 0 ? (float)esc / enc : 0):F2}, breach share {(enc > 0 ? (float)br / enc : 0):F2}, " +
                              $"dives {dv}, bites {bites} ({(enc > 0 ? (float)bites / enc : 0):F2}/encounter), max step {maxStep:F1} u" +
                              (lab.TryGetProperty(pol, out var lr) ? $"   [lab: {lr.GetProperty("encounters_per_min").GetDouble():F2}/min, escape {lr.GetProperty("escape_share").GetDouble():F2}, breach {lr.GetProperty("breach_share").GetDouble():F2}]" : ""));
        }
        var wa = rows["wander"]; var bk = rows["breaker"]; var sl = rows["still"];
        Check(wa.enc >= 18 && bk.enc >= 18, $"the rhythm repeats: {wa.enc / 18f:F2} (wander) and {bk.enc / 18f:F2} (gap reader) encounters a minute (lab {lab.GetProperty("wander").GetProperty("encounters_per_min").GetDouble():F2} and {lab.GetProperty("breaker").GetProperty("encounters_per_min").GetDouble():F2})");
        Check(bk.enc > 0 && (float)bk.esc / bk.enc >= 0.75f, $"the iris is a way out: the gap reader escapes {(float)bk.esc / bk.enc:F2} of its encounters (lab {lab.GetProperty("breaker").GetProperty("escape_share").GetDouble():F2})");
        Check(wa.enc > 0 && (float)wa.bites / wa.enc > 2f * (bk.enc > 0 ? (float)bk.bites / bk.enc : 0f),
              $"careless pays, skilled does not: {(float)wa.bites / wa.enc:F2} bites/encounter wandering vs {(bk.enc > 0 ? (float)bk.bites / bk.enc : 0):F2} reading the gap");
        Check(sl.dives > 0 && sl.bites > 0, $"surrounded, then everyone dives: a hovering pilot is dived on ({sl.dives} dives, {sl.bites} bites)");
        int tBad = rows.Values.Sum(r => r.teleBad), tEarly = rows.Values.Sum(r => r.armedEarly), tMiss = rows.Values.Sum(r => r.dangerMiss), all = rows.Values.Sum(r => r.bites);
        Check(tBad == 0 && all > 0, $"every bite is telegraphed: intent above 0.5 for >= 0.25 s before each of {all} bites ({tBad} not)");
        Check(tEarly == 0 && tMiss == 0, $"the danger tier (what burns petals on contact) is exactly the members that may bite, and never during ROAM/GATHER/SCATTER ({tMiss} mismatches, {tEarly} early)");
        float bound = SubstrateResearch.GameSiege().Siege.Dive * Dt * 1.15f;
        float ms = rows.Values.Max(r => r.maxStep);
        Check(ms <= bound, $"no teleport: the largest per-tick move is {ms:F1} u (bound {bound:F1} u = the dive's {SubstrateResearch.GameSiege().Siege.Dive} u/s; the substrate's other species stay under 25.8 u)");
    }

    // ───────────────────────────────────────────────────────────── S4

    static void SiegeLifecycle()
    {
        Console.WriteLine("\nS4. siege lifecycle: real food, no pilot for 4 min then a wanderer for 4 min - it feeds, breeds, and the ledger closes");
        var w = new World(256, 77);
        var rng = new Random(77);
        w.Scatter(rng, 900, 0.55f, 0.9f, 20, 20f, 60f);
        double f0 = w.LiveMass();
        var P = SubstrateResearch.GameSiege();
        int q = w.Core.AddPopulation(P, 3, 690f, 1080f);
        w.Core.Seed(q, P.N0, Ball(rng, 750f, 850f), 70f);
        var pop = w.Core.Pops[q];
        double s0 = w.Core.MassIn, drift = 0;
        int rammed = 0;
        for (int t = 0; t < 4800; t++)
        {
            if (t == 2400) w.Pilots.Add(new Pilot(78, Ball(rng, 500f, 800f), new Vector3(0, 0, 1)) { Id = 3 });
            // a ram every 4 s in the second half: the member dies (through its proxy in the game), its body stays as mass
            if (t > 2400 && t % 40 == 0)
            {
                int k = Enumerable.Range(pop.Start, pop.Cap).FirstOrDefault(i => w.Core.Alive[i] && !w.Core.Starving[i], -1);
                if (k >= 0) { w.KillAndLay(k); rammed++; }
            }
            w.Step();
            drift = Math.Max(drift, Math.Abs(w.LiveMass() + w.Core.MassHeld() - (f0 + s0)) / (f0 + s0));
        }
        var c = w.Core;
        double held = c.MassHeld(), core = Math.Abs(c.MassIn - c.MassOut - held) / Math.Max(1, c.MassIn);
        Console.WriteLine($"    eaten {w.Eaten:F0}, births {pop.Births}, starvations {pop.Starvations}, rammed {rammed}, alive {pop.Alive}, encounters {pop.Siege.Encounters}");
        Check(w.Eaten > 0 && pop.Births > 0, $"it completes its life cycle: it eats ({w.Eaten:F0} u^3 of flora) and breeds ({pop.Births} births) - the lab only regrew rammed members");
        Check(pop.Alive >= P.N0 / 2 && pop.Siege.Encounters > 0, $"it survives the rams and keeps hunting ({pop.Alive} alive after {rammed} rams, {pop.Siege.Encounters} encounters)");
        Check(core < 1e-6 && drift < 1e-5, $"the ledger closes: agent in - out = held (drift {core:E1}), food + bodies constant (max drift {drift:E1})");
    }
}
