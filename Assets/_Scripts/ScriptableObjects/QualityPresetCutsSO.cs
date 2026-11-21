using System;
using CosmicShore.Core;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// What each graphics quality preset takes OFF the GPU. A phone is GPU-bound (fill rate and
    /// bandwidth on a tile GPU), so a lower preset has to draw fewer and cheaper pixels, not just
    /// simulate less: Medium drops the costs nobody misses (the animated procedural sky becomes its
    /// one-sample bake, the wormhole's extra camera renders smaller), and Low goes after the heavy
    /// hitters (anti-aliasing, render scale, HDR, bloom resolution, transparent overdraw - thinned,
    /// never removed, where the transparent things are functional like the cytoplasm shards).
    ///
    /// Each row has two halves:
    /// <list type="bullet">
    /// <item><b>Written on pick</b>: settings the player can see and change (AA, render scale). Picking
    /// the preset writes them; a later hand change wins and marks the preset Custom.</item>
    /// <item><b>Render cuts</b>: applied while the preset is in effect, including after a hand change
    /// made it Custom (<see cref="GraphicsSettingsData.EffectivePreset"/>). They ADD to the device
    /// tier's own cuts (<see cref="PlatformProfileSO"/>); a preset never undoes a tier cut.</item>
    /// </list>
    /// Read through <c>GraphicsCuts</c>, never directly. Loaded from <c>Resources/QualityPresetCuts</c>.
    /// <c>Docs/PLATFORM_UNIFICATION.md</c> §3.10.
    /// </summary>
    [CreateAssetMenu(
        fileName = "QualityPresetCuts",
        menuName = "ScriptableObjects/Platform/" + nameof(QualityPresetCutsSO))]
    public class QualityPresetCutsSO : ScriptableObject
    {
        /// <summary>Resources path of the shipped asset.</summary>
        public const string ResourcePath = "QualityPresetCuts";

        [Tooltip("The baked version of each procedural sky. A preset with 'Baked Sky' on draws the " +
                 "replacement wherever a scene authored the sky (PlatformRenderApplier). A Resources " +
                 "PATH, loaded only when used, so the bake's texture is not resident on a preset that " +
                 "never draws it.")]
        [SerializeField] PlatformProfileSO.SkyboxReplacement[] bakedSkies =
            Array.Empty<PlatformProfileSO.SkyboxReplacement>();

        [Tooltip("One row per quality preset. A preset with no row cuts nothing.")]
        [SerializeField] PresetCuts[] presets = Array.Empty<PresetCuts>();

        /// <summary>The row for <paramref name="preset"/>, or null when it has none.</summary>
        public PresetCuts For(QualityPresetSetting preset)
        {
            if (presets == null) return null;
            foreach (var row in presets)
                if (row != null && row.Preset == preset) return row;
            return null;
        }

        /// <summary>The baked sky drawn in place of <paramref name="authored"/>, or null when it has none.</summary>
        public Material BakedSkyFor(Material authored) =>
            PlatformProfileSO.SkyboxReplacement.Find(bakedSkies, authored, name);
    }

    /// <summary>One quality preset's GPU cuts. A class, not a struct, so a row added in the inspector
    /// starts at "no change" (the field initializers) rather than at zero.</summary>
    [Serializable]
    public class PresetCuts
    {
        [Tooltip("The quality preset this row belongs to.")]
        [SerializeField] QualityPresetSetting preset = QualityPresetSetting.High;

        [Header("Written when the player picks this preset")]
        [Tooltip("Picking the preset sets the anti-aliasing below. Off = the player's AA is kept.")]
        [SerializeField] bool setsAntiAliasing;

        [Tooltip("Anti-aliasing written on pick. Off removes the MSAA resolve or the post-AA pass " +
                 "(SMAA is three full-screen passes, FXAA one).")]
        [SerializeField] AntiAliasingSetting antiAliasing = AntiAliasingSetting.Off;

        [Tooltip("Picking the preset lowers render scale to at most this percent (never raises it). " +
                 "Render scale is the largest GPU lever there is: every pass pays per pixel. 100 = keep.")]
        [SerializeField, Range(25, 100)] int maxRenderScalePercent = 100;

        [Tooltip("Upscaler written when the pick lowers render scale. Linear is one bilinear blit; FSR " +
                 "adds an ALU-heavy pass a low-end phone GPU pays for.")]
        [SerializeField] UpscalingSetting upscalingWhenScaled = UpscalingSetting.Linear;

        [Header("Render cuts (while this preset is in effect)")]
        [Tooltip("Draw the baked sky in place of the procedural one: one texture sample per pixel " +
                 "instead of ~20 octaves of noise and two Voronoi searches, and the nebulae, dust and " +
                 "stars stop drifting and twinkling.")]
        [SerializeField] bool bakedSky;

        [Tooltip("Turn the URP asset's HDR off: an LDR colour buffer is half the bandwidth of an HDR " +
                 "one, and lets MSAA sit in a tile GPU's on-chip memory. Values above 1 clamp, so " +
                 "emissive highlights bloom less.")]
        [SerializeField] bool disableHdr;

        [Tooltip("Bloom builds its blur pyramid from a QUARTER-resolution copy instead of half: about " +
                 "a quarter of the pixels in every bloom pass. Softer, wider glow.")]
        [SerializeField] bool bloomAtQuarterResolution;

        [Tooltip("Most bloom pyramid levels (each a downsample and an upsample pass). 0 = the scene " +
                 "profile's own value (the gameplay profile uses 4).")]
        [SerializeField, Range(0, 8)] int bloomMaxIterations;

        [Tooltip("Cap on a CapsuleMembrane's icosphere level: 3 draws 642 transparent capsules " +
                 "instead of 2,562 (overdraw). -1 = no cap. Read when a membrane wakes.")]
        [SerializeField, Range(-1, 4)] int membraneMaxSubdivisions = -1;

        [Tooltip("Ceiling on a wormhole mouth's exact-view render resolution, as a fraction of the " +
                 "gameplay camera's (a second camera rendering the world). 1 = no cap.")]
        [SerializeField, Range(0.1f, 1f)] float offscreenViewMaxRenderScale = 1f;

        [Tooltip("Fraction of a cell's cytoplasm shards spawned (1 = all): fewer transparent shards, " +
                 "less overdraw. The shards are FUNCTIONAL - they point at the cell's crystal - so a " +
                 "preset thins them and never removes them. Read when a cell starts.")]
        [SerializeField, Range(GraphicsCuts.MinCytoplasmDensity, 1f)] float cytoplasmDensity = 1f;

        [Tooltip("One view on the screen at a time: no crew vessel draws a picture-in-picture, the camera " +
                 "hidden under a full-screen seat view draws nothing, and the person starts in the crew " +
                 "seat whose view is the cheapest (the Wrasse, the Ratel). Read every frame (CrewSingleView).")]
        [SerializeField] bool singleView;

        public QualityPresetSetting Preset => preset;
        public bool SetsAntiAliasing => setsAntiAliasing;
        public AntiAliasingSetting AntiAliasing => antiAliasing;
        public int MaxRenderScalePercent => maxRenderScalePercent;
        public UpscalingSetting UpscalingWhenScaled => upscalingWhenScaled;
        public bool BakedSky => bakedSky;
        public bool DisableHdr => disableHdr;
        public bool BloomAtQuarterResolution => bloomAtQuarterResolution;
        public int BloomMaxIterations => bloomMaxIterations;
        public int MembraneMaxSubdivisions => membraneMaxSubdivisions;
        public float OffscreenViewMaxRenderScale => offscreenViewMaxRenderScale;
        public float CytoplasmDensity => cytoplasmDensity;
        public bool SingleView => singleView;

        /// <summary>True when this row changes bloom at all.</summary>
        public bool CutsBloom => bloomAtQuarterResolution || bloomMaxIterations > 0;

        /// <summary>
        /// Write this row's "on pick" settings onto <paramref name="data"/>: its AA when it sets one,
        /// and render scale lowered to its ceiling (with its upscaler) when above it.
        /// </summary>
        public void ApplyPickTo(GraphicsSettingsData data)
        {
            if (data == null) return;
            if (setsAntiAliasing) data.AntiAliasing = antiAliasing;
            if (maxRenderScalePercent < 100 && data.RenderScalePercent > maxRenderScalePercent)
            {
                data.RenderScalePercent = maxRenderScalePercent;
                data.Upscaling = upscalingWhenScaled;
            }
        }
    }
}
