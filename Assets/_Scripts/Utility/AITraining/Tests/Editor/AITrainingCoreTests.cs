#if UNITY_EDITOR
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility.AITraining.Editor;
using NUnit.Framework;
using UnityEditor;
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
                        CosmicShore.Data.GameModes.SkimRace,
                        4, g, 100f, 5);
            var found = arch.FindBestAvailable(
                CosmicShore.Data.VesselClassType.Manta,
                CosmicShore.Data.GameModes.SkimRace,
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
                        CosmicShore.Data.GameModes.SkimRace,
                        4, g, 100f, 5);
            var found = arch.FindBestAvailable(
                CosmicShore.Data.VesselClassType.Sparrow,
                CosmicShore.Data.GameModes.SkimRace,
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

        [Test]
        public void EditorWindow_OpensWithoutMissingTypes()
        {
            var window = EditorWindow.GetWindow<TrainingEditorWindow>(false, "AI Training", false);
            Assert.IsNotNull(window);
            window.Close();
        }

        [Test]
        public void Population_AbandonCheckout_RewindsWithoutRecording()
        {
            var pop = new TrainingPopulation { ConfiguredSize = 3, EliteCount = 1 };
            pop.Initialize();
            var state = ScriptableObject.CreateInstance<TrainingSessionStateSO>();
            state.Population = pop;

            pop.Checkout(out _);
            pop.Checkout(out _);
            pop.Checkout(out _);
            pop.AbandonCheckout(3);

            Assert.AreEqual(0, state.EpisodesCompleted);
            Assert.AreEqual(0, pop.Generation);
            pop.Checkout(out int idx);
            Assert.AreEqual(0, idx);
            Assert.AreEqual(0, pop.Generation);
            Object.DestroyImmediate(state);
        }

        [Test]
        public void Loop_ThreeMatchesAtPopulationSize_IncrementsGeneration()
        {
            const int pilots = 3;
            var pop = new TrainingPopulation { ConfiguredSize = pilots, EliteCount = 1 };
            pop.Initialize();
            var state = ScriptableObject.CreateInstance<TrainingSessionStateSO>();
            state.Population = pop;

            for (int match = 0; match < 3; match++)
            {
                var genomes = new TrainingGenome[pilots];
                var idxs = new int[pilots];
                for (int i = 0; i < pilots; i++)
                    genomes[i] = pop.Checkout(out idxs[i]);

                Assert.AreEqual(match, pop.Generation);

                for (int i = 0; i < pilots; i++)
                {
                    var fit = new TrainingFitness();
                    fit.Add("Crystals", 1f + i, 100f);
                    pop.ReturnFitness(idxs[i], fit, genomes[i]);
                    state.RecordEpisode(fit, genomes[i]);
                }
            }

            Assert.AreEqual(2, pop.Generation);
            Assert.AreEqual(9, state.EpisodesCompleted);
            Object.DestroyImmediate(state);
        }

        [Test]
        public void HexRace_ScoreFromRoundStats_IsNegated()
        {
            Assert.AreEqual(-40f, FitnessProfileSO.SignedRaw(
                GameModes.SkimRace, FitnessProfileSO.ComponentKind.ScoreFromRoundStats, 40f));
            Assert.AreEqual(12f, FitnessProfileSO.SignedRaw(
                GameModes.SkimRace, FitnessProfileSO.ComponentKind.CrystalCollection, 12f));
            Assert.AreEqual(-40f, FitnessProfileSO.SignedRaw(
                GameModes.Joust, FitnessProfileSO.ComponentKind.ScoreFromRoundStats, 40f));
            Assert.AreEqual(40f, FitnessProfileSO.SignedRaw(
                GameModes.BroodRush, FitnessProfileSO.ComponentKind.ScoreFromRoundStats, 40f));
            Assert.AreEqual(40f, FitnessProfileSO.SignedRaw(
                GameModes.AstroLeague, FitnessProfileSO.ComponentKind.ScoreFromRoundStats, 40f));
            Assert.AreEqual(40f, FitnessProfileSO.SignedRaw(
                GameModes.ScarabScramble, FitnessProfileSO.ComponentKind.ScoreFromRoundStats, 40f));
        }

        [Test]
        public void SkimRace_ApplyFor_IsSkimRaceProfile_AndRenamedModesDoNotFallThrough()
        {
            var profile = ScriptableObject.CreateInstance<FitnessProfileSO>();
            profile.ApplyFor(GameModes.SkimRace);
            Assert.AreEqual(3, profile.Entries.Count);
            Assert.AreEqual(FitnessProfileSO.ComponentKind.CrystalCollection, profile.Entries[0].Kind);
            Assert.AreEqual(100f, profile.Entries[0].Weight);
            Assert.AreEqual("Crystals", profile.Entries[0].Label);
            Assert.AreEqual(FitnessProfileSO.ComponentKind.ScoreFromRoundStats, profile.Entries[1].Kind);
            Assert.AreEqual(0.1f, profile.Entries[1].Weight);
            Assert.AreEqual("GolfScore", profile.Entries[1].Label);
            Assert.AreEqual(FitnessProfileSO.ComponentKind.TimePenalty, profile.Entries[2].Kind);
            Assert.AreEqual(1f, profile.Entries[2].Weight);
            Assert.AreEqual("TimePenalty", profile.Entries[2].Label);
            Assert.IsTrue(FitnessProfileSO.ScoreIsGolf(GameModes.SkimRace));
            Assert.AreEqual(3, profile.Build().Count);

            var fallback = ScriptableObject.CreateInstance<FitnessProfileSO>();
            fallback.ApplyRacingDefaults();
            foreach (var mode in new[] { GameModes.SkimRace, GameModes.Scurry, GameModes.Joust, GameModes.Cleave, GameModes.BroodRush })
            {
                profile.ApplyFor(mode);
                Assert.AreNotEqual(fallback.Description, profile.Description, mode + " fell through to the default profile.");
                Assert.IsNotEmpty(profile.Entries, mode.ToString());
            }
            Object.DestroyImmediate(fallback);
            Object.DestroyImmediate(profile);
        }

        /// <summary>
        /// Raw values from a live Skim Race rollout: Jade, 1 crystal, golf Score
        /// 115.69, 119.28 s. The line read total=-30.85.
        /// </summary>
        [Test]
        public void SkimRace_Harvest_TotalIsWeightedSum_AndShowsAllThreeTerms()
        {
            var profile = ScriptableObject.CreateInstance<FitnessProfileSO>();
            profile.ApplyFor(GameModes.SkimRace);
            float[] authored = { 1f, 115.69f, -119.28f };

            var fit = new TrainingFitness { EpisodeSeconds = 119.3f };
            float expected = 0f;
            for (int i = 0; i < profile.Entries.Count; i++)
            {
                var e = profile.Entries[i];
                float raw = FitnessProfileSO.SignedRaw(GameModes.SkimRace, e.Kind, authored[i]);
                fit.Add(e.Label, raw, e.Weight);
                expected += raw * e.Weight;
            }

            Assert.AreEqual(-30.849f, fit.Total, 0.001f);
            Assert.AreEqual(expected, fit.Total, 0.0001f);
            Assert.AreEqual(fit.Components.Sum(c => c.Weighted), fit.Total, 0.0001f);
            var line = fit.Summarize();
            var afterPipe = line.Substring(line.IndexOf('|'));
            StringAssert.Contains("Crystals=100.0(1.00)", afterPipe);
            StringAssert.Contains("GolfScore=-11.6(-115.69)", afterPipe);
            StringAssert.Contains("TimePenalty=-119.3(-119.28)", afterPipe);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void NewMetricKinds_FactoryReturnsAComponent()
        {
            Assert.IsNotNull(FitnessComponentFactory.Create(FitnessProfileSO.ComponentKind.HostilePrismsDestroyed, "p"));
            Assert.IsNotNull(FitnessComponentFactory.Create(FitnessProfileSO.ComponentKind.LifeformsKilled, "k"));
            Assert.IsNotNull(FitnessComponentFactory.Create(FitnessProfileSO.ComponentKind.CombatPoints, "c"));
            Assert.IsNotNull(FitnessComponentFactory.Create(FitnessProfileSO.ComponentKind.GoalsScored, "g"));
        }

        [Test]
        public void Catalog_EveryLiveMode_HasScenarioAndProfileMatchingApplyFor()
        {
            Assert.AreEqual(12, TrainingModeCatalog.Live.Count);
            foreach (var row in TrainingModeCatalog.Live)
            {
                var scenario = AssetDatabase.LoadAssetAtPath<TrainingScenarioSO>(row.ScenarioPath);
                var profile = AssetDatabase.LoadAssetAtPath<FitnessProfileSO>(row.ProfilePath);
                Assert.IsNotNull(scenario, row.Token);
                Assert.IsNotNull(profile, row.Token);
                Assert.AreEqual(row.GameMode, scenario.GameMode, row.Token);
                Assert.AreEqual(row.Vessel, scenario.Vessel, row.Token);
                Assert.AreEqual(4, scenario.Intensity, row.Token);
                Assert.AreEqual(row.PlayerCount, scenario.OpponentCount, row.Token);
                Assert.AreEqual(row.TargetMode, scenario.TargetMode, row.Token);
                Assert.AreEqual(24, scenario.PopulationSize, row.Token);
                Assert.AreEqual(120f, scenario.MaxEpisodeSeconds, row.Token);
                Assert.IsTrue(scenario.EarlyExitConditions == null || scenario.EarlyExitConditions.Count == 0, row.Token);
                Assert.AreSame(profile, scenario.FitnessProfile, row.Token);

                var expected = ScriptableObject.CreateInstance<FitnessProfileSO>();
                expected.ApplyFor(row.GameMode);
                Assert.AreEqual(expected.Description, profile.Description, row.Token);
                Assert.AreEqual(expected.Entries.Count, profile.Entries.Count, row.Token);
                for (int i = 0; i < expected.Entries.Count; i++)
                {
                    Assert.AreEqual(expected.Entries[i].Kind, profile.Entries[i].Kind, row.Token + " kind");
                    Assert.AreEqual(expected.Entries[i].Weight, profile.Entries[i].Weight, 0.0001f, row.Token + " weight");
                    Assert.AreEqual(expected.Entries[i].Label, profile.Entries[i].Label, row.Token + " label");
                }
                Object.DestroyImmediate(expected);

                bool personal = row.GameMode == GameModes.BroodRush
                    || row.GameMode == GameModes.AstroLeague
                    || row.GameMode == GameModes.ScarabScramble;
                Assert.AreEqual(!personal, FitnessProfileSO.ScoreIsGolf(row.GameMode), row.Token);
                if (row.GameMode == GameModes.BroodRush)
                    StringAssert.Contains("representative", profile.Description);
            }
        }

        [Test]
        public void FormatEta_Overnight_Remaining_AndMeasured()
        {
            Assert.AreEqual("Overnight — stops when you press Stop",
                TrainingEditorWindow.FormatEta(3, -1, 10));
            Assert.AreEqual("Overnight — stops when you press Stop",
                TrainingEditorWindow.FormatEta(3, 0, 10));
            Assert.AreEqual("Target reached", TrainingEditorWindow.FormatEta(4, 4, 10));
            Assert.AreEqual("2 episodes left", TrainingEditorWindow.FormatEta(1, 3, 0));
            Assert.AreEqual("20s", TrainingEditorWindow.FormatEta(0, 2, 10));
        }

        [Test]
        public void ApplyOperatorLimits_ZeroWatchdog_KeepsDefault()
        {
            var go = new GameObject("limits");
            var runner = go.AddComponent<TrainingSessionRunner>();
            runner.ApplyOperatorLimits(-1, 0f);
            Assert.AreEqual(-1, runner.TargetEpisodes);
            Assert.AreEqual(180f, runner.WatchdogTimeoutSeconds, 0.01f);
            runner.ApplyOperatorLimits(12, 90f);
            Assert.AreEqual(12, runner.TargetEpisodes);
            Assert.AreEqual(90f, runner.WatchdogTimeoutSeconds, 0.01f);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void NoveltyArchiveSize_GrowsOnReturnFitness()
        {
            var pop = new TrainingPopulation { ConfiguredSize = 2 };
            pop.Initialize();
            Assert.AreEqual(0, pop.NoveltyArchiveSize);
            var genome = pop.Checkout(out int idx);
            pop.ReturnFitness(idx, new TrainingFitness(), genome);
            Assert.AreEqual(1, pop.NoveltyArchiveSize);
        }

        [Test]
        public void ControlDefaults_OvernightAndHumanOff()
        {
            var control = ScriptableObject.CreateInstance<TrainingControlSO>();
            Assert.AreEqual(-1, control.TargetEpisodes);
            Assert.AreEqual(180f, control.WatchdogSeconds, 0.01f);
            Assert.IsTrue(control.DeployArchiveInNormalPlay);
            Assert.IsFalse(control.HumanPlaysThisLaunch);
            Assert.AreEqual(0f, control.SimulationTimeScale);
            Assert.IsFalse(control.MuteAudio);
            Assert.IsFalse(control.DisableCameraRendering);
            Assert.IsFalse(control.UseStoredGenomeForLowerIntensity);
            Assert.IsNull(control.Schedule);
            Object.DestroyImmediate(control);
        }

        [Test]
        public void CommitBarrier_OpenCheckout_DoesNotChangeSerializedPopulation()
        {
            var pop = new TrainingPopulation { ConfiguredSize = 3, EliteCount = 1 };
            pop.Initialize();
            var committed = pop.Checkout(out int idx);
            var fit = new TrainingFitness();
            fit.Add("Crystals", 4f, 1f);
            pop.ReturnFitness(idx, fit, committed);
            string disk = JsonUtility.ToJson(pop);
            float fitness = pop.Population[idx].Fitness;
            int cursor = pop.NextCheckoutIndex;
            Assert.IsFalse(pop.HasOpenCheckout);

            pop.Checkout(out _);
            pop.Checkout(out _);
            Assert.IsTrue(pop.HasOpenCheckout);
            Assert.AreEqual(disk, JsonUtility.ToJson(pop));
            pop.AbandonCheckout(2);
            Assert.IsFalse(pop.HasOpenCheckout);
            Assert.AreEqual(disk, JsonUtility.ToJson(pop));
            Assert.AreEqual(fitness, pop.Population[idx].Fitness);
            Assert.AreEqual(cursor, pop.NextCheckoutIndex);

            var restored = new TrainingPopulation();
            JsonUtility.FromJsonOverwrite(disk, restored);
            Assert.AreEqual(cursor, restored.NextCheckoutIndex);
            Assert.AreEqual(fitness, restored.Population[idx].Fitness, 0.0001f);
            Assert.IsFalse(restored.HasOpenCheckout);
            restored.Checkout(out int again);
            Assert.AreEqual(cursor % restored.PopulationSize, again);
        }

        [Test]
        public void Resume_MatchingScenario_DoesNotResetCompletedEpisodes()
        {
            var scenario = ScriptableObject.CreateInstance<TrainingScenarioSO>();
            scenario.ApplyCatalogDefaults(TrainingModeCatalog.Live[0]);
            var state = ScriptableObject.CreateInstance<TrainingSessionStateSO>();
            state.ResetForScenario(scenario.Key, scenario);
            var genome = state.Population.Checkout(out int idx);
            var fit = new TrainingFitness();
            fit.Add("Crystals", 3f, 1f);
            state.Population.ReturnFitness(idx, fit, genome);
            state.RecordEpisode(fit, genome);
            int episodes = state.EpisodesCompleted;
            float hof = state.HallOfFameBestFitness;
            string disk = JsonUtility.ToJson(state);

            var loaded = ScriptableObject.CreateInstance<TrainingSessionStateSO>();
            JsonUtility.FromJsonOverwrite(disk, loaded);
            Assert.AreEqual(episodes, loaded.EpisodesCompleted);
            Assert.AreEqual(hof, loaded.HallOfFameBestFitness, 0.0001f);
            Assert.AreEqual(scenario.Key, loaded.ScenarioKey);
            Assert.IsFalse(loaded.Population.HasOpenCheckout);

            var gd = ScriptableObject.CreateInstance<GameDataSO>();
            var runnerGo = new GameObject("resume-runner");
            var runner = runnerGo.AddComponent<TrainingSessionRunner>();
            runner.Configure(scenario, loaded, null, null, gd, null);
            runner.StartSession();
            Assert.AreEqual(episodes, loaded.EpisodesCompleted);
            Assert.AreEqual(hof, loaded.HallOfFameBestFitness, 0.0001f);
            runner.StopSession();
            Assert.AreEqual(episodes, loaded.EpisodesCompleted);
            Assert.AreEqual(hof, loaded.HallOfFameBestFitness, 0.0001f);
            Object.DestroyImmediate(runnerGo);
            Object.DestroyImmediate(state);
            Object.DestroyImmediate(loaded);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(gd);
        }

        [Test]
        public void SessionKey_MatchesOnVesselModeIdAndIntensity()
        {
            var scenario = ScriptableObject.CreateInstance<TrainingScenarioSO>();
            scenario.GameMode = GameModes.SkimRace;
            scenario.Vessel = VesselClassType.Squirrel;
            scenario.Intensity = 4;

            Assert.AreEqual("Squirrel_SkimRace_I4", scenario.Key);
            Assert.IsTrue(scenario.MatchesSessionKey("Squirrel_SkimRace_I4"));
            Assert.IsTrue(scenario.MatchesSessionKey("Squirrel_HexRace_I4"), "Mode 33 saved before the rename is the same session.");

            Assert.IsFalse(scenario.MatchesSessionKey("Manta_HexRace_I4"), "A different vessel resets.");
            Assert.IsFalse(scenario.MatchesSessionKey("Squirrel_Scurry_I4"), "A different mode id resets.");
            Assert.IsFalse(scenario.MatchesSessionKey("Squirrel_MultiplayerJoust_I4"), "A different legacy mode id resets.");
            Assert.IsFalse(scenario.MatchesSessionKey("Squirrel_HexRace_I3"), "A different intensity resets.");
            Assert.IsFalse(scenario.MatchesSessionKey(""));
            Assert.IsFalse(scenario.MatchesSessionKey(null));
            Assert.IsFalse(scenario.MatchesSessionKey("Squirrel_NotAMode_I4"));
            Assert.IsFalse(scenario.MatchesSessionKey("Squirrel_HexRace"));

            Assert.IsTrue(TrainingScenarioSO.TryParseKey("Squirrel_HexRace_I4", out var vessel, out var mode, out int intensity));
            Assert.AreEqual(VesselClassType.Squirrel, vessel);
            Assert.AreEqual(33, (int)mode);
            Assert.AreEqual(4, intensity);
            Object.DestroyImmediate(scenario);
        }

        [Test]
        public void Resume_LegacyModeKey_KeepsPopulation_OtherIdentityResets()
        {
            var scenario = ScriptableObject.CreateInstance<TrainingScenarioSO>();
            scenario.ApplyCatalogDefaults(TrainingModeCatalog.Live[0]);
            Assert.AreEqual(GameModes.SkimRace, scenario.GameMode);

            TrainingSessionStateSO MakeLegacyState()
            {
                var s = ScriptableObject.CreateInstance<TrainingSessionStateSO>();
                s.ResetForScenario(scenario.Key, scenario);
                var genome = s.Population.Checkout(out int idx);
                var fit = new TrainingFitness();
                fit.Add("Crystals", 3f, 1f);
                s.Population.ReturnFitness(idx, fit, genome);
                s.RecordEpisode(fit, genome);
                s.ScenarioKey = "Squirrel_HexRace_I4";
                return s;
            }

            int RunAndCountEpisodes(TrainingSessionStateSO s)
            {
                var gd = ScriptableObject.CreateInstance<GameDataSO>();
                var go = new GameObject("legacy-key-runner");
                var runner = go.AddComponent<TrainingSessionRunner>();
                runner.Configure(scenario, s, null, null, gd, null);
                runner.StartSession();
                int episodes = s.EpisodesCompleted;
                runner.StopSession();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(gd);
                return episodes;
            }

            var legacy = MakeLegacyState();
            Assert.AreEqual(1, RunAndCountEpisodes(legacy), "Squirrel_HexRace_I4 must resume under SkimRace.");
            Assert.AreEqual("Squirrel_HexRace_I4", legacy.ScenarioKey);

            var otherIntensity = MakeLegacyState();
            scenario.Intensity = 3;
            Assert.AreEqual(0, RunAndCountEpisodes(otherIntensity));
            scenario.Intensity = 4;

            var otherVessel = MakeLegacyState();
            scenario.Vessel = VesselClassType.Manta;
            Assert.AreEqual(0, RunAndCountEpisodes(otherVessel));
            scenario.Vessel = VesselClassType.Squirrel;

            var otherMode = MakeLegacyState();
            scenario.GameMode = GameModes.Scurry;
            Assert.AreEqual(0, RunAndCountEpisodes(otherMode));

            Object.DestroyImmediate(legacy);
            Object.DestroyImmediate(otherIntensity);
            Object.DestroyImmediate(otherVessel);
            Object.DestroyImmediate(otherMode);
            Object.DestroyImmediate(scenario);
        }

        /// <summary>
        /// The checked-in overnight session must resume under today's SkimRace scenario.
        /// Accepts either the current key or a pre-rename HexRace key via MatchesSessionKey.
        /// Runs the runner on an in-memory copy so the asset on disk is untouched.
        /// </summary>
        [Test]
        public void Resume_SavedSkimRaceSquirrelSession_FindsCheckedInPopulation()
        {
            const string statePath = "Assets/_SO_Assets/AI Training/SessionState.asset";
            const string archivePath = "Assets/_SO_Assets/AI Training/Archive.asset";
            var row = TrainingModeCatalog.Live[0];
            var scenario = AssetDatabase.LoadAssetAtPath<TrainingScenarioSO>(row.ScenarioPath);
            var saved = AssetDatabase.LoadAssetAtPath<TrainingSessionStateSO>(statePath);
            var archive = AssetDatabase.LoadAssetAtPath<TrainingArchiveSO>(archivePath);
            Assert.IsNotNull(scenario, row.ScenarioPath);
            Assert.IsNotNull(saved, statePath);
            Assert.IsNotNull(archive, archivePath);

            Assert.AreEqual(33, (int)scenario.GameMode);
            Assert.AreEqual(VesselClassType.Squirrel, scenario.Vessel);
            Assert.AreEqual(4, scenario.Intensity);
            Assert.AreEqual("Squirrel_SkimRace_I4", scenario.Key);
            Assert.IsFalse(string.IsNullOrEmpty(saved.ScenarioKey));
            Assert.IsTrue(scenario.MatchesSessionKey(saved.ScenarioKey),
                $"Checked-in SessionState key '{saved.ScenarioKey}' must match scenario {scenario.Key}.");

            Assert.AreEqual(24, saved.EpisodesCompleted);
            Assert.AreEqual(0, saved.Population.Generation);
            Assert.AreEqual(277.09552f, saved.HallOfFameBestFitness, 0.0001f);
            Assert.IsNotNull(saved.HallOfFameBest);

            var entry = archive.Find(VesselClassType.Squirrel, GameModes.SkimRace, 4);
            Assert.IsNotNull(entry);
            Assert.AreEqual(33, (int)entry.GameMode);
            Assert.AreEqual(4, entry.Intensity);
            Assert.AreEqual(277.09552f, entry.Fitness, 0.0001f);

            string keyBefore = saved.ScenarioKey;
            var copy = Object.Instantiate(saved);
            int populationSize = copy.Population.PopulationSize;
            string hofBefore = JsonUtility.ToJson(copy.HallOfFameBest);
            var gd = ScriptableObject.CreateInstance<GameDataSO>();
            var go = new GameObject("saved-session-runner");
            var runner = go.AddComponent<TrainingSessionRunner>();
            runner.Configure(scenario, copy, null, null, gd, null);
            runner.StartSession();
            runner.StopSession();

            Assert.AreEqual(24, copy.EpisodesCompleted);
            Assert.AreEqual(0, copy.Population.Generation);
            Assert.AreEqual(populationSize, copy.Population.PopulationSize);
            Assert.AreEqual(277.09552f, copy.HallOfFameBestFitness, 0.0001f);
            Assert.AreEqual(hofBefore, JsonUtility.ToJson(copy.HallOfFameBest));
            Assert.AreEqual(keyBefore, copy.ScenarioKey,
                "Resume must keep the on-disk key (not rewrite or wipe the population).");
            Assert.IsTrue(scenario.MatchesSessionKey(copy.ScenarioKey));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(gd);
            Object.DestroyImmediate(copy);
        }

        [Test]
        public void Watchdog_Records_AndRunnerDoesNotLoadScene()
        {
            string path = System.IO.Path.Combine(Application.dataPath,
                "_Scripts/Utility/AITraining/Runner/TrainingSessionRunner.cs");
            string text = System.IO.File.ReadAllText(path);
            StringAssert.Contains("EndEpisodeInternal(timedOut: true", text);
            StringAssert.DoesNotContain("ResetForReplay", text);
            StringAssert.DoesNotContain("SceneManager.LoadScene", text);
            StringAssert.Contains("Time.unscaledTime - _watchdogStartTime", text);
        }

        [Test]
        public void EndEpisode_ClearsContextThatHarvestMustReadFirst()
        {
            var go = new GameObject("pilot-context");
            var pilot = go.AddComponent<TrainingPilot>();
            pilot.BeginEpisode();
            Assert.IsNotNull(pilot.GetCurrentContextOrNull());
            pilot.EndEpisode();
            Assert.IsNull(pilot.GetCurrentContextOrNull());
            Object.DestroyImmediate(go);
        }

        [Test]
        public void FitnessHarvest_ReadsContextBeforeEndEpisode()
        {
            string path = System.IO.Path.Combine(Application.dataPath,
                "_Scripts/Utility/AITraining/Runner/TrainingSessionRunner.cs");
            string text = System.IO.File.ReadAllText(path);
            int start = text.IndexOf("void EndEpisodeInternal", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int end = text.IndexOf("void RequestMatchReplay", start, System.StringComparison.Ordinal);
            Assert.Greater(end, start);
            string body = text.Substring(start, end - start);
            int harvest = body.IndexOf("var ctx = pilot.GetCurrentContextOrNull();", System.StringComparison.Ordinal);
            int close = body.IndexOf("pilot.EndEpisode()", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(harvest, 0);
            Assert.Greater(close, harvest);
        }

        [Test]
        public void BatchTimeScale_MissingOrOne_LeavesClockAlone()
        {
            Assert.AreEqual(1f, TrainingControlSO.ResolveTimeScale(0f));
            Assert.AreEqual(1f, TrainingControlSO.ResolveTimeScale(1f));
            Assert.AreEqual(4f, TrainingControlSO.ResolveTimeScale(4f));
            Assert.AreEqual(TrainingControlSO.MaxSimulationTimeScale, TrainingControlSO.ResolveTimeScale(100f));
        }

        [Test]
        public void Dither_Level4_IsIdentity_AndLowerTiersOnlyDegradeTheDecision()
        {
            var dither = new IntensityDitherer();
            var level4 = dither.LevelsByIntensity[3];
            Assert.AreEqual(0f, level4.DropoutChance);
            Assert.AreEqual(0f, level4.NoiseAmplitude);
            Assert.AreEqual(0f, level4.ReactionDelaySeconds);
            Assert.AreEqual(0f, level4.AbilitySkipChance);
            Assert.AreEqual(1f, level4.ThrottleScale);
            for (int i = 0; i < 3; i++)
            {
                var level = dither.LevelsByIntensity[i];
                Assert.Greater(level.DropoutChance, 0f, "intensity " + (i + 1));
                Assert.Greater(level.NoiseAmplitude, 0f, "intensity " + (i + 1));
                Assert.Greater(level.ReactionDelaySeconds, 0f, "intensity " + (i + 1));
                Assert.Greater(level.AbilitySkipChance, 0f, "intensity " + (i + 1));
                Assert.Less(level.ThrottleScale, 1f, "intensity " + (i + 1));
            }
        }

        [Test]
        public void Deploy_DitherDoesNotRewriteStoredGenes()
        {
            var arch = ScriptableObject.CreateInstance<TrainingArchiveSO>();
            var stored = TrainingGenome.FromRegistryDefaults();
            stored.Set("a", 0.9f);
            arch.Upsert(CosmicShore.Data.VesselClassType.Squirrel, GameModes.SkimRace, 4, stored, 1f, 0);
            var lower = TrainingGenome.FromRegistryDefaults();
            lower.Set("a", 0.2f);
            arch.Upsert(CosmicShore.Data.VesselClassType.Squirrel, GameModes.SkimRace, 1, lower, 1f, 0);

            var flown = ArchiveDeployment.ResolveGenome(arch, GameModes.SkimRace,
                CosmicShore.Data.VesselClassType.Sparrow, 1, useStoredGenomeForLowerIntensity: false);
            Assert.AreEqual(0.9f, flown.Get("a"), 0.0001f);
            flown.Set("a", 0.1f);
            Assert.AreEqual(0.9f, arch.Find(CosmicShore.Data.VesselClassType.Squirrel, GameModes.SkimRace, 4).Genome.Get("a"), 0.0001f);
            Assert.AreEqual(1, ArchiveDeployment.FlownIntensity(1, false));
            Assert.AreEqual(4, ArchiveDeployment.FlownIntensity(4, false));

            var opted = ArchiveDeployment.ResolveGenome(arch, GameModes.SkimRace,
                CosmicShore.Data.VesselClassType.Squirrel, 1, useStoredGenomeForLowerIntensity: true);
            Assert.AreEqual(0.2f, opted.Get("a"), 0.0001f);
            Assert.AreEqual(4, ArchiveDeployment.FlownIntensity(1, true));
            Assert.IsNull(ArchiveDeployment.ResolveGenome(arch, GameModes.SkimRace,
                CosmicShore.Data.VesselClassType.Squirrel, 2, useStoredGenomeForLowerIntensity: true));
            Object.DestroyImmediate(arch);
        }

        [Test]
        public void Deploy_LockedModeUsesCatalogHull_UnlockedModeUsesLiveClass()
        {
            var arch = ScriptableObject.CreateInstance<TrainingArchiveSO>();
            var squirrel = TrainingGenome.FromRegistryDefaults();
            squirrel.Set("a", 0.8f);
            var manta = TrainingGenome.FromRegistryDefaults();
            manta.Set("a", 0.3f);
            arch.Upsert(CosmicShore.Data.VesselClassType.Squirrel, GameModes.SkimRace, 4, squirrel, 1f, 0);
            arch.Upsert(CosmicShore.Data.VesselClassType.Manta, GameModes.Scurry, 4, manta, 1f, 0);
            arch.Upsert(CosmicShore.Data.VesselClassType.Squirrel, GameModes.Scurry, 4, squirrel, 1f, 0);

            Assert.AreEqual(CosmicShore.Data.VesselClassType.Squirrel,
                ArchiveDeployment.ResolveVessel(GameModes.SkimRace, CosmicShore.Data.VesselClassType.Sparrow));
            var locked = ArchiveDeployment.ResolveGenome(arch, GameModes.SkimRace,
                CosmicShore.Data.VesselClassType.Sparrow, 4, false);
            Assert.AreEqual(0.8f, locked.Get("a"), 0.0001f);

            Assert.AreEqual(CosmicShore.Data.VesselClassType.Manta,
                ArchiveDeployment.ResolveVessel(GameModes.Scurry, CosmicShore.Data.VesselClassType.Manta));
            var unlocked = ArchiveDeployment.ResolveGenome(arch, GameModes.Scurry,
                CosmicShore.Data.VesselClassType.Manta, 2, false);
            Assert.AreEqual(0.3f, unlocked.Get("a"), 0.0001f);

            Assert.IsNull(ArchiveDeployment.ResolveGenome(arch, GameModes.Scurry,
                CosmicShore.Data.VesselClassType.Sparrow, 4, false));
            Assert.IsNull(ArchiveDeployment.ResolveGenome(null, GameModes.SkimRace,
                CosmicShore.Data.VesselClassType.Squirrel, 4, false));
            Object.DestroyImmediate(arch);
        }

        [Test]
        public void Deploy_InstallStopsAIPilotBeforeTheTrainedPilotStarts()
        {
            string path = System.IO.Path.Combine(Application.dataPath,
                "_Scripts/Utility/AITraining/Pilot/ArchiveDeployment.cs");
            string text = System.IO.File.ReadAllText(path);
            int stop = text.IndexOf("StopAIPilot", System.StringComparison.Ordinal);
            int off = text.IndexOf("enabled = false", System.StringComparison.Ordinal);
            int begin = text.IndexOf("BeginEpisode", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(stop, 0);
            Assert.Less(stop, begin);
            Assert.Less(off, begin);

            string service = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,
                "_Scripts/Utility/AITraining/Pilot/TrainingDeploymentService.cs"));
            StringAssert.Contains("GetComponent<TrainingPilot>() != null) return", service);
            StringAssert.Contains("TryInstall", service);
            string bridge = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,
                "_Scripts/Utility/AITraining/Pilot/TrainingAIDeploymentBridge.cs"));
            StringAssert.Contains("GetComponent<TrainingPilot>() != null) return", bridge);
        }

        [Test]
        public void Schedule_TwoModes_AdvanceOnEpisodes_ThenFinish()
        {
            var schedule = TwoSlotSchedule(hexEpisodes: 2, hexHours: 4f, joustEpisodes: 1, joustHours: 2f);
            var state = ScriptableObject.CreateInstance<TrainingSessionStateSO>();

            var one = TrainingSchedule.AfterEpisode(schedule, 0, 1, 0d);
            Assert.AreEqual(TrainingSchedule.Action.Continue, one.Action);
            Assert.AreEqual(0, one.Index);

            var two = TrainingSchedule.AfterEpisode(schedule, 0, 2, 10d);
            Assert.AreEqual(TrainingSchedule.Action.Advance, two.Action);
            Assert.AreEqual(1, two.Index);
            TrainingSchedule.Apply(state, two);
            Assert.AreEqual(1, state.ScheduleIndex);
            Assert.AreEqual(0, state.ScheduleSlotEpisodes);
            Assert.AreEqual(0d, state.ScheduleSlotElapsedSeconds);

            var done = TrainingSchedule.AfterEpisode(schedule, state.ScheduleIndex, 1, 0d);
            Assert.AreEqual(TrainingSchedule.Action.Finished, done.Action);
            TrainingSchedule.Apply(state, done);
            Assert.AreEqual(2, state.ScheduleIndex);

            Object.DestroyImmediate(schedule);
            Object.DestroyImmediate(state);
        }

        [Test]
        public void Schedule_WallClock_AdvancesWithZeroEpisodes()
        {
            var schedule = TwoSlotSchedule(hexEpisodes: 0, hexHours: 4f, joustEpisodes: 1, joustHours: 2f);
            var hit = TrainingSchedule.AfterEpisode(schedule, 0, 0, 4d * 3600d);
            Assert.AreEqual(TrainingSchedule.Action.Advance, hit.Action);
            Assert.AreEqual("wall-clock budget", hit.Reason);

            var open = TrainingSchedule.AfterEpisode(schedule, 0, 0, 4d * 3600d - 1d);
            Assert.AreEqual(TrainingSchedule.Action.Continue, open.Action);
            Object.DestroyImmediate(schedule);
        }

        [Test]
        public void Schedule_HaltResume_KeepsCursorAcrossPopulationReset()
        {
            var state = ScriptableObject.CreateInstance<TrainingSessionStateSO>();
            state.ScheduleIndex = 1;
            state.ScheduleSlotEpisodes = 3;
            state.ScheduleSlotElapsedSeconds = 100d;
            state.ScheduleSlotAttempts = 1;

            var scenario = ScriptableObject.CreateInstance<TrainingScenarioSO>();
            scenario.GameMode = GameModes.Joust;
            scenario.Vessel = VesselClassType.Squirrel;
            scenario.Intensity = 4;
            state.ResetForScenario(scenario.Key, scenario);

            Assert.AreEqual(1, state.ScheduleIndex);
            Assert.AreEqual(3, state.ScheduleSlotEpisodes);
            Assert.AreEqual(100d, state.ScheduleSlotElapsedSeconds);
            Assert.AreEqual(1, state.ScheduleSlotAttempts);
            Assert.AreEqual(0, state.EpisodesCompleted);

            string json = JsonUtility.ToJson(state);
            var copy = ScriptableObject.CreateInstance<TrainingSessionStateSO>();
            JsonUtility.FromJsonOverwrite(json, copy);
            Assert.AreEqual(1, copy.ScheduleIndex);
            Assert.AreEqual(3, copy.ScheduleSlotEpisodes);
            Assert.AreEqual(100d, copy.ScheduleSlotElapsedSeconds, 0.001d);
            Assert.AreEqual(1, copy.ScheduleSlotAttempts);

            var schedule = TwoSlotSchedule(hexEpisodes: 2, hexHours: 4f, joustEpisodes: 10, joustHours: 4f);
            var still = TrainingSchedule.AfterEpisode(
                schedule, copy.ScheduleIndex, copy.ScheduleSlotEpisodes, copy.ScheduleSlotElapsedSeconds);
            Assert.AreEqual(TrainingSchedule.Action.Continue, still.Action);
            Assert.AreEqual(1, still.Index);

            Object.DestroyImmediate(state);
            Object.DestroyImmediate(copy);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(schedule);
        }

        [Test]
        public void Schedule_Failure_RetriesThenSkips_AndDoesNotThrow()
        {
            var schedule = TwoSlotSchedule(hexEpisodes: 2, hexHours: 4f, joustEpisodes: 1, joustHours: 2f);
            schedule.Slots[0].MaxAttempts = 2;
            schedule.Slots[1].MaxAttempts = 2;
            var state = ScriptableObject.CreateInstance<TrainingSessionStateSO>();
            state.ScheduleSlotEpisodes = 4;
            state.ScheduleSlotElapsedSeconds = 50d;

            var first = TrainingSchedule.AfterFailure(schedule, 0, state.ScheduleSlotAttempts);
            Assert.AreEqual(TrainingSchedule.Action.RetrySame, first.Action);
            TrainingSchedule.Apply(state, first);
            Assert.AreEqual(0, state.ScheduleIndex);
            Assert.AreEqual(1, state.ScheduleSlotAttempts);
            Assert.AreEqual(4, state.ScheduleSlotEpisodes);
            Assert.AreEqual(50d, state.ScheduleSlotElapsedSeconds);

            var second = TrainingSchedule.AfterFailure(schedule, state.ScheduleIndex, state.ScheduleSlotAttempts);
            Assert.AreEqual(TrainingSchedule.Action.Advance, second.Action);
            TrainingSchedule.Apply(state, second);
            Assert.AreEqual(1, state.ScheduleIndex);
            Assert.AreEqual(0, state.ScheduleSlotAttempts);
            Assert.AreEqual(0, state.ScheduleSlotEpisodes);

            var last = TrainingSchedule.AfterFailure(schedule, 1, 1);
            Assert.AreEqual(TrainingSchedule.Action.Finished, last.Action);

            var missing = ScriptableObject.CreateInstance<TrainingScheduleSO>();
            missing.Slots.Add(new TrainingScheduleSO.Slot());
            var skipped = TrainingSchedule.AfterEpisode(missing, 0, 0, 0d);
            Assert.AreEqual(TrainingSchedule.Action.Finished, skipped.Action);

            Object.DestroyImmediate(schedule);
            Object.DestroyImmediate(state);
            Object.DestroyImmediate(missing);
        }

        [Test]
        public void Schedule_ArchiveBuckets_StayIsolatedAcrossAdvance()
        {
            var arch = ScriptableObject.CreateInstance<TrainingArchiveSO>();
            var hexGenome = TrainingGenome.FromRegistryDefaults();
            hexGenome.Set("a", 0.2f);
            var joustGenome = TrainingGenome.FromRegistryDefaults();
            joustGenome.Set("a", 0.8f);
            arch.Upsert(VesselClassType.Squirrel, GameModes.SkimRace, 4, hexGenome, 1f, 3);
            arch.Upsert(VesselClassType.Squirrel, GameModes.Joust, 4, joustGenome, 2f, 5);

            string hexKey = TrainingArchiveSO.MakeKey(VesselClassType.Squirrel, GameModes.SkimRace, 4);
            string joustKey = TrainingArchiveSO.MakeKey(VesselClassType.Squirrel, GameModes.Joust, 4);
            Assert.AreNotEqual(hexKey, joustKey);

            var schedule = TwoSlotSchedule(2, 4f, 1, 2f);
            var state = ScriptableObject.CreateInstance<TrainingSessionStateSO>();
            var advance = TrainingSchedule.AfterEpisode(schedule, 0, 2, 0d);
            TrainingSchedule.Apply(state, advance);

            Assert.AreEqual(0.2f, arch.Find(VesselClassType.Squirrel, GameModes.SkimRace, 4).Genome.Get("a"), 0.0001f);
            Assert.AreEqual(0.8f, arch.Find(VesselClassType.Squirrel, GameModes.Joust, 4).Genome.Get("a"), 0.0001f);
            Assert.AreEqual(2, arch.Entries.Count);

            Object.DestroyImmediate(arch);
            Object.DestroyImmediate(schedule);
            Object.DestroyImmediate(state);
        }

        [Test]
        public void Schedule_RunnerHandsOffWithoutLoadingAScene()
        {
            string runner = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,
                "_Scripts/Utility/AITraining/Runner/TrainingSessionRunner.cs"));
            StringAssert.Contains("BindSchedule", runner);
            StringAssert.Contains("Relaunch", runner);
            StringAssert.Contains("NoteScheduleFailure", runner);
            StringAssert.DoesNotContain("SceneManager.LoadScene", runner);

            string launcher = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,
                "_Scripts/Utility/AITraining/Runner/TrainingAutoLauncher.cs"));
            StringAssert.Contains("public void Relaunch()", launcher);
            StringAssert.Contains("InvokeGameLaunch", launcher);
        }

        static TrainingScheduleSO TwoSlotSchedule(int hexEpisodes, float hexHours, int joustEpisodes, float joustHours)
        {
            var schedule = ScriptableObject.CreateInstance<TrainingScheduleSO>();
            var hex = ScriptableObject.CreateInstance<TrainingScenarioSO>();
            hex.GameMode = GameModes.SkimRace;
            hex.Vessel = VesselClassType.Squirrel;
            hex.Intensity = 4;
            var joust = ScriptableObject.CreateInstance<TrainingScenarioSO>();
            joust.GameMode = GameModes.Joust;
            joust.Vessel = VesselClassType.Squirrel;
            joust.Intensity = 4;
            schedule.Slots.Add(new TrainingScheduleSO.Slot
            {
                Scenario = hex,
                TargetEpisodes = hexEpisodes,
                WallClockHours = hexHours,
                MaxAttempts = 2,
            });
            schedule.Slots.Add(new TrainingScheduleSO.Slot
            {
                Scenario = joust,
                TargetEpisodes = joustEpisodes,
                WallClockHours = joustHours,
                MaxAttempts = 2,
            });
            return schedule;
        }
    }
}
#endif
