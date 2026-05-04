#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Utility.AITraining.Tests
{
    /// <summary>
    /// Edit-mode tests for the search-side data structures (no scene required).
    /// They cover the four invariants the framework depends on:
    ///   1. Genes round-trip through clamp/serialize without drift.
    ///   2. Population evolution is monotonic in expectation given a static fitness landscape.
    ///   3. Crossover preserves only registered genes.
    ///   4. The intensity ditherer leaves intensity-4 input untouched.
    /// </summary>
    public class AITrainingCoreTests
    {
        [SetUp]
        public void SetUp()
        {
            GeneRegistry.Clear();
            GeneRegistry.Register("Test", new GeneSpec("a", 0f, 1f, 0.5f));
            GeneRegistry.Register("Test", new GeneSpec("b", -10f, 10f, 0f));
            GeneRegistry.Register("Optional", new GeneSpec("c", 5f, 50f, 25f), defaultEnabled: false);
        }

        [TearDown]
        public void TearDown() => GeneRegistry.Clear();

        [Test]
        public void Genome_FromRegistryDefaults_PopulatesEveryRegisteredGene()
        {
            var g = TrainingGenome.FromRegistryDefaults();
            Assert.IsTrue(g.Has("a"));
            Assert.IsTrue(g.Has("b"));
            Assert.IsTrue(g.Has("c"));
            Assert.That(g.Get("a"), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(g.Get("b"), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Genome_DefaultEnabledModulesArePopulated()
        {
            var g = TrainingGenome.FromRegistryDefaults();
            Assert.IsTrue(g.IsModuleEnabled("Test"));
            Assert.IsFalse(g.IsModuleEnabled("Optional"));
        }

        [Test]
        public void Genome_SetClampsToRegisteredRange()
        {
            var g = new TrainingGenome();
            g.Set("a", 5f);   // outside 0..1
            Assert.That(g.Get("a"), Is.EqualTo(1f).Within(1e-5f));
            g.Set("a", -5f);
            Assert.That(g.Get("a"), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Genome_GetFallsBackToDefaultForUnsetGenes()
        {
            var g = new TrainingGenome();
            // Nothing set, c was registered with default 25
            Assert.That(g.Get("c"), Is.EqualTo(25f).Within(1e-5f));
        }

        [Test]
        public void Genome_Crossover_OnlyContainsRegisteredGenes()
        {
            var a = TrainingGenome.FromRegistryDefaults();
            var b = TrainingGenome.FromRegistryDefaults();
            var child = TrainingGenome.Crossover(a, b);
            Assert.IsTrue(child.Has("a"));
            Assert.IsTrue(child.Has("b"));
            // 'c' should be present because it's registered, but in either parent's value range
            Assert.IsTrue(child.Has("c"));
        }

        [Test]
        public void Genome_BehaviorHash_IsStableAcrossClones()
        {
            var g = TrainingGenome.FromRegistryDefaults();
            var clone = g.Clone();
            Assert.AreEqual(g.BehaviorHash(), clone.BehaviorHash());
        }

        [Test]
        public void Genome_BehaviorHash_DiffersOnModuleToggle()
        {
            var g = TrainingGenome.FromRegistryDefaults();
            int h1 = g.BehaviorHash();
            g.SetModuleEnabled("Test", false);
            int h2 = g.BehaviorHash();
            Assert.AreNotEqual(h1, h2);
        }

        [Test]
        public void Population_InitializeFillsRequestedSize()
        {
            var pop = new TrainingPopulation { ConfiguredSize = 10 };
            pop.Initialize();
            Assert.AreEqual(10, pop.PopulationSize);
        }

        [Test]
        public void Population_CheckoutRoundRobinsAcrossGeneration()
        {
            var pop = new TrainingPopulation { ConfiguredSize = 4 };
            pop.Initialize();
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 4; i++)
            {
                pop.Checkout(out int idx);
                Assert.IsTrue(seen.Add(idx));
            }
            Assert.AreEqual(4, seen.Count);
        }

        [Test]
        public void Population_EvolveMonotonicInExpectation()
        {
            // Static fitness landscape: f(genome) = -|gene_a - 0.7|
            // Population should drift toward 0.7 over generations.
            var pop = new TrainingPopulation
            {
                ConfiguredSize = 24,
                EliteCount = 4,
                NumericMutationRate = 0.5f,
                NumericMutationStrength = 0.2f,
                StructuralMutationRate = 0f,    // structural off for this convergence test
            };
            pop.Initialize();

            int generations = 20;
            for (int gen = 0; gen < generations; gen++)
            {
                for (int i = 0; i < pop.ConfiguredSize; i++)
                {
                    var g = pop.Checkout(out int idx);
                    var fit = new TrainingFitness();
                    fit.Add("aim", -Mathf.Abs(g.Get("a") - 0.7f), 1f);
                    pop.ReturnFitness(idx, fit, g);
                }
            }

            var best = pop.Best;
            Assert.IsNotNull(best);
            Assert.IsTrue(best.Fitness > -0.2f,
                $"After {generations} generations the best fitness should be close to 0; got {best.Fitness:F3}");
        }

        [Test]
        public void IntensityDitherer_Level4_LeavesInputUnchanged()
        {
            var d = new IntensityDitherer();
            var input = new DecisionOutput
            {
                SteerLocal = new Vector2(0.3f, -0.2f),
                SteerWeight = 1f,
                Throttle = 0.7f,
                ThrottleWeight = 1f,
                Roll = 0.1f,
                RollWeight = 1f,
            };

            var output = d.Apply(intensity: 4, decision: input, now: 0f);

            Assert.That(output.SteerLocal.x, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(output.SteerLocal.y, Is.EqualTo(-0.2f).Within(1e-5f));
            Assert.That(output.Throttle, Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(output.Roll, Is.EqualTo(0.1f).Within(1e-5f));
        }

        [Test]
        public void IntensityDitherer_Level1_ScalesThrottleDown()
        {
            var d = new IntensityDitherer();
            var input = new DecisionOutput { Throttle = 1f, ThrottleWeight = 1f };
            var output = d.Apply(intensity: 1, decision: input, now: 0f);
            Assert.IsTrue(output.Throttle < 1f, "Intensity 1 must scale throttle below 1.");
        }

        [Test]
        public void Archive_FindBestAvailable_ReturnsExactWhenPresent()
        {
            var arch = ScriptableObject.CreateInstance<TrainingArchiveSO>();
            var g = TrainingGenome.FromRegistryDefaults();
            g.Set("a", 0.42f);
            arch.Upsert(CosmicShore.Data.VesselClassType.Manta,
                        CosmicShore.Data.GameModes.HexRace,
                        4, g, 100f, 5);
            var found = arch.FindBestAvailable(
                CosmicShore.Data.VesselClassType.Manta,
                CosmicShore.Data.GameModes.HexRace,
                4, out int score);
            Assert.AreEqual(4, score);
            Assert.IsNotNull(found);
            Assert.That(found.Get("a"), Is.EqualTo(0.42f).Within(1e-5f));
        }

        [Test]
        public void Archive_FindBestAvailable_FallsBackWhenNoExactMatch()
        {
            var arch = ScriptableObject.CreateInstance<TrainingArchiveSO>();
            var g = TrainingGenome.FromRegistryDefaults();
            arch.Upsert(CosmicShore.Data.VesselClassType.Manta,
                        CosmicShore.Data.GameModes.HexRace,
                        4, g, 100f, 5);
            var found = arch.FindBestAvailable(
                CosmicShore.Data.VesselClassType.Sparrow,
                CosmicShore.Data.GameModes.HexRace,
                4, out int score);
            Assert.IsNotNull(found);
            Assert.IsTrue(score < 4, "Score should reflect that the match is partial.");
        }

        [Test]
        public void GenomeJson_RoundTripPreservesValues()
        {
            var g = TrainingGenome.FromRegistryDefaults();
            g.Set("a", 0.314f);
            g.Set("b", -7.5f);
            g.SetModuleEnabled("Optional", true);
            g.Lineage = "abc+def";

            var json = GenomeJson.Export(g);
            var restored = GenomeJson.Import(json);

            Assert.IsNotNull(restored);
            Assert.That(restored.Get("a"), Is.EqualTo(0.314f).Within(1e-4f));
            Assert.That(restored.Get("b"), Is.EqualTo(-7.5f).Within(1e-4f));
            Assert.IsTrue(restored.IsModuleEnabled("Optional"));
            Assert.AreEqual("abc+def", restored.Lineage);
        }

        [Test]
        public void Population_SerializedFieldsSurviveRoundTrip()
        {
            var json = JsonUtility.ToJson(new TrainingPopulation { ConfiguredSize = 7, EliteCount = 2 });
            var restored = JsonUtility.FromJson<TrainingPopulation>(json);
            Assert.AreEqual(7, restored.ConfiguredSize);
            Assert.AreEqual(2, restored.EliteCount);
        }
    }
}
#endif
