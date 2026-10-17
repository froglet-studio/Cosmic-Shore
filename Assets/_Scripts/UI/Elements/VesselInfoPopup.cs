using System.Text;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A small card that says which vessel an AI seat will fly: the hull's icon, name, element,
    /// the same description the Hangar shows, and its abilities. Opened by the (i) on an AI chip
    /// (<see cref="DomainAvatarChip.SetHullInfo"/>) so a host can tell their picks apart without
    /// leaving the launch panel. Closed by tapping anywhere, its ✕, or the modal closing.
    ///
    /// <para>Author the card on a prefab and wire it into the modal for bespoke art; when nothing
    /// is wired the modal calls <see cref="Build"/>, which lays the same card out from primitives,
    /// so the feature works before any art lands - the same rule the chip's ✕ follows.</para>
    /// </summary>
    public class VesselInfoPopup : MonoBehaviour
    {
        [Header("Card")]
        [Tooltip("The card itself; tapping outside it closes the popup.")]
        [SerializeField] RectTransform card;
        [SerializeField] CanvasGroup canvasGroup;

        [Header("Content")]
        [SerializeField] Image vesselIcon;
        [SerializeField] TMP_Text vesselNameText;
        [Tooltip("One line under the name: the seat's team and the hull's primary element.")]
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] TMP_Text descriptionText;
        [Tooltip("The hull's abilities, one per line. Hidden when the hull lists none.")]
        [SerializeField] TMP_Text abilitiesText;

        [Header("Ability rows (optional)")]
        [Tooltip("Authored rows: each element's ability as a coloured hex and its name, from the hull's " +
                 "ElementalAbilityMapSO (Charge, Mass, Space, Time). When wired, abilitiesText is not used.")]
        [SerializeField] AbilityRow[] abilityRows = System.Array.Empty<AbilityRow>();
        [Tooltip("Shown over the rows; hidden with them when the hull lists no abilities.")]
        [SerializeField] GameObject abilitiesGroup;
        [Tooltip("Element colours for the rows' hexes.")]
        [SerializeField] AbilityLockupStyleSO lockupStyle;

        [Header("Words")]
        [Tooltip("The subtitle when the seat and the element are both known; {seat} and {element} are filled in.")]
        [SerializeField] string subtitleTemplate = "{seat}  ·  {element} vessel";
        [Tooltip("The subtitle when only the element is known; {element} is filled in.")]
        [SerializeField] string elementOnlyTemplate = "{element} vessel";
        [Tooltip("The heading over the abilities list (the text fallback). Empty shows no heading.")]
        [SerializeField] string abilitiesHeading = "ABILITIES";
        [Tooltip("Put before each ability in the text fallback.")]
        [SerializeField] string abilityBullet = "•  ";

        [System.Serializable]
        public struct AbilityRow
        {
            public Element element;
            public Image hex;
            public TMP_Text name;
        }

        [Header("Close")]
        [Tooltip("Full-panel transparent button behind the card: a tap outside closes it.")]
        [SerializeField] Button backdropButton;
        [SerializeField] Button closeButton;

        [Header("Animation")]
        [SerializeField, Min(0f)] float openSeconds = 0.15f;

        Tween _tween;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            if (backdropButton) backdropButton.onClick.AddListener(Hide);
            if (closeButton) closeButton.onClick.AddListener(Hide);
        }

        void OnDisable()
        {
            _tween?.Kill();
            if (card) card.localScale = Vector3.one;
        }

        /// <summary>Open the card for <paramref name="vessel"/>. <paramref name="seatLabel"/> names
        /// who flies it ("Ruby AI"), and leads the subtitle when given.</summary>
        public void Show(SO_Vessel vessel, string seatLabel = null)
        {
            if (!vessel) return;

            if (vesselIcon)
            {
                vesselIcon.sprite = vessel.IconActive;
                vesselIcon.enabled = vessel.IconActive;
            }

            if (vesselNameText)
                vesselNameText.text = DisplayName(vessel).ToUpperInvariant();

            if (subtitleText)
            {
                string subtitle = Subtitle(vessel, seatLabel);
                subtitleText.text = subtitle;
                subtitleText.gameObject.SetActive(subtitle.Length > 0);
            }

            if (descriptionText)
            {
                string description = vessel.Description?.Trim() ?? string.Empty;
                descriptionText.text = description;
                descriptionText.gameObject.SetActive(description.Length > 0);
            }

            if (abilityRows is { Length: > 0 })
            {
                bool any = FillAbilityRows(vessel);
                if (abilitiesGroup) abilitiesGroup.SetActive(any);
            }
            else if (abilitiesText)
            {
                string abilities = AbilityList(vessel);
                abilitiesText.text = abilities;
                abilitiesText.gameObject.SetActive(abilities.Length > 0);
            }

            transform.SetAsLastSibling();
            gameObject.SetActive(true);

            _tween?.Kill();
            if (card && openSeconds > 0f)
            {
                card.localScale = Vector3.one * 0.9f;
                _tween = card.DOScale(1f, openSeconds).SetEase(Ease.OutBack).SetUpdate(true);
            }
            if (canvasGroup) canvasGroup.alpha = 1f;
        }

        public void Hide() => gameObject.SetActive(false);

        public static string DisplayName(SO_Vessel vessel) =>
            !string.IsNullOrWhiteSpace(vessel.Name) ? vessel.Name : vessel.Class.ToString();

        string Subtitle(SO_Vessel vessel, string seatLabel)
        {
            bool hasSeat = !string.IsNullOrWhiteSpace(seatLabel);
            bool hasElement = vessel.PrimaryElement is not (Element.None or Element.Omni);
            string element = vessel.PrimaryElement.ToString();
            if (hasSeat && hasElement) return Fill(subtitleTemplate, seatLabel, element);
            if (hasSeat) return seatLabel;
            return hasElement ? Fill(elementOnlyTemplate, seatLabel, element) : string.Empty;
        }

        static string Fill(string template, string seat, string element) =>
            string.IsNullOrEmpty(template) ? string.Empty : template.Replace("{seat}", seat ?? string.Empty).Replace("{element}", element);

        string AbilityList(SO_Vessel vessel)
        {
            if (vessel.Abilities is not { Count: > 0 }) return string.Empty;
            var sb = new StringBuilder();
            foreach (var ability in vessel.Abilities)
            {
                if (!ability || string.IsNullOrWhiteSpace(ability.Name)) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(abilityBullet).Append(ability.Name);
            }
            if (sb.Length == 0) return string.Empty;
            return string.IsNullOrEmpty(abilitiesHeading) ? sb.ToString() : abilitiesHeading + "\n" + sb;
        }

        /// <summary>Each authored row shows its element's ability on this hull (the Hangar's names); a slot
        /// the hull leaves open hides its row. Returns whether any row shows.</summary>
        bool FillAbilityRows(SO_Vessel vessel)
        {
            var map = ElementalAbilityMapSO.LoadFor(vessel.Class);
            bool any = false;
            foreach (var row in abilityRows)
            {
                var entry = map ? map.GetEntry(row.element) : null;
                bool show = entry != null && !string.IsNullOrWhiteSpace(entry.AbilityLabel);
                if (row.name)
                {
                    row.name.text = show ? entry.AbilityLabel : string.Empty;
                    row.name.transform.parent.gameObject.SetActive(show);
                }
                if (row.hex && lockupStyle) row.hex.color = lockupStyle.ColourFor(row.element);
                any |= show;
            }
            return any;
        }

        #region Generated layout

        static readonly Color BackdropColor = new(0f, 0f, 0f, 0.45f);
        static readonly Color CardColor = new(0.07f, 0.09f, 0.14f, 0.97f);
        static readonly Color AccentColor = new(0.36f, 0.78f, 0.95f, 1f);
        static readonly Color BodyColor = new(0.85f, 0.88f, 0.93f, 1f);
        static readonly Color MutedColor = new(0.6f, 0.66f, 0.75f, 1f);

        /// <summary>
        /// Lay the card out from primitives under <paramref name="parent"/> (the launch modal):
        /// a dimming backdrop over the whole parent, and a 380-wide card centred in it with the
        /// icon and name on top and the description and abilities beneath. <paramref name="font"/>
        /// is the modal's own font so the card reads as part of it; null uses TMP's default.
        /// </summary>
        public static VesselInfoPopup Build(RectTransform parent, TMP_FontAsset font)
        {
            var root = NewRect("VesselInfoPopup (generated)", parent);
            Stretch(root);
            var backdrop = root.gameObject.AddComponent<Image>();
            backdrop.color = BackdropColor;
            var backdropButton = root.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var card = NewRect("Card", root);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(380f, 0f);
            var cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = CardColor;
            // The card swallows its own taps, so only a tap on the backdrop closes the popup.
            cardImage.raycastTarget = true;
            var cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(20, 20, 18, 20);
            cardLayout.spacing = 10f;
            cardLayout.childControlWidth = cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;
            var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Accent stripe along the top edge, outside the layout.
            var stripe = NewRect("Accent", card);
            stripe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            stripe.anchorMin = new Vector2(0f, 1f);
            stripe.anchorMax = new Vector2(1f, 1f);
            stripe.pivot = new Vector2(0.5f, 1f);
            stripe.sizeDelta = new Vector2(0f, 4f);
            stripe.gameObject.AddComponent<Image>().color = AccentColor;

            // Header: icon left, name + subtitle right.
            var header = NewRect("Header", card);
            var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 14f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = headerLayout.childForceExpandHeight = false;

            var iconRect = NewRect("Icon", header);
            var iconLayout = iconRect.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = iconLayout.preferredHeight = 88f;
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var titles = NewRect("Titles", header);
            titles.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var titlesLayout = titles.gameObject.AddComponent<VerticalLayoutGroup>();
            titlesLayout.spacing = 2f;
            titlesLayout.childControlWidth = titlesLayout.childControlHeight = true;
            titlesLayout.childForceExpandWidth = true;
            titlesLayout.childForceExpandHeight = false;

            var name = NewText("Name", titles, font, 30f, Color.white, FontStyles.Bold);
            var subtitle = NewText("Subtitle", titles, font, 16f, AccentColor, FontStyles.Normal);
            var description = NewText("Description", card, font, 18f, BodyColor, FontStyles.Normal);
            var abilities = NewText("Abilities", card, font, 16f, MutedColor, FontStyles.Normal);

            // ✕ in the card's top-right corner, outside the layout.
            var closeRect = NewRect("Close", card);
            closeRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-8f, -10f);
            closeRect.sizeDelta = new Vector2(32f, 32f);
            var closeImage = closeRect.gameObject.AddComponent<Image>();
            closeImage.color = new Color(1f, 1f, 1f, 0f);   // hit area only
            var closeButton = closeRect.gameObject.AddComponent<Button>();
            var closeGlyph = NewText("Glyph", closeRect, font, 22f, MutedColor, FontStyles.Bold);
            Stretch((RectTransform)closeGlyph.transform);
            closeGlyph.alignment = TextAlignmentOptions.Center;
            closeGlyph.text = "✕";

            var popup = root.gameObject.AddComponent<VesselInfoPopup>();
            popup.card = card;
            popup.canvasGroup = group;
            popup.vesselIcon = icon;
            popup.vesselNameText = name;
            popup.subtitleText = subtitle;
            popup.descriptionText = description;
            popup.abilitiesText = abilities;
            popup.backdropButton = backdropButton;
            popup.closeButton = closeButton;
            // Awake already ran on AddComponent, before the buttons were assigned.
            backdropButton.onClick.AddListener(popup.Hide);
            closeButton.onClick.AddListener(popup.Hide);

            root.gameObject.SetActive(false);
            return popup;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size,
                                       Color color, FontStyles style)
        {
            var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        #endregion
    }
}
