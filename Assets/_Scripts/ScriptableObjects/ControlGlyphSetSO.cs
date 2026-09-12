using System;
using System.Collections.Generic;
using UnityEngine;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The fleet's ONE control-glyph table: which artwork stands for a physical control.
    ///
    /// <para>Every vessel used to author its own glyphs, and the result was three HUDs carrying
    /// device-icon roots with no switcher to drive them - never lit, never matched to the player's
    /// actual device, and stranded wherever the old ability row happened to sit. The lockup already
    /// owns where a control chip goes and how big it is; this is what lets it own the CONTENT too,
    /// so a vessel authors no glyphs at all.</para>
    ///
    /// <para>Keyed by physical CONTROL, not by ability: an ability's own
    /// <c>ElementalAbilityMapSO</c> entry names its <c>InputEvents</c>,
    /// <see cref="CosmicShore.UI.InputHintBindingMap"/> turns that into the control, and this turns
    /// the control into a picture. Nothing is guessed and nothing is per-vessel.</para>
    ///
    /// <para>Pad families deliberately share one sprite. The only working hint set in the project
    /// (the Squirrel's) already used the same L1/R1 art for Xbox and PlayStation, and the one HUD
    /// that tried to differ authored a set that did not correspond - Xbox A/B/R1/R2 against PS
    /// circle/triangle/L2/square - which is how you get a label that is confidently wrong.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ControlGlyphSet",
                     menuName = "ScriptableObjects/UI/Control Glyph Set")]
    public class ControlGlyphSetSO : ScriptableObject
    {
        [Serializable]
        public class Glyph
        {
            [Tooltip("The physical control this artwork stands for.")]
            public HintBinding binding = HintBinding.None;

            [Tooltip("Pad artwork at rest.")]
            public Sprite padGlyph;

            [Tooltip("Pad artwork while the control is held. Empty = tint the resting one instead.")]
            public Sprite padGlyphHeld;

            [Tooltip("What a keyboard player sees. Empty = this control has no keyboard equivalent " +
                     "and the chip stays blank there, which is honest - a pad glyph shown to a " +
                     "keyboard player is misinformation, and that is the state this replaced.")]
            public string keyboardLabel;
        }

        [Tooltip("One entry per physical control the fleet's abilities actually bind.")]
        [SerializeField] private List<Glyph> glyphs = new();

        [Header("Chip presentation")]
        [Tooltip("Colour of a glyph at rest.")]
        public Color restColor = new(0.65f, 0.65f, 0.7f, 1f);
        [Tooltip("Colour while the control is held.")]
        public Color heldColor = Color.white;
        [Tooltip("Point size of the keyboard label.")]
        [Min(1f)] public float keyboardLabelSize = 14f;

        /// <summary>
        /// The artwork for a control, or null when the fleet authors none for it.
        ///
        /// <para>A keyboard binding falls back to its PAD twin
        /// (<see cref="CosmicShore.UI.InputHintBindingMap.Canonical"/>), because one entry here
        /// carries BOTH representations of one logical control while
        /// <c>InputHintBindingMap.BindingFor</c> answers with a different binding per device.
        /// Without the fallback a keyboard lookup asked for <c>KeyLeftShift</c> while the label
        /// was authored on <c>PadLeftTrigger</c> and drew nothing — which is why no vessel has
        /// ever shown a keyboard chip. The exact binding still wins, so an asset that wants a
        /// keyboard-specific row can still author one.</para>
        ///
        /// <para>Returning a pad-keyed entry to a keyboard player is safe: the caller
        /// (<c>AbilityLockupView.RefreshChip</c>) picks <c>padGlyph</c> or <c>keyboardLabel</c>
        /// from the DEVICE, never from which binding matched, so a keyboard player still cannot
        /// be shown pad artwork.</para>
        /// </summary>
        public Glyph For(HintBinding binding)
        {
            if (binding == HintBinding.None) return null;

            var exact = Find(binding);
            if (exact != null) return exact;

            var canonical = CosmicShore.UI.InputHintBindingMap.Canonical(binding);
            return canonical == binding ? null : Find(canonical);
        }

        Glyph Find(HintBinding binding)
        {
            foreach (var glyph in glyphs)
                if (glyph != null && glyph.binding == binding) return glyph;
            return null;
        }
    }
}
