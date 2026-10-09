// The evolution harness's gates (Docs/EVOLUTION.md §6), run against the SHIPPED core compiled from Assets/_Scripts.
// Every gate carries a planted-bug negative control that must FAIL and must have been exercised (the ecology LOD
// harness's rule: a control that cannot bite is a fake pass).
//
//   groups: core      - the genome core: expression identities, mutation bounds and determinism, the wire format,
//                       the ledger, the stomach provisioning
//           arena     - the evidence model: determinism, the selection-off control's identity, caps, planted bugs
//           evidence  - the runs the doc and the lab quote: regimes x seeds, with the mutation-off and selection-off
//                       controls; writes Tools/Evolution/results/results.json (+ shipped.json)
//           golden    - the parity golden for the JavaScript port: Tools/Evolution/results/golden.json
//
//   EVO_SEEDS (default 5), EVO_HOURS (default 4) size the evidence runs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CosmicShore.Gameplay;

namespace EvolutionHarness
{
    public static class Program
    {
        static int s_fail;
        static int s_negatives, s_negativesBit;

        static void Check(bool ok, string what)
        {
            Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
            if (!ok) s_fail++;
        }

        /// <summary>A planted defect: the gate must FAIL on it. Counted, so an unexercised control is itself a failure.</summary>
        static void Negative(bool gatePassedOnBug, string what)
        {
            s_negatives++;
            if (!gatePassedOnBug) s_negativesBit++;
            Console.WriteLine($"  [{(gatePassedOnBug ? "FAIL" : "ok")}] negative control bites: {what}");
            if (gatePassedOnBug) s_fail++;
        }

        static string s_root = ".";

        public static int Main(string[] args)
        {
            var groups = new HashSet<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--root" && i + 1 < args.Length) { s_root = args[++i]; continue; }
                foreach (var g in args[i].Split(',', StringSplitOptions.RemoveEmptyEntries)) groups.Add(g);
            }
            if (groups.Count == 0) groups = new HashSet<string> { "core", "arena", "evidence", "golden" };

            var t0 = DateTime.Now;
            if (groups.Contains("core")) Core();
            if (groups.Contains("arena")) Arena();
            if (groups.Contains("evidence")) Evidence();
            if (groups.Contains("golden")) Golden();

            Console.WriteLine($"evolution harness: {(s_fail == 0 ? "OK" : s_fail + " FAILURES")} " +
                              $"({s_negativesBit}/{s_negatives} negative controls bit; {(DateTime.Now - t0).TotalSeconds:F0} s)");
            return s_fail == 0 ? 0 : 1;
        }

        // ============================================================================================ CORE

        sealed class FixedRng : IGenomeRng
        {
            readonly double _v;
            public FixedRng(double v) => _v = v;
            public double NextUnit() => _v;
        }

        static void Core()
        {
            Console.WriteLine("core: the genome core");
            var s = EvolutionSettings.Default(true);

            // --- expression identities ---
            bool identities = true;
            foreach (float R in new[] { 1f, 1.2f, 1.5f, 1.84f, 2f, 3f, 10f })
            {
                identities &= GenomeExpression.Multiplier(0f, R) == 1f;
                identities &= GenomeExpression.Multiplier(1f, R) == R;
                identities &= Math.Abs(GenomeExpression.Multiplier(-1f, R) - 1f / R) <= 1e-7f;
            }
            Check(identities, "Multiplier: f(0) = 1, f(+1) = R, f(-1) = 1/R for every range");

            bool reciprocal = true, monotonic = true, smooth = true;
            foreach (float R in new[] { 1.5f, 2f, 3f })
            {
                float prev = float.NegativeInfinity;
                for (int k = -100; k <= 100; k++)
                {
                    float g = k / 100f;
                    float f = GenomeExpression.Multiplier(g, R), fm = GenomeExpression.Multiplier(-g, R);
                    reciprocal &= Math.Abs(f * fm - 1f) <= 2e-7f;
                    monotonic &= f >= prev;
                    prev = f;
                }
                // C1 at 0: the one-sided slopes agree to first order.
                float h = 1e-3f;
                float right = (GenomeExpression.Multiplier(h, R) - 1f) / h, left = (1f - GenomeExpression.Multiplier(-h, R)) / h;
                smooth &= Math.Abs(right - left) < 0.01f * (R - 1f) + 1e-3f;
            }
            Check(reciprocal, "Multiplier: f(-g) * f(g) = 1 across the band (a lean one way divides by what the other multiplies)");
            Check(monotonic, "Multiplier: monotonic across the band for every range");
            Check(smooth, "Multiplier: C1 at gene 0 (no kink an r/K optimum could snag on)");
            // negative: a map that is linear on both sides is NOT reciprocal-symmetric
            {
                bool brokenReciprocal = true;
                for (int k = -100; k <= 100; k++)
                {
                    float g = k / 100f;
                    float f = 1f + 0.5f * g, fm = 1f - 0.5f * g;
                    brokenReciprocal &= Math.Abs(f * fm - 1f) <= 2e-7f;
                }
                Negative(brokenReciprocal, "a linear-both-sides map fails the reciprocal test");
            }
            Check(GenomeExpression.Multiplier(0.5f, 0.5f) == 1f && GenomeExpression.Multiplier(0.5f, float.NaN) == 1f,
                  "Multiplier: a range below 1 or NaN is inert (never inverts a trait)");
            Check(GenomeExpression.Multiplier(float.NaN, 2f) == 1f, "Multiplier: a NaN gene reads as the founder");

            // --- expression of a whole genome ---
            var neutral = GenomeExpression.Express(LifeformGenome.Founder, s);
            Check(neutral.Pace == 1f && neutral.Reach == 1f && neutral.Upkeep == 1f && neutral.Fecundity == 1f
                  && neutral.Provision == 1f && neutral.Cohesion == 1f, "Express: the founder is the authored species exactly");
            var off = GenomeExpression.Express(new LifeformGenome(1f, 1f, 1f, 1f), EvolutionSettings.Default(false));
            Check(off.Pace == 1f && off.Upkeep == 1f && off.Fecundity == 1f && off.Provision == 1f,
                  "Express: with the switch OFF every genome expresses Neutral");
            var fast = GenomeExpression.Express(new LifeformGenome(1f, 0f, 0f, 0f), s);
            Check(fast.Pace == s.TempoPaceRange && fast.Upkeep == s.TempoUpkeepRange && fast.Reach == 1f,
                  "Express: Tempo +1 = pace x1.5 and upkeep x1.84 (the cost rides the same gene)");
            var far = GenomeExpression.Express(new LifeformGenome(0f, 1f, 0f, 0f), s);
            Check(far.Reach == s.ReachRadiusRange && far.Upkeep == s.ReachUpkeepRange, "Express: Reach +1 = radius x1.5 and upkeep x1.36");
            var both = GenomeExpression.Express(new LifeformGenome(1f, 1f, 0f, 0f), s);
            Check(Math.Abs(both.Upkeep - s.TempoUpkeepRange * s.ReachUpkeepRange) < 1e-6f, "Express: fast AND far pays both costs (product)");
            var r = GenomeExpression.Express(new LifeformGenome(0f, 0f, 1f, 0f), s);
            Check(r.Fecundity == s.FecundityRange && Math.Abs(r.Provision - 1f / s.FecundityProvisionRange) < 1e-6f,
                  "Express: Fecundity +1 = twice the births per feed, children born half full");
            var kk = GenomeExpression.Express(new LifeformGenome(0f, 0f, -1f, 0f), s);
            Check(Math.Abs(kk.Fecundity - 1f / s.FecundityRange) < 1e-6f && kk.Provision == 1f,
                  "Express: Fecundity -1 = half the births, children still born full (the locus only costs below 0)");
            Check(GenomeExpression.FeedsPerOffspring(20, r) == 10 && GenomeExpression.FeedsPerOffspring(20, kk) == 40
                  && GenomeExpression.FeedsPerOffspring(0, r) == 0 && GenomeExpression.FeedsPerOffspring(1, r) == 1,
                  "FeedsPerOffspring: 20 -> 10 at +1, 40 at -1; 0 stays 0 (no reproduction); never below 1");
            Check(GenomeExpression.StarvationSeconds(90f, fast) < 90f && GenomeExpression.StarvationSeconds(0f, fast) == 0f
                  && Math.Abs(GenomeExpression.StarvationSeconds(90f, fast) * fast.Upkeep - 90f) < 1e-3f,
                  "StarvationSeconds: 90 s / upkeep; 0 (never starves) stays 0");

            // --- mutation ---
            var rng = new GenomeRng(7);
            var parent = new LifeformGenome(0.2f, -0.3f, 0.1f, 0f);
            var child = GenomeMutation.Mutate(parent, s, rng);
            Check(child != parent, "Mutate: a child differs from its parent (rate 1, sigma 0.08)");
            Check(LifeformGenome.Distance(parent, child) < 0.08f * 6f * 2f, "Mutate: a step is small (within six sigmas per locus)");
            var countingA = new CountingRng(11);
            GenomeMutation.Mutate(parent, s, countingA);
            Check(countingA.Draws == 12 * LifeformGenome.LocusCount, $"Mutate: rate 1 draws exactly 12 x {LifeformGenome.LocusCount} uniforms (Irwin-Hall, no rate draw)");
            var countingB = new CountingRng(11);
            var same = GenomeMutation.Mutate(parent, EvolutionSettings.Default(false), countingB);
            Check(same == parent && countingB.Draws == 0, "Mutate: with the switch OFF the parent is returned and NO draw is made");
            var countingC = new CountingRng(11);
            var sigma0 = s.WithoutMutation();
            Check(GenomeMutation.Mutate(parent, sigma0, countingC) == parent && countingC.Draws == 0, "Mutate: sigma 0 is the parent, no draw");
            {
                var wild = s.Clone(); wild.MutationSigma = 10f;
                var g = parent; bool inBand = true;
                var r2 = new GenomeRng(3);
                for (int i = 0; i < 2000; i++)
                {
                    g = GenomeMutation.Mutate(g, wild, r2);
                    for (int l = 0; l < LifeformGenome.LocusCount; l++) inBand &= g[l] >= -1f && g[l] <= 1f;
                }
                Check(inBand, "Mutate: 2000 generations at sigma 10 never leave the band");
            }
            {
                var r1 = new GenomeRng(99); var r2 = new GenomeRng(99); var r3 = new GenomeRng(100);
                var a = GenomeMutation.Mutate(parent, s, r1); var b = GenomeMutation.Mutate(parent, s, r2); var c = GenomeMutation.Mutate(parent, s, r3);
                Check(a == b && a != c, "Mutate: deterministic per seed, different across seeds");
            }
            {
                var r4 = new GenomeRng(5);
                double sum = 0, sq = 0; int n = 200000;
                for (int i = 0; i < n; i++) { double v = GenomeMutation.Normal(r4); sum += v; sq += v * v; }
                double mean = sum / n, sd = Math.Sqrt(sq / n - mean * mean);
                Check(Math.Abs(mean) < 0.01 && Math.Abs(sd - 1) < 0.01, $"Normal: mean {mean:+0.000;-0.000}, sd {sd:0.000} over 200k (Irwin-Hall)");
                double biased = 0; var fixedRng = new FixedRng(0.9);
                for (int i = 0; i < 1000; i++) biased += GenomeMutation.Normal(fixedRng);
                Negative(Math.Abs(biased / 1000) < 0.01, "a biased uniform source shifts the normal's mean");
            }
            {
                var spread = s.Clone(); spread.FounderSpread = 0.1f;
                var r5 = new GenomeRng(1);
                double sum = 0; int n = 20000; bool inBand = true;
                for (int i = 0; i < n; i++) { var f = GenomeMutation.Founder(spread, r5); sum += f.Tempo; inBand &= Math.Abs(f.Tempo) <= 1f; }
                Check(Math.Abs(sum / n) < 0.005 && inBand, "Founder: centred on the authored species, in band");
                var none = s.Clone(); none.FounderSpread = 0f;
                var cnt = new CountingRng(1);
                Check(GenomeMutation.Founder(none, cnt).IsFounder && cnt.Draws == 0, "Founder: spread 0 is the authored species with no draw");
            }

            // --- wire ---
            {
                bool roundTrip = true; float worst = 0;
                var r6 = new GenomeRng(42);
                for (int i = 0; i < 5000; i++)
                {
                    var g = new LifeformGenome((float)(r6.NextUnit() * 2 - 1), (float)(r6.NextUnit() * 2 - 1), (float)(r6.NextUnit() * 2 - 1), (float)(r6.NextUnit() * 2 - 1));
                    var q = LifeformGenome.Unpack(g.Pack());
                    for (int l = 0; l < LifeformGenome.LocusCount; l++) worst = Math.Max(worst, Math.Abs(q[l] - g[l]));
                    roundTrip &= q.Quantized() == q;
                }
                Check(worst <= 0.5f / 127f + 1e-6f && roundTrip, $"Pack/Unpack: worst error {worst:0.0000} (<= 1/254), quantization idempotent");
                Check(LifeformGenome.Founder.Pack() == 0u && LifeformGenome.Unpack(0u).IsFounder, "Pack: the founder is 0 on the wire");
                var ends = new LifeformGenome(1f, -1f, 1f, -1f);
                Check(LifeformGenome.Unpack(ends.Pack()) == ends, "Pack: the band ends survive exactly");
                Negative(LifeformGenome.Unpack(new LifeformGenome(0.5f, 0, 0, 0).Pack() ^ 0x7F) == new LifeformGenome(0.5f, 0, 0, 0), "a corrupted byte is not the same genome");
            }

            // --- ledger ---
            {
                var ledger = new EvolutionLedger();
                uint a = ledger.RecordFounder("tadpole", new LifeformGenome(0.1f, 0, 0, 0), 0);
                uint b = ledger.RecordFounder("tadpole", new LifeformGenome(-0.1f, 0, 0, 0), 0);
                uint c = ledger.RecordBirth("tadpole", a, new LifeformGenome(0.2f, 0, 0, 0), 10);
                uint d = ledger.RecordBirth("tadpole", c, new LifeformGenome(0.3f, 0, 0, 0), 20);
                ledger.RecordFounder("shark", LifeformGenome.Founder, 0);
                Check(ledger.LivingCount == 5 && ledger.LivingOf("tadpole") == 4, "Ledger: founders and births join the living");
                Check(ledger.TryGetLiving(d, out var dd) && dd.Generation == 2 && dd.LineageId == a && dd.ParentId == c,
                      "Ledger: a grandchild is generation 2 of its founder's lineage");
                Check(ledger.RecordDeath(b, LifeformDeathCause.Starvation, 30) && !ledger.RecordDeath(b, LifeformDeathCause.Starvation, 31)
                      && ledger.LivingCount == 4, "Ledger: a death leaves the living once");
                var row = ledger.SnapshotSpecies("tadpole", 40);
                double mean = (0.1 + 0.2 + 0.3) / 3.0;
                Check(row.Population == 3 && Math.Abs(row.Mean[0] - mean) < 1e-6 && row.Lineages == 1 && row.MaxGeneration == 2
                      && row.Births == 2 && row.DeathsByCause[(int)LifeformDeathCause.Starvation] == 1,
                      "Ledger: a census row carries n, mean, lineages, generations, births and deaths by cause");
                var row2 = ledger.SnapshotSpecies("tadpole", 50);
                Check(row2.Births == 0 && row2.DeathsByCause[0] == 0, "Ledger: since-last-row counters reset");
                uint orphan = ledger.RecordBirth("tadpole", 99999, LifeformGenome.Founder, 60);
                Check(ledger.TryGetLiving(orphan, out var o) && o.Generation == 0 && o.LineageId == orphan, "Ledger: an unknown parent makes the child a founder (never lost)");
                var json = ledger.ToJson("test");
                bool parses = true;
                try { using var doc = JsonDocument.Parse(json); parses = doc.RootElement.GetProperty("species").GetArrayLength() == 2; }
                catch { parses = false; }
                Check(parses, "Ledger: ToJson parses and lists both species");
                ledger.MaxSnapshotsPerSpecies = 16;
                for (int i = 0; i < 40; i++) ledger.SnapshotSpecies("tadpole", 100 + i);
                Check(ledger.SnapshotsOf("tadpole").Count == 16, "Ledger: rows are capped per species");
                ledger.Clear();
                Check(ledger.LivingCount == 0 && ledger.Species.Count == 0, "Ledger: Clear forgets everything");
                Negative(Math.Abs(row.Mean[0] - 0.1) < 1e-6, "a census mean that ignored the births would read the founder");
            }

            // --- the stomach's provisioning ---
            {
                var st = FaunaStomach.FromStarvationClock(90f, 16f);
                st.FillFraction(0f, 0.5f);
                Check(Math.Abs(st.Level(0f) - 8f) < 1e-5f && Math.Abs(st.SecondsLeft(0f) - 45f) < 1e-3f, "FaunaStomach.FillFraction: half full starves in half the clock");
                st.FillFraction(0f, 1.5f);
                Check(st.Level(0f) == 16f, "FaunaStomach.FillFraction: clamped to full");
                st.FillFraction(0f, float.NaN);
                Check(st.Level(0f) == 16f, "FaunaStomach.FillFraction: NaN reads as full");
            }

            Check(Enum.GetValues(typeof(GenomeLocus)).Length == LifeformGenome.LocusCount, "GenomeLocus count matches LifeformGenome.LocusCount");
            Check(Enum.GetValues(typeof(LifeformDeathCause)).Length == EvolutionLedger.CauseCount, "LifeformDeathCause count matches EvolutionLedger.CauseCount");
        }

        // ============================================================================================ ARENA

        static ArenaParams Blob()
        {
            // The Blob cell's authored numbers (ArenaParams defaults) at the population scale the evidence runs at:
            // a cap of 6 is too small a population for selection to beat drift, so the runs use the shipped
            // SpawnProfileSO.FaunaPopulationScale lever at x20 (cap 120, floor 80) and x5 on the predator (cap 10, floor 5).
            var p = new ArenaParams { PopulationScale = 20, PredatorScale = 5 };
            return p;
        }

        static List<ArenaRow> Run(ArenaParams p, uint seed, int steps, int every)
        {
            var arena = new EvolutionArena(p, seed);
            var rows = new List<ArenaRow>();
            for (int s = 0; s < steps; s += every)
            {
                arena.Advance(every);
                rows.Add(arena.Census());
            }
            return rows;
        }

        static bool SameRows(List<ArenaRow> a, List<ArenaRow> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                var x = a[i]; var y = b[i];
                if (x.Herbivores != y.Herbivores || x.Predators != y.Predators || x.Food != y.Food || x.Births != y.Births
                    || x.Starved != y.Starved || x.Eaten != y.Eaten || x.Draws != y.Draws) return false;
                for (int l = 0; l < LifeformGenome.LocusCount; l++) if (x.Mean[l] != y.Mean[l] || x.Sd[l] != y.Sd[l]) return false;
            }
            return true;
        }

        static void Arena()
        {
            Console.WriteLine("arena: the evidence model");
            var p = Blob();
            const int steps = 2400, every = 120;

            var a = Run(p, 1, steps, every);
            var b = Run(p, 1, steps, every);
            var c = Run(p, 2, steps, every);
            Check(SameRows(a, b), "Arena: the same seed replays the same trajectory, draw for draw");
            Check(!SameRows(a, c), "Arena: a different seed is a different trajectory");
            Check(a[a.Count - 1].Herbivores > 0 && a[a.Count - 1].Births > 0 || a.Any(r => r.Births > 0), "Arena: the population lives and breeds over 20 minutes");

            // The selection-off control is EXACTLY the run with every range at 1 (same draws, same births): inert phenotype == inert ranges.
            var inert = p.Clone(); inert.InertPhenotype = true;
            var unit = p.Clone();
            unit.Evolution.TempoPaceRange = unit.Evolution.TempoUpkeepRange = unit.Evolution.ReachRadiusRange = unit.Evolution.ReachUpkeepRange = 1f;
            unit.Evolution.FecundityRange = unit.Evolution.FecundityProvisionRange = unit.Evolution.CohesionRange = 1f;
            Check(SameRows(Run(inert, 3, steps, every), Run(unit, 3, steps, every)),
                  "Arena: the selection-OFF control equals a run whose every range is 1 (genomes still mutate; nothing selects them)");
            Check(!SameRows(Run(inert, 3, steps, every), Run(p, 3, steps, every)),
                  "Arena: ... and differs from the selected run (the phenotype is doing something)");

            var noMut = p.Clone(); noMut.Evolution = noMut.Evolution.WithoutMutation(); noMut.Evolution.FounderSpread = 0f;
            var still = Run(noMut, 4, steps, every);
            Check(still.All(r => r.Mean.All(m => m == 0) && r.Sd.All(sd => sd == 0)), "Arena: mutation OFF with no founder spread never leaves the authored species");

            int herbCap = (int)Math.Round(p.HerbCap * p.PopulationScale), predCap = (int)Math.Round(p.PredCap * p.PredatorScale);
            Check(a.All(r => r.Herbivores <= herbCap && r.Predators <= predCap), $"Arena: the caps hold (herbivores <= {herbCap}, predators <= {predCap})");
            var bug = p.Clone(); bug.Bug = ArenaBug.IgnoreHerbivoreCap;
            var bugRows = Run(bug, 1, steps, every);
            Negative(bugRows.All(r => r.Herbivores <= herbCap), "a cap the reproduction path ignores is caught by the cap gate");

            var free = p.Clone(); free.Bug = ArenaBug.FreeUpkeep;
            var freeRows = Run(free, 1, steps, every);
            Negative(freeRows.Sum(r => r.Starved) > 0, "a stomach that never drains is caught by the starvation count");
            Check(a.Sum(r => r.Starved) > 0 || a.Sum(r => r.Eaten) > 0, "Arena: something dies - the economy selects");
        }

        // ============================================================================================ EVIDENCE

        sealed class Regime
        {
            public string Name, Note;
            public ArenaParams P;
        }

        static List<Regime> Regimes()
        {
            var blob = Blob();
            var list = new List<Regime>
            {
                new Regime { Name = "blob", Note = "the Blob cell as authored (x20 herbivores, x5 predators): the cap binds, predation makes the turnover", P = blob },
            };
            // ECONOMY-LIMITED regimes: the cap is lifted (x50) so what bounds the population is food, not MaxLivePopulation.
            var famine = blob.Clone(); famine.PopulationScale = 50;
            list.Add(new Regime { Name = "famine", Note = "x50 herbivores on the shipped food: starvation, not the cap, bounds the population", P = famine });
            var sparse = famine.Clone(); sparse.FoodCap = 250; sparse.FloraGrowth = 8;
            list.Add(new Regime { Name = "sparse", Note = "x50 herbivores, a third of the food regrowing at 8 prisms/s", P = sparse });
            var rich = blob.Clone(); rich.FoodCap = 1500; rich.FloraGrowth = 40;
            list.Add(new Regime { Name = "rich", Note = "twice the food, regrowing at 40 prisms/s (x20, cap-bound)", P = rich });
            var nopred = blob.Clone(); nopred.PredatorScale = 0;
            list.Add(new Regime { Name = "nopred", Note = "no predators: the only turnover is famine", P = nopred });
            var mutOff = blob.Clone(); mutOff.Evolution = mutOff.Evolution.WithoutMutation();
            list.Add(new Regime { Name = "control-mutation-off", Note = "CONTROL: heredity without variation (founder spread kept, no mutation)", P = mutOff });
            var selOff = blob.Clone(); selOff.InertPhenotype = true;
            list.Add(new Regime { Name = "control-selection-off", Note = "CONTROL: the abiotic baseline - genomes inherit and mutate but express nothing", P = selOff });
            var selOffFamine = famine.Clone(); selOffFamine.InertPhenotype = true;
            list.Add(new Regime { Name = "control-selection-off-famine", Note = "CONTROL for famine: the same economy-limited cell with an inert genome", P = selOffFamine });
            var noSeed = famine.Clone(); noSeed.Seeder = false;
            list.Add(new Regime { Name = "closed", Note = "famine with no seeder: a closed population, no founder immigration", P = noSeed });
            return list;
        }

        static void Evidence()
        {
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("EVO_SEEDS"), out var sv) ? Math.Max(1, sv) : 5;
            double hours = double.TryParse(Environment.GetEnvironmentVariable("EVO_HOURS"), NumberStyles.Float, CultureInfo.InvariantCulture, out var hv) ? hv : 4;
            Console.WriteLine($"evidence: {Regimes().Count} regimes x {seeds} seeds x {hours} h of cell time");

            var outDir = Path.Combine(s_root, "Tools", "Evolution", "results");
            Directory.CreateDirectory(outDir);
            using var fs = File.Create(Path.Combine(outDir, "results.json"));
            using var w = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = false });
            w.WriteStartObject();
            w.WriteString("generated", DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            w.WriteNumber("hours", hours);
            w.WriteNumber("seeds", seeds);
            w.WriteStartArray("loci");
            for (int l = 0; l < LifeformGenome.LocusCount; l++) w.WriteStringValue(LifeformGenome.LocusLabel((GenomeLocus)l));
            w.WriteEndArray();
            w.WriteStartArray("regimes");

            int steps = (int)Math.Round(hours * 3600 / Blob().Dt);
            int every = (int)Math.Round(60 / Blob().Dt);   // one row per minute of cell time
            var summaryLines = new List<string>();
            foreach (var reg in Regimes())
            {
                w.WriteStartObject();
                w.WriteString("name", reg.Name);
                w.WriteString("note", reg.Note);
                WriteParams(w, reg.P);
                w.WriteStartArray("runs");
                var finals = new List<double[]>();
                var shifts = new List<double[]>();
                var pops = new List<double>();
                foreach (uint seed in Enumerable.Range(1, seeds))
                {
                    var rows = Run(reg.P, seed, steps, every);
                    w.WriteStartObject();
                    w.WriteNumber("seed", seed);
                    WriteRows(w, rows);
                    var arena = new EvolutionArena(reg.P, seed); // final genomes for the histogram, replayed (cheap)
                    arena.Advance(steps);
                    var genomes = new List<LifeformGenome>(); arena.CopyGenomes(genomes);
                    w.WriteStartArray("final");
                    foreach (var g in genomes) { w.WriteStartArray(); for (int l = 0; l < LifeformGenome.LocusCount; l++) w.WriteNumberValue(g[l]); w.WriteEndArray(); }
                    w.WriteEndArray();
                    // scorecard: time-averaged mean over the last quarter against the first row's mean
                    int from = rows.Count * 3 / 4;
                    var fin = new double[LifeformGenome.LocusCount];
                    var sh = new double[LifeformGenome.LocusCount];
                    int cnt = 0;
                    for (int i = from; i < rows.Count; i++) { if (rows[i].Herbivores == 0) continue; cnt++; for (int l = 0; l < fin.Length; l++) fin[l] += rows[i].Mean[l]; }
                    for (int l = 0; l < fin.Length; l++) { fin[l] = cnt > 0 ? fin[l] / cnt : double.NaN; sh[l] = fin[l] - rows[0].Mean[l]; }
                    finals.Add(fin); shifts.Add(sh);
                    pops.Add(rows.Skip(from).Average(r => r.Herbivores));
                    w.WriteStartArray("finalMean"); foreach (var v in fin) w.WriteNumberValue(v); w.WriteEndArray();
                    w.WriteStartArray("shift"); foreach (var v in sh) w.WriteNumberValue(v); w.WriteEndArray();
                    w.WriteNumber("meanPopulation", pops[pops.Count - 1]);
                    w.WriteNumber("extinctRows", rows.Count(r => r.Herbivores == 0));
                    w.WriteNumber("maxGeneration", rows[rows.Count - 1].MaxGeneration);
                    w.WriteNumber("births", rows.Sum(r => r.Births));
                    w.WriteNumber("starved", rows.Sum(r => r.Starved));
                    w.WriteNumber("eaten", rows.Sum(r => r.Eaten));
                    w.WriteNumber("founders", rows.Sum(r => r.Founders));
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                // the regime's scorecard across seeds
                w.WriteStartObject("scorecard");
                w.WriteStartArray("shiftMean"); for (int l = 0; l < LifeformGenome.LocusCount; l++) w.WriteNumberValue(shifts.Average(s => s[l])); w.WriteEndArray();
                w.WriteStartArray("shiftSd"); for (int l = 0; l < LifeformGenome.LocusCount; l++) w.WriteNumberValue(Sd(shifts.Select(s => s[l]))); w.WriteEndArray();
                w.WriteNumber("meanPopulation", pops.Average());
                w.WriteEndObject();
                w.WriteEndObject();

                var line = $"  {reg.Name,-24} pop {pops.Average(),6:0.0}  shift:";
                for (int l = 0; l < LifeformGenome.LocusCount; l++)
                    line += $" {LifeformGenome.LocusLabel((GenomeLocus)l)} {shifts.Average(s => s[l]):+0.000;-0.000}±{Sd(shifts.Select(s => s[l])):0.000}";
                Console.WriteLine(line);
                summaryLines.Add(line);
            }
            w.WriteEndArray();
            w.WriteEndObject();
            w.Flush();

            // shipped.json: the provenance of every number the arena runs on, for the lab's SHIPPED block
            File.WriteAllText(Path.Combine(outDir, "shipped.json"), ShippedJson());
            Console.WriteLine($"  wrote {Path.Combine(outDir, "results.json")} and shipped.json");
        }

        static double Sd(IEnumerable<double> xs)
        {
            var a = xs.ToArray();
            if (a.Length < 2) return 0;
            double m = a.Average();
            return Math.Sqrt(a.Sum(x => (x - m) * (x - m)) / (a.Length - 1));
        }

        static void WriteParams(Utf8JsonWriter w, ArenaParams p)
        {
            w.WriteStartObject("params");
            w.WriteNumber("HerbFeedsPerOffspring", p.HerbFeedsPerOffspring);
            w.WriteNumber("HerbCooldown", p.HerbCooldown);
            w.WriteNumber("HerbCap", p.HerbCap);
            w.WriteNumber("HerbFloor", p.HerbFloor);
            w.WriteNumber("HerbStarvation", p.HerbStarvation);
            w.WriteNumber("PredFeedsPerOffspring", p.PredFeedsPerOffspring);
            w.WriteNumber("PredCooldown", p.PredCooldown);
            w.WriteNumber("PredCap", p.PredCap);
            w.WriteNumber("PredFloor", p.PredFloor);
            w.WriteNumber("PredStarvation", p.PredStarvation);
            w.WriteNumber("HuntInterval", p.HuntInterval);
            w.WriteNumber("PredationImmunity", p.PredationImmunity);
            w.WriteNumber("StomachCapacity", p.StomachCapacity);
            w.WriteNumber("SpawnWave", p.SpawnWave);
            w.WriteBoolean("ConservedStomach", p.ConservedStomach);
            w.WriteNumber("FoodCap", p.FoodCap);
            w.WriteNumber("FloraGrowth", p.FloraGrowth);
            w.WriteNumber("GrazeRate", p.GrazeRate);
            w.WriteNumber("HalfSaturation", p.HalfSaturation);
            w.WriteNumber("PopulationScale", p.PopulationScale);
            w.WriteNumber("PredatorScale", p.PredatorScale);
            w.WriteNumber("Dt", p.Dt);
            w.WriteBoolean("Seeder", p.Seeder);
            w.WriteBoolean("InertPhenotype", p.InertPhenotype);
            w.WriteStartObject("Evolution");
            var e = p.Evolution;
            w.WriteBoolean("Enabled", e.Enabled);
            w.WriteNumber("MutationRate", e.MutationRate);
            w.WriteNumber("MutationSigma", e.MutationSigma);
            w.WriteNumber("FounderSpread", e.FounderSpread);
            w.WriteNumber("TempoPaceRange", e.TempoPaceRange);
            w.WriteNumber("TempoUpkeepRange", e.TempoUpkeepRange);
            w.WriteNumber("ReachRadiusRange", e.ReachRadiusRange);
            w.WriteNumber("ReachUpkeepRange", e.ReachUpkeepRange);
            w.WriteNumber("FecundityRange", e.FecundityRange);
            w.WriteNumber("FecundityProvisionRange", e.FecundityProvisionRange);
            w.WriteNumber("CohesionRange", e.CohesionRange);
            w.WriteEndObject();
            w.WriteEndObject();
        }

        static void WriteRows(Utf8JsonWriter w, List<ArenaRow> rows)
        {
            // compact: one array per row - [t, herb, pred, food, mean x4, sd x4, meanGen, maxGen, births, founders, starved, eaten, predBirths, predStarved, draws]
            w.WriteStartArray("rows");
            foreach (var r in rows)
            {
                w.WriteStartArray();
                w.WriteNumberValue(r.T); w.WriteNumberValue(r.Herbivores); w.WriteNumberValue(r.Predators); w.WriteNumberValue(r.Food);
                for (int l = 0; l < LifeformGenome.LocusCount; l++) w.WriteNumberValue(r.Mean[l]);
                for (int l = 0; l < LifeformGenome.LocusCount; l++) w.WriteNumberValue(r.Sd[l]);
                w.WriteNumberValue(r.MeanGeneration); w.WriteNumberValue(r.MaxGeneration);
                w.WriteNumberValue(r.Births); w.WriteNumberValue(r.Founders); w.WriteNumberValue(r.Starved); w.WriteNumberValue(r.Eaten);
                w.WriteNumberValue(r.PredBirths); w.WriteNumberValue(r.PredStarved); w.WriteNumberValue(r.Draws);
                w.WriteEndArray();
            }
            w.WriteEndArray();
        }

        static string ShippedJson()
        {
            var p = new ArenaParams();
            var e = p.Evolution;
            string J(string key, object v, string source) =>
                $"\"{key}\":{{\"value\":{Convert.ToString(v, CultureInfo.InvariantCulture)!.ToLowerInvariant()},\"source\":\"{source}\"}}";
            var items = new[]
            {
                J("HerbFeedsPerOffspring", p.HerbFeedsPerOffspring, "Blob Tadpole Fauna Config Data.FeedsPerOffspring"),
                J("HerbCooldown", p.HerbCooldown, "Blob Tadpole Fauna Config Data.ReproductionCooldownSeconds"),
                J("HerbCap", p.HerbCap, "Blob Tadpole Fauna Config Data.MaxLivePopulation"),
                J("HerbFloor", p.HerbFloor, "Blob Tadpole Fauna Config Data.PopulationSize"),
                J("HerbStarvation", p.HerbStarvation, "TadPoleFauna.prefab starvationSeconds / Variant.StarvationSeconds"),
                J("PredFeedsPerOffspring", p.PredFeedsPerOffspring, "Blob Shark Fauna Config Data.FeedsPerOffspring"),
                J("PredCooldown", p.PredCooldown, "Blob Shark Fauna Config Data.ReproductionCooldownSeconds"),
                J("PredCap", p.PredCap, "Blob Shark Fauna Config Data.MaxLivePopulation"),
                J("PredFloor", p.PredFloor, "Blob Shark Fauna Config Data.PopulationSize"),
                J("PredStarvation", p.PredStarvation, "MassSharkFauna.prefab starvationSeconds"),
                J("HuntInterval", p.HuntInterval, "LightFaunaDataSO.huntIntervalSeconds (script default)"),
                J("PredationImmunity", p.PredationImmunity, "Fauna.predationImmunitySeconds"),
                J("StomachCapacity", p.StomachCapacity, "CellPhaseThresholds.NominalPrismVolume"),
                J("SpawnWave", p.SpawnWave, "Blob Cell Spawn Profile.BaseFaunaSpawnTime"),
                J("ConservedStomach", p.ConservedStomach, "CellConfigDataSO.ConservedFaunaStomach (Blob: off)"),
                J("FoodCap", p.FoodCap, "Blob Cell Spawn Profile.FloraSpawnVolumeCeiling 12000 / 16"),
                J("FloraGrowth", p.FloraGrowth, "MODEL ASSUMPTION (Tools/ecosim FLORA_GROWTH_PER_S)"),
                J("GrazeRate", p.GrazeRate, "MODEL ASSUMPTION (Tools/ecosim GRAZE_PER_HERBIVORE_S)"),
                J("HalfSaturation", p.HalfSaturation, "MODEL ASSUMPTION"),
                J("Dt", p.Dt, "MODEL ASSUMPTION (a third of the tadpole's 1.5 s behaviour tick)"),
                J("MutationRate", e.MutationRate, "EvolutionSettings.MutationRate"),
                J("MutationSigma", e.MutationSigma, "EvolutionSettings.MutationSigma"),
                J("FounderSpread", e.FounderSpread, "EvolutionSettings.FounderSpread"),
                J("TempoPaceRange", e.TempoPaceRange, "EvolutionSettings.TempoPaceRange"),
                J("TempoUpkeepRange", e.TempoUpkeepRange, "EvolutionSettings.TempoUpkeepRange"),
                J("ReachRadiusRange", e.ReachRadiusRange, "EvolutionSettings.ReachRadiusRange"),
                J("ReachUpkeepRange", e.ReachUpkeepRange, "EvolutionSettings.ReachUpkeepRange"),
                J("FecundityRange", e.FecundityRange, "EvolutionSettings.FecundityRange"),
                J("FecundityProvisionRange", e.FecundityProvisionRange, "EvolutionSettings.FecundityProvisionRange"),
                J("CohesionRange", e.CohesionRange, "EvolutionSettings.CohesionRange"),
            };
            return "{" + string.Join(",", items) + "}";
        }

        // ============================================================================================ GOLDEN

        static void Golden()
        {
            Console.WriteLine("golden: the parity trajectory for Tools/Evolution/sim.js");
            var outDir = Path.Combine(s_root, "Tools", "Evolution", "results");
            Directory.CreateDirectory(outDir);
            var cases = new List<(string name, ArenaParams p, uint seed, int steps, int every)>
            {
                ("blob-seed1", Blob(), 1, 2400, 120),
                ("sparse-seed2", Regimes().First(r => r.Name == "sparse").P, 2, 2400, 120),
                ("selection-off-seed3", Regimes().First(r => r.Name == "control-selection-off").P, 3, 1200, 120),
                ("conserved-seed4", Conserved(), 4, 1200, 120),
            };
            using var fs = File.Create(Path.Combine(outDir, "golden.json"));
            using var w = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = false });
            w.WriteStartObject();
            w.WriteString("note", "exact (===) trajectory per case; every number is a double the JS port must reproduce bit for bit");
            w.WriteStartArray("cases");
            foreach (var c in cases)
            {
                w.WriteStartObject();
                w.WriteString("name", c.name);
                w.WriteNumber("seed", c.seed);
                w.WriteNumber("steps", c.steps);
                w.WriteNumber("every", c.every);
                WriteParams(w, c.p);
                WriteRows(w, Run(c.p, c.seed, c.steps, c.every));
                // a few mutation samples straight from the core, for the gate's unit half
                var rng = new GenomeRng(c.seed);
                w.WriteStartArray("mutations");
                var g = new LifeformGenome(0.25f, -0.5f, 0.75f, 0f);
                for (int i = 0; i < 8; i++)
                {
                    g = GenomeMutation.Mutate(g, c.p.Evolution, rng);
                    w.WriteStartArray(); for (int l = 0; l < LifeformGenome.LocusCount; l++) w.WriteNumberValue(g[l]); w.WriteEndArray();
                }
                w.WriteEndArray();
                var ph = GenomeExpression.Express(g, c.p.Evolution);
                w.WriteStartArray("phenotype");
                w.WriteNumberValue(ph.Pace); w.WriteNumberValue(ph.Reach); w.WriteNumberValue(ph.Upkeep);
                w.WriteNumberValue(ph.Fecundity); w.WriteNumberValue(ph.Provision); w.WriteNumberValue(ph.Cohesion);
                w.WriteEndArray();
                w.WriteNumber("packed", g.Pack());
                w.WriteStartArray("rngFirst8");
                var r2 = new GenomeRng(c.seed);
                for (int i = 0; i < 8; i++) w.WriteNumberValue(r2.NextUnit());
                w.WriteEndArray();
                w.WriteEndObject();
                Console.WriteLine($"  {c.name}: {c.steps} steps");
            }
            w.WriteEndArray();
            w.WriteEndObject();
            w.Flush();
            Console.WriteLine($"  wrote {Path.Combine(outDir, "golden.json")}");
        }

        static ArenaParams Conserved()
        {
            var p = Blob();
            p.ConservedStomach = true;
            return p;
        }
    }
}
