using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The gamepad's steering response curve (<see cref="CosmicShore.Gameplay.GamepadInputStrategy"/>).
    /// Touch, keyboard and mouse do not read it.
    ///
    /// <para><b>Why it exists.</b> Until 2026-10-21 every hull's nose chased the stick's commanded
    /// rotation with a 0.67 s first-order lag (<c>LERP_AMOUNT = 1.5/s</c>). For a pad that lag was
    /// most of the feel: over a hold of T seconds the nose got only
    /// <c>1 - tau (1 - e^(-T/tau)) / T</c> of what the stick commanded (17% of a 0.25 s correction,
    /// 30% of 0.5 s, 48% of 1 s, 68% of 2 s). Taking the lag off for human pilots
    /// (<c>VesselTransformer.NoseFollowFraction</c>) made the pad snappy, but the same stick now
    /// turns the hull several times as far over a short correction. This curve gives the pad its
    /// old sensitivity back through the response profile instead of the lag: gentle near the
    /// centre, where the short corrections live, and close to the full turn rate at full stick,
    /// with no delay at any deflection.</para>
    ///
    /// <para><b>What it shapes.</b> The steering readings only, after the strategy's ease and before
    /// Invert Y: pitch (<c>YSum</c>), yaw (<c>XSum</c>), roll (<c>YDiff</c>) and the eased sticks the
    /// one-thumb hulls steer by. Throttle (<c>XDiff</c>) and the raw stick positions (aim, UI) are
    /// left as they are. Shaping keeps the sign and maps 0 to 0, so every gesture keyed on direction
    /// still reads the same.</para>
    ///
    /// <para><b>Every field is a playtest dial.</b> Tune it here, never in code, never per hull.
    /// Exponent 1 and sensitivity 1 are exactly the pad as it flew between 2026-10-21 and this
    /// asset. With no asset at <c>Resources/GamepadFlightConfig</c> the defaults below apply
    /// (the <see cref="MouseFlightConfigSO"/> precedent).</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "GamepadFlightConfig",
        menuName = "ScriptableObjects/Input/Gamepad Flight Config")]
    public class GamepadFlightConfigSO : ScriptableObject
    {
        [Header("Steering response")]
        [Tooltip("How gentle the centre of the stick is, as a power on the eased stick value " +
                 "(|value|^exponent). 1 leaves the curve as it is; higher softens small and medium " +
                 "deflections while full stick stays at full. The shipped 1.5 gives a quarter stick " +
                 "27% and a half stick 54% of what it gave with no curve, close to what the old 0.67 s " +
                 "nose lag let through on a short correction.")]
        [Range(1f, 3f)]
        [SerializeField] float responseExponent = 1.5f;

        [Tooltip("Overall steering gain on a pad, applied after the exponent: full stick turns at " +
                 "this fraction of the hull's turn rate. 1 keeps the hull's full rate; the shipped " +
                 "0.85 matches what a 2-3 s full-stick turn used to give through the old lag (68-78% " +
                 "over the hold).")]
        [Range(0.25f, 1.5f)]
        [SerializeField] float steeringSensitivity = 0.85f;

        public float ResponseExponent => Mathf.Clamp(responseExponent, 1f, 3f);
        public float SteeringSensitivity => Mathf.Clamp(steeringSensitivity, 0.25f, 1.5f);

        /// <summary>One steering reading through this curve.</summary>
        public float Shape(float value) => Shape(value, ResponseExponent, SteeringSensitivity);

        /// <summary>
        /// <c>sign(value) * sensitivity * |value|^exponent</c>. Keeps the sign, maps 0 to 0, and is
        /// monotonic, so a gesture keyed on a reading's direction reads the same through it.
        /// </summary>
        public static float Shape(float value, float exponent, float sensitivity) =>
            Mathf.Sign(value) * sensitivity * Mathf.Pow(Mathf.Abs(value), exponent);

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
        /// authored defaults above, so the pad still flies the curve with no asset present.
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
