// Group `arms` (Docs/SUBSTRATE_FAUNA.md §11): the game's ARMS RACE port - the shoal and its harriers - against the lab
// run it came from (Tools/NCA/arms_*.py, run a9 "selfish herd", generation 1500; arms_fixture.json).
//
//   A0 fidelity   - every number of the pond equals the lab's Cfg; the policy literals equal the lab's weights bit for bit
//   A1 parity     - given the lab's state mid-encounter (a vessel crossing the pond), the port observes, decides and steps
//                   as the lab does: the same inputs, the same outputs, the same positions, the same catch attempts
//   A2 behaviour  - from the lab's own start states, the port's shoal schools and its harriers pack as the lab's did
//                   (its three seeds x 60 s, the lab's metric definitions); the untrained generation 0 does not school
//   A3 in the game's tick - two substrate populations around a real pond with real flora and a vessel crossing it:
//                   catches are the food web's kills, the burst is the only danger and only after its 0.4 s wind-up
//   A4 lifecycle  - 10 minutes on regrowing flora: both species feed and breed, neither collapses, the ledger closes
//   A5 colliders  - the pond's proxy caps (spent from the cell's spare colliders) still engage: a vessel ramming
//                   through the shoal meets a proxied fish, a vessel the harriers hunt is bitten by proxied ones
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CosmicShore.Gameplay;

static partial class SubstrateHarness
{
    static JsonElement ArmsFixture()
    {
        string path = Path.Combine(_repo, "Tools", "Build", "substrate_harness", "arms_fixture.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    static float[] Floats(JsonElement a) => a.EnumerateArray().Select(e => e.GetSingle()).ToArray();
    static bool[] Bools(JsonElement a) => a.EnumerateArray().Select(e => e.GetBoolean()).ToArray();

    static void Arms()
    {
        var fx = ArmsFixture();
        ArmsFidelity(fx);
        ArmsParity(fx);
        ArmsBehave(fx);
        ArmsInGame();
        ArmsLifecycle();
        ArmsProxies();
    }

    /// <summary>The pond's proxies: SPENT from the Swarm cell's spare colliders (1,171 of 1,200 after #1017), not
    /// re-divided from the substrate's 39 (group Q) - author_substrate_fauna.py ARMS_PROXIES reads this list.</summary>
    static readonly (string species, int cap, float engage)[] ArmsProxyCaps =
    {
        ("shoal", 6, 100f), ("harrier", 4, 200f),
    };

    // ───────────────────────────────────────────────────────────── A0

    static void ArmsFidelity(JsonElement fx)
    {
        Console.WriteLine("\nA0. arms fidelity: the pond's numbers vs the lab's arms_sim.Cfg, the policies vs the lab's weights");
        var cfg = fx.GetProperty("cfg");
        var sh = SubstrateResearch.GameShoal(); var hr = SubstrateResearch.GameHarrier();
        int n = 0; var bad = new List<string>();
        sh.Arms.Visit((k, v) =>
        {
            if (!cfg.TryGetProperty(k, out var e)) { bad.Add($"{k}: not in the lab"); return; }
            n++;
            if (Math.Abs(e.GetDouble() - v) > 1e-6 * Math.Max(1.0, Math.Abs(e.GetDouble()))) bad.Add($"{k}: C# {v} vs lab {e.GetDouble()}");
        });
        Check(bad.Count == 0 && n >= 21, $"{n} lab numbers match (pond R {sh.Arms.PondR}, prey {sh.Arms.PreyV} u/s turn {sh.Arms.PreyTurn}, " +
              $"harriers {sh.Arms.PredV}/{sh.Arms.PredBurst} u/s turn {sh.Arms.PredTurn}, catch {sh.Arms.CatchR} u, confusion {sh.Arms.Confusion})" +
              (bad.Count > 0 ? " - " + string.Join("; ", bad.Take(5)) : ""));
        var a = new Dictionary<string, float>(); var b = new Dictionary<string, float>();
        sh.Arms.Visit((k, v) => a[k] = v); hr.Arms.Visit((k, v) => b[k] = v);
        bool same = a.Count == b.Count && a.All(kv => b[kv.Key] == kv.Value) && sh.Arms.SatedHunger == hr.Arms.SatedHunger
                    && sh.Arms.FoodFull == hr.Arms.FoodFull && sh.Arms.VesselPad == hr.Arms.VesselPad && sh.Arms.EatenTimeout == hr.Arms.EatenTimeout;
        Check(same && sh.Arms.Role == 1 && hr.Arms.Role == 2 && hr.PreyName == "shoal",
              "the shoal (role 1) and the harriers (role 2, prey \"shoal\") describe the same pond");
        // the bodies the substrate draws and the speeds its continuity gate bounds are the lab's
        bool body = sh.Solitary.Speed == sh.Arms.PreyV && sh.Solitary.Size == sh.Arms.PreySize
                    && hr.Solitary.Speed == hr.Arms.PredV && hr.Gregarious.Speed == hr.Arms.PredBurst && hr.Solitary.Size == hr.Arms.PredSize;
        Check(body, $"bodies and speeds are the lab's: shoal {sh.Solitary.Speed} u/s x {sh.Solitary.Size} u, harrier {hr.Solitary.Speed}/{hr.Gregarious.Speed} u/s x {hr.Solitary.Size} u");
        Check(hr.StrikeWindupS == SubstrateResearch.GamePack().StrikeWindupS && hr.StrikeWindupS > 0f,
              $"the harriers' burst winds up {hr.StrikeWindupS} s before it can burn, the pack's fair-burn rule (#1003)");
        // the shipped literals ARE the lab's g1500 weights
        var pol = fx.GetProperty("policy").GetProperty("g1500");
        var q = Floats(pol.GetProperty("thq")); var p = Floats(pol.GetProperty("thp"));
        int dq = q.Length == SubstrateArmsPolicy.Prey.Length ? q.Where((v, k) => BitConverter.SingleToInt32Bits(v) != BitConverter.SingleToInt32Bits(SubstrateArmsPolicy.Prey[k])).Count() : -1;
        int dp = p.Length == SubstrateArmsPolicy.Predator.Length ? p.Where((v, k) => BitConverter.SingleToInt32Bits(v) != BitConverter.SingleToInt32Bits(SubstrateArmsPolicy.Predator[k])).Count() : -1;
        int expQ = SubstrateArmsSim.PreyIn * 32 + 32 + 32 * 32 + 32 + 32 * SubstrateArmsSim.PreyOut + SubstrateArmsSim.PreyOut;
        int expP = SubstrateArmsSim.PredIn * 32 + 32 + 32 * 32 + 32 + 32 * SubstrateArmsSim.PredOut + SubstrateArmsSim.PredOut;
        Check(dq == 0 && dp == 0 && q.Length == expQ && p.Length == expP,
              $"SubstrateArmsPolicy = the lab's a9 g1500 weights bit for bit ({q.Length} prey + {p.Length} predator floats, sha256 {fx.GetProperty("source").GetProperty("sha256").GetProperty("g1500").GetString()[..12]})");
    }

    // ───────────────────────────────────────────────────────────── A1

    static SubstrateArmsSim ArmsSimFrom(JsonElement state, float[] food)
    {
        var K = SubstrateResearch.GameShoal().Arms;
        var q = state.GetProperty("prey"); var p = state.GetProperty("pred");
        int nq = q.GetProperty("alive").GetArrayLength(), np = p.GetProperty("alive").GetArrayLength();
        var sim = new SubstrateArmsSim(K, nq, np) { Dt = Dt };
        void Side(JsonElement s, Vector3[] P, Vector3[] V, Vector3[] F, Vector3[] U, bool[] A, float[] S)
        {
            var pos = s.GetProperty("pos"); var vel = s.GetProperty("vel"); var fwd = s.GetProperty("fwd"); var up = s.GetProperty("up");
            var al = s.GetProperty("alive"); var sg = s.GetProperty("sig");
            for (int i = 0; i < A.Length; i++)
            {
                P[i] = V3(pos, 3 * i); V[i] = V3(vel, 3 * i); F[i] = V3(fwd, 3 * i); U[i] = V3(up, 3 * i);
                A[i] = al[i].GetBoolean(); S[i] = sg[i].GetSingle();
            }
        }
        Side(q, sim.Pq, sim.Vq, sim.Fq, sim.Uq, sim.Aq, sim.Sq);
        Side(p, sim.Pp, sim.Vp, sim.Fp, sim.Up, sim.Ap, sim.Sp);
        var st = p.GetProperty("stam"); var hd = p.GetProperty("hand");
        for (int i = 0; i < np; i++) { sim.Stam[i] = st[i].GetSingle(); sim.Hand[i] = hd[i].GetSingle(); sim.MayBurst[i] = true; }
        sim.Food = (float[])food.Clone();
        return sim;
    }

    /// <summary>|ours - lab| over the rows of living agents: (max, count above tol, count compared).</summary>
    static (float max, int over, int n) Diff(float[] ours, float[] lab, bool[] alive, int width, float tol)
    {
        float mx = 0f; int over = 0, n = 0;
        for (int i = 0; i < alive.Length; i++)
        {
            if (!alive[i]) continue;
            for (int k = 0; k < width; k++)
            {
                float d = MathF.Abs(ours[i * width + k] - lab[i * width + k]);
                mx = MathF.Max(mx, d); n++;
                if (d > tol) over++;
            }
        }
        return (mx, over, n);
    }

    static float[] Flat(Vector3[] v) { var o = new float[v.Length * 3]; for (int i = 0; i < v.Length; i++) { o[3 * i] = v[i].X; o[3 * i + 1] = v[i].Y; o[3 * i + 2] = v[i].Z; } return o; }

    static void ArmsParity(JsonElement fx)
    {
        Console.WriteLine("\nA1. arms parity: the lab's mid-encounter states (seed 5, a vessel crossing the pond) observed, decided and stepped by the port");
        var pol = fx.GetProperty("policy").GetProperty("g1500");
        var thq = Floats(pol.GetProperty("thq")); var thp = Floats(pol.GetProperty("thp"));
        int obsOver = 0, obsN = 0, outOver = 0, outN = 0, posOver = 0, posN = 0, burstMiss = 0, attMiss = 0, attTotal = 0, cases = 0;
        float obsMax = 0f, outMax = 0f, posMax = 0f, velMax = 0f, stamMax = 0f;
        foreach (var c in fx.GetProperty("parity").EnumerateArray())
        {
            cases++;
            var before = c.GetProperty("before");
            var sim = ArmsSimFrom(before, Floats(c.GetProperty("food")));
            var ve = c.GetProperty("vessel");
            sim.GPos[0] = V3(ve.GetProperty("pos")); sim.GVel[0] = V3(ve.GetProperty("vel")); sim.GRad[0] = ve.GetProperty("radius").GetSingle(); sim.NG = 1;
            var Aq = (bool[])sim.Aq.Clone(); var Ap = (bool[])sim.Ap.Clone();
            sim.Observe();
            var o1 = Diff(sim.Xq, Floats(c.GetProperty("obs_prey")), Aq, SubstrateArmsSim.PreyIn, 1e-3f);
            var o2 = Diff(sim.Xp, Floats(c.GetProperty("obs_pred")), Ap, SubstrateArmsSim.PredIn, 1e-3f);
            obsMax = MathF.Max(obsMax, MathF.Max(o1.max, o2.max)); obsOver += o1.over + o2.over; obsN += o1.n + o2.n;
            sim.Decide(thq, thp);
            var labOq = Floats(c.GetProperty("out_prey")); var labOp = Floats(c.GetProperty("out_pred"));
            var d1 = Diff(sim.Oq, labOq, Aq, SubstrateArmsSim.PreyOut, 1e-3f);
            var d2 = Diff(sim.Op, labOp, Ap, SubstrateArmsSim.PredOut, 1e-3f);
            outMax = MathF.Max(outMax, MathF.Max(d1.max, d2.max)); outOver += d1.over + d2.over; outN += d1.n + d2.n;
            // the step from the lab's own outputs (isolates the physics from the observation's float noise)
            Array.Copy(labOq, sim.Oq, labOq.Length); Array.Copy(labOp, sim.Op, labOp.Length);
            sim.Apply(() => 1.0);   // the lab's NoCatch draw: attempts counted, nobody caught
            var aft = c.GetProperty("after");
            var aq = aft.GetProperty("prey"); var ap = aft.GetProperty("pred");
            var p1 = Diff(Flat(sim.Pq), Floats(aq.GetProperty("pos")), Aq, 3, 5e-3f);
            var p2 = Diff(Flat(sim.Pp), Floats(ap.GetProperty("pos")), Ap, 3, 5e-3f);
            posMax = MathF.Max(posMax, MathF.Max(p1.max, p2.max)); posOver += p1.over + p2.over; posN += p1.n + p2.n;
            velMax = MathF.Max(velMax, MathF.Max(Diff(Flat(sim.Vq), Floats(aq.GetProperty("vel")), Aq, 3, 1e-2f).max,
                                                 Diff(Flat(sim.Vp), Floats(ap.GetProperty("vel")), Ap, 3, 1e-2f).max));
            stamMax = MathF.Max(stamMax, Diff(sim.Stam, Floats(ap.GetProperty("stam")), Ap, 1, 1e-4f).max);
            var lb = Bools(ap.GetProperty("burst"));
            for (int i = 0; i < lb.Length; i++) if (lb[i] != sim.Burst[i]) burstMiss++;
            var labAtt = c.GetProperty("attempts").EnumerateArray().Select(x => (x[0].GetInt32(), x[1].GetInt32(), x[2].GetInt32())).ToList();
            var ours = sim.Attempts.Select(x => (x.p, x.q, x.crowd)).ToList();
            attTotal += labAtt.Count;
            if (!labAtt.SequenceEqual(ours)) attMiss++;
        }
        Check(obsOver <= obsN / 1000 && obsMax < 1e-2f, $"observations: {obsN} inputs over {cases} states, max |diff| {obsMax:E1} ({obsOver} above 1e-3)");
        Check(outOver <= outN / 1000 && outMax < 1e-2f, $"decisions: {outN} MLP outputs, max |diff| {outMax:E1} ({outOver} above 1e-3)");
        Check(posOver == 0 && burstMiss == 0 && stamMax < 1e-4f,
              $"the step: {posN / 3} agents land where the lab put them (max {posMax:E1} u, velocity {velMax:E1} u/s), the same bursts, stamina within {stamMax:E1}");
        Check(attMiss == 0 && attTotal > 0, $"swept catches: the same {attTotal} catch attempts (predator, prey, crowd) in every state");
        // negative control: the parity gate sees a port that drops the vessel from the prey's eyes
        {
            var c = fx.GetProperty("parity")[0];
            var sim = ArmsSimFrom(c.GetProperty("before"), Floats(c.GetProperty("food")));
            sim.NG = 0;
            sim.Observe();
            var o = Diff(sim.Xq, Floats(c.GetProperty("obs_prey")), (bool[])sim.Aq.Clone(), SubstrateArmsSim.PreyIn, 1e-3f);
            var p = Diff(sim.Xp, Floats(c.GetProperty("obs_pred")), (bool[])sim.Ap.Clone(), SubstrateArmsSim.PredIn, 1e-3f);
            Check(o.over + p.over > 0, $"negative control: a port blind to the vessel fails parity ({o.over + p.over} inputs off)");
        }
    }

    // ───────────────────────────────────────────────────────────── A2

    sealed class ArmsMetrics
    {
        public readonly List<double> PolG = new(), PolL = new(), Social = new(), Nnd = new(), Spacing = new(), Pack = new();
        public double Catches;
        public double Mean(List<double> x) => x.Count == 0 ? double.NaN : x.Average();

        /// <summary>arms_metrics.prey_metrics + pred_metrics (polarisation, social share, nnd, spacing, pack share) on one frame.</summary>
        public void Frame(Vector3[] Pq, Vector3[] Vq, bool[] Aq, Vector3[] Pp, bool[] Ap)
        {
            var P = new List<Vector3>(); var U = new List<Vector3>();
            for (int i = 0; i < Aq.Length; i++)
                if (Aq[i]) { P.Add(Pq[i]); U.Add(Vq[i] / MathF.Max(Vq[i].Length(), 1e-6f)); }
            int n = P.Count;
            if (n >= 5)
            {
                var sum = Vector3.Zero; foreach (var u in U) sum += u;
                PolG.Add((sum / n).Length());
                double pl = 0; int grp = 0; double nn = 0;
                for (int i = 0; i < n; i++)
                {
                    var s = Vector3.Zero; int m = 0; float best = float.PositiveInfinity;
                    for (int j = 0; j < n; j++)
                    {
                        float d = Vector3.Distance(P[i], P[j]);
                        if (d < 15f) { s += U[j]; m++; }
                        if (j != i && d < best) best = d;
                    }
                    if (m >= 3) { grp++; pl += (s / m).Length(); }
                    nn += best;
                }
                if (grp > 0) PolL.Add(pl / grp);
                Social.Add((double)grp / n);
                Nnd.Add(nn / n);
            }
            var A = new List<Vector3>();
            for (int i = 0; i < Ap.Length; i++) if (Ap[i]) A.Add(Pp[i]);
            if (A.Count >= 2)
            {
                double sp = 0; int pk = 0;
                for (int i = 0; i < A.Count; i++)
                {
                    float best = float.PositiveInfinity;
                    for (int j = 0; j < A.Count; j++) if (j != i) best = MathF.Min(best, Vector3.Distance(A[i], A[j]));
                    sp += best; if (best < 40f) pk++;
                }
                Spacing.Add(sp / A.Count); Pack.Add((double)pk / A.Count);
            }
        }
    }

    /// <summary>The lab's behave(): its start state, its food template regrowing, no vessel, 60 s.</summary>
    static ArmsMetrics ArmsRun(JsonElement start, float[] thq, float[] thp, float secs)
    {
        var tmpl = Floats(start.GetProperty("food"));
        var sim = ArmsSimFrom(start.GetProperty("state"), tmpl);
        var rng = new Random(start.GetProperty("seed").GetInt32() + 11);
        var m = new ArmsMetrics();
        int steps = (int)MathF.Round(secs / Dt);
        for (int t = 0; t < steps; t++)
        {
            sim.Observe();
            sim.Decide(thq, thp);
            sim.Apply(() => rng.NextDouble());
            m.Catches += sim.Catches.Count;
            sim.LabGraze(tmpl);
            if (t % 5 == 0) m.Frame(sim.Pq, sim.Vq, sim.Aq, sim.Pp, sim.Ap);
        }
        return m;
    }

    static void ArmsBehave(JsonElement fx)
    {
        var bh = fx.GetProperty("behave");
        float secs = bh.GetProperty("secs").GetSingle();
        Console.WriteLine($"\nA2. arms behaviour: the lab's three start states x {secs} s through the port, the lab's metric definitions");
        var rows = new Dictionary<string, Dictionary<string, double>>();
        foreach (var gen in new[] { "g1500", "g0" })
        {
            var pol = fx.GetProperty("policy").GetProperty(gen);
            var thq = Floats(pol.GetProperty("thq")); var thp = Floats(pol.GetProperty("thp"));
            var per = bh.GetProperty("starts").EnumerateArray().Select(s => ArmsRun(s, thq, thp, secs)).ToList();
            var r = new Dictionary<string, double>
            {
                ["catch_per_min"] = per.Average(m => m.Catches / (secs / 60.0)),
                ["polarisation_local"] = per.Average(m => m.Mean(m.PolL)),
                ["polarisation_global"] = per.Average(m => m.Mean(m.PolG)),
                ["social_share"] = per.Average(m => m.Mean(m.Social)),
                ["nnd"] = per.Average(m => m.Mean(m.Nnd)),
                ["pack_share"] = per.Average(m => m.Mean(m.Pack)),
                ["pred_spacing"] = per.Average(m => m.Mean(m.Spacing)),
            };
            rows[gen] = r;
            var lab = bh.GetProperty("lab_" + gen).GetProperty("mean");
            Console.WriteLine($"    {gen,-5} " + string.Join(", ", r.Select(kv => $"{kv.Key} {kv.Value:F3} (lab {lab.GetProperty(kv.Key).GetDouble():F3})")));
        }
        var g = rows["g1500"]; var z = rows["g0"];
        var L = bh.GetProperty("lab_g1500").GetProperty("mean");
        double Lab(string k) => L.GetProperty(k).GetDouble();
        Check(Math.Abs(g["polarisation_local"] - Lab("polarisation_local")) < 0.08 && Math.Abs(g["social_share"] - Lab("social_share")) < 0.12,
              $"the shoal schools as the lab's did: local polarisation {g["polarisation_local"]:F2} (lab {Lab("polarisation_local"):F2}), " +
              $"{g["social_share"]:F2} of it in schools (lab {Lab("social_share"):F2})");
        Check(Math.Abs(g["nnd"] / Lab("nnd") - 1) < 0.3, $"as tight: nearest neighbour {g["nnd"]:F1} u (lab {Lab("nnd"):F1} u)");
        Check(Math.Abs(g["pack_share"] - Lab("pack_share")) < 0.12 && Math.Abs(g["pred_spacing"] / Lab("pred_spacing") - 1) < 0.25,
              $"the harriers pack as the lab's did: pack share {g["pack_share"]:F2} (lab {Lab("pack_share"):F2}), spacing {g["pred_spacing"]:F0} u (lab {Lab("pred_spacing"):F0} u)");
        Check(Math.Abs(g["catch_per_min"] / Lab("catch_per_min") - 1) < 0.35, $"and catch at the lab's rate: {g["catch_per_min"]:F1}/min (lab {Lab("catch_per_min"):F1}/min)");
        Check(z["polarisation_local"] < 0.7 && g["polarisation_local"] - z["polarisation_local"] > 0.2 && z["nnd"] > 1.5 * g["nnd"],
              $"negative control: generation 0 (untrained) does not school through the same port (local polarisation {z["polarisation_local"]:F2}, nearest neighbour {z["nnd"]:F1} u)");
    }

    // ───────────────────────────────────────────────────────────── A3 / A4: the game

    sealed class Pond
    {
        public World W;
        public int Q, P;
        public SubstratePopulation Shoal, Harrier;
        public Vector3 C0;
        public readonly List<Vector3> Hearts = new();
        public readonly List<int> PlantOf = new();
        public readonly List<int> Leaf = new();
        public readonly List<float> LeafDeadAt = new();
        public double Regrown;
    }

    /// <summary>The pond's planting pen (author_swarm_fauna.py, Middle region): half-angle about +Y and radial range.</summary>
    const float PenDeg = 5f, PenLo = 715f, PenHi = 815f;

    /// <summary>A Middle-shell pond as the cell plants it: both species in band 690-840 penned to a 15 degree sector about
    /// +Y, the pond centred on that axis at the band's middle, and two Time plants (the floor of 10 over five pens) rooted
    /// in the pond's pen - each plant the
    /// author's Borromean Time plant (360 plates, 3,279 u^3) - sensed as the game senses flora: one unit per heart.</summary>
    static Pond MakePond(int seed, int plants = 2)
    {
        var w = new World(256, seed);
        var rng = new Random(seed);
        var o = new Pond { W = w };
        var sh = SubstrateResearch.GameShoal(); var hr = SubstrateResearch.GameHarrier();
        o.Q = w.Core.AddPopulation(sh, 4, 690f, 840f); w.Core.SetSector(o.Q, Vector3.UnitY, 15f);
        o.P = w.Core.AddPopulation(hr, 2, 690f, 840f); w.Core.SetSector(o.P, Vector3.UnitY, 15f);
        o.Shoal = w.Core.Pops[o.Q]; o.Harrier = w.Core.Pops[o.P];
        o.C0 = SubstrateArms.PondCentre(o.Shoal);
        for (int k = 0; k < plants; k++)
        {
            float r = PenLo + (PenHi - PenLo) * (float)rng.NextDouble();
            float ang = PenDeg * MathF.PI / 180f * MathF.Sqrt((float)rng.NextDouble()), az = 2f * MathF.PI * (float)rng.NextDouble();
            var heart = new Vector3(MathF.Sin(ang) * MathF.Cos(az), MathF.Cos(ang), MathF.Sin(ang) * MathF.Sin(az)) * r;
            o.Hearts.Add(heart);
            for (int l = 0; l < 360; l++)
            {
                o.Leaf.Add(w.MassPos.Count); o.LeafDeadAt.Add(-1f); o.PlantOf.Add(k);
                w.MassPos.Add(heart + new Vector3((float)Gauss(rng), (float)Gauss(rng), (float)Gauss(rng)) * 15f);
                w.MassVol.Add(3279f / 360f);
                w.MassAlive.Add(true);
            }
        }
        w.SenseFood = () =>
        {
            var live = new bool[o.Hearts.Count];
            for (int k = 0; k < o.Leaf.Count; k++) if (w.MassAlive[o.Leaf[k]]) live[o.PlantOf[k]] = true;
            var f = new List<SubstrateFood>();
            for (int k = 0; k < live.Length; k++) if (live[k]) f.Add(new SubstrateFood { Pos = o.Hearts[k], Volume = 1f });
            return f;
        };
        w.Core.Seed(o.Q, sh.N0, o.C0, 40f);
        w.Core.Seed(o.P, hr.N0, o.C0 + new Vector3(110f, 0f, 0f), 25f);
        return o;
    }

    /// <summary>A plant re-leafs: an eaten leaf grows back at its place after regrowS (new flora, counted apart).</summary>
    static void Regrow(Pond o, float regrowS)
    {
        var w = o.W;
        for (int k = 0; k < o.Leaf.Count; k++)
        {
            int m = o.Leaf[k];
            if (w.MassAlive[m]) { o.LeafDeadAt[k] = -1f; continue; }
            if (o.LeafDeadAt[k] < 0f) { o.LeafDeadAt[k] = w.Core.T; continue; }
            if (w.Core.T - o.LeafDeadAt[k] >= regrowS) { w.MassAlive[m] = true; o.Regrown += w.MassVol[m]; o.LeafDeadAt[k] = -1f; }
        }
    }

    /// <summary>The fixture's vessel: a chord through the pond 60 u off its centre, back and forth at 120 u/s.</summary>
    static (Vector3 pos, Vector3 vel) Crossing(float t)
    {
        const float span = 380f, v = 120f;
        float s = (t * v) % (2 * span);
        return s < span ? (new Vector3(s - span / 2, 60f, -20f), new Vector3(v, 0f, 0f)) : (new Vector3(span * 1.5f - s, 60f, -20f), new Vector3(-v, 0f, 0f));
    }

    static void ArmsInGame()
    {
        Console.WriteLine("\nA3. arms in the game's tick: the shoal and its harriers as substrate populations, real flora, a vessel crossing the pond, 3 seeds x 3 min");
        int catches = 0, preyed = 0, shoalDanger = 0, earlyDanger = 0, dangerTicks = 0, bites = 0, outside = 0, steps = 0;
        float maxStep = 0f, minHeld = float.MaxValue;
        var pol = new List<double>(); var pack = new List<double>();
        foreach (int seed in new[] { 41, 42, 43 })
        {
            var o = MakePond(seed);
            var w = o.W;
            var pl = new Pilot(seed, o.C0, Vector3.UnitX) { Id = 5, Mode = "replay" };
            w.Pilots.Add(pl);
            var m = new ArmsMetrics();
            for (int t = 0; t < 1800; t++)
            {
                var (vp, vv) = Crossing(t * Dt);
                pl.Pos = o.C0 + vp; pl.Vel = vv;
                int log0 = w.Log.Count;
                w.Step();
                Regrow(o, 60f);
                steps++;
                var c = w.Core;
                for (int i = o.Shoal.Start; i < o.Shoal.Start + o.Shoal.Cap; i++) if (c.Alive[i] && c.Danger[i]) shoalDanger++;
                for (int k = 0; k < o.Harrier.Cap; k++)
                {
                    int i = o.Harrier.Start + k;
                    if (!c.Alive[i]) continue;
                    if (Vector3.Distance(c.Pos[i], o.C0) > o.Harrier.P.Arms.PondR + 1f) outside++;
                    if (!c.Danger[i]) continue;
                    dangerTicks++;
                    minHeld = MathF.Min(minHeld, o.Harrier.Arms.BurstHeld[k]);
                    if (o.Harrier.Arms.BurstHeld[k] < o.Harrier.P.StrikeWindupS - 1e-3f) earlyDanger++;
                }
                for (int i = o.Shoal.Start; i < o.Shoal.Start + o.Shoal.Cap; i++)
                    if (c.Alive[i] && Vector3.Distance(c.Pos[i], o.C0) > o.Shoal.P.Arms.PondR + 1f) outside++;
                for (int e = log0; e < w.Log.Count; e++)
                    if (w.Log[e].Kind == SubstrateEventKind.Bite && c.PopOf[w.Log[e].Index] == o.P) bites++;
                if (t >= 300 && t % 5 == 0)
                {
                    var sim = o.Shoal.Arms.Sim;
                    m.Frame(sim.Pq, sim.Vq, sim.Aq, sim.Pp, sim.Ap);
                }
            }
            catches += (int)o.Harrier.Arms.Catches; preyed += w.Preyed;
            maxStep = MathF.Max(maxStep, w.MaxStep);
            pol.Add(m.Mean(m.PolL)); pack.Add(m.Mean(m.Pack));
            Console.WriteLine($"    seed {seed}: catches {o.Harrier.Arms.Catches} ({o.Harrier.Arms.Attempts} attempts), shoal {o.Shoal.Alive} alive ({o.Shoal.Births} births), " +
                              $"harriers {o.Harrier.Alive} ({o.Harrier.Births} births), local polarisation {m.Mean(m.PolL):F2}, pack share {m.Mean(m.Pack):F2}, eaten {w.Eaten:F0} u^3");
        }
        Check(catches > 0 && preyed == catches, $"every catch is a kill the food web lands: {catches} catches, {preyed} prey eaten through the owner (the proxy's path)");
        Check(shoalDanger == 0, "the shoal never burns a vessel (prey, no danger tier)");
        // a vessel that lingers in the pond is hunted: the harriers read it as prey (the lab's player test)
        int hoverBites = 0, hoverEarly = 0;
        foreach (int seed in new[] { 44, 45, 46 })
        {
            var o = MakePond(seed);
            var w = o.W;
            var pl = new Pilot(seed, o.C0 + new Vector3(-60f, 40f, 0f), Vector3.UnitX) { Id = 6, Mode = "replay" };
            for (int t = 0; t < 900; t++)
            {
                // drifting slowly across the pond (20 u/s), back and forth
                float x = -120f + 240f * MathF.Abs(((t * Dt * 20f / 240f) % 2f) - 1f);
                pl.Pos = o.C0 + new Vector3(x, 40f, 0f); pl.Vel = new Vector3(20f, 0f, 0f);
                if (t == 0) w.Pilots.Add(pl);
                int log0 = w.Log.Count;
                w.Step();
                for (int e = log0; e < w.Log.Count; e++)
                    if (w.Log[e].Kind == SubstrateEventKind.Bite && w.Core.PopOf[w.Log[e].Index] == o.P)
                    {
                        hoverBites++;
                        if (o.Harrier.Arms.BurstHeld[w.Log[e].Index - o.Harrier.Start] < o.Harrier.P.StrikeWindupS - 1e-3f) hoverEarly++;
                    }
            }
        }
        Check(dangerTicks > 0 && earlyDanger == 0 && hoverEarly == 0,
              $"a harrier burns only mid-burst, after {SubstrateResearch.GameHarrier().StrikeWindupS} s of it ({dangerTicks} dangerous agent-ticks, shortest burst then {minHeld:F1} s, {earlyDanger + hoverEarly} early)");
        Check(hoverBites > 0, $"the harriers hunt a vessel that lingers: {hoverBites} bites on a vessel drifting through the pond at 20 u/s (3 x 90 s); " +
                              $"{bites} on one cruising through at 120 u/s (3 x 3 min) - faster than their burst");
        Check(pol.Average() > 0.75 && pack.Average() > 0.2, $"the lab's behaviour survives the port: local polarisation {pol.Average():F2}, pack share {pack.Average():F2} in the game's tick");
        Check(outside == 0, "nobody leaves the pond");
        Check(maxStep <= 25.8f, $"no teleport: the largest per-tick move is {maxStep:F1} u (the burst's {SubstrateResearch.GameHarrier().Gregarious.Speed * Dt:F0} u; the substrate's bound 25.8 u)");
    }

    static void ArmsLifecycle()
    {
        Console.WriteLine("\nA4. arms lifecycle: 10 min on regrowing flora (a vessel crosses in the second half) - both feed and breed, neither collapses, the ledger closes");
        foreach (int seed in new[] { 51, 52 })
        {
            var o = MakePond(seed);
            var w = o.W;
            double f0 = w.LiveMass(), s0 = w.Core.MassIn, drift = 0;
            int minShoal = int.MaxValue, minHarrier = int.MaxValue;
            Pilot pl = null;
            for (int t = 0; t < 6000; t++)
            {
                if (t == 3000) { pl = new Pilot(seed, o.C0, Vector3.UnitX) { Id = 9, Mode = "replay" }; w.Pilots.Add(pl); }
                if (pl != null) { var (vp, vv) = Crossing(t * Dt); pl.Pos = o.C0 + vp; pl.Vel = vv; }
                w.Step();
                Regrow(o, 60f);
                if (t > 600) { minShoal = Math.Min(minShoal, o.Shoal.Alive); minHarrier = Math.Min(minHarrier, o.Harrier.Alive); }
                drift = Math.Max(drift, Math.Abs(w.LiveMass() + w.Core.MassHeld() - (f0 + s0 + o.Regrown)) / (f0 + s0 + o.Regrown));
            }
            var c = w.Core;
            double core = Math.Abs(c.MassIn - c.MassOut - c.MassHeld()) / Math.Max(1, c.MassIn);
            var S = o.Shoal; var H = o.Harrier;
            Console.WriteLine($"    seed {seed}: shoal {S.Alive} alive (min {minShoal}), {S.Births} births, {S.Starvations} starved; harriers {H.Alive} (min {minHarrier}), {H.Births} births, " +
                              $"{H.Starvations} starved; {H.Arms.Catches} catches; flora eaten {w.Eaten:F0} u^3, regrown {o.Regrown:F0} u^3");
            Check(w.Eaten > 0 && S.Births > 0, $"seed {seed}: the shoal completes its life cycle: it grazes the pond's flora and breeds ({S.Births} births)");
            Check(H.Arms.Catches > 0 && H.Births > 0, $"seed {seed}: the harriers complete theirs: they eat the shoal ({H.Arms.Catches} catches) and breed ({H.Births} births)");
            Check(minShoal >= S.P.N0 / 3 && minHarrier >= 4 && H.Alive >= 4,
                  $"seed {seed}: neither collapses (shoal never below {minShoal}, harriers never below {minHarrier}, {H.Alive} at the end)");
            Check(core < 1e-6 && drift < 1e-5, $"seed {seed}: the ledger closes: agent in - out = held (drift {core:E1}), flora + bodies = start + regrowth (max drift {drift:E1})");
        }
    }

    // ───────────────────────────────────────────────────────────── A5

    static void ArmsProxies()
    {
        int sum = ArmsProxyCaps.Sum(c => c.cap);
        Console.WriteLine($"\nA5. the pond's proxies: {sum} ({2 * sum} colliders, from the Swarm cell's spare) - does each cap still engage?");
        foreach (var (name, cap, engage) in ArmsProxyCaps)
        {
            int contacts = 0, covered = 0, harmAll = 0, harmCov = 0;
            foreach (int seed in new[] { 7, 23, 41 })
            {
                var o = MakePond(seed);
                var w = o.W;
                for (int t = 0; t < 300; t++) w.Step();   // the shoal schools, the harriers spread out
                int q = name == "shoal" ? o.Q : o.P;
                var r = (contacts: 0, covered: 0, maxHorizon: 0, harmAll: 0, harmCovered: 0);
                if (name == "shoal")
                {
                    // a vessel ramming straight through the school, six passes from six sides (each aimed at where it is now)
                    var dirs = new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitZ, Vector3.Normalize(new Vector3(1, 0, 1)), Vector3.Normalize(new Vector3(-1, 0, 1)) };
                    foreach (var d in dirs)
                    {
                        w.Pilots.Clear();
                        var c = Centroid(w.Core, o.Q);
                        w.Pilots.Add(new Pilot(seed, c - d * 200f, d) { Mode = "straight", Speed = 120f, Id = 1 });
                        var x = Coverage(w, q, cap, engage, 34);
                        r.contacts += x.contacts; r.covered += x.covered; r.harmAll += x.harmAll; r.harmCovered += x.harmCovered;
                    }
                }
                else
                {
                    // a vessel that lingers in the pond: the harriers' hunt (it reads as their prey)
                    w.Pilots.Add(new Pilot(seed, o.C0 + new Vector3(0f, 40f, 0f), Vector3.UnitX) { Mode = "still", Id = 1 });
                    r = Coverage(w, q, cap, engage, 900);
                }
                contacts += r.contacts; covered += r.covered; harmAll += r.harmAll; harmCov += r.harmCovered;
            }
            float frac = contacts > 0 ? covered / (float)contacts : 1f, hfrac = harmAll > 0 ? harmCov / (float)harmAll : 1f;
            bool ram = SubstrateResearch.ByName(name, game: true).ContactWeight <= 0f;
            Console.WriteLine($"    {name,-9} cap {cap,2} within {engage,3:F0} u: {covered}/{contacts} {(ram ? "rammable passes" : "contacts")} on a proxied agent ({frac:P1}); harm events {harmCov}/{harmAll}");
            Check(contacts > 0 && (ram ? frac >= 0.9f : frac >= 0.95f || hfrac >= 0.9f),
                  $"{name}: a cap of {cap} within {engage} u still engages - {frac:P1} of {contacts} {(ram ? "rammable passes" : "contacts")}" + (ram ? "" : $", {hfrac:P1} of {harmAll} harm events"));
        }
    }

    static Vector3 Centroid(SubstrateCore c, int q)
    {
        var s = Vector3.Zero; int n = 0;
        foreach (int i in LiveOf(c, q)) { s += c.Pos[i]; n++; }
        return n > 0 ? s / n : Vector3.Zero;
    }
}
