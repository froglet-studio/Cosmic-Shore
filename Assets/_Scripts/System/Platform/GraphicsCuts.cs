using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The GPU cuts in force right now: the device tier's (<see cref="PlatformProfile.Current"/>) merged
    /// with the quality preset's (<see cref="QualityPresetCutsSO"/>). Every reader of a render cut asks
    /// here, so a tier and a preset can never disagree about what is drawn: a flag is on when either
    /// asks for it, and a cap is the tighter of the two.
    ///
    /// The preset is pushed in by <c>GraphicsSettingsApplier.ApplyQuality</c>
    /// (<see cref="SetPreset"/>); before that runs only the tier's cuts apply. Cuts read when an object
    /// wakes (membrane, cytoplasm) reach objects created after a change - the settings panel already
    /// says a quality change needs a restart. The sky, HDR and bloom follow a change at once.
    /// <c>Docs/PLATFORM_UNIFICATION.md</c> §3.10.
    /// </summary>
    public static class GraphicsCuts
    {
        static QualityPresetSetting? s_preset;
        static QualityPresetCutsSO s_asset;
        static bool s_loaded;

        /// <summary>Enter Play Mode runs without a domain reload: drop the last session's preset.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_preset = null;
            s_asset = null;
            s_loaded = false;
        }

        /// <summary>The quality preset whose cuts are in force, or null before settings applied.</summary>
        public static QualityPresetSetting? Preset => s_preset;

        /// <summary>Set the quality preset whose cuts are in force (never Custom: pass the preset Custom
        /// started from, <see cref="GraphicsSettingsData.EffectivePreset"/>).</summary>
        public static void SetPreset(QualityPresetSetting preset) => s_preset = preset;

        /// <summary>The shipped preset table, or null when the asset is missing (an error names it).</summary>
        public static QualityPresetCutsSO Asset
        {
            get
            {
                if (s_loaded) return s_asset;
                s_loaded = true;
                s_asset = Resources.Load<QualityPresetCutsSO>(QualityPresetCutsSO.ResourcePath);
                if (!s_asset)
                    CSDebug.LogError($"[GraphicsCuts] Resources/{QualityPresetCutsSO.ResourcePath}.asset is " +
                                     "missing: no quality preset cuts any GPU work. Restore " +
                                     $"Assets/Resources/{QualityPresetCutsSO.ResourcePath}.asset.");
                return s_asset;
            }
        }

        /// <summary>The row for <paramref name="preset"/>, or null when it has none.</summary>
        public static PresetCuts For(QualityPresetSetting preset) => Asset ? Asset.For(preset) : null;

        /// <summary>The row of the preset in force, or null.</summary>
        public static PresetCuts Current => s_preset.HasValue ? For(s_preset.Value) : null;

        public static bool DisableHdr
        {
            get
            {
                var profile = PlatformProfile.Current;
                return (profile && profile.DisableHdr) || (Current?.DisableHdr ?? false);
            }
        }

        /// <summary>The fewest cytoplasm shards any cut may leave, as a fraction of a cell's full field.
        /// The shards point at the cell's crystal, so they are a navigation cue: thinned, never removed.</summary>
        public const float MinCytoplasmDensity = 0.1f;

        /// <summary>Fraction of each cell's cytoplasm shards spawned: the lower of the tier's and the
        /// preset's, never below <see cref="MinCytoplasmDensity"/>.</summary>
        public static float CytoplasmDensity
        {
            get
            {
                var profile = PlatformProfile.Current;
                return LowerDensity(profile ? profile.CytoplasmDensity : 1f, Current?.CytoplasmDensity ?? 1f);
            }
        }

        /// <summary>The lower of two densities, clamped to [<see cref="MinCytoplasmDensity"/>, 1].</summary>
        public static float LowerDensity(float a, float b) =>
            Mathf.Clamp(Mathf.Min(a, b), MinCytoplasmDensity, 1f);

        /// <summary>Highest CapsuleMembrane subdivision level drawn; -1 = no cap.</summary>
        public static int MembraneMaxSubdivisions
        {
            get
            {
                var profile = PlatformProfile.Current;
                return TighterLevelCap(profile ? profile.MembraneMaxSubdivisions : -1,
                    Current?.MembraneMaxSubdivisions ?? -1);
            }
        }

        /// <summary>Ceiling on a wormhole mouth's exact-view render scale; 1 = no cap.</summary>
        public static float OffscreenViewMaxRenderScale
        {
            get
            {
                var profile = PlatformProfile.Current;
                return Mathf.Min(profile ? profile.FoldGateWindowMaxRenderScale : 1f,
                    Current?.OffscreenViewMaxRenderScale ?? 1f);
            }
        }

        /// <summary>The sky drawn in place of <paramref name="authored"/>, or null to keep it: the tier's
        /// replacement first, then the preset's bake when the preset in force asks for one.</summary>
        public static Material SkyboxReplacementFor(Material authored)
        {
            if (!authored) return null;
            var profile = PlatformProfile.Current;
            var replacement = profile ? profile.SkyboxReplacementFor(authored) : null;
            if (replacement) return replacement;
            return Current is { BakedSky: true } && Asset ? Asset.BakedSkyFor(authored) : null;
        }

        /// <summary>The tighter of two level caps where -1 means none.</summary>
        public static int TighterLevelCap(int a, int b)
        {
            if (a < 0) return b < 0 ? -1 : b;
            return b < 0 ? a : Mathf.Min(a, b);
        }
    }
}
