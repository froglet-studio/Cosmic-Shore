using CosmicShore.Gameplay;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// THE searchable surface of the shipped AIPilot, expressed as genes.
    ///
    /// This is the architecture decision that defines the framework (the
    /// "parallel-pilot trap", recorded in the README): training TUNES the real
    /// pilot — the one that carries the orbit break, objective scoring with
    /// commitment hysteresis, the drift commit loop and the aim telegraph —
    /// rather than replacing it with a simpler steering stack that would regress
    /// all of the above and rot as the real pilot keeps improving.
    ///
    /// Three modules give the search three kinds of freedom:
    ///
    ///   PilotTuning  (always on)  — continuous dials: skill, throttle band,
    ///                               orbit-break geometry, objective commitment.
    ///   PilotStyle   (toggleable) — discrete personality: ram, drift,
    ///                               approach-run objective preference. A genome
    ///                               with the module OFF keeps the vessel's
    ///                               authored style, so "do nothing" is always
    ///                               in the search space.
    ///   AbilityTempo (toggleable) — scales each authored ability's Duration and
    ///                               Cooldown, so evolution finds each vessel's
    ///                               cadence without knowing what the abilities are.
    ///
    /// Structural mutation flipping PilotStyle/AbilityTempo on and off is what
    /// "learning new behaviors" means here: honest, bounded behavioral variety
    /// on top of a pilot that always flies competently.
    /// </summary>
    public static class PilotTuningGenes
    {
        public const string ModuleTuning = "PilotTuning";
        public const string ModuleStyle = "PilotStyle";
        public const string ModuleTempo = "AbilityTempo";

        public const string GeneSkill = "pilot.skill";
        public const string GeneThrottleBase = "pilot.throttle_base";
        public const string GeneThrottleRamp = "pilot.throttle_ramp";
        public const string GeneApproachRun = "pilot.approach_run_seconds";
        public const string GeneOrbitAwayBias = "pilot.orbit_away_bias";
        public const string GeneCaptureRadius = "pilot.capture_radius";
        public const string GeneSwitchImprovement = "pilot.switch_improvement";

        public const string GeneRam = "style.ram";
        public const string GeneDrift = "style.drift";
        public const string GenePreferRunDistance = "style.prefer_run_distance";

        public const string GeneAbilityDurationScale = "tempo.ability_duration_scale";
        public const string GeneAbilityCooldownScale = "tempo.ability_cooldown_scale";

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void EditorInit() => EnsureRegistered();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RuntimeInit() => EnsureRegistered();

        public static void EnsureRegistered()
        {
            // Keyed on registry presence rather than a static latch so tests that
            // Clear() the registry can re-register the real gene set.
            if (GeneRegistry.Modules.ContainsKey(ModuleTuning)) return;

            // Continuous dials. Ranges bracket the shipped inspector defaults so the
            // authored pilot is always inside the search space, never at its edge.
            GeneRegistry.Register(ModuleTuning, new GeneSpec(GeneSkill, 0.35f, 1f, 1f));
            GeneRegistry.Register(ModuleTuning, new GeneSpec(GeneThrottleBase, 0.4f, 1f, 0.6f));
            GeneRegistry.Register(ModuleTuning, new GeneSpec(GeneThrottleRamp, 0f, 0.01f, 0.001f));
            GeneRegistry.Register(ModuleTuning, new GeneSpec(GeneApproachRun, 0.8f, 3f, 1.5f));
            GeneRegistry.Register(ModuleTuning, new GeneSpec(GeneOrbitAwayBias, 0f, 1.2f, 0.35f));
            GeneRegistry.Register(ModuleTuning, new GeneSpec(GeneCaptureRadius, 10f, 40f, 18f));
            GeneRegistry.Register(ModuleTuning, new GeneSpec(GeneSwitchImprovement, 0.5f, 1f, 0.75f));

            // Style toggles, expressed as 0..1 genes thresholded at 0.5 so crossover
            // and mutation act on them exactly like every other gene.
            GeneRegistry.Register(ModuleStyle, new GeneSpec(GeneRam, 0f, 1f, 0.25f), defaultEnabled: false);
            GeneRegistry.Register(ModuleStyle, new GeneSpec(GeneDrift, 0f, 1f, 0.75f), defaultEnabled: false);
            GeneRegistry.Register(ModuleStyle, new GeneSpec(GenePreferRunDistance, 0f, 1f, 0.5f), defaultEnabled: false);

            GeneRegistry.Register(ModuleTempo, new GeneSpec(GeneAbilityDurationScale, 0.5f, 2f, 1f), defaultEnabled: false);
            GeneRegistry.Register(ModuleTempo, new GeneSpec(GeneAbilityCooldownScale, 0.4f, 2.5f, 1f), defaultEnabled: false);
        }

        /// <summary>
        /// Maps a genome onto the pilot's tuning surface. Modules that are disabled
        /// contribute NOTHING (null fields), which ApplyExternalTuning reads as
        /// "keep the authored value" — so partial genomes are always safe.
        /// </summary>
        public static AIPilot.ExternalTuning ToTuning(TrainingGenome g)
        {
            var t = new AIPilot.ExternalTuning();
            if (g == null) return t;

            if (g.IsModuleEnabled(ModuleTuning))
            {
                t.SkillLevel = g.Get(GeneSkill);
                t.ThrottleBase = g.Get(GeneThrottleBase);
                t.ThrottleRamp = g.Get(GeneThrottleRamp);
                t.ApproachRunSeconds = g.Get(GeneApproachRun);
                t.OrbitBreakAwayBias = g.Get(GeneOrbitAwayBias);
                t.ObjectiveCaptureRadius = g.Get(GeneCaptureRadius);
                t.ObjectiveSwitchImprovement = g.Get(GeneSwitchImprovement);
            }

            if (g.IsModuleEnabled(ModuleStyle))
            {
                t.Ram = g.Get(GeneRam) >= 0.5f;
                t.Drift = g.Get(GeneDrift) >= 0.5f;
                t.PreferApproachRunDistance = g.Get(GenePreferRunDistance) >= 0.5f;
            }

            if (g.IsModuleEnabled(ModuleTempo))
            {
                t.AbilityDurationScale = g.Get(GeneAbilityDurationScale);
                t.AbilityCooldownScale = g.Get(GeneAbilityCooldownScale);
            }

            return t;
        }

        /// <summary>
        /// A human-readable personality name derived from the genome's dominant
        /// traits. Purely cosmetic — shown in the archive roster and match logs so
        /// "who am I flying against tonight?" has an answer better than a hash.
        /// Stable for a given genome (no randomness).
        /// </summary>
        public static string PersonalityName(TrainingGenome g)
        {
            if (g == null) return "Blank";

            bool styled = g.IsModuleEnabled(ModuleStyle);
            bool ram = styled && g.Get(GeneRam) >= 0.5f;
            bool drift = styled && g.Get(GeneDrift) >= 0.5f;
            float skill = g.Get(GeneSkill);
            float throttle = g.Get(GeneThrottleBase);
            float cooldown = g.IsModuleEnabled(ModuleTempo) ? g.Get(GeneAbilityCooldownScale) : 1f;

            string adjective =
                skill >= 0.9f ? "Ace" :
                skill >= 0.7f ? "Steady" :
                "Rookie";

            string noun =
                ram ? "Rammer" :
                drift ? "Drifter" :
                cooldown <= 0.7f ? "Stormcaller" :
                throttle >= 0.85f ? "Speedster" :
                throttle <= 0.5f ? "Cruiser" :
                "Racer";

            return $"{adjective} {noun}";
        }
    }
}
