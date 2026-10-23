using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The gamepad's steering smoother (<see cref="CosmicShore.Gameplay.GamepadInputStrategy"/>, through
    /// <see cref="CosmicShore.Gameplay.CascadeSmoother"/>). Touch, keyboard and mouse do not read it.
    ///
    /// <para><b>Why it exists.</b> Until 2026-10-21 every hull's nose chased the stick's commanded
    /// rotation through one first-order lag (<c>LERP_AMOUNT = 1.5/s</c>, a 0.67 s time constant; added
    /// 2023-11-06 to smooth the gyro, 2/s, retuned to 1.5/s 2024-11-19). On a pad that lag was the
    /// smoothness: a ship should not move with a thumb's jerk. But it smoothed by being slow (80 degrees
    /// behind the command at a full Squirrel turn, ~1.5 s to reach 90% of a new rate), and taking it off
    /// for human pilots (<c>VesselTransformer.NoseFollowFraction</c>) took the smoothness with it, so the
    /// nose turned corners wherever the stick did. A first-order lag also only makes the angle's FIRST
    /// derivative continuous.</para>
    ///
    /// <para><b>What it does instead.</b> Smoothness comes from the filter's ORDER and lag from its
    /// TIME, so this smooths the stick's steering readings through several stages: six stages sharing
    /// 0.35 s make the nose continuous to its sixth derivative and reach 90% of a new rate in ~0.53 s,
    /// ~42 degrees behind at 120 deg/s (the old lag: ~1.5 s, 80 degrees). The nose itself stays
    /// lag-free; it integrates the smoothed rates. (First shipped at 4 / 0.12 s, ~0.2 s to 90%; Garrett,
    /// 2026-10-23: "needs more smoothing ... it still feels like it turns too fast and moves too jerky".)
    /// A sustained turn's RATE is the hull's either way; the smoother only shapes how a turn starts
    /// and stops.</para>
    ///
    /// <para><b>What it smooths.</b> Pitch (<c>YSum</c>), yaw (<c>XSum</c>), roll (<c>YDiff</c>) and the eased
    /// sticks the one-thumb hulls steer by, after Invert Y and after the straight-line gestures (which
    /// keep judging the raw sticks). Throttle (<c>XDiff</c>) and the raw stick positions (aim, UI) are
    /// left as they are.</para>
    ///
    /// <para><b>Every field is a playtest dial.</b> Tune it here, never in code, never per hull. A total
    /// delay of 0 is the unsmoothed pad. With no asset at <c>Resources/GamepadFlightConfig</c> the
    /// defaults below apply (the <see cref="MouseFlightConfigSO"/> precedent).</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "GamepadFlightConfig",
        menuName = "ScriptableObjects/Input/Gamepad Flight Config")]
    public class GamepadFlightConfigSO : ScriptableObject
    {
        [Header("Nose lag")]
        [Tooltip("ON: a pad pilot's nose chases the stick's command through the fleet's one-stage lag " +
                 "(LERP_AMOUNT 1.5/s, a 0.67 s time constant) - exactly how every pad flew from 2024-11-19 " +
                 "until 2026-10-21. OFF: the nose is the command each frame, as on touch, and only the " +
                 "smoother below shapes it. Turn the smoother's delay to 0 while this is on, or the two stack. " +
                 "Live in Play, for A/B (Garrett, 2026-10-23).")]
        [SerializeField] bool legacyNoseLag = true;

        [Header("Steering smoother")]
        [Tooltip("How many smoothing stages the pad's steering runs through. Each stage makes one more " +
                 "derivative of the nose's motion continuous (6: no jump in turn rate or its next five " +
                 "derivatives). More stages at the same total delay feel heavier and rounder, not slower.")]
        [Range(1, 8)]
        [SerializeField] int smoothingStages = 6;

        [Tooltip("The smoother's total mean delay, in seconds (each stage gets this / stages). This is " +
                 "the lag dial: 0.35 s reaches 90% of a new turn rate in ~0.53 s (0.12 s: ~0.2 s; 0.6 s: " +
                 "~0.9 s). 0 turns the smoother off (shipped 0 while Legacy Nose Lag is on; 6 / 0.35 was the last try). The old nose lag was one stage of 0.67 s (~1.5 s to 90%).")]
        [Range(0f, 1f)]
        [SerializeField] float smoothingDelaySeconds = 0f;

        public bool LegacyNoseLag => legacyNoseLag;
        public int SmoothingStages => Mathf.Clamp(smoothingStages, 1, 8);
        public float SmoothingDelaySeconds => Mathf.Clamp(smoothingDelaySeconds, 0f, 1f);

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
        /// authored defaults above, so the pad still flies smoothed with no asset present.
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
