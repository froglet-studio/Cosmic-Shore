using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The fleet's phone controls: one <see cref="TouchControlProfile"/> per hull, plus the tilt and
    /// button-layout tuning every hull shares. Loaded from <c>Resources/TouchControlProfiles</c>.
    /// A hull with no entry flies the default thumb-lift scheme with no extras.
    /// </summary>
    [CreateAssetMenu(fileName = "TouchControlProfiles", menuName = "ScriptableObjects/Input/Touch Control Profiles")]
    public class TouchControlProfileLibrarySO : ScriptableObject
    {
        public const string ResourcePath = "TouchControlProfiles";

        [Header("Hulls")]
        [SerializeField] List<TouchControlProfile> profiles = new();

        [Header("Tilt")]
        [Tooltip("Degrees of tilt from where the phone was held when tilt took over that read as a full stick.")]
        [SerializeField, Range(5f, 60f)] float tiltFullScaleDegrees = 25f;

        [Tooltip("Degrees of tilt that read as nothing, so a steady hand flies straight.")]
        [SerializeField, Range(0f, 10f)] float tiltDeadZoneDegrees = 2.5f;

        [Tooltip("How fast the tilt follows the phone (1/s) on a phone WITHOUT a gravity sensor, where the raw " +
                 "accelerometer carries every jolt of the hand. A gravity sensor is read unsmoothed (no lag).")]
        [SerializeField, Range(1f, 40f)] float tiltSmoothingPerSecond = 12f;

        [Tooltip("The phone-as-window roll (the Dolphin's aim, TouchControlProfile.tiltRollOnOneThumb): the hull " +
                 "rolls one to one with the phone up to this many degrees either side of where the phone was " +
                 "held when the aim began, and holds there past it.")]
        [SerializeField, Range(30f, 180f)] float tiltRollMaxDegrees = 90f;

        [Header("Nose")]
        [Tooltip("How fast a touch pilot's nose closes on the thumbs' command, per second (the frame closes " +
                 "1 - e^(-rate * dt) of the gap): a first-order lag that answers at once and eases into the " +
                 "target. 0 is no lag (2026-10-21 to 10-23). The pad flies 1.5 (GamepadFlightConfig). Before " +
                 "2026-10-21 the Squirrel, Stoat, Thresher and Angler flew touch at 9 and the Butterfly at 5; " +
                 "every other hull at 1.5. A thumb on glass stops dead and lifts off in a frame, so a short lag (5-9) rounds " +
                 "those edges without the pad's long glide. One value for every hull; shipped 9 (a 0.11 s time " +
                 "constant), the value the touch controls flew on four hulls 2026-10-05 to 10-21.")]
        [SerializeField, Range(0f, 20f)] float noseFollowPerSecond = 9f;

        [Header("Button layout (millimetres on the glass)")]
        [Tooltip("Width of one totem or button on the glass. Platform guidance is 7-9 mm minimum.")]
        [SerializeField, Range(6f, 16f)] float buttonMillimetres = 10f;

        [Tooltip("Gap between neighbouring buttons on the glass.")]
        [SerializeField, Range(0.5f, 6f)] float buttonGapMillimetres = 1.5f;

        [Tooltip("Radius of the first arc of buttons round the bottom-right corner (the right thumb's pivot).")]
        [SerializeField, Range(15f, 80f)] float innerArcMillimetres = 22f;

        [Tooltip("Distance between the first and second arc.")]
        [SerializeField, Range(8f, 30f)] float arcSpacingMillimetres = 12f;

        [Tooltip("The buttons never reach higher than this fraction of the screen, so the view stays clear.")]
        [SerializeField, Range(0.2f, 0.7f)] float maxHeightFraction = 0.55f;

        [Tooltip("...and never further left than this fraction of the screen's width.")]
        [SerializeField, Range(0.15f, 0.5f)] float maxWidthFraction = 0.36f;

        [Header("Art (Tools/Build/touch_glyph_art.py draws it)")]
        [SerializeField] Sprite glyphLiftLeftThumb;
        [SerializeField] Sprite glyphLiftRightThumb;
        [SerializeField] Sprite glyphLiftEitherThumb;
        [SerializeField] Sprite glyphLiftBothThumbs;
        [SerializeField] Sprite glyphThumbsApart;
        [Tooltip("The filled disc every extra button and the throttle knob are drawn on.")]
        [SerializeField] Sprite buttonDisc;
        [Tooltip("The rim drawn round every extra button, in its element colour.")]
        [SerializeField] Sprite buttonRim;

        [Header("Throttle slider (millimetres on the glass)")]
        [SerializeField, Range(6f, 16f)] float sliderWidthMillimetres = 9f;
        [SerializeField, Range(15f, 60f)] float sliderHeightMillimetres = 32f;

        public Sprite ButtonDisc => buttonDisc;
        public Sprite ButtonRim => buttonRim;
        public float SliderWidthMillimetres => sliderWidthMillimetres;
        public float SliderHeightMillimetres => sliderHeightMillimetres;

        public Sprite GlyphSprite(TouchGlyph glyph) => glyph switch
        {
            TouchGlyph.LiftLeftThumb => glyphLiftLeftThumb,
            TouchGlyph.LiftRightThumb => glyphLiftRightThumb,
            TouchGlyph.LiftEitherThumb => glyphLiftEitherThumb,
            TouchGlyph.LiftBothThumbs => glyphLiftBothThumbs,
            TouchGlyph.ThumbsApart => glyphThumbsApart,
            _ => null,
        };

        public float TiltFullScaleDegrees => tiltFullScaleDegrees;
        public float TiltDeadZoneDegrees => tiltDeadZoneDegrees;
        public float TiltSmoothingPerSecond => tiltSmoothingPerSecond;
        public float TiltRollMaxDegrees => tiltRollMaxDegrees;
        public float NoseFollowPerSecond => Mathf.Max(0f, noseFollowPerSecond);
        public float ButtonMillimetres => buttonMillimetres;
        public float ButtonGapMillimetres => buttonGapMillimetres;
        public float InnerArcMillimetres => innerArcMillimetres;
        public float ArcSpacingMillimetres => arcSpacingMillimetres;
        public float MaxHeightFraction => maxHeightFraction;
        public float MaxWidthFraction => maxWidthFraction;

        public IReadOnlyList<TouchControlProfile> Profiles => profiles;

        static TouchControlProfileLibrarySO s_loaded;
        static bool s_loadAttempted;
        static readonly TouchControlProfile s_default = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loaded = null;
            s_loadAttempted = false;
        }

        /// <summary>The shipped library, or null when the asset is missing (the error is logged once).</summary>
        public static TouchControlProfileLibrarySO Instance
        {
            get
            {
                if (s_loadAttempted) return s_loaded;
                s_loadAttempted = true;
                s_loaded = Resources.Load<TouchControlProfileLibrarySO>(ResourcePath);
                if (!s_loaded)
                    Debug.LogError($"[TouchControlProfiles] No TouchControlProfileLibrarySO at Resources/{ResourcePath}: " +
                                   "every hull flies the default thumb-lift scheme on a phone.");
                return s_loaded;
            }
        }

        /// <summary>The profile for <paramref name="vessel"/>, or the default thumb-lift profile.</summary>
        public static TouchControlProfile For(VesselClassType vessel)
        {
            var library = Instance;
            return library ? library.Find(vessel) : s_default;
        }

        public TouchControlProfile Find(VesselClassType vessel)
        {
            for (int i = 0; i < profiles.Count; i++)
                if (profiles[i] != null && profiles[i].vessel == vessel) return profiles[i];
            return s_default;
        }
    }
}
