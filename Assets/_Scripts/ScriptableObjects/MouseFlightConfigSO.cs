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
    /// stick clamped to the unit circle, and this asset owns the numbers that turn one into the
    /// other.</para>
    ///
    /// <para><b>The scheme has two regimes.</b> Near centre the spring is live and the stick is a
    /// RATE control — mouse speed is turn rate, and letting go straightens the vessel out. Out past
    /// <see cref="HoldOuterRadius"/> the spring is dead and the stick is a POSITION control — a
    /// push that gets it there parks it, and the vessel keeps turning with the mouse dead still.
    /// Read <c>MouseVirtualStick</c>'s class doc before retuning; the fields are not independent
    /// and none of them means anything on its own.</para>
    ///
    /// <para><b>The gain and the spring are the ORIGINAL shipped pair and should stay that way
    /// unless a playtest says otherwise.</b> The annulus was first shipped with them lowered to
    /// 0.0045 / 1.5, chosen so the annulus sat a "comfortable sweep" out — and that was a
    /// regression that read as the mouse not working at all. The reasoning checked the SUSTAINED
    /// curve (<c>v · k / spring</c>, near enough identical at 318 vs 333 px/s for full deflection)
    /// and never checked the IMPULSE response, which is what a player actually judges: a 100 px
    /// flick went from 0.86 deflection to 0.40, i.e. from 67°/s to 17°/s on the Sparrow. *A
    /// control curve is a claim about the steady state; a flick is a claim about the transient,
    /// and tuning one while only measuring the other is how a scheme gets four times less
    /// responsive with every number looking right.*</para>
    ///
    /// <para><b>Every field here is a playtest dial, not a measurement.</b> Tune them here, never
    /// in code, and never per-vessel: a control scheme that reads differently on each hull is one
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
        [Tooltip("Stick units gained per pixel of mouse movement — the scheme's raw sensitivity. " +
                 "Its most useful reading is its reciprocal: 1 / thisValue is how many pixels of " +
                 "mouse travel take the stick from centre to hard over (the shipped 0.011 is " +
                 "~91 px). This is the dial that decides how a FLICK reads, which is what a " +
                 "player judges responsiveness by - lowering it makes the vessel feel dead even " +
                 "when the sustained curve is unchanged (see the class doc).")]
        [Min(0.0001f)]
        [SerializeField] float stickUnitsPerPixel = 0.011f;

        [Tooltip("How hard the stick springs back to centre INSIDE the hold band, as an " +
                 "exponential rate in reciprocal seconds. This is the spring a real thumbstick " +
                 "has and a mouse does not, and near centre it is what makes MOUSE SPEED mean " +
                 "TURN RATE: a sustained drag of v px/s settles at v x stickUnitsPerPixel / " +
                 "thisValue. Letting go decays with time constant 1 / thisValue (the shipped 3.5 " +
                 "is 0.29 s) until the dead zone lands it on exactly centred.\n\n" +
                 "Set to 0 for a pure accumulator with no return anywhere (what " +
                 "DualMouseInputStrategy effectively does).")]
        [Min(0f)]
        [SerializeField] float springPerSecond = 3.5f;

        [Tooltip("Deflection below which the stick reads as exactly centred. It is not optional " +
                 "polish: the spring above is exponential and only ever APPROACHES zero, so this " +
                 "is what actually lands on it. Without it the vessel carries a permanent " +
                 "sub-perceptual turn, which reads as drift rather than as a control.")]
        [Range(0.001f, 0.25f)]
        [SerializeField] float deadZone = 0.02f;

        [Header("Hold annulus")]
        [Tooltip("Deflection at which the spring STARTS fading out. Below this the stick is a " +
                 "pure rate control. Together with the outer radius this sets EscapeSpeed — how " +
                 "briskly you have to sweep to commit to a held turn — so raise it to make " +
                 "committing more deliberate.")]
        [Range(0f, 1f)]
        [SerializeField] float holdInnerRadius = 0.88f;

        [Tooltip("Inner edge of the HOLD ANNULUS: at and beyond this deflection the spring is " +
                 "exactly zero, so the stick stays where a sweep parked it and the vessel keeps " +
                 "turning with the mouse dead still. This is the ring that fixes 'holding a hard " +
                 "turn costs hundreds of pixels of desk'.\n\n" +
                 "Set to 1 to disable the annulus entirely and get the original pure-spring " +
                 "scheme back, bit for bit.")]
        [Range(0f, 1f)]
        [SerializeField] float holdOuterRadius = 0.97f;

        [Header("Widget")]
        [Tooltip("Draw the virtual stick on screen while the scheme is flying. A bounded-cursor " +
                 "scheme without one is unflyable in a specific way: you cannot tell whether you " +
                 "are parked in the hold annulus or drifting back through the spring, and those " +
                 "two states fly completely differently. Off is for capture and screenshots.")]
        [SerializeField] bool showWidget = true;

        [Tooltip("Widget radius as a fraction of the SHORTER screen dimension, so it holds its " +
                 "size on any aspect. 0.1 is ~108 px at 1080p.")]
        [Range(0.03f, 0.35f)]
        [SerializeField] float widgetScreenFraction = 0.1f;

        [Tooltip("Widget tint. Deliberately neutral by default: domain colour means TEAM " +
                 "everywhere else in the game (Docs/PALETTE.md), so an instrument that wears one " +
                 "is making a claim it does not mean.")]
        [SerializeField] Color widgetColor = new Color(1f, 1f, 1f, 1f);

        public float StickUnitsPerPixel => Mathf.Max(0.0001f, stickUnitsPerPixel);
        public float SpringPerSecond => Mathf.Max(0f, springPerSecond);
        public float DeadZone => Mathf.Clamp(deadZone, 0.001f, 0.25f);

        public float HoldOuterRadius => Mathf.Clamp01(holdOuterRadius);

        /// <summary>Clamped to the outer radius, so a band authored inside-out cannot make the
        /// spring falloff disagree with itself.</summary>
        public float HoldInnerRadius => Mathf.Min(Mathf.Clamp01(holdInnerRadius), HoldOuterRadius);

        public bool ShowWidget => showWidget;
        public float WidgetScreenFraction => Mathf.Clamp(widgetScreenFraction, 0.03f, 0.35f);
        public Color WidgetColor => widgetColor;

        /// <summary>Mouse travel, in pixels, from centre to hard over.</summary>
        public float PixelsToFullDeflection => 1f / StickUnitsPerPixel;

        /// <summary>Mouse travel, in pixels, from centre to the inner edge of the hold annulus —
        /// the sweep that commits to a turn the vessel then holds for free.</summary>
        public float PixelsToHoldAnnulus => HoldOuterRadius / StickUnitsPerPixel;

        /// <summary>
        /// The deflection a sustained drag of <paramref name="pixelsPerSecond"/> settles at under
        /// these numbers — the curve to reason about when retuning, rather than any field alone.
        /// </summary>
        public float SustainedDeflection(float pixelsPerSecond)
            => CosmicShore.Gameplay.MouseVirtualStick.SustainedDeflection(
                   pixelsPerSecond, StickUnitsPerPixel, SpringPerSecond,
                   HoldInnerRadius, HoldOuterRadius);

        /// <summary>
        /// The drag speed at and above which the stick commits to the hold annulus and stays
        /// there. The scheme's one threshold: slower is a stable partial turn, faster is a locked
        /// one.
        /// </summary>
        public float EscapeSpeed
            => CosmicShore.Gameplay.MouseVirtualStick.EscapeSpeed(
                   StickUnitsPerPixel, SpringPerSecond, HoldInnerRadius, HoldOuterRadius);

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
