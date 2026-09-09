using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One <b>variant</b> of a toy on the Toy Box's detail window - a domain to wear, a world to
    /// switch to, an Ark to sail, a species to release. The flat twin of a station inside a toy's
    /// matrix, where <see cref="ToyboxCard"/> is the flat twin of the toy itself.
    ///
    /// <para>Like every card in this menu it is a VIEW: it holds no toy state and makes no
    /// decision. <see cref="ToyConfigureModal"/> binds it and owns what a press does, so a card
    /// can never become a second authority on what a toy's option means.</para>
    ///
    /// <para><b>Colour is the same treatment the Toy Box grid uses, with one addition it needs and
    /// the grid does not.</b> A toy card tints its fill from the toy's own accent, and every toy
    /// has a different one; a variants list is usually one toy's accent repeated down the whole
    /// column (the cell selector paints every world in the selector's colour) with the domain
    /// changer as the exception that genuinely gives three. So the fill alone cannot say which row
    /// is selected, and the BORDER carries that instead - which is also why the two are driven from
    /// one accent rather than authored separately.</para>
    ///
    /// <para>The rim is always brighter than the base, in every state. That is not a look
    /// preference: it is the invariant <c>Docs/PALETTE.md</c> §4.0 states for the prism tiers, and
    /// it held on nine of twelve tier x domain pairs by accident rather than by rule until each
    /// violation was separately rationalised and then recognised as one defect. A card is a
    /// different surface, but the reading is the same one the player has already learnt.</para>
    ///
    /// <para><b>Authored alpha survives.</b> Only RGB is written; each graphic's own alpha is
    /// captured once and put back. A designer's translucent plate stays translucent, and a card
    /// whose art was drawn to sit over a gradient does not turn into a solid block the first time
    /// it is bound.</para>
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ToyVariantCard : MonoBehaviour
    {
        [SerializeField, Tooltip("The card's fill. Tinted from the option's own accent - the " +
                                 "colour the station wears in the world.")]
        Image background;

        [SerializeField, Tooltip("The card's rim. Carries SELECTION, because a whole column of " +
                                 "one toy's accent cannot say it in the fill.")]
        Image border;

        [SerializeField] TMP_Text nameText;

        [SerializeField, Tooltip("Optional second line: 'current', 'flying', a painting's " +
                                 "progress. Left unwired the card is just a name, which is what " +
                                 "the authored template is.")]
        TMP_Text detailText;

        [SerializeField, Tooltip("Optional extra mark for the selected row, for a template that " +
                                 "authors one. The border tint is the fallback and is always applied.")]
        GameObject selectedMarker;

        [Header("Tint")]
        [SerializeField, Range(0f, 1f), Tooltip("How much of the accent the fill carries at rest. " +
                 "Muted rather than dark: the Toy Box grid draws its cards at the FULL accent, and " +
                 "a variants list two shades below that reads as a disabled version of the same " +
                 "product rather than as a different part of it.")]
        float restFill = 0.45f;

        [SerializeField, Range(0f, 1f), Tooltip("How much of the accent the fill carries once " +
                 "the row is the selected one - close to the grid card's own full accent, so the " +
                 "row the player picked is the one that looks like a Toy Box card.")]
        float selectedFill = 0.80f;

        [SerializeField, Range(0f, 1f), Tooltip("How much of the accent the rim carries at rest. " +
                 "Kept above restFill - see the class remarks. The selected row's rim goes to the " +
                 "full accent, which keeps it above the brighter selected fill too.")]
        float restRim = 0.70f;

        Button _button;
        Color _backgroundAlpha = Color.white;
        Color _borderAlpha = Color.white;
        bool _captured;

        /// <summary>The option this card is currently drawing, or null.</summary>
        public ToyShellOption Option { get; private set; }

        public Button Button => _button ? _button : _button = GetComponent<Button>();

        void Awake() => CaptureAuthoredAlpha();

        /// <summary>
        /// Remember each graphic's authored alpha, once. Read in <c>Awake</c> so it is the
        /// TEMPLATE's value rather than whatever the last bind left behind - re-reading it per
        /// bind would capture a colour this component itself wrote.
        /// </summary>
        void CaptureAuthoredAlpha()
        {
            if (_captured) return;
            _captured = true;
            if (background) _backgroundAlpha = background.color;
            if (border) _borderAlpha = border.color;
        }

        public void Bind(ToyShellOption option, bool selected)
        {
            CaptureAuthoredAlpha();
            Option = option;

            if (option == null)
            {
                if (nameText) nameText.text = "";
                if (detailText) detailText.text = "";
                if (selectedMarker) selectedMarker.SetActive(false);
                Button.interactable = false;
                return;
            }

            // A branch opens another layer rather than acting, and the chevron is how a flat list
            // says so without a second sprite to author. Appended here rather than baked into the
            // toy's own Label, which is the station's name and is read by the world too.
            if (nameText) nameText.text = option.IsBranch ? $"{option.Label}  ›" : option.Label;
            if (detailText) detailText.text = option.Detail;

            var accent = option.Accent;
            float fill = selected ? Mathf.Max(restFill, selectedFill) : restFill;
            // The rim never falls under the fill: at rest that is restRim's own floor, and on the
            // selected row - where the fill is brightest - it goes to the full accent.
            float rim = selected ? 1f : Mathf.Max(fill, restRim);

            if (background) background.color = Tint(accent, fill, _backgroundAlpha.a);
            if (border) border.color = Tint(accent, rim, _borderAlpha.a);
            if (selectedMarker) selectedMarker.SetActive(selected);

            // A row with neither an action nor a layer under it is there to be READ - the hull you
            // are already flying, the domain you already wear. It stays drawn and stops responding.
            Button.interactable = option.Apply != null || option.IsBranch;
        }

        static Color Tint(Color accent, float amount, float alpha) =>
            new(accent.r * amount, accent.g * amount, accent.b * amount, alpha);
    }
}
