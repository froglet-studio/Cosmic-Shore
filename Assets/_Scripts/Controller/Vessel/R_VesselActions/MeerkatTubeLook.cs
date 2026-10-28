using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Which look a formed Meerkat wormhole tube draws with. A side-by-side test switch (Yash, 2026-10-28: "keep it side by
    /// side ... buttons to swap between original wormhole shader and the new version, to judge the frame gain"), flipped live
    /// by the Lavalamp lab (<c>Assets/_Scenes/Game_TestDesign/LavalampWormholeLab.unity</c>).
    /// <see cref="Mode"/> defaults to <see cref="MeerkatTubeLookMode.HighQuality"/> (2026-10-29, the PC look) and is reset to it
    /// on every load; <see cref="MeerkatTubeLookMode.Original"/> is the shipped lens kept beside it as the A/B fallback.
    /// </summary>
    public enum MeerkatTubeLookMode
    {
        /// <summary>As shipped: the contour-field lens (<see cref="MeerkatWormholeLensPass"/>) + the calm wall (<see cref="MeerkatTubeWallView"/>).</summary>
        Original = 0,
        /// <summary>The cheap mobile water pipe only (<see cref="MeerkatWaterTubeView"/>): no lens, no calm wall.</summary>
        WaterOnly = 1,
        /// <summary>The lens only (no calm wall).</summary>
        LensOnly = 2,
        /// <summary>The calm wall only (no lens).</summary>
        WallOnly = 3,
        /// <summary>No tube look at all (the reticle, spindle and glass still draw): the frame-time floor.</summary>
        None = 4,
        /// <summary>The water pipe over the lens, to judge them together.</summary>
        WaterAndLens = 5,
        /// <summary>
        /// The PC look (Docs/Shaders/WORMHOLE_SHADERS.md §11): the same contour-field lens drawn with <c>_MW_HQ</c> - a visible
        /// outer layer, rim, glint, dispersion, and the network's iridescent upgrade (<see cref="MeerkatWormholeHQ"/>). The
        /// field runs in BOTH looks: Yash's adds the layer and the spindle bridge, Garrett's keeps the layer to a whisper.
        /// </summary>
        HighQuality = 6,
    }

    public static class MeerkatTubeLook
    {
        /// <summary>The look every tube draws with now. Original unless a lab changed it.</summary>
        public static MeerkatTubeLookMode Mode { get; set; } = Default;

        /// <summary>The look the game starts with (and every load resets to).</summary>
        public const MeerkatTubeLookMode Default = MeerkatTubeLookMode.HighQuality;

        /// <summary>The full-screen contour-field lens runs.</summary>
        public static bool LensOn => Mode is MeerkatTubeLookMode.Original or MeerkatTubeLookMode.LensOnly or MeerkatTubeLookMode.WaterAndLens
            or MeerkatTubeLookMode.HighQuality;
        /// <summary>The lens draws with its high-quality PC layer (<c>_MW_HQ</c>).</summary>
        public static bool HighQuality => Mode == MeerkatTubeLookMode.HighQuality;

        /// <summary>
        /// A lab's override of every config's <c>formedLook</c> (null: each config's own). Lets the Lavalamp lab flip between
        /// YASH'S LOOK and GARRETT'S LOOK without writing to the config asset.
        /// </summary>
        public static ScriptableObjects.MeerkatFormedLook? FormedOverride { get; set; }

        /// <summary>The look a tube of this config draws with now.</summary>
        public static ScriptableObjects.MeerkatFormedLook Formed(ScriptableObjects.MeerkatWormholeConfigSO cfg) =>
            FormedOverride ?? cfg.FormedLook;

        /// <summary>The contour field bends this config's tubes now: in the PC look always (both looks), else only in Garrett's.</summary>
        public static bool FieldOn(ScriptableObjects.MeerkatWormholeConfigSO cfg) =>
            cfg.ContourFieldBends && (HighQuality || Formed(cfg) == ScriptableObjects.MeerkatFormedLook.ContourField);
        /// <summary>The original additive calm wall draws.</summary>
        public static bool OldWallOn => Mode is MeerkatTubeLookMode.Original or MeerkatTubeLookMode.WallOnly;
        /// <summary>The new water pipe draws.</summary>
        public static bool WaterOn => Mode is MeerkatTubeLookMode.WaterOnly or MeerkatTubeLookMode.WaterAndLens;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            Mode = Default;
            FormedOverride = null;
        }
    }
}
