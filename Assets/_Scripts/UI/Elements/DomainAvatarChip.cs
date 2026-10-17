using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One avatar slot inside a domain button's avatar strip. Pools are managed by
    /// <see cref="DomainInfoData"/> - never instantiate or destroy these directly.
    ///
    /// <para>A chip standing for a PLACED AI (the launch panel's Add AI mode) can carry a ✕ -
    /// see <see cref="SetKickable"/>. Author the ✕ art as a Button on the prefab and wire
    /// <see cref="kickButton"/>; an unwired chip builds a plain functional ✕ from primitives so
    /// the feature works before the art lands, and steps aside the moment the field is wired.</para>
    ///
    /// <para>An AI chip can also show the HULL that AI will fly - see <see cref="SetHull"/>: an
    /// opponent's pinned hull, or the hull a teammate picked for an ally. An ally chip on the
    /// local pilot's own team is tappable, and a tap steps its hull. Wire
    /// <see cref="hullPickButton"/> for authored art; unwired, the chip itself becomes the button
    /// (the avatar image is its hit area).</para>
    ///
    /// <para>A chip showing a hull also NAMES it - see <see cref="SetHullInfo"/>: hovering grows
    /// the chip and shows the vessel's name in a pill beneath it, and an (i) badge in the top-right
    /// corner opens the vessel's info card (the ✕, when the chip has both, moves to the top-left).
    /// The hull icons are small at strip size; this is how a host tells their picks apart. Wire
    /// <see cref="infoButton"/> / <see cref="hoverLabel"/> for authored art; unwired, both are
    /// generated from primitives like the ✕.</para>
    /// </summary>
    public class DomainAvatarChip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] Image avatarImage;
        [Tooltip("Optional outline / ring GameObject enabled only for the local player's chip.")]
        [SerializeField] GameObject localPlayerOutline;

        [Header("Seat Row (optional)")]
        [Tooltip("The pilot's name. Optional: an avatar-only chip leaves it empty.")]
        [SerializeField] TMPro.TMP_Text nameText;
        [Tooltip("\"You / Host\", \"Pilot\", \"AI\" and so on. Optional.")]
        [SerializeField] TMPro.TMP_Text roleText;

        [Header("Kick")]
        [Tooltip("Optional ✕ shown on a kickable AI chip (Add AI placements, host only). Leave " +
                 "empty and a plain generated ✕ stands in until the art is authored.")]
        [SerializeField] Button kickButton;

        [Header("Hull pick")]
        [Tooltip("Optional button that steps an ally AI's hull. Leave empty and the chip itself " +
                 "becomes the button, with the avatar image as its hit area.")]
        [SerializeField] Button hullPickButton;

        [Header("Hull info")]
        [Tooltip("Optional (i) badge that opens the hull's info card. Leave empty and a small " +
                 "generated badge stands in, in the chip's top-right corner.")]
        [SerializeField] Button infoButton;

        [Tooltip("Optional label that shows the hull's name while the pointer is over the chip. " +
                 "Leave empty and a generated pill beneath the chip stands in.")]
        [SerializeField] TMP_Text hoverLabel;

        [Tooltip("Scale the chip grows to while hovered, so the hull icon can be read.")]
        [SerializeField, Min(1f)] float hoverScale = 1.35f;

        [SerializeField, Min(0f)] float hoverSeconds = 0.12f;

        System.Action _onKick;
        Button _generatedKick;
        bool _kickWired;

        System.Action _onPickHull;
        Button _generatedHullPick;
        bool _hullPickWired;

        string _hullName;
        System.Action _onInfo;
        Button _generatedInfo;
        bool _infoWired;
        GameObject _generatedLabelRoot;
        TMP_Text _generatedLabel;
        Tween _hoverTween;

        public void Set(Sprite sprite, bool isLocal)
        {
            // Keep the Image enabled even when sprite is null - that way a chip with
            // a missing/loading avatar still shows the prefab's placeholder sprite
            // instead of disappearing entirely.
            if (avatarImage)
            {
                if (sprite != null)
                    avatarImage.sprite = sprite;
                avatarImage.enabled = true;
            }

            if (localPlayerOutline)
                localPlayerOutline.SetActive(isLocal);

            // Chips are reused: a fresh Set is a fresh identity, so any kick / hull-pick state
            // from the previous occupant is stale until the owner re-asks for one.
            SetKickable(false);
            SetHullPickable(false);
            SetHullInfo(null, null);

            gameObject.SetActive(true);
        }

        /// <summary>
        /// The seat row's words: who sits here and in what role. A chip with no name or role label
        /// ignores it, so the avatar-only chip keeps working.
        /// </summary>
        public void SetLabel(string displayName, string role)
        {
            if (nameText) nameText.text = displayName ?? string.Empty;
            if (roleText) roleText.text = role ?? string.Empty;
        }

        /// <summary>
        /// Show or hide the chip's ✕. <paramref name="onKick"/> fires on click; the caller owns
        /// what a kick MEANS (the modal removes the placement - there is no AI object yet).
        /// </summary>
        public void SetKickable(bool kickable, System.Action onKick = null)
        {
            _onKick = kickable ? onKick : null;

            var button = kickButton ? kickButton : _generatedKick;
            if (!button && kickable)
                button = _generatedKick = BuildFallbackKick();
            if (!button) return;

            if (!_kickWired)
            {
                button.onClick.AddListener(HandleKickClicked);
                _kickWired = true;
            }

            button.gameObject.SetActive(kickable);
            PlaceGeneratedKick();
        }

        /// <summary>The generated ✕ sits top-right, unless the (i) badge is showing there: then it
        /// moves to the top-left so the two never overlap. An authored ✕ is left where it was put.</summary>
        void PlaceGeneratedKick()
        {
            if (!_generatedKick) return;
            bool infoShown = _onInfo != null;
            var rect = (RectTransform)_generatedKick.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(infoShown ? 0f : 1f, 1f);
            rect.pivot = new Vector2(infoShown ? 0.25f : 0.75f, 0.75f);
        }

        void HandleKickClicked() => _onKick?.Invoke();

        /// <summary>
        /// Show the hull this AI seat will fly in place of its avatar (<paramref name="hullIcon"/>
        /// null keeps the avatar - an ally nobody has picked a hull for yet). When
        /// <paramref name="onPick"/> is non-null the chip is tappable and a tap raises it; the
        /// caller owns what a pick MEANS (the modal steps the seat's hull through the host).
        /// </summary>
        public void SetHull(Sprite hullIcon, System.Action onPick)
        {
            if (avatarImage && hullIcon)
            {
                avatarImage.sprite = hullIcon;
                avatarImage.enabled = true;
            }
            SetHullPickable(onPick != null, onPick);
        }

        void SetHullPickable(bool pickable, System.Action onPick = null)
        {
            _onPickHull = pickable ? onPick : null;

            var button = hullPickButton ? hullPickButton : _generatedHullPick;
            if (!button && pickable)
                button = _generatedHullPick = BuildFallbackHullPick();
            if (!button) return;

            if (!_hullPickWired)
            {
                button.onClick.AddListener(HandleHullPickClicked);
                _hullPickWired = true;
            }

            // The authored button is shown only while pickable; the generated one IS the chip,
            // so it is switched off rather than hidden.
            if (hullPickButton) hullPickButton.gameObject.SetActive(pickable);
            else button.enabled = pickable;
        }

        void HandleHullPickClicked() => _onPickHull?.Invoke();

        /// <summary>
        /// Name the hull this chip shows. <paramref name="hullName"/> is drawn in the hover label
        /// (null: the chip shows an avatar, not a hull, and hovering does nothing).
        /// <paramref name="onInfo"/> non-null shows the (i) badge, and a tap on it raises it; the
        /// caller owns what the badge OPENS (the modal's vessel info card).
        /// </summary>
        public void SetHullInfo(string hullName, System.Action onInfo)
        {
            _hullName = string.IsNullOrWhiteSpace(hullName) ? null : hullName;
            _onInfo = _hullName != null ? onInfo : null;
            if (_hullName == null) ShowHover(false);

            bool showBadge = _onInfo != null;
            var button = infoButton ? infoButton : _generatedInfo;
            if (!button && showBadge)
                button = _generatedInfo = BuildFallbackInfo();
            if (button)
            {
                if (!_infoWired)
                {
                    button.onClick.AddListener(HandleInfoClicked);
                    _infoWired = true;
                }
                button.gameObject.SetActive(showBadge);
            }

            PlaceGeneratedKick();
        }

        void HandleInfoClicked()
        {
            ShowHover(false);
            _onInfo?.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_hullName != null) ShowHover(true);
        }

        public void OnPointerExit(PointerEventData eventData) => ShowHover(false);

        void OnDisable()
        {
            _hoverTween?.Kill();
            transform.localScale = Vector3.one;
            if (hoverLabel) hoverLabel.gameObject.SetActive(false);
            if (_generatedLabelRoot) _generatedLabelRoot.SetActive(false);
        }

        void ShowHover(bool show)
        {
            if (show)
            {
                var label = hoverLabel ? hoverLabel : _generatedLabel ? _generatedLabel : BuildFallbackLabel();
                label.text = _hullName.ToUpperInvariant();
                (hoverLabel ? hoverLabel.gameObject : _generatedLabelRoot).SetActive(true);
            }
            else
            {
                if (hoverLabel) hoverLabel.gameObject.SetActive(false);
                if (_generatedLabelRoot) _generatedLabelRoot.SetActive(false);
            }

            if (!isActiveAndEnabled) return;
            _hoverTween?.Kill();
            float target = show ? hoverScale : 1f;
            if (hoverSeconds <= 0f) transform.localScale = Vector3.one * target;
            else _hoverTween = transform.DOScale(target, hoverSeconds)
                .SetEase(show ? Ease.OutBack : Ease.OutQuad)
                .SetUpdate(true);
        }

        /// <summary>
        /// A small round (i) badge in the chip's top-right corner, so the info card works before any
        /// art is authored. Never built while <see cref="infoButton"/> is wired.
        /// </summary>
        Button BuildFallbackInfo()
        {
            var go = new GameObject("InfoBadge (generated)", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);   // top-right corner
            rect.pivot = new Vector2(0.75f, 0.75f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(18f, 18f);

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.16f, 0.45f, 0.85f, 0.95f);

            var glyph = new GameObject("Glyph", typeof(RectTransform), typeof(TextMeshProUGUI));
            var glyphRect = (RectTransform)glyph.transform;
            glyphRect.SetParent(rect, false);
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = glyphRect.offsetMax = Vector2.zero;
            var text = glyph.GetComponent<TextMeshProUGUI>();
            text.text = "i";
            text.fontSize = 14f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            return go.GetComponent<Button>();
        }

        /// <summary>
        /// A dark pill under the chip holding the hull's name. It carries its own sorting canvas so
        /// the neighbouring chips and tiles never draw over it, and it takes no raycasts so it never
        /// steals the hover from the chip. Never built while <see cref="hoverLabel"/> is wired.
        /// </summary>
        TMP_Text BuildFallbackLabel()
        {
            var root = new GameObject("HoverLabel (generated)", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);   // under the chip
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -4f);

            var canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;

            var bg = root.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.1f, 0.92f);
            bg.raycastTarget = false;

            var fitter = root.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 3, 3);
            layout.childControlWidth = layout.childControlHeight = true;

            var label = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            ((RectTransform)label.transform).SetParent(rect, false);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.fontSize = 13f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            _generatedLabelRoot = root;
            _generatedLabel = text;
            root.SetActive(false);
            return text;
        }

        /// <summary>The chip itself as the hull-pick button: clicks on the avatar image bubble up
        /// to it. Never built while <see cref="hullPickButton"/> is wired.</summary>
        Button BuildFallbackHullPick()
        {
            if (!TryGetComponent(out Button button))
                button = gameObject.AddComponent<Button>();
            if (avatarImage)
            {
                avatarImage.raycastTarget = true;
                button.targetGraphic = avatarImage;
            }
            return button;
        }

        /// <summary>
        /// A functional ✕ built from primitives - a small dark disc with two crossed bars -
        /// so a kickable chip works before any art is authored. Replace it by wiring
        /// <see cref="kickButton"/> on the prefab; this never builds while that is set.
        /// </summary>
        Button BuildFallbackKick()
        {
            var go = new GameObject("KickX (generated)", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);   // top-right corner
            rect.pivot = new Vector2(0.75f, 0.75f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(16f, 16f);

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.75f, 0.12f, 0.12f, 0.95f);

            for (int i = 0; i < 2; i++)
            {
                var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
                var barRect = (RectTransform)bar.transform;
                barRect.SetParent(rect, false);
                barRect.sizeDelta = new Vector2(10f, 2f);
                barRect.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
                bar.GetComponent<Image>().color = Color.white;
                bar.GetComponent<Image>().raycastTarget = false;
            }

            return go.GetComponent<Button>();
        }

        public void Hide()
        {
            SetKickable(false);
            SetHullPickable(false);
            SetHullInfo(null, null);
            gameObject.SetActive(false);
        }
    }
}
