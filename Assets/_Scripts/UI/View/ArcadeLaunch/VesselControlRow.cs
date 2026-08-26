using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One line of the launch panel's controls block: an icon, and beside it what the control does.
    ///
    /// <para><b>The icon animates the way it animates in the GAME.</b> Not a decorative pulse — the
    /// row replays the ability lockup's own three-beat grammar
    /// (<c>Docs/ABILITY_LOCKUP.md</c>): a press flash, then the clockwise cooldown veil sweeping
    /// off the icon, then the ready flash that is the beat the player actually waits for. Every
    /// colour and duration is read from the fleet's <see cref="AbilityLockupStyleSO"/>, so the
    /// preview cannot drift from the HUD — retune the recharge veil once and both follow.</para>
    ///
    /// <para>The veil is built LAZILY and to the icon's own rect, so a row that never demonstrates
    /// a cooldown (a passive ability, a flight axis) costs nothing and draws nothing. Its
    /// <c>fillClockwise = false</c> is not a mistake: the veil DEPLETES, so the flag names the
    /// direction the wedge is drawn and the edge the player watches is its far end travelling the
    /// other way. That is the same reasoning — and the same value —
    /// <c>AbilityLockupView.BuildCooldownOverlay</c> carries.</para>
    ///
    /// <para>A row draws NOTHING it was handed as null. A missing chip glyph leaves the chip blank
    /// rather than substituting another control's artwork: a glyph shown to a player whose device
    /// does not have that control is misinformation, and blank is the correct state.</para>
    ///
    /// <para>The row owns no clock. <see cref="VesselControlsPanel"/> drives every row from one
    /// phase, so the block reads as a single travelling demonstration and an off-screen panel
    /// costs nothing.</para>
    /// </summary>
    public class VesselControlRow : MonoBehaviour
    {
        [Header("Icon")]
        [SerializeField, Tooltip("The ability's (or flight control's) own artwork. Left at the " +
                                 "prefab's sprite when none is authored for this slot.")]
        Image icon;

        [SerializeField, Tooltip("Optional: the element that upgrades this ability, drawn as its " +
                                 "petal - the same mark the HUD's flower row uses, so the launch " +
                                 "panel and the in-game HUD say the same thing. Hidden on a " +
                                 "flight-control row, which no element upgrades.")]
        Image elementPetal;

        [Header("Text")]
        [SerializeField, Tooltip("Optional headline, e.g. 'Press RT to activate Drift'. When this " +
                                 "is unwired the headline is written into the description instead, " +
                                 "so a row with a single text field still says everything.")]
        TMP_Text nameText;

        [SerializeField, Tooltip("What the control does. This is the one text field a row really " +
                                 "needs.")]
        TMP_Text descriptionText;

        [Header("Control chip")]
        [SerializeField, Tooltip("Optional pad artwork for the control. Switched off when the fleet " +
                                 "authors no glyph for it - a passive ability has no button at all.")]
        Image chipGlyph;

        [SerializeField, Tooltip("Optional keyboard label for the same control. Switched off when " +
                                 "the control has no keyboard equivalent.")]
        TMP_Text chipLabel;

        [Header("Animation")]
        [SerializeField, Tooltip("Scale the icon reaches at the peak of this row's turn. 1 disables " +
                                 "the pulse.")]
        [Min(1f)] float highlightScale = 1.1f;

        [SerializeField, Tooltip("Alpha of a row that is not this sweep's turn. 1 disables the dim.")]
        [Range(0f, 1f)] float restAlpha = 0.55f;

        [SerializeField, Tooltip("Replay the in-game recharge on this row's turn: press flash, the " +
                                 "clockwise veil sweeping off the icon, then the ready flash. Off " +
                                 "for a row whose control does not recharge (a flight axis).")]
        bool demonstrateCooldown = true;

        CanvasGroup _group;
        Image _veil;
        Image _flash;
        AbilityLockupStyleSO _style;
        Vector3 _iconRestScale = Vector3.one;
        Sprite _iconRestSprite;
        bool _capturedRestScale;

        /// <summary>The element this row was bound to, or <see cref="Element.None"/> for a flight axis.</summary>
        public Element Element { get; private set; } = Element.None;

        /// <summary>Whether this row plays the recharge demonstration.</summary>
        public bool DemonstratesCooldown => demonstrateCooldown;

        /// <summary>True when this row is a SECTION HEADING rather than a control.</summary>
        public bool IsSection { get; private set; }

        void Awake() => CaptureRestScale();

        void CaptureRestScale()
        {
            if (!_capturedNameColor && nameText)
            {
                _nameRestColor = nameText.color;
                _capturedNameColor = true;
            }

            if (!_capturedDescColor && descriptionText)
            {
                _descRestColor = descriptionText.color;
                _capturedDescColor = true;
            }

            if (_capturedRestScale) return;
            if (icon)
            {
                _iconRestScale = icon.rectTransform.localScale;
                _iconRestSprite = icon.sprite;
            }
            _capturedRestScale = true;
        }

        /// <summary>
        /// Fill the row. Every argument may be null or empty: the row switches off the parts it was
        /// given nothing for and keeps the rest.
        /// </summary>
        /// <param name="headline">e.g. "Press RT to activate Drift" — the sentence, not the noun.</param>
        /// <param name="detail">The longer description, or empty.</param>
        /// <param name="showsCooldown">False for a control that does not recharge.</param>
        public void Bind(Element element, string headline, string detail, Sprite iconSprite,
                         Sprite petal, Sprite padGlyph, string keyboardLabel, bool showsCooldown)
        {
            CaptureRestScale();
            Element = element;
            IsSection = false;
            demonstrateCooldown = showsCooldown;
            RestoreSectionChrome();

            // Restore the prefab's own art when this row gets none: rows are REUSED across
            // cards, so "leave the sprite alone" actually means "keep whichever ability's icon
            // happened to be here last" - an objective row wearing the previous card's gun.
            if (icon) icon.sprite = iconSprite ? iconSprite : _iconRestSprite;

            if (elementPetal)
            {
                elementPetal.gameObject.SetActive(petal);
                if (petal) elementPetal.sprite = petal;
            }

            WriteText(headline, detail);

            if (chipGlyph)
            {
                chipGlyph.gameObject.SetActive(padGlyph);
                if (padGlyph) chipGlyph.sprite = padGlyph;
            }

            if (chipLabel)
            {
                bool hasLabel = !string.IsNullOrWhiteSpace(keyboardLabel);
                chipLabel.gameObject.SetActive(hasLabel);
                if (hasLabel) chipLabel.text = keyboardLabel;
            }

            gameObject.SetActive(true);
            SetSweep(0f);
            SetCooldown(0f);
            SetFlash(Color.clear);
        }

        /// <summary>
        /// Draw this row as a SECTION HEADING - "CONTROLS", "ABILITIES" - rather than a control.
        ///
        /// <para>The block is a list of two different KINDS of thing (how you fly, and what you can
        /// do), and a flat list of eight rows makes the reader work that out for themselves. This
        /// is the same shape the Froglet Master Tool uses for its own categories: a heading, then
        /// the things under it.</para>
        ///
        /// <para>It is the SAME prefab with its control parts switched off, deliberately - a
        /// separate header prefab is one more asset to author and keep in visual step, for a row
        /// that is a piece of text. Nothing here is animated: a heading that pulses competes with
        /// the demonstration travelling through the rows beneath it.</para>
        /// </summary>
        public void BindSection(string title)
        {
            CaptureRestScale();
            Element = Element.None;
            IsSection = true;
            demonstrateCooldown = false;

            if (icon) icon.gameObject.SetActive(false);
            if (elementPetal) elementPetal.gameObject.SetActive(false);
            if (chipGlyph) chipGlyph.gameObject.SetActive(false);
            if (chipLabel) chipLabel.gameObject.SetActive(false);

            // Whichever text the row HAS. The shipped row prefab wires only descriptionText -
            // nameText is a nicety - and a heading that only knows how to draw through the nicety
            // is a heading that silently does not exist on the very prefab everyone authors.
            var headingText = nameText ? nameText : descriptionText;
            if (nameText) nameText.gameObject.SetActive(nameText == headingText);
            if (descriptionText) descriptionText.gameObject.SetActive(descriptionText == headingText);

            if (headingText)
            {
                headingText.text = (title ?? string.Empty).ToUpperInvariant();
                var c = ReadyFlashColor;
                c.a = 1f;                        // a heading is structure, never a half-faded flash
                headingText.color = c;
            }

            gameObject.SetActive(true);
            SetCooldown(0f);
            SetFlash(Color.clear);
            ResolveGroup().alpha = 1f;      // a heading is never dimmed by the sweep
        }

        /// <summary>
        /// Undo what <see cref="BindSection"/> switched off, so a pooled row can go back to being a
        /// control. A row is REUSED across cards - the panel grows the list and rebinds - so a row
        /// that was a heading on the Sparrow's card and is an ability on the Dolphin's would
        /// otherwise draw no icon, no chip and a heading-coloured name.
        /// </summary>
        void RestoreSectionChrome()
        {
            if (icon) icon.gameObject.SetActive(true);
            if (nameText)
            {
                nameText.gameObject.SetActive(true);
                if (_capturedNameColor) nameText.color = _nameRestColor;
            }
            if (descriptionText && _capturedDescColor) descriptionText.color = _descRestColor;
        }

        Color _nameRestColor = Color.white;
        bool _capturedNameColor;
        Color _descRestColor = Color.white;
        bool _capturedDescColor;

        /// <summary>
        /// A row with one text field says everything in it: the headline first, the detail on the
        /// next line. Splitting them across two fields is a nicety, not a requirement — the block
        /// has to read correctly on the simplest prefab anyone would author.
        /// </summary>
        void WriteText(string headline, string detail)
        {
            headline = headline?.Trim() ?? string.Empty;
            detail = detail?.Trim() ?? string.Empty;

            if (nameText)
            {
                nameText.text = headline;
                if (descriptionText)
                {
                    descriptionText.gameObject.SetActive(detail.Length > 0);
                    descriptionText.text = detail;
                }
                return;
            }

            if (!descriptionText) return;

            descriptionText.gameObject.SetActive(true);
            descriptionText.text = headline.Length > 0 && detail.Length > 0
                ? headline + "\n" + detail
                : headline.Length > 0 ? headline : detail;
        }

        /// <summary>
        /// This row's share of the panel's sweep, 0 (at rest) to 1 (its turn). Scale and alpha are
        /// the whole highlight — nothing here writes a rect, so a row sits inside a layout group
        /// without fighting it.
        /// </summary>
        public void SetSweep(float weight01)
        {
            float w = Mathf.Clamp01(weight01);

            if (icon)
                icon.rectTransform.localScale = _iconRestScale * Mathf.LerpUnclamped(1f, highlightScale, w);

            var group = ResolveGroup();
            if (group) group.alpha = Mathf.Lerp(restAlpha, 1f, w);
        }

        /// <summary>
        /// The recharge veil over the icon. 1 the instant the ability fires, 0 when it is ready —
        /// the same argument shape as <c>VesselHUDView.SetAbilityCooldown</c>, deliberately, so the
        /// two are read the same way by anyone who knows one of them.
        /// </summary>
        public void SetCooldown(float remaining01)
        {
            remaining01 = Mathf.Clamp01(remaining01);
            bool active = remaining01 > 0.0001f;

            // Parked at ready is the common case and this is driven every frame: writing fillAmount
            // dirties the Image's mesh, which is fine while the sweep is moving and pure waste while
            // it is not. Same early-out the lockup's own cooldown carries.
            if (!active && !_veil) return;
            if (!_veil)
            {
                if (!active) return;
                BuildOverlays();
                if (!_veil) return;
            }

            _veil.fillAmount = remaining01;
            if (_veil.gameObject.activeSelf != active) _veil.gameObject.SetActive(active);
        }

        /// <summary>A one-shot tint over the icon — the press and ready beats.</summary>
        public void SetFlash(Color color)
        {
            if (!_flash)
            {
                if (color.a <= 0.001f) return;
                BuildOverlays();
                if (!_flash) return;
            }
            _flash.color = color;
        }

        /// <summary>The fleet's press-flash colour, so the row and the HUD flash identically.</summary>
        public Color PressFlashColor => ResolveStyle() ? ResolveStyle().pressFlashColor : new Color(1f, 1f, 1f, 0.22f);

        /// <summary>The fleet's ready-flash colour — the loudest thing the card ever does.</summary>
        public Color ReadyFlashColor =>
            ResolveStyle() ? ResolveStyle().cooldownReadyFlashColor : new Color(1f, 1f, 1f, 0.5f);

        /// <summary>
        /// Build the veil and the flash over the icon, once, on the first row that needs them.
        ///
        /// <para>Both are siblings drawn AFTER the icon rather than children of it, for the reason
        /// the lockup records: they have to darken and light the icon, and a child would inherit
        /// its scale (which this row animates) and draw at the wrong size on every pulse.</para>
        /// </summary>
        void BuildOverlays()
        {
            if (!icon) return;

            var host = icon.rectTransform.parent as RectTransform;
            if (!host) return;

            var style = ResolveStyle();
            _veil = ResolveOverlay(host, "CooldownVeil");
            _veil.type = Image.Type.Filled;
            _veil.fillMethod = Image.FillMethod.Radial360;
            _veil.fillOrigin = (int)Image.Origin360.Top;
            // See the class summary: the veil DEPLETES, so false is what reads as clockwise. The
            // lockup's own overlay carries the identical value and the identical reason.
            _veil.fillClockwise = false;
            _veil.color = style ? style.cooldownVeilColor : new Color(0.024f, 0.031f, 0.063f, 0.72f);
            _veil.gameObject.SetActive(false);

            _flash = ResolveOverlay(host, "AbilityFlash");
            _flash.color = Color.clear;
        }

        Image ResolveOverlay(RectTransform host, string name)
        {
            var existing = host.Find(name);
            var image = existing ? existing.GetComponent<Image>() : null;
            if (!image)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(host, false);
                image = go.GetComponent<Image>();
            }

            var rt = image.rectTransform;
            var source = icon.rectTransform;
            rt.anchorMin = source.anchorMin;
            rt.anchorMax = source.anchorMax;
            rt.pivot = source.pivot;
            rt.anchoredPosition = source.anchoredPosition;
            rt.sizeDelta = source.sizeDelta;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.SetAsLastSibling();      // over the icon - the point of living out here

            image.sprite = PlainSprite;
            image.raycastTarget = false;
            image.preserveAspect = false;
            return image;
        }

        AbilityLockupStyleSO ResolveStyle()
        {
            if (!_style) _style = Resources.Load<AbilityLockupStyleSO>("AbilityLockupStyle");
            return _style;
        }

        CanvasGroup ResolveGroup()
        {
            if (_group) return _group;
            if (!TryGetComponent(out _group))
                _group = gameObject.AddComponent<CanvasGroup>();
            // The block is a readout: it must never eat a click meant for the panel behind it.
            _group.blocksRaycasts = false;
            _group.interactable = false;
            return _group;
        }

        static Sprite _plainSprite;

        /// <summary>
        /// A plain white box for the overlays, which need a sprite only because <c>Type.Filled</c>
        /// demands one. Built from <c>Texture2D.whiteTexture</c> so it costs no asset and nothing to
        /// keep in sync — the same trick <c>AbilityLockupView</c> uses for its gauge.
        /// </summary>
        static Sprite PlainSprite =>
            _plainSprite ? _plainSprite
                         : _plainSprite = Sprite.Create(
                             Texture2D.whiteTexture,
                             new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                             new Vector2(0.5f, 0.5f));
    }
}
