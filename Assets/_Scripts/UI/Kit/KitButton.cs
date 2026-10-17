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
        /// <summary>Any spend - icon + amount. Drawn in Mass (rev 6: Mass is what you own).</summary>
        Purchase = 3,
        /// <summary>Free / primary action.</summary>
        Cta = 4,
        /// <summary>Launch a game. Drawn in Charge (rev 6). The opaque Play button is the one gradient fill in the menu (§1.4).</summary>
        Play = 5,
        /// <summary>Destructive. Danger is a full-bleed fill only (§2), so it is always drawn opaque.</summary>
        Danger = 6,
        /// <summary>Rev 6: an action about something you own that is not a spend (equip, upgrade a preview).</summary>
        Mass = 7,
        /// <summary>Rev 6: an action about people - invite, join, accept.</summary>
        Space = 8,
        /// <summary>Rev 6: an action about progress - claim, watch a record, see the leaderboard.</summary>
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

        /// <summary>An element-coloured variant: outline + tint + element label, or an element fill with a dark label.</summary>
        private Palette Element(UIColorToken token, bool opaque, Color dark) => opaque
            ? new Palette(Token(token), Token(token), dark)
            : new Palette(Token(token), Token(token, TintAlpha), Token(token));

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
                case KitButtonVariant.Mass:
                    return Element(UIColorToken.ElementMass, opaque, dark);
                case KitButtonVariant.Space:
                    return Element(UIColorToken.ElementSpace, opaque, dark);
                case KitButtonVariant.Time:
                    return Element(UIColorToken.ElementTime, opaque, dark);
                case KitButtonVariant.Cta:
                    return opaque
                        ? new Palette(Token(UIColorToken.Cta), Token(UIColorToken.Cta), dark)
                        : new Palette(Token(UIColorToken.Cta), Token(UIColorToken.Cta, TintAlpha), Token(UIColorToken.Cta));
                case KitButtonVariant.Play:
                {
                    // Rev 6: Play is Charge. The opaque fill runs Charge -> Charge halfway to white (the
                    // KitGradient); its border is that lighter end. Transparent, the label is Charge too.
                    var charge = Token(UIColorToken.ElementCharge);
                    return opaque
                        ? new Palette(Color.Lerp(charge, Color.white, KitGradient.DefaultLighten), charge, dark)
                        : new Palette(charge, Token(UIColorToken.ElementCharge, PlayTintAlpha), charge);
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
