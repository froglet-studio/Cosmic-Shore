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
        public void IntensityDitherer_Level4_IsIdentity()
        {
            // Intensity 4 is the trained ceiling. The ditherer must return the frame
            // bit-for-bit untouched — this is the invariant that makes "flawless"
            // mean something.
            var d = new IntensityDitherer();
            var input = new IntensityDitherer.InputFrame
            {
                XSum = 0.3f,
                YSum = -0.2f,
                XDiff = 0.7f,
                YDiff = 0.3f,
                EasedLeft = new Vector2(0.5f, -0.5f)
            };

            var output = d.Apply(intensity: 4, frame: input, now: 0f);

            Assert.That(output.XSum, Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(output.YSum, Is.EqualTo(-0.2f).Within(1e-6f));
            Assert.That(output.XDiff, Is.EqualTo(0.7f).Within(1e-6f));
            Assert.That(output.YDiff, Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(output.EasedLeft.x, Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void IntensityDitherer_Level1_ScalesThrottleDown()
        {
            var d = new IntensityDitherer();
            var input = new IntensityDitherer.InputFrame { XDiff = 1f };
            var output = d.Apply(intensity: 1, frame: input, now: 0f);
            Assert.IsTrue(output.XDiff < 1f, "Intensity 1 must scale the throttle channel below 1.");
        }

        [Test]
        public void IntensityDitherer_TempoFactors_AreIdentityAtLevel4()
        {
            var d = new IntensityDitherer();
            var s4 = d.GetSettings(4);
            Assert.That(s4.SkillFactor, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(s4.AbilityCooldownFactor, Is.EqualTo(1f).Within(1e-6f));

            var s1 = d.GetSettings(1);
            Assert.IsTrue(s1.SkillFactor < 1f, "Intensity 1 must lower the skill dial.");
            Assert.IsTrue(s1.AbilityCooldownFactor > 1f, "Intensity 1 must slow the ability cadence.");
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

        // ── Pilot tuning mapping ─────────────────────────────────────

        [Test]
        public void PilotTuningGenes_DisabledModulesContributeNothing()
        {
            GeneRegistry.Clear();
            PilotTuningGenes.EnsureRegistered();

            var g = TrainingGenome.FromRegistryDefaults();
            g.SetModuleEnabled(PilotTuningGenes.ModuleTuning, false);
            g.SetModuleEnabled(PilotTuningGenes.ModuleStyle, false);
            g.SetModuleEnabled(PilotTuningGenes.ModuleTempo, false);

            var t = PilotTuningGenes.ToTuning(g);

            // Every field null == "keep the authored value": a fully disabled genome
            // must be indistinguishable from no genome at all.
            Assert.IsFalse(t.SkillLevel.HasValue);
            Assert.IsFalse(t.ThrottleBase.HasValue);
            Assert.IsFalse(t.Ram.HasValue);
            Assert.IsFalse(t.Drift.HasValue);
            Assert.IsFalse(t.AbilityDurationScale.HasValue);
        }

        [Test]
        public void PilotTuningGenes_EnabledModulesMapThrough()
        {
            GeneRegistry.Clear();
            PilotTuningGenes.EnsureRegistered();

            var g = TrainingGenome.FromRegistryDefaults();
            g.SetModuleEnabled(PilotTuningGenes.ModuleStyle, true);
            g.Set(PilotTuningGenes.GeneSkill, 0.8f);
            g.Set(PilotTuningGenes.GeneRam, 0.9f);
            g.Set(PilotTuningGenes.GeneDrift, 0.1f);

            var t = PilotTuningGenes.ToTuning(g);

            Assert.That(t.SkillLevel, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.IsTrue(t.Ram.HasValue && t.Ram.Value, "Ram gene 0.9 must map to true.");
            Assert.IsTrue(t.Drift.HasValue && !t.Drift.Value, "Drift gene 0.1 must map to false.");
        }

        [Test]
        public void PilotTuningGenes_PersonalityNameIsStable()
        {
            GeneRegistry.Clear();
            PilotTuningGenes.EnsureRegistered();

            var g = TrainingGenome.FromRegistryDefaults();
            g.SetModuleEnabled(PilotTuningGenes.ModuleStyle, true);
            g.Set(PilotTuningGenes.GeneRam, 0.9f);
            g.Set(PilotTuningGenes.GeneSkill, 0.95f);

            string a = PilotTuningGenes.PersonalityName(g);
            string b = PilotTuningGenes.PersonalityName(g);
            Assert.AreEqual(a, b, "Personality naming must be deterministic.");
            StringAssert.Contains("Rammer", a);
            StringAssert.Contains("Ace", a);
        }

        // ── Archive roster ───────────────────────────────────────────

        [Test]
        public void Archive_Roster_KeepsDethronedChampion()
        {
            GeneRegistry.Clear();
            PilotTuningGenes.EnsureRegistered();

            var arch = ScriptableObject.CreateInstance<TrainingArchiveSO>();

            var first = TrainingGenome.FromRegistryDefaults();
            first.Set(PilotTuningGenes.GeneSkill, 0.5f);
            arch.Upsert(CosmicShore.Data.VesselClassType.Manta, CosmicShore.Data.GameModes.HexRace, 4, first, 100f, 1);

            // A fitter genome with a DIFFERENT behavior fingerprint dethrones the
            // champion — which must land in the roster, not vanish.
            var second = TrainingGenome.FromRegistryDefaults();
            second.SetModuleEnabled(PilotTuningGenes.ModuleStyle, true);
            second.Set(PilotTuningGenes.GeneRam, 0.95f);
            arch.Upsert(CosmicShore.Data.VesselClassType.Manta, CosmicShore.Data.GameModes.HexRace, 4, second, 150f, 2);

            var entry = arch.Find(CosmicShore.Data.VesselClassType.Manta, CosmicShore.Data.GameModes.HexRace, 4);
            Assert.IsNotNull(entry);
            Assert.That(entry.Fitness, Is.EqualTo(150f).Within(1e-3f));
            Assert.IsTrue(entry.Roster != null && entry.Roster.Count >= 1,
                "The dethroned champion must get a roster seat.");
        }

        [Test]
        public void Archive_Roster_RejectsWeakGenomes()
        {
            GeneRegistry.Clear();
            PilotTuningGenes.EnsureRegistered();

            var arch = ScriptableObject.CreateInstance<TrainingArchiveSO>();

            var champion = TrainingGenome.FromRegistryDefaults();
            arch.Upsert(CosmicShore.Data.VesselClassType.Manta, CosmicShore.Data.GameModes.HexRace, 4, champion, 100f, 1);

            // Far below the fitness floor (0.6 × champion) — no seat, however distinct.
            var weak = TrainingGenome.FromRegistryDefaults();
            weak.SetModuleEnabled(PilotTuningGenes.ModuleStyle, true);
            weak.Set(PilotTuningGenes.GeneRam, 0.99f);
            arch.Upsert(CosmicShore.Data.VesselClassType.Manta, CosmicShore.Data.GameModes.HexRace, 4, weak, 10f, 2);

            var entry = arch.Find(CosmicShore.Data.VesselClassType.Manta, CosmicShore.Data.GameModes.HexRace, 4);
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Roster == null || entry.Roster.Count == 0,
                "A genome under the roster fitness floor must be rejected.");
        }

        [Test]
        public void Archive_SampleRoster_ReturnsGenomeAndPersonality()
        {
            GeneRegistry.Clear();
            PilotTuningGenes.EnsureRegistered();

            var arch = ScriptableObject.CreateInstance<TrainingArchiveSO>();
            var g = TrainingGenome.FromRegistryDefaults();
            arch.Upsert(CosmicShore.Data.VesselClassType.Manta, CosmicShore.Data.GameModes.HexRace, 4, g, 100f, 1);

            var sampled = arch.SampleRoster(CosmicShore.Data.VesselClassType.Manta,
                CosmicShore.Data.GameModes.HexRace, 4, out string personality);

            Assert.IsNotNull(sampled);
            Assert.IsFalse(string.IsNullOrEmpty(personality));
        }
    }
}
#endif
