#if UNITY_EDITOR
using System;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pins the heritable genome's contract (Docs/EVOLUTION.md): the expression map's identities, that a biome
    /// with the switch OFF is bit-for-bit the shipped ecology, that mutation is bounded and deterministic, that the
    /// wire format round-trips, and that the ledger records what the economy did without steering it. The evolution
    /// harness (<c>Tools/Build/evolution_harness/run.sh</c>) runs the same checks headlessly with planted-bug
    /// negative controls; these are the edit-mode half, so a change to the core fails in the Editor too.
    /// </summary>
    [TestFixture]
    public class LifeformGenomeTests
    {
        // ---- the switch ----

        [Test]
        public void SwitchOff_EveryGenomeExpressesNeutral()
        {
            var off = EvolutionSettings.Default(false);
            var p = GenomeExpression.Express(new LifeformGenome(1f, 1f, 1f, 1f), off);
            Assert.AreEqual(1f, p.Pace);
            Assert.AreEqual(1f, p.Reach);
            Assert.AreEqual(1f, p.Upkeep);
            Assert.AreEqual(1f, p.Fecundity);
            Assert.AreEqual(1f, p.Provision);
            Assert.AreEqual(1f, p.Cohesion);
        }

        [Test]
        public void SwitchOff_MutationReturnsTheParentWithoutDrawing()
        {
            var off = EvolutionSettings.Default(false);
            var rng = new CountingRng(5);
            var parent = new LifeformGenome(0.3f, -0.2f, 0.1f, 0f);
            Assert.AreEqual(parent, GenomeMutation.Mutate(parent, off, rng));
            Assert.AreEqual(parent, GenomeMutation.Mutate(parent, off, rng));
            Assert.IsTrue(GenomeMutation.Founder(off, rng).IsFounder);
            Assert.AreEqual(0, rng.Draws, "a biome with evolution off must not disturb the random stream");
        }

        [Test]
        public void ShippedDefault_IsOff()
        {
            // Every CellConfigDataSO carries a fresh EvolutionSettings; this is the value it carries.
            Assert.IsFalse(new EvolutionSettings().Enabled);
        }

        // ---- expression ----

        [TestCase(1.5f)]
        [TestCase(1.84f)]
        [TestCase(2f)]
        [TestCase(3f)]
        public void Multiplier_Identities(float range)
        {
            Assert.AreEqual(1f, GenomeExpression.Multiplier(0f, range));
            Assert.AreEqual(range, GenomeExpression.Multiplier(1f, range));
            Assert.AreEqual(1f / range, GenomeExpression.Multiplier(-1f, range), 1e-7f);
        }

        [Test]
        public void Multiplier_IsReciprocalSymmetricAndMonotonic()
        {
            float prev = float.NegativeInfinity;
            for (int k = -100; k <= 100; k++)
            {
                float g = k / 100f;
                float f = GenomeExpression.Multiplier(g, 2f);
                Assert.AreEqual(1f, f * GenomeExpression.Multiplier(-g, 2f), 2e-7f, $"f(g) * f(-g) at {g}");
                Assert.GreaterOrEqual(f, prev, $"monotonic at {g}");
                prev = f;
            }
        }

        [Test]
        public void Multiplier_RangeBelowOneOrNaN_IsInert()
        {
            Assert.AreEqual(1f, GenomeExpression.Multiplier(1f, 0.5f));
            Assert.AreEqual(1f, GenomeExpression.Multiplier(1f, float.NaN));
            Assert.AreEqual(1f, GenomeExpression.Multiplier(float.NaN, 2f));
        }

        [Test]
        public void Express_FounderIsTheAuthoredSpecies()
        {
            var p = GenomeExpression.Express(LifeformGenome.Founder, EvolutionSettings.Default(true));
            Assert.AreEqual(1f, p.Pace);
            Assert.AreEqual(1f, p.Upkeep);
            Assert.AreEqual(1f, p.Fecundity);
            Assert.AreEqual(1f, p.Provision);
        }

        [Test]
        public void Express_TempoCostsUpkeep_ReachCostsUpkeep_BothCostTheProduct()
        {
            var s = EvolutionSettings.Default(true);
            var fast = GenomeExpression.Express(new LifeformGenome(1f, 0f, 0f, 0f), s);
            Assert.AreEqual(s.TempoPaceRange, fast.Pace);
            Assert.AreEqual(s.TempoUpkeepRange, fast.Upkeep);
            var far = GenomeExpression.Express(new LifeformGenome(0f, 1f, 0f, 0f), s);
            Assert.AreEqual(s.ReachRadiusRange, far.Reach);
            Assert.AreEqual(s.ReachUpkeepRange, far.Upkeep);
            var both = GenomeExpression.Express(new LifeformGenome(1f, 1f, 0f, 0f), s);
            Assert.AreEqual(s.TempoUpkeepRange * s.ReachUpkeepRange, both.Upkeep, 1e-6f);
        }

        [Test]
        public void Express_FecundityIsAnRKAxis_ProvisioningOnlyCostsAboveZero()
        {
            var s = EvolutionSettings.Default(true);
            var r = GenomeExpression.Express(new LifeformGenome(0f, 0f, 1f, 0f), s);
            Assert.AreEqual(s.FecundityRange, r.Fecundity);
            Assert.AreEqual(1f / s.FecundityProvisionRange, r.Provision, 1e-6f);
            var k = GenomeExpression.Express(new LifeformGenome(0f, 0f, -1f, 0f), s);
            Assert.AreEqual(1f / s.FecundityRange, k.Fecundity, 1e-6f);
            Assert.AreEqual(1f, k.Provision);
            Assert.AreEqual(10, GenomeExpression.FeedsPerOffspring(20, r));
            Assert.AreEqual(40, GenomeExpression.FeedsPerOffspring(20, k));
        }

        [Test]
        public void FeedsPerOffspring_ZeroStaysZero_NeverBelowOne()
        {
            var r = GenomeExpression.Express(new LifeformGenome(0f, 0f, 1f, 0f), EvolutionSettings.Default(true));
            Assert.AreEqual(0, GenomeExpression.FeedsPerOffspring(0, r), "0 = the species does not reproduce");
            Assert.AreEqual(1, GenomeExpression.FeedsPerOffspring(1, r), "a birth always costs at least one feed");
        }

        [Test]
        public void StarvationSeconds_DividedByUpkeep_ZeroStaysZero()
        {
            var fast = GenomeExpression.Express(new LifeformGenome(1f, 0f, 0f, 0f), EvolutionSettings.Default(true));
            Assert.AreEqual(90f / fast.Upkeep, GenomeExpression.StarvationSeconds(90f, fast), 1e-3f);
            Assert.AreEqual(0f, GenomeExpression.StarvationSeconds(0f, fast), "a clock the species does not run cannot be bought");
        }

        [Test]
        public void Express_NeverTouchesSizeOrHeart()
        {
            // The genome is behaviour and metabolism only (Docs/ECOSYSTEM.md §40 keeps size per species x element).
            var fields = typeof(LifeformPhenotype).GetFields();
            foreach (var f in fields)
            {
                StringAssert.DoesNotContain("Scale", f.Name);
                StringAssert.DoesNotContain("Heart", f.Name);
                StringAssert.DoesNotContain("Size", f.Name);
            }
        }

        // ---- mutation ----

        [Test]
        public void Mutate_IsSmallBoundedAndDeterministic()
        {
            var s = EvolutionSettings.Default(true);
            var parent = new LifeformGenome(0.2f, -0.3f, 0.1f, 0f);
            var a = GenomeMutation.Mutate(parent, s, new GenomeRng(99));
            var b = GenomeMutation.Mutate(parent, s, new GenomeRng(99));
            var c = GenomeMutation.Mutate(parent, s, new GenomeRng(100));
            Assert.AreEqual(a, b, "deterministic per seed");
            Assert.AreNotEqual(a, c, "different across seeds");
            Assert.AreNotEqual(parent, a, "a child differs from its parent");
            Assert.Less(LifeformGenome.Distance(parent, a), s.MutationSigma * 6f * 2f, "a step is within six sigmas per locus");
        }

        [Test]
        public void Mutate_RateOneDrawsTwelvePerLocus_SigmaZeroDrawsNothing()
        {
            var s = EvolutionSettings.Default(true);
            var rng = new CountingRng(1);
            GenomeMutation.Mutate(LifeformGenome.Founder, s, rng);
            Assert.AreEqual(12 * LifeformGenome.LocusCount, rng.Draws);
            var quiet = new CountingRng(1);
            Assert.AreEqual(LifeformGenome.Founder, GenomeMutation.Mutate(LifeformGenome.Founder, s.WithoutMutation(), quiet));
            Assert.AreEqual(0, quiet.Draws);
        }

        [Test]
        public void Mutate_NeverLeavesTheBand()
        {
            var wild = EvolutionSettings.Default(true);
            wild.MutationSigma = 10f;
            var g = LifeformGenome.Founder;
            var rng = new GenomeRng(3);
            for (int i = 0; i < 1000; i++)
            {
                g = GenomeMutation.Mutate(g, wild, rng);
                for (int l = 0; l < LifeformGenome.LocusCount; l++)
                    Assert.That(g[l], Is.InRange(LifeformGenome.GeneMin, LifeformGenome.GeneMax));
            }
        }

        [Test]
        public void Normal_IsStandard()
        {
            var rng = new GenomeRng(5);
            double sum = 0, sq = 0;
            const int n = 100000;
            for (int i = 0; i < n; i++) { double v = GenomeMutation.Normal(rng); sum += v; sq += v * v; }
            double mean = sum / n, sd = Math.Sqrt(sq / n - mean * mean);
            Assert.AreEqual(0.0, mean, 0.015);
            Assert.AreEqual(1.0, sd, 0.015);
        }

        [Test]
        public void Founder_SpreadZeroIsTheAuthoredSpecies_SpreadCentresOnIt()
        {
            var none = EvolutionSettings.Default(true);
            none.FounderSpread = 0f;
            var quiet = new CountingRng(1);
            Assert.IsTrue(GenomeMutation.Founder(none, quiet).IsFounder);
            Assert.AreEqual(0, quiet.Draws);

            var spread = EvolutionSettings.Default(true);
            spread.FounderSpread = 0.1f;
            var rng = new GenomeRng(1);
            double sum = 0;
            const int n = 10000;
            for (int i = 0; i < n; i++) sum += GenomeMutation.Founder(spread, rng).Tempo;
            Assert.AreEqual(0.0, sum / n, 0.01);
        }

        // ---- the wire ----

        [Test]
        public void Pack_RoundTripsWithinHalfAStep_FounderIsZero_EndsSurvive()
        {
            Assert.AreEqual(0u, LifeformGenome.Founder.Pack());
            Assert.IsTrue(LifeformGenome.Unpack(0u).IsFounder);
            var ends = new LifeformGenome(1f, -1f, 1f, -1f);
            Assert.AreEqual(ends, LifeformGenome.Unpack(ends.Pack()));
            var rng = new GenomeRng(42);
            for (int i = 0; i < 2000; i++)
            {
                var g = new LifeformGenome((float)(rng.NextUnit() * 2 - 1), (float)(rng.NextUnit() * 2 - 1),
                                           (float)(rng.NextUnit() * 2 - 1), (float)(rng.NextUnit() * 2 - 1));
                var q = LifeformGenome.Unpack(g.Pack());
                for (int l = 0; l < LifeformGenome.LocusCount; l++)
                    Assert.AreEqual(g[l], q[l], 0.5f / 127f + 1e-6f);
                Assert.AreEqual(q, q.Quantized(), "quantization is idempotent");
            }
        }

        [Test]
        public void GenomeRng_IsDeterministicAndUniform()
        {
            var a = new GenomeRng(7); var b = new GenomeRng(7);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextUnit(), b.NextUnit());
            var r = new GenomeRng(8);
            double sum = 0; const int n = 100000;
            for (int i = 0; i < n; i++) { double u = r.NextUnit(); Assert.That(u, Is.InRange(0.0, 1.0 - 1e-12)); sum += u; }
            Assert.AreEqual(0.5, sum / n, 0.005);
        }

        // ---- the ledger ----

        [Test]
        public void Ledger_TracksLineagesGenerationsAndCauses()
        {
            var ledger = new EvolutionLedger();
            uint a = ledger.RecordFounder("tadpole", new LifeformGenome(0.1f, 0, 0, 0), 0);
            uint b = ledger.RecordFounder("tadpole", new LifeformGenome(-0.1f, 0, 0, 0), 0);
            uint c = ledger.RecordBirth("tadpole", a, new LifeformGenome(0.2f, 0, 0, 0), 10);
            uint d = ledger.RecordBirth("tadpole", c, new LifeformGenome(0.3f, 0, 0, 0), 20);
            Assert.AreEqual(4, ledger.LivingCount);
            Assert.IsTrue(ledger.TryGetLiving(d, out var grandchild));
            Assert.AreEqual(2, grandchild.Generation);
            Assert.AreEqual(a, grandchild.LineageId);
            Assert.AreEqual(c, grandchild.ParentId);

            Assert.IsTrue(ledger.RecordDeath(b, LifeformDeathCause.Starvation, 30));
            Assert.IsFalse(ledger.RecordDeath(b, LifeformDeathCause.Starvation, 31), "a death leaves the living once");
            var row = ledger.SnapshotSpecies("tadpole", 40);
            Assert.AreEqual(3, row.Population);
            Assert.AreEqual((0.1 + 0.2 + 0.3) / 3.0, row.Mean[0], 1e-6);
            Assert.AreEqual(1, row.Lineages);
            Assert.AreEqual(2, row.MaxGeneration);
            Assert.AreEqual(2, row.Births);
            Assert.AreEqual(1, row.DeathsByCause[(int)LifeformDeathCause.Starvation]);
            var next = ledger.SnapshotSpecies("tadpole", 50);
            Assert.AreEqual(0, next.Births, "since-last-row counters reset");

            uint orphan = ledger.RecordBirth("tadpole", 123456, LifeformGenome.Founder, 60);
            Assert.IsTrue(ledger.TryGetLiving(orphan, out var o));
            Assert.AreEqual(0, o.Generation, "an unknown parent makes the child a founder rather than losing the row");

            ledger.Clear();
            Assert.AreEqual(0, ledger.LivingCount);
            Assert.AreEqual(0, ledger.Species.Count);
        }

        [Test]
        public void Ledger_RowsAreCappedPerSpecies_AndJsonIsWellFormed()
        {
            var ledger = new EvolutionLedger { MaxSnapshotsPerSpecies = 16 };
            ledger.RecordFounder("shark", LifeformGenome.Founder, 0);
            for (int i = 0; i < 40; i++) ledger.SnapshotSpecies("shark", i);
            Assert.AreEqual(16, ledger.SnapshotsOf("shark").Count);
            string json = ledger.ToJson("test \"quoted\"");
            StringAssert.StartsWith("{", json);
            StringAssert.EndsWith("}", json);
            StringAssert.Contains("\"label\":\"test \\\"quoted\\\"\"", json);
            StringAssert.Contains("\"loci\":[\"tempo\",\"reach\",\"fecundity\",\"cohesion\"]", json);
            StringAssert.Contains("\"name\":\"shark\"", json);
        }

        // ---- the stomach's provisioning ----

        [Test]
        public void FaunaStomach_FillFraction_HalfFullStarvesInHalfTheClock()
        {
            var st = FaunaStomach.FromStarvationClock(90f, 16f);
            st.FillFraction(0f, 0.5f);
            Assert.AreEqual(8f, st.Level(0f), 1e-5f);
            Assert.AreEqual(45f, st.SecondsLeft(0f), 1e-3f);
            st.FillFraction(0f, 2f);
            Assert.AreEqual(16f, st.Level(0f), "clamped to full");
            st.FillFraction(0f, float.NaN);
            Assert.AreEqual(16f, st.Level(0f), "NaN reads as full");
        }

        // ---- enum / count freezes ----

        [Test]
        public void LocusAndCauseCountsAreFrozen()
        {
            Assert.AreEqual(LifeformGenome.LocusCount, Enum.GetValues(typeof(GenomeLocus)).Length);
            Assert.AreEqual(EvolutionLedger.CauseCount, Enum.GetValues(typeof(LifeformDeathCause)).Length);
            Assert.AreEqual(0, (int)GenomeLocus.Tempo);
            Assert.AreEqual(1, (int)GenomeLocus.Reach);
            Assert.AreEqual(2, (int)GenomeLocus.Fecundity);
            Assert.AreEqual(3, (int)GenomeLocus.Cohesion);
            Assert.AreEqual(0, (int)LifeformDeathCause.Starvation);
            Assert.AreEqual(4, (int)LifeformDeathCause.Teardown);
        }

        sealed class CountingRng : IGenomeRng
        {
            readonly GenomeRng _rng;
            public int Draws;
            public CountingRng(uint seed) => _rng = new GenomeRng(seed);
            public double NextUnit() { Draws++; return _rng.NextUnit(); }
        }
    }
}
#endif
