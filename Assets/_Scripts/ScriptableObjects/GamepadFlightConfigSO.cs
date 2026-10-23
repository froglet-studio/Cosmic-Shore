using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// How a hull's nose follows a human pilot's gamepad (<c>VesselTransformer.NoseFollowFraction</c>).
    /// Touch has its own dial (<c>TouchControlProfileLibrarySO.NoseFollowPerSecond</c>); keyboard and
    /// mouse have none.
    ///
    /// <para><b>The pad keeps the fleet's nose lag (Garrett, 2026-10-23).</b> The nose chases the
    /// stick's commanded rotation by a fixed fraction of the gap each frame (a first-order lag), which
    /// is responsive - its biggest step is the first frame - and eases into the target. It came in
    /// 2023-11-06 to smooth the gyro (2/s, <c>248e71c5</c>), sat at 1.5/s from 2024-11-19
    /// (<c>311b07d4</c>) and was taken off every human pilot 2026-10-21. Two replacements were flown on
    /// a pad and lost to it: a response curve on stick position (no smoothness: it shapes how far, not
    /// how), and a chain of 4-6 fast smoothing stages (smooth to many derivatives, but a multi-stage
    /// chain starts almost still, which reads as input lag: "much worse input lag than what we had").
    /// </para>
    ///
    /// <para><b>A playtest dial.</b> Tune it here, never in code, never per hull. 0 is a lag-free pad.
    /// With no asset at <c>Resources/GamepadFlightConfig</c> the default below applies (the
    /// <see cref="MouseFlightConfigSO"/> precedent).</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "GamepadFlightConfig",
        menuName = "ScriptableObjects/Input/Gamepad Flight Config")]
    public class GamepadFlightConfigSO : ScriptableObject
    {
        [Header("Nose")]
        [Tooltip("How fast a pad pilot's nose closes on the stick's command, per second (the frame closes " +
                 "1 - e^(-rate * dt) of the gap). 1.5 is the fleet lag every pad flew from 2024-11-19 " +
                 "until 2026-10-21: a 0.67 s time constant. Higher is tighter; 0 is no lag (the nose IS the " +
                 "command). Live in Play.")]
        [Range(0f, 20f)]
        [SerializeField] float noseFollowPerSecond = 1.5f;

        public float NoseFollowPerSecond => Mathf.Max(0f, noseFollowPerSecond);

        // ------------------------------------------------------------------------------------
        // Instance

        const string ResourcePath = "GamepadFlightConfig";
        static GamepadFlightConfigSO s_instance;
        static bool s_loadAttempted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_loadAttempted = false;
        }

        /// <summary>
        /// The fleet's one gamepad-flight config. Falls back to an in-memory instance carrying the
        /// authored default above, so the pad keeps its lag with no asset present.
        /// </summary>
        public static GamepadFlightConfigSO Instance
        {
            get
            {
                if (s_instance) return s_instance;
                if (!s_loadAttempted)
                {
                    s_loadAttempted = true;
                    s_instance = Resources.Load<GamepadFlightConfigSO>(ResourcePath);
                }
                if (!s_instance)
                    s_instance = CreateInstance<GamepadFlightConfigSO>();
                return s_instance;
            }
        }
    }
}
