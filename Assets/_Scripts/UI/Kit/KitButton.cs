using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>The button variants of Docs/STYLE_FOUNDATION.md §10.1. Explicit values: serialized.</summary>
    public enum KitButtonVariant
    {
        /// <summary>Standard action.</summary>
        Default = 0,
        /// <summary>Unavailable. A button whose own interactable flag is off is drawn this way whatever its variant.</summary>
        Inactive = 1,
        /// <summary>Cancel; "coming soon" in the store.</summary>
        Cancel = 2,
        /// <summary>Spends something: a purchase, an upgrade, any button that confirms using a resource. Gold, and gold
        /// means nothing else (Garrett's palette, Docs/UI_COLOR_RULES.md).</summary>
        Purchase = 3,
        /// <summary>Free / primary action. Lime: always categorically good.</summary>
        Cta = 4,
        /// <summary>Launch a game: the call to action, in lime. The opaque Play button is the one gradient fill in the menu
        /// (§1.4).</summary>
        Play = 5,
        /// <summary>Destructive. Fire, for danger, caution and problems only. Always drawn opaque (§2).</summary>
        Danger = 6,
        /// <summary>Retired (rev 6 drew it in an element colour; elements carry no colour). Draws as <see cref="Default"/>.
        /// Kept so serialized buttons keep their value.</summary>
        Mass = 7,
        /// <summary>Retired, as <see cref="Mass"/>. Draws as <see cref="Default"/>.</summary>
        Space = 8,
        /// <summary>Retired, as <see cref="Mass"/>. Draws as <see cref="Default"/>.</summary>
        Time = 9,
    }

    /// <summary>§10.1: opaque on popups and wherever a button sits on a line, transparent everywhere else.</summary>
    public enum KitButtonFill
    {
        Transparent = 0,
        Opaque = 1,
    }

    /// <summary>
    /// A §10.1 button: a slivered fill, a slivered border and a caps label (plus optional icons),
    /// all tinted from the theme by variant, fill mode and interaction state. See
    /// <see cref="KitSelectableBase"/> for the state model and the focus ring.
    ///
    /// The colour table is the approved style-B kit mockup (Docs/MENU_REDESIGN_B.md, item 0): a
    /// transparent button is its role colour as the border, the same colour at 12% as the fill, and
    /// a light or role-coloured label; an opaque button is a solid role-colour fill with a dark label.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Button")]
    public class KitButton : KitSelectableBase
    {
        private const float TintAlpha = 0.12f;         // the transparent fill, §3's "Team (Dark) @ 12%" idiom
        private const float PlayTintAlpha = 0.24f;     // the transparent Play button reads stronger than Default
        private const float HoverTintBoost = 0.08f;
        private const float PressTintBoost = 0.20f;
        private const float HoverLighten = 0.10f;
        private const float PressDarken = 0.15f;

        [Header("Style (§10.1)")]
        [Tooltip("Which §10.1 variant this button is.")]
        [SerializeField] private KitButtonVariant variant = KitButtonVariant.Default;
        [Tooltip("Opaque on popups and on a line; transparent everywhere else (§10.1). Danger ignores this.")]
        [SerializeField] private KitButtonFill fill = KitButtonFill.Transparent;

        [Header("Parts")]
        [Tooltip("The slivered fill (kit_sliver10_fill, Sliced).")]
        [SerializeField] private Graphic fillGraphic;
        [Tooltip("The slivered border (kit_sliver10_line1, Sliced).")]
        [SerializeField] private Graphic borderGraphic;
        [Tooltip("Label and icons - every graphic that takes the content colour.")]
        [SerializeField] private Graphic[] contentGraphics = { };
        [Tooltip("The Play gradient on the fill graphic. Enabled only for an opaque Play button.")]
        [SerializeField] private KitGradient playGradient;

        public KitButtonVariant Variant
        {
            get => variant;
            set
            {
                variant = value;
                Refresh(true);
            }
        }

        public KitButtonFill Fill
        {
            get => fill;
            set
            {
                fill = value;
                Refresh(true);
            }
        }

        private readonly struct Palette
        {
            public readonly Color Border;
            public readonly Color Fill;
            public readonly Color Content;

            public Palette(Color border, Color fill, Color content)
            {
                Border = border;
                Fill = fill;
                Content = content;
            }
        }

        protected override void CollectColors(VisualState state, List<(Graphic graphic, Color color)> into)
        {
            var v = state == VisualState.Disabled ? KitButtonVariant.Inactive : variant;
            bool opaque = fill == KitButtonFill.Opaque || v == KitButtonVariant.Danger;
            var p = PaletteFor(v, opaque);

            Color fillColor = p.Fill;
            if (state == VisualState.Highlighted || state == VisualState.Pressed)
            {
                if (opaque)
                {
                    float a = fillColor.a;
                    fillColor = state == VisualState.Pressed
                        ? Color.Lerp(fillColor, Color.black, PressDarken)
                        : Color.Lerp(fillColor, Color.white, HoverLighten);
                    fillColor.a = a;
                }
                else
                    fillColor.a += state == VisualState.Pressed ? PressTintBoost : HoverTintBoost;
            }

            if (fillGraphic) into.Add((fillGraphic, fillColor));
            if (borderGraphic) into.Add((borderGraphic, p.Border));
            if (contentGraphics != null)
                foreach (var g in contentGraphics)
                    if (g) into.Add((g, p.Content));

            if (playGradient) playGradient.enabled = v == KitButtonVariant.Play && opaque;
        }

        /// <summary>The variant's colours by role (Docs/UI_COLOR_RULES.md): neutral blue, lime for the call to action,
        /// gold for a spend, fire for danger.</summary>
        private Palette PaletteFor(KitButtonVariant v, bool opaque)
        {
            var dark = Token(UIColorToken.SurfaceBlack);
            switch (v)
            {
                case KitButtonVariant.Inactive:
                    return opaque
                        ? new Palette(Token(UIColorToken.InactiveLight), Token(UIColorToken.InactiveLight), Token(UIColorToken.TextInactive))
                        : new Palette(Token(UIColorToken.InactiveLight), Token(UIColorToken.InactiveLight, TintAlpha), Token(UIColorToken.InactiveLight));
                case KitButtonVariant.Cancel:
                    return opaque
                        ? new Palette(Token(UIColorToken.NeutralLightest), Token(UIColorToken.NeutralLightest), dark)
                        : new Palette(Token(UIColorToken.NeutralLightest), Token(UIColorToken.NeutralLightest, TintAlpha), Token(UIColorToken.TextLight));
                case KitButtonVariant.Purchase:
                    return opaque
                        ? new Palette(Token(UIColorToken.Purchase), Token(UIColorToken.Purchase), dark)
                        : new Palette(Token(UIColorToken.Purchase), Token(UIColorToken.Purchase, TintAlpha), Token(UIColorToken.Purchase));
                case KitButtonVariant.Cta:
                    return opaque
                        ? new Palette(Token(UIColorToken.Cta), Token(UIColorToken.Cta), dark)
                        : new Palette(Token(UIColorToken.Cta), Token(UIColorToken.Cta, TintAlpha), Token(UIColorToken.Cta));
                case KitButtonVariant.Play:
                {
                    // Play is the call to action, in lime. The opaque fill runs lime -> lime halfway to white (the
                    // KitGradient); its border is that lighter end. Transparent, the label is lime too.
                    var cta = Token(UIColorToken.Cta);
                    return opaque
                        ? new Palette(Color.Lerp(cta, Color.white, KitGradient.DefaultLighten), cta, dark)
                        : new Palette(cta, Token(UIColorToken.Cta, PlayTintAlpha), cta);
                }
                case KitButtonVariant.Danger:
                    return new Palette(Token(UIColorToken.Danger), Token(UIColorToken.Danger), dark);
                default:
                    return opaque
                        ? new Palette(Token(UIColorToken.TextLight), Token(UIColorToken.TextLight), dark)
                        : new Palette(Token(UIColorToken.ControlActive), Token(UIColorToken.ControlActive, TintAlpha), Token(UIColorToken.TextLight));
            }
        }
    }
}
