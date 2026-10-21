using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How a vessel's abilities are pressed on a phone (Garrett, 2026-10-21; Docs/Reports/MOBILE_VESSELS.md).
    /// </summary>
    public enum TouchScheme
    {
        /// <summary>Lifting a thumb is an ability (the pad's triggers): lift the right thumb = LT, the
        /// left = RT, both = <see cref="InputEvents.IdleAction"/>. For hulls whose abilities all sit on
        /// those gestures (Squirrel, Dolphin, Manta, Rhino, Butterfly, Thresher).</summary>
        ThumbLifts = 0,

        /// <summary>Lifting a thumb does nothing; every ability is its totem card (plus the profile's
        /// extra buttons), laid out round the right thumb. For hulls that need the cards anyway, where a
        /// thumb that lifts to reach one would otherwise fire that thumb's ability on the way.</summary>
        TotemButtons = 1,
    }

    /// <summary>The touch control a card names on a phone, drawn where a pad shows its button glyph.</summary>
    public enum TouchGlyph
    {
        None = 0,
        LiftLeftThumb = 1,
        LiftRightThumb = 2,
        LiftEitherThumb = 3,
        LiftBothThumbs = 4,
        ThumbsApart = 5,
    }

    /// <summary>How an on-screen touch button presses its input.</summary>
    public enum TouchButtonBehaviour
    {
        /// <summary>Down presses, up releases - the control's own semantics.</summary>
        Hold = 0,

        /// <summary>A tap presses and the next tap releases, so a thumb is free while it stays on.</summary>
        Toggle = 1,
    }

    /// <summary>A touch button that is not one of the four totems (a fire button, a zoom, a recall).</summary>
    [Serializable]
    public class TouchExtraButton
    {
        [Tooltip("The input the button presses, raised on the pilot's InputStatus like the pad control.")]
        public InputEvents input = InputEvents.RightStickAction;

        [Tooltip("Hold: down presses, up releases. Toggle: one tap presses, the next releases.")]
        public TouchButtonBehaviour behaviour = TouchButtonBehaviour.Hold;

        [Tooltip("While held, the finger becomes the RIGHT stick: dragging it aims (the Brittle Star's " +
                 "fire button). Only single-stick hulls read the right stick for aiming.")]
        public bool aimsWhileHeld;

        [Tooltip("The button's icon.")]
        public Sprite icon;

        [Tooltip("Element whose colour the button wears. None uses the drift colour.")]
        public Element tint = Element.None;

        [Tooltip("Player-facing caption under the button. Empty shows none.")]
        public string label;

        [Tooltip("Seated before the totems, in the easiest reach (a fire button).")]
        public bool primary;
    }

    /// <summary>The glyph a non-elemental card shows on a phone (the Squirrel's drift).</summary>
    [Serializable]
    public class TouchCoreGlyph
    {
        public CoreAbility core = CoreAbility.Drift;
        public TouchGlyph glyph = TouchGlyph.None;
    }

    /// <summary>The glyph one card shows on a phone.</summary>
    [Serializable]
    public class TouchElementGlyph
    {
        public Element element = Element.Charge;
        public TouchGlyph glyph = TouchGlyph.None;
    }

    /// <summary>
    /// One vessel's phone controls. Lives in <see cref="TouchControlProfileLibrarySO"/>
    /// (Resources/TouchControlProfiles), read by <see cref="TouchInputStrategy"/> for flight and by the
    /// vessel HUD for the cards and buttons.
    /// </summary>
    [Serializable]
    public class TouchControlProfile
    {
        [Tooltip("The hull this profile is for.")]
        public VesselClassType vessel;

        [Tooltip("ThumbLifts: thumb lifts are abilities. TotemButtons: cards (and extra buttons) are the " +
                 "only ability controls, arranged round the right thumb.")]
        public TouchScheme scheme = TouchScheme.ThumbLifts;

        [Header("Thumb lifts")]
        [Tooltip("ThumbLifts only. Hold a lift back this long before it presses, and drop it if a finger " +
                 "lands on a card meanwhile - for a hull that has a card AND lift abilities, so reaching the " +
                 "card does not fire the lift. 0 presses at once.")]
        [Min(0f)] public float liftDeferSeconds;

        [Header("Tilt")]
        [Tooltip("While one thumb flies, tilting the phone like a steering wheel rolls the hull (the axis " +
                 "the second thumb gave). The Dolphin.")]
        public bool tiltRollOnOneThumb;

        [Tooltip("With no thumb on the glass, tilting the phone steers: wheel-turn yaws, tipping forward and " +
                 "back pitches. The Manta's soar, the Thresher's launch aim.")]
        public bool tiltSteersWhileIdle;

        [Header("Both thumbs off")]
        [Tooltip("With no thumb on the glass, both analog triggers read fully pulled - the Rhino energizes " +
                 "its sword the way two held triggers do on a pad.")]
        public bool idlePullsBothTriggers;

        [Header("Totem buttons")]
        [Tooltip("Cards whose press latches: a tap presses, the next tap releases (the Serpent's scope).")]
        public List<Element> toggleElements = new();

        [Tooltip("Cards that are not buttons on a phone because another control replaces them (the " +
                 "Scarab's throttle card, replaced by its slider).")]
        public List<Element> displayOnlyElements = new();

        [Tooltip("Buttons beyond the four totems, laid out with them round the right thumb.")]
        public List<TouchExtraButton> extraButtons = new();

        [Tooltip("A set-and-hold throttle slider writing InputStatus.Throttle (the Scarab).")]
        public bool throttleSlider;

        [Tooltip("Where the throttle slider starts when the hull spawns, 0..1.")]
        [Range(0f, 1f)] public float throttleSliderStart = 0.5f;

        [Header("Glyphs")]
        [Tooltip("What each card names on a phone. A card with no entry names nothing.")]
        public List<TouchElementGlyph> glyphs = new();

        [Tooltip("What each non-elemental card names on a phone.")]
        public List<TouchCoreGlyph> coreGlyphs = new();

        public bool UsesThumbLifts => scheme == TouchScheme.ThumbLifts;

        public TouchGlyph GlyphFor(Element element)
        {
            for (int i = 0; i < glyphs.Count; i++)
                if (glyphs[i] != null && glyphs[i].element == element) return glyphs[i].glyph;
            return TouchGlyph.None;
        }

        public TouchGlyph GlyphFor(CoreAbility core)
        {
            for (int i = 0; i < coreGlyphs.Count; i++)
                if (coreGlyphs[i] != null && coreGlyphs[i].core == core) return coreGlyphs[i].glyph;
            return TouchGlyph.None;
        }

        public bool IsToggle(Element element) => toggleElements.Contains(element);

        public bool IsDisplayOnly(Element element) => displayOnlyElements.Contains(element);
    }
}
