using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
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
    ///
    /// <para><b>The selected row also LIGHTS and LIFTS, and both come from the HUD's own style
    /// asset.</b> The glow is <c>AbilityLockupStyleSO.bloomSprite</c> behind the card — the same
    /// sprite the ability lockup puts behind an upgraded card and the goal stack puts behind its
    /// plate — so the Toy Box reads as one product with the HUD rather than as a menu that invented
    /// its own idea of "selected". Reading a HUD style from a menu surface is the established
    /// pattern here, not a new coupling: the arcade card's ability preview does the same thing for
    /// the same reason (<c>Docs/ArcadeLaunch/ARCHITECTURE.md</c>).</para>
    ///
    /// <para>The bloom is built LAZILY and only ever on the card that needs one, so a list nobody
    /// has touched costs nothing; it fades rather than appearing, because continuity of existence
    /// applies to UI too; and it is a FIRST sibling, so it sits behind the authored art instead of
    /// over it. It wears the CALL-TO-ACTION colour (<c>SO_ColorSet.GetCtaSignalColor</c>, the lime
    /// a free pickup wears in the world - handed in by the window, which is what holds the theme)
    /// and BREATHES while the row is selected, so "this is the one the button will act on" is
    /// said by the one colour the palette reserves for "act on me" rather than by the row being
    /// a shade brighter than its neighbours.</para>
    ///
    /// <para><b>The fill is lifted toward white, never multiplied down.</b> The plate sprite is a
    /// dark navy slab, so <c>accent * 0.45</c> - the first cut - went to black under any accent and
    /// the rows read as text floating on nothing. A lerp FROM white TOWARD the accent keeps the
    /// authored plate at its own brightness at 0 and colours it at 1, so the card is always at
    /// least as visible as the sprite the designer drew.</para>
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
        [SerializeField, Range(0f, 1f), Tooltip("How far the fill is pulled from white TOWARD the " +
                 "accent at rest. 0 draws the plate exactly as authored; 1 paints it the accent. " +
                 "Muted rather than dark - see the class remarks.")]
        float restFill = 0.35f;

        [SerializeField, Range(0f, 1f), Tooltip("How far the fill is pulled toward the accent once " +
                 "the row is the selected one.")]
        float selectedFill = 0.60f;

        [SerializeField, Range(0f, 1f), Tooltip("How far the rim is pulled toward the accent at rest. " +
                 "The selected row's rim goes to the CTA colour instead - the same colour its glow wears.")]
        float restRim = 0.55f;

        [Header("Selection")]
        [SerializeField, Min(0f), Tooltip("How far the glow reaches past the card's own rect. The " +
                 "lockup's own 26 is sized for a HUD card standing alone; a grid cell has a " +
                 "neighbour 20 units away, so this is smaller on purpose.")]
        float bloomPadding = 12f;

        [SerializeField, Min(1f), Tooltip("How much the selected card lifts. Small on purpose - " +
                 "the grid has fixed cells, so a big lift reads as the card outgrowing its slot.")]
        float selectedScale = 1.04f;

        [SerializeField, Min(0.01f), Tooltip("Seconds for the glow and the lift.")]
        float selectionDuration = 0.16f;

        [SerializeField, Min(0.1f), Tooltip("Seconds for one breath of the selected row's glow " +
                 "(bright -> dim -> bright). Slow: a pulse under a second reads as an alarm.")]
        float pulsePeriod = 1.6f;

        [SerializeField, Range(0f, 1f), Tooltip("How far the glow dims at the bottom of a breath, " +
                 "as a fraction of its lit alpha. It never reaches zero - a glow that switches " +
                 "off is a selection that looks lost twice a second.")]
        float pulseFloor = 0.45f;

        [SerializeField, Tooltip("The glow's colour when the window has not handed one in - the " +
                 "lime the palette reserves for 'act on me'.")]
        Color fallbackCta = new(0.62f, 1f, 0.16f, 1f);

        Button _button;
        Color _backgroundAlpha = Color.white;
        Color _borderAlpha = Color.white;
        bool _captured;

        Image _bloom;
        bool _bloomMissing;
        bool _selected;
        Tween _bloomTween;
        Tween _pulseTween;
        Tween _liftTween;
        Color _cta;
        bool _ctaSet;

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

        /// <summary>
        /// The call-to-action colour the selected row glows in. Set by the window that owns the
        /// theme (<c>SO_ColorSet.GetCtaSignalColor</c>) before it binds rows; a card left without
        /// one glows in <see cref="fallbackCta"/>.
        /// </summary>
        public void SetCtaColor(Color cta)
        {
            _cta = cta;
            _ctaSet = cta.a > 0.001f;
        }

        Color Cta => _ctaSet ? _cta : fallbackCta;

        public void Bind(ToyShellOption option, bool selected)
        {
            CaptureAuthoredAlpha();
            Option = option;

            if (option == null)
            {
                if (nameText) nameText.text = "";
                if (detailText) detailText.text = "";
                if (selectedMarker) selectedMarker.SetActive(false);
                ApplySelection(false, Color.white);
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
            // selected row it wears the CTA colour - the glow's colour, so rim and glow read as
            // one lit edge rather than two opinions about which row is picked.
            float rim = Mathf.Max(fill, restRim);

            if (background) background.color = Tint(accent, fill, _backgroundAlpha.a);
            if (border) border.color = selected ? WithAlpha(Cta, _borderAlpha.a) : Tint(accent, rim, _borderAlpha.a);
            if (selectedMarker) selectedMarker.SetActive(selected);
            ApplySelection(selected, accent);

            // A row with neither an action nor a layer under it is there to be READ - the hull you
            // are already flying, the domain you already wear. It stays drawn and stops responding.
            Button.interactable = option.Apply != null || option.IsBranch;
        }

        /// <summary>
        /// Light and lift the selected card. Idempotent: re-binding the same state re-tints the
        /// glow (the accent can change under it when a domain is re-picked) without restarting the
        /// tweens, so a redraw cannot make a settled card flicker.
        /// </summary>
        void ApplySelection(bool selected, Color accent)
        {
            var style = ToyCardStyle;
            bool changed = selected != _selected;
            _selected = selected;

            if (selected && style) EnsureBloom(style);

            if (_bloom)
            {
                var styled = style ? style.bloomColor : new Color(0.96f, 0.96f, 1f, 0.3f);
                // Hue from the CTA, alpha from the style - the palette says "act on me" and the
                // product says how bright. Lifted a little toward white so a saturated lime glow
                // reads as light rather than as a green plate behind the card.
                var cta = Cta;
                float lit = Mathf.Max(styled.a, 0.55f);
                var target = new Color(Mathf.Lerp(cta.r, 1f, 0.2f),
                                       Mathf.Lerp(cta.g, 1f, 0.2f),
                                       Mathf.Lerp(cta.b, 1f, 0.2f),
                                       selected ? lit : 0f);

                _bloom.enabled = selected || _bloom.color.a > 0.001f;
                if (changed)
                {
                    _bloomTween?.Kill();
                    _pulseTween?.Kill();
                    _pulseTween = null;
                    _bloomTween = _bloom.DOColor(target, selectionDuration)
                        .SetEase(Ease.OutCubic).SetUpdate(true).SetLink(_bloom.gameObject);
                    // Then BREATHE for as long as the row stays selected: the lit alpha down to
                    // the floor and back, forever, on unscaled time like every menu tween.
                    if (selected)
                    {
                        float floor = lit * Mathf.Clamp01(pulseFloor);
                        _bloomTween.OnComplete(() =>
                        {
                            if (!_selected || !_bloom) return;
                            _pulseTween = _bloom.DOFade(floor, pulsePeriod * 0.5f)
                                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                                .SetUpdate(true).SetLink(_bloom.gameObject);
                        });
                    }
                }
                else
                {
                    // A re-tint mid-breath keeps the breath: only the hue is written.
                    _bloom.color = new Color(target.r, target.g, target.b, selected ? _bloom.color.a : 0f);
                }
            }

            if (!changed) return;

            _liftTween?.Kill();
            _liftTween = transform.DOScale(Vector3.one * (selected ? Mathf.Max(1f, selectedScale) : 1f),
                                           selectionDuration)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>
        /// Build the glow the first time a card is selected, and never again — including after a
        /// failure, which is what <c>_bloomMissing</c> is for: with no style asset there is nothing
        /// to draw, and retrying that on every bind would walk Resources once per row per redraw.
        /// </summary>
        void EnsureBloom(AbilityLockupStyleSO style)
        {
            if (_bloom || _bloomMissing) return;
            if (!style || !style.bloomSprite) { _bloomMissing = true; return; }

            var go = new GameObject("SelectionBloom", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            // FIRST, so it sits behind the card's authored art rather than over it.
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-bloomPadding, -bloomPadding);
            rect.offsetMax = new Vector2(bloomPadding, bloomPadding);

            _bloom = go.GetComponent<Image>();
            _bloom.sprite = style.bloomSprite;
            _bloom.type = Image.Type.Sliced;
            _bloom.raycastTarget = false;
            _bloom.color = new Color(1f, 1f, 1f, 0f);
        }

        static AbilityLockupStyleSO _style;
        static bool _styleLoaded;

        /// <summary>
        /// The HUD's own style asset, loaded once for the whole session. Static because every card
        /// wants the same one and a per-card Resources load is a load per row per redraw.
        /// </summary>
        static AbilityLockupStyleSO ToyCardStyle
        {
            get
            {
                if (_styleLoaded) return _style;
                _styleLoaded = true;
                _style = Resources.Load<AbilityLockupStyleSO>("AbilityLockupStyle");
                return _style;
            }
        }

        void OnDisable()
        {
            // A pooled card is hidden mid-tween whenever the layer changes under it. Kill and snap
            // to rest, or it comes back holding a half-played lift.
            _bloomTween?.Kill();
            _pulseTween?.Kill();
            _liftTween?.Kill();
            _bloomTween = _pulseTween = _liftTween = null;
            transform.localScale = Vector3.one;
            if (_bloom) _bloom.color = new Color(_bloom.color.r, _bloom.color.g, _bloom.color.b, 0f);
            _selected = false;
        }

        /// <summary>White pulled <paramref name="amount"/> of the way toward the accent, at the authored alpha.</summary>
        static Color Tint(Color accent, float amount, float alpha) =>
            new(Mathf.Lerp(1f, accent.r, amount), Mathf.Lerp(1f, accent.g, amount), Mathf.Lerp(1f, accent.b, amount), alpha);

        static Color WithAlpha(Color c, float alpha) => new(c.r, c.g, c.b, alpha);
    }
}
