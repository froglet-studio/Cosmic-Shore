using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Feel for the desktop ONE-THUMB flight scheme
    /// (<see cref="CosmicShore.Gameplay.SingleStickMouseInputStrategy"/>): the mouse is the
    /// single stick a Sparrow / Serpent / Grizzly / Termite / Falcon / Shrike / Scarab flies on.
    ///
    /// <para>The mouse hands us a DELTA and the vessel wants a POSITION — a single-stick
    /// transformer reads <c>EasedLeftJoystickPosition</c> every frame, so "how far is the stick
    /// pushed" is the only question it asks. So the strategy integrates delta into a virtual
    /// stick clamped to the unit circle, and this asset owns the three numbers that turn one
    /// into the other.</para>
    ///
    /// <para><b>Every field here is a playtest dial, not a measurement.</b> The two that matter
    /// are not independent — the scheme's real control curve is
    /// <c>deflection = drag px/s × stickUnitsPerPixel / springPerSecond</c>, so retune them as a
    /// PAIR against that, never one at a time. The shipped 0.011 / 3.5 puts full deflection at a
    /// brisk ~318 px/s sweep with a 0.29 s return, and 0.011 is close to
    /// <c>DualMouseInputStrategy</c>'s 0.013 for a reason: that is roughly where a mouse sweep
    /// stops feeling like a nudge. Nobody has flown these numbers yet. Tune them here, never in
    /// code, and never per-vessel: a control scheme that reads differently on each hull is one
    /// the player has to re-learn six times.</para>
    ///
    /// <para>Place the asset at <c>Resources/MouseFlightConfig</c>. With no asset the defaults
    /// below apply, so the scheme is never silently off just because the asset is missing (the
    /// <c>SelfTrailContactConfigSO</c> / <c>SpeedTunnelConfigSO</c> precedent).</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "MouseFlightConfig",
        menuName = "ScriptableObjects/Input/Mouse Flight Config")]
    public class MouseFlightConfigSO : ScriptableObject
    {
        [Header("Stick")]
        [Tooltip("Stick units gained per pixel of mouse movement. Together with the spring below " +
                 "this sets the scheme's real control curve: a sustained drag of v px/s settles " +
                 "the stick at v x thisValue / springPerSecond. At the shipped 0.011 / 3.5 a " +
                 "brisk ~318 px/s sweep is full deflection and half that is a half turn.")]
        [Min(0.0001f)]
        [SerializeField] float stickUnitsPerPixel = 0.011f;

        [Tooltip("How hard the stick springs back to centre, as an exponential rate in " +
                 "reciprocal seconds. This is the spring a real thumbstick has and a mouse does " +
                 "not, and it is what makes MOUSE SPEED mean TURN RATE - without it any drag at " +
                 "all winds up at full deflection. Letting go decays with time constant " +
                 "1 / thisValue (the shipped 3.5 is 0.29 s, so a full-deflection turn is down to " +
                 "a tenth in about two thirds of a second) until the dead zone lands it on " +
                 "exactly centred.\n\n" +
                 "Set to 0 for the other school of mouse flight: no spring, so a push keeps the " +
                 "vessel turning until you push back (what DualMouseInputStrategy effectively " +
                 "does). THE dial to try first if the scheme reads wrong.")]
        [Min(0f)]
        [SerializeField] float springPerSecond = 3.5f;

        [Tooltip("Deflection below which the stick reads as exactly centred. It is not optional " +
                 "polish: the spring above is exponential and only ever APPROACHES zero, so this " +
                 "is what actually lands on it. Without it the vessel carries a permanent " +
                 "sub-perceptual turn, which reads as drift rather than as a control.")]
        [Range(0.001f, 0.25f)]
        [SerializeField] float deadZone = 0.02f;

        public float StickUnitsPerPixel => Mathf.Max(0.0001f, stickUnitsPerPixel);
        public float SpringPerSecond => Mathf.Max(0f, springPerSecond);
        public float DeadZone => Mathf.Clamp(deadZone, 0.001f, 0.25f);

        /// <summary>
        /// The deflection a sustained drag of <paramref name="pixelsPerSecond"/> settles at under
        /// these numbers - the curve to reason about when retuning, rather than either field
        /// alone.
        /// </summary>
        public float SustainedDeflection(float pixelsPerSecond)
            => CosmicShore.Gameplay.MouseVirtualStick.SustainedDeflection(
                   pixelsPerSecond, StickUnitsPerPixel, SpringPerSecond);

        // ------------------------------------------------------------------
        // Instance

        const string ResourcePath = "MouseFlightConfig";
        static MouseFlightConfigSO s_instance;
        static bool s_loadAttempted;

        // If s_instance ever goes null after the first attempt, the latch would otherwise skip
        // Resources.Load forever and silently serve CreateInstance code defaults.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_loadAttempted = false;
        }

        /// <summary>
        /// The fleet's one mouse-flight config. Falls back to an in-memory instance carrying the
        /// authored defaults above, so the scheme still flies with no asset present.
        /// </summary>
        public static MouseFlightConfigSO Instance
        {
            get
            {
                if (s_instance) return s_instance;
                if (!s_loadAttempted)
                {
                    s_loadAttempted = true;
                    s_instance = Resources.Load<MouseFlightConfigSO>(ResourcePath);
                }
                if (!s_instance)
                    s_instance = CreateInstance<MouseFlightConfigSO>();
                return s_instance;
            }
        }
    }
}
