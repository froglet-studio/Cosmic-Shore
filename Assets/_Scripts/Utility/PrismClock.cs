using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The single source of truth for the clock-material law's clock
    /// (Docs/PRISM_ANIMATION.md). Every animation stamp writes <see cref="Now"/>
    /// as its start time, and the SAME value is published to the GPU once per
    /// frame as the global uniform <c>_PrismClock</c> — the graphs' Clock input.
    /// Both sides read the same number BY CONSTRUCTION, so no render-pipeline
    /// time semantics can ever skew them.
    ///
    /// History (why not the shader's Time node): the first wiring used
    /// _Time via a Time node, stamping Time.timeSinceLevelLoad per Unity's
    /// built-in docs — but URP feeds shader time from Time.time (scaled, never
    /// resets across scene loads), so by Menu_Main every stamp was seconds in
    /// the past and every bloom evaluated as already finished: prisms popped in
    /// fully grown with zero errors. The global-uniform publisher removes that
    /// entire failure class.
    ///
    /// Notes:
    /// - Time.time is scaled, so hitstop / pause freeze prism animation — desired.
    /// - Monotonic across scene loads — stamps can never go stale mid-scene.
    /// - One Shader.SetGlobalFloat per frame is the law's allowed O(1) clock
    ///   publisher (PRISM_ANIMATION.md §1) — it is not a per-prism update.
    /// - Edit mode (no publisher): the global reads 0 and materials rest at
    ///   their settled defaults (rate/duration 0), so previews render the end
    ///   state — correct.
    /// </summary>
    public static class PrismClock
    {
        /// <summary>Current clock value — stamp this as every animation's start time.</summary>
        public static float Now => Time.time;

        /// <summary>
        /// A start time far enough in the past that any animation stamped with it
        /// renders fully settled (used behind loading veils where the bloom would
        /// be invisible anyway — the clock equivalent of CompleteGrowthImmediately).
        /// </summary>
        public static float SettledPast => Now - 1000f;

        static readonly int PrismClockId = Shader.PropertyToID("_PrismClock");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallPublisher()
        {
            Shader.SetGlobalFloat(PrismClockId, Now);
            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from
            // play-mode-exit cleanup), same pattern as PrismRenderService's flush host.
            var go = new GameObject("[PrismClock]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<PrismClockPublisher>();
        }

        sealed class PrismClockPublisher : MonoBehaviour
        {
            void Update() => Shader.SetGlobalFloat(PrismClockId, Now);
        }
    }
}
