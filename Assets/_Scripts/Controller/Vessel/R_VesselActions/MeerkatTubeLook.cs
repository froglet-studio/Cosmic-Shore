using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Which look a formed Meerkat wormhole tube draws with. A side-by-side test switch (Yash, 2026-10-28: "keep it side by
    /// side ... buttons to swap between original wormhole shader and the new version, to judge the frame gain"), flipped live
    /// by the Lavalamp lab (<c>Assets/_Scenes/Game_TestDesign/LavalampWormholeLab.unity</c>).
    /// <see cref="Mode"/> defaults to <see cref="MeerkatTubeLookMode.Original"/> and is reset to it on every load, so the game
    /// draws exactly what it drew before the switch existed until someone picks a look on purpose.
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
    }

    public static class MeerkatTubeLook
    {
        /// <summary>The look every tube draws with now. Original unless a lab changed it.</summary>
        public static MeerkatTubeLookMode Mode { get; set; } = MeerkatTubeLookMode.Original;

        /// <summary>The full-screen contour-field lens runs.</summary>
        public static bool LensOn => Mode is MeerkatTubeLookMode.Original or MeerkatTubeLookMode.LensOnly or MeerkatTubeLookMode.WaterAndLens;
        /// <summary>The original additive calm wall draws.</summary>
        public static bool OldWallOn => Mode is MeerkatTubeLookMode.Original or MeerkatTubeLookMode.WallOnly;
        /// <summary>The new water pipe draws.</summary>
        public static bool WaterOn => Mode is MeerkatTubeLookMode.WaterOnly or MeerkatTubeLookMode.WaterAndLens;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => Mode = MeerkatTubeLookMode.Original;
    }
}
