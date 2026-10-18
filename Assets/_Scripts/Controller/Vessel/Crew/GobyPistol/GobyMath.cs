using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The Goby's speed rules, ported line for line from the lab (<c>closeness</c> and <c>aiGapThrottle</c> in
    /// <c>Docs/Studios/VesselStudio/goby.html</c>; GobyPistolMathTests hold them to the page's goldens). The Goby zips: its
    /// cruise is the prefab's own DefaultThrottleScaler (cloned from the artifact's gCruise), and with the Pistol close behind
    /// it pulls away harder still, so it is almost impossible to catch (Garrett, 2026-10-16).</summary>
    public static class GobyMath
    {
        /// <summary>The lab's <c>closeness</c>: x1 at gCloseRange and beyond, x gCloseMax with the Pistol right on its tail.</summary>
        public static float Closeness(float gap, float gCloseMax, float gCloseRange) =>
            1f + (gCloseMax - 1f) * Mathf.Clamp01(1f - gap / gCloseRange);

        /// <summary>The lab's <c>aiGapThrottle</c>: the AI-flown Goby eases off beyond aiMaxGap so the Pistol has a world.</summary>
        public static float AiGapThrottle(float gap, float aiMaxGap) => gap > aiMaxGap ? 0.3f : 1f;

        /// <summary>What the Goby's throttle scaler is multiplied by this frame: the closeness, and the AI's easing when the
        /// duo's AI flies it (the human flies the Pistol).</summary>
        public static float SpeedScale(float gap, float gCloseMax, float gCloseRange, bool aiFlown, float aiMaxGap) =>
            Closeness(gap, gCloseMax, gCloseRange) * (aiFlown ? AiGapThrottle(gap, aiMaxGap) : 1f);
    }
}
