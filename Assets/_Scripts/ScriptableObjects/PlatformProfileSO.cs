using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Everything that differs between hardware tiers, for ONE <see cref="DeviceTier"/>. The three
    /// profiles (Desktop, MobileHigh, MobileLow) are held by <see cref="PlatformProfileSetSO"/>, and
    /// the device's one is <c>PlatformProfile.Current</c>. This replaces the Android strip branch's
    /// compile-time <c>PerfStrip</c> flags: a per-platform choice is a field here, read at runtime,
    /// so one build serves every platform (<c>Docs/PLATFORM_UNIFICATION.md</c> §3).
    ///
    /// It carries the first-run graphics recommendation (Step 3), the per-tier render choices
    /// (Step 4) and the per-tier content choices (Step 5). Every field's DEFAULT is "no change", and the
    /// Desktop and MobileHigh assets keep those defaults, so Windows and iOS behave exactly as
    /// bleeding-edge did before the field existed - only an asset that sets a field changes anything.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlatformProfile",
        menuName = "ScriptableObjects/Platform/" + nameof(PlatformProfileSO))]
    public class PlatformProfileSO : ScriptableObject
    {
        [Header("First-run graphics recommendation (SettingsAutoDetector)")]
        [Tooltip("Use the capability-score heuristic (CPU cores, RAM, VRAM, display pixels) - what " +
                 "every device got before tiers existed. Leave ON for Desktop. MobileHigh keeps it ON " +
                 "so iPhones are unchanged; MobileLow turns it OFF, because core count ranks a budget " +
                 "8-core phone ABOVE an iPhone. When OFF, the fields below are the recommendation.")]
        [SerializeField] bool useCapabilityHeuristic = true;

        [Tooltip("Quality preset (QualitySettings level) recommended on first run.")]
        [SerializeField] QualityPresetSetting preset = QualityPresetSetting.VeryLow;

        [Tooltip("Rendered pixels per frame before upscaling. A display above it gets a render-scale " +
                 "reduction to fit; 1,300,000 puts a 1080x2400 phone at ~71%.")]
        [SerializeField, Min(0)] int pixelBudget = 1_300_000;

        [Tooltip("Never recommend a render scale below this, whatever the display.")]
        [SerializeField, Range(25, 100)] int minRenderScalePercent = 50;

        [Tooltip("Upscaler used when the recommended render scale is below 100%. Linear is one " +
                 "bilinear blit; FSR adds an ALU-heavy pass a low-end mobile GPU pays for.")]
        [SerializeField] UpscalingSetting upscalingWhenScaled = UpscalingSetting.Linear;

        [Tooltip("Anti-aliasing recommended on first run.")]
        [SerializeField] AntiAliasingSetting antiAliasing = AntiAliasingSetting.FXAA;

        [Tooltip("Frame-rate cap recommended on first run: the display's refresh rate, but never " +
                 "above this.")]
        [SerializeField, Min(1)] int maxTargetFrameRate = 60;

        [Header("Render (applied at runtime on this tier)")]
        [Tooltip("Turn the active URP asset's HDR off on this tier (GraphicsSettingsApplier). An LDR " +
                 "colour buffer is half the bandwidth of an HDR one, and lets 4x MSAA sit in a mobile " +
                 "GPU's tile memory. Bloom still runs: the gameplay profile's threshold 0.2 / clamp 0.5 " +
                 "never reads above the LDR range. Values above 1 (HDR crystal cores, emissive " +
                 "highlights) clamp, so they bloom less. Off = the URP asset's own setting.")]
        [SerializeField] bool disableHdr;

        [Tooltip("Skybox materials replaced on this tier: when a scene's RenderSettings.skybox is an " +
                 "entry's authored material, the replacement draws instead (PlatformRenderApplier, on " +
                 "every scene load). MobileLow swaps the 767-line procedural HyperSea sky for its offline " +
                 "bake (Tools/Build/bake_static_skybox.py) - one texture sample per pixel. The replacement " +
                 "is a Resources PATH, loaded only when it is used: every tier loads every profile, so " +
                 "a reference here would keep the bake's texture resident on tiers that never draw it.")]
        [SerializeField] SkyboxReplacement[] skyboxReplacements = Array.Empty<SkyboxReplacement>();

        [Tooltip("Cap on a CapsuleMembrane's icosphere subdivision level on this tier; -1 = no cap. " +
                 "The membrane then draws only the capsules of that level (642 at 3 instead of 2,562 " +
                 "at 4) - the same seeded layout a membrane authored at that level would have, played " +
                 "from the same baked animation. Radius, and so every gameplay reader of it, is untouched.")]
        [SerializeField, Range(-1, 4)] int membraneMaxSubdivisions = -1;

        [Tooltip("Ceiling on a wormhole mouth's EXACT-view render resolution, as a fraction of the " +
                 "gameplay camera's (WormholeView - the Wormhole cell's pair and every Butterfly fold's " +
                 "pair; the field keeps its fold-gate name so the shipped tier assets need no migration). " +
                 "The view renders only the mouth's own footprint, so this only bites when a mouth fills " +
                 "the screen - the approach and the carry through. 1 = no cap beyond the mouth's own " +
                 "render scale.")]
        [SerializeField, Range(0.1f, 1f)] float foldGateWindowMaxRenderScale = 1f;

        [Header("Content: trails (applied at runtime on this tier)")]
        [Tooltip("The Menu_Main autopilot (the lava lamp behind the menu) lays no trail on this tier. " +
                 "Creation-side only - a hold of its own (VesselPrismController.SetTierHold), separate " +
                 "from the pen folds and painting use; nothing already laid is removed. Freestyle and " +
                 "a live mode preview lay trail as usual.")]
        [SerializeField] bool menuAutopilotLaysNoTrail;

        [Tooltip("Freestyle trail WAITS while the pilot's cell holds more than this many live prisms " +
                 "(the pen lifts), and comes back down at the resume count below once the food web has " +
                 "grazed room. A spawner that waits - never a cap. 0 = the trail never waits.")]
        [SerializeField, Min(0)] int freestyleCellPrismBudget;

        [Tooltip("The freestyle pen comes back down when the cell drops to this many live prisms " +
                 "(hysteresis under the budget above).")]
        [SerializeField, Min(0)] int freestyleCellPrismResume;

        [Tooltip("Skim Race trail cap on this tier: each vessel keeps its share of a race-wide budget " +
                 "of trail prisms; past it the OLDEST prism withers away (RaceTrailCap). An owner-" +
                 "authorized exception to the no-trail-cap law, recorded in Docs/ECOSYSTEM.md §0. " +
                 "Race budget 0 = no cap.")]
        [SerializeField] RaceTrailBudget skimRaceTrail;

        [Tooltip("Joust trail cap on this tier - the same mechanism and exception as Skim Race. " +
                 "Race budget 0 = no cap.")]
        [SerializeField] RaceTrailBudget joustTrail;

        [Header("Content: menu and HUD (applied at runtime on this tier)")]
        [Tooltip("While the pilot flies freestyle, the menu's screen roots and nav bar are " +
                 "DEACTIVATED, not just faded (a CanvasGroup at alpha 0 still runs every Update, " +
                 "coroutine and canvas rebuild), and restored exactly as they were on exit.")]
        [SerializeField] bool deactivateMenuWhileFlying;

        [Tooltip("The HUD top bar's domain glow rests at its tint instead of breathing forever (an " +
                 "endless alpha tween re-batches the HUD canvas every frame). It still punches on a " +
                 "score change.")]
        [SerializeField] bool quietScoreGlow;

        [Tooltip("Cells spawn no cytoplasm motes on this tier (~300 transparent mote objects per " +
                 "cell). Cosmetic only: not mass, not lifeforms.")]
        [SerializeField] bool disableCytoplasm;

        [Header("Content: Wanderway (applied at runtime on this tier)")]
        [Tooltip("Overrides on the Wanderway belt's budget, applied to the config WanderToy builds " +
                 "from its settings asset - the asset itself is untouched, so other tiers keep it. " +
                 "-1 = keep the asset's value.")]
        [SerializeField] WanderwayBudget wanderwayBudget = WanderwayBudget.KeepAll;

        /// <summary>This profile's first-run graphics recommendation, as plain data.</summary>
        public PlatformAutoDetect AutoDetect => new(useCapabilityHeuristic, preset, pixelBudget,
            minRenderScalePercent, upscalingWhenScaled, antiAliasing, maxTargetFrameRate);

        /// <summary>True when this tier turns the URP asset's HDR off.</summary>
        public bool DisableHdr => disableHdr;

        /// <summary>Highest CapsuleMembrane subdivision level drawn on this tier; -1 = no cap.</summary>
        public int MembraneMaxSubdivisions => membraneMaxSubdivisions;

        /// <summary>Ceiling on a fold-gate window's render scale on this tier.</summary>
        public float FoldGateWindowMaxRenderScale => foldGateWindowMaxRenderScale;

        /// <summary>True when the menu autopilot lays no trail on this tier.</summary>
        public bool MenuAutopilotLaysNoTrail => menuAutopilotLaysNoTrail;

        /// <summary>Live cell prisms above which the freestyle trail waits; 0 = never.</summary>
        public int FreestyleCellPrismBudget => freestyleCellPrismBudget;

        /// <summary>Live cell prisms at which a waiting freestyle trail resumes.</summary>
        public int FreestyleCellPrismResume => freestyleCellPrismResume;

        /// <summary>Skim Race trail cap on this tier.</summary>
        public RaceTrailBudget SkimRaceTrail => skimRaceTrail;

        /// <summary>Joust trail cap on this tier.</summary>
        public RaceTrailBudget JoustTrail => joustTrail;

        public bool DeactivateMenuWhileFlying => deactivateMenuWhileFlying;
        public bool QuietScoreGlow => quietScoreGlow;
        public bool DisableCytoplasm => disableCytoplasm;
        public WanderwayBudget Wanderway => wanderwayBudget;

        /// <summary>The skybox this tier draws in place of <paramref name="authored"/>, or null to keep
        /// it. Loads the replacement on first use (Resources caches it after that).</summary>
        public Material SkyboxReplacementFor(Material authored)
        {
            if (!authored || skyboxReplacements == null) return null;
            foreach (var entry in skyboxReplacements)
            {
                if (entry.Authored != authored || string.IsNullOrEmpty(entry.ReplacementResource)) continue;
                var replacement = Resources.Load<Material>(entry.ReplacementResource);
                if (!replacement)
                    CSDebug.LogWarning($"[PlatformProfile] {name}: skybox replacement " +
                                       $"'{entry.ReplacementResource}' is not a Material under a Resources folder.");
                return replacement;
            }
            return null;
        }

        /// <summary>
        /// A race-wide trail budget shared by every vessel in a match: each keeps
        /// <c>budget / vessels</c> prisms, clamped to [floor, ceiling], so the cap does not multiply
        /// with the seat count (12 seats x a flat 2,000 would be 24,000 live prisms).
        /// </summary>
        [Serializable]
        public struct RaceTrailBudget
        {
            [Tooltip("Live trail prisms shared by every vessel in the match. 0 = no cap.")]
            [SerializeField, Min(0)] int raceBudget;

            [Tooltip("The least a vessel keeps, however many seats there are.")]
            [SerializeField, Min(1)] int floorPerVessel;

            [Tooltip("The most a vessel keeps, however few seats there are.")]
            [SerializeField, Min(1)] int ceilingPerVessel;

            public RaceTrailBudget(int raceBudget, int floorPerVessel, int ceilingPerVessel)
            {
                this.raceBudget = raceBudget;
                this.floorPerVessel = floorPerVessel;
                this.ceilingPerVessel = ceilingPerVessel;
            }

            /// <summary>True when this tier caps the trail at all.</summary>
            public bool Active => raceBudget > 0;

            /// <summary>Each vessel's cap in a match of <paramref name="vessels"/>.</summary>
            public int PerVessel(int vessels)
            {
                int share = raceBudget / Math.Max(1, vessels);
                int floor = Math.Max(1, floorPerVessel);
                return Math.Clamp(share, floor, Math.Max(floor, ceilingPerVessel));
            }
        }

        /// <summary>
        /// Overrides on the Wanderway belt's <see cref="ConveyorConfig"/>; -1 keeps the settings
        /// asset's value. Resident belt prisms = pool size x prism budget per scene.
        /// </summary>
        [Serializable]
        public struct WanderwayBudget
        {
            [Tooltip("Scenes on the belt (-1 = keep).")]
            [SerializeField] int poolSize;

            [Tooltip("Prisms per belt scene (-1 = keep).")]
            [SerializeField] int prismBudgetPerScene;

            [Tooltip("Scenes kept ahead of the pilot (-1 = keep).")]
            [SerializeField] int aheadTargetScenes;

            [Tooltip("Crystals per belt scene (-1 = keep).")]
            [SerializeField] int maxCrystalsPerScene;

            [Tooltip("Belt scenes that carry flora/fauna recipes: 0 = off, 1 = on, -1 = keep.")]
            [SerializeField] int lifeformScenes;

            [Tooltip("Scenes allowed to transition in at once (-1 = keep).")]
            [SerializeField] int maxConcurrentArrivals;

            public static WanderwayBudget KeepAll => new()
            {
                poolSize = -1, prismBudgetPerScene = -1, aheadTargetScenes = -1,
                maxCrystalsPerScene = -1, lifeformScenes = -1, maxConcurrentArrivals = -1,
            };

            /// <summary>Write every set override onto <paramref name="cfg"/>.</summary>
            public void ApplyTo(ConveyorConfig cfg)
            {
                if (cfg == null) return;
                if (poolSize > 0) cfg.PoolSize = poolSize;
                if (prismBudgetPerScene > 0) cfg.PrismBudget = prismBudgetPerScene;
                if (aheadTargetScenes > 0) cfg.AheadTargetScenes = aheadTargetScenes;
                if (maxCrystalsPerScene >= 0) cfg.MaxCrystalsPerScene = maxCrystalsPerScene;
                if (lifeformScenes >= 0) cfg.LifeformScenes = lifeformScenes > 0;
                if (maxConcurrentArrivals > 0) cfg.MaxConcurrentArrivals = maxConcurrentArrivals;
            }
        }

        /// <summary>One skybox swap: draw the material at <see cref="ReplacementResource"/> wherever
        /// a scene authored <see cref="Authored"/>.</summary>
        [Serializable]
        public struct SkyboxReplacement
        {
            [Tooltip("The skybox material a scene authors (RenderSettings).")]
            [SerializeField] Material authored;

            [Tooltip("Resources path (no extension) of the material drawn instead on this tier, e.g. " +
                     "PlatformSkyboxes/StaticHyperSeaSkybox. A path, not a reference - see skyboxReplacements.")]
            [SerializeField] string replacementResource;

            public Material Authored => authored;
            public string ReplacementResource => replacementResource;
        }
    }

    /// <summary>
    /// A <see cref="PlatformProfileSO"/>'s first-run graphics recommendation, as plain data so
    /// <c>SettingsAutoDetector.RecommendFromProfile</c> can be tested without an asset.
    /// </summary>
    public readonly struct PlatformAutoDetect
    {
        public readonly bool UseCapabilityHeuristic;
        public readonly QualityPresetSetting Preset;
        public readonly long PixelBudget;
        public readonly int MinRenderScalePercent;
        public readonly UpscalingSetting UpscalingWhenScaled;
        public readonly AntiAliasingSetting AntiAliasing;
        public readonly int MaxTargetFrameRate;

        public PlatformAutoDetect(bool useCapabilityHeuristic, QualityPresetSetting preset, long pixelBudget,
                                  int minRenderScalePercent, UpscalingSetting upscalingWhenScaled,
                                  AntiAliasingSetting antiAliasing, int maxTargetFrameRate)
        {
            UseCapabilityHeuristic = useCapabilityHeuristic;
            Preset = preset;
            PixelBudget = pixelBudget;
            MinRenderScalePercent = minRenderScalePercent;
            UpscalingWhenScaled = upscalingWhenScaled;
            AntiAliasing = antiAliasing;
            MaxTargetFrameRate = maxTargetFrameRate;
        }
    }
}
