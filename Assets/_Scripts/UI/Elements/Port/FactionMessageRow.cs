using System;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One message in the Port's faction feed (Docs/MENU_REDESIGN_B.md sections 90-91). Rev 32 D, "Sea-lit panels"
    /// (Shombith 2026-10-27, "Go with D"): every message is a card lit from its top-left corner in the sea's periwinkle,
    /// sized to its words; yours sit on the right with the full periwinkle-to-violet border, a brighter fill and YOU. The
    /// selection blue is only the pad's focus outline (Docs/UI_COLOR_RULES.md). Pressing a row, or A / Y on it, opens its
    /// actions.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Faction Message Row")]
    public class FactionMessageRow : MonoBehaviour
    {
        /// <summary>The colours of one kind of message (everyone else's, or yours).</summary>
        [Serializable]
        public struct Look
        {
            [Tooltip("The border, from the lit corner to the far one.")]
            public Color borderFrom, borderTo;
            [Tooltip("The card's fill, from the lit corner to the far one.")]
            public Color fillFrom, fillTo;
            [Tooltip("The soft light in the lit corner.")]
            public Color glow;
            [Tooltip("The avatar's ring, from the lit corner to the far one.")]
            public Color ringFrom, ringTo;
            [Tooltip("The sender's name.")]
            public Color name;
        }

        [SerializeField] private Button button;
        [Tooltip("The row: avatar and card. Yours is aligned to the right with the avatar after the card.")]
        [SerializeField] private HorizontalLayoutGroup layout;
        [SerializeField] private RectTransform avatarHolder;
        [SerializeField] private Image avatar;
        [SerializeField] private KitTwoToneGradient avatarRing;
        [Tooltip("The card; its width follows its words, up to Max Card Width.")]
        [SerializeField] private LayoutElement card;
        [SerializeField] private KitTwoToneGradient cardBorder;
        [SerializeField] private KitTwoToneGradient cardFill;
        [SerializeField] private Graphic cardGlow;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("YOU, shown on your own messages.")]
        [SerializeField] private CanvasGroup youTag;

        [Header("Size")]
        [SerializeField, Min(120f)] private float maxCardWidth = 860f;
        [Tooltip("The card's left and right padding together.")]
        [SerializeField, Min(0f)] private float cardPadding = 36f;
        [Tooltip("Room the name line needs beyond the name itself (YOU and the time).")]
        [SerializeField, Min(0f)] private float headExtra = 110f;

        [Header("Look")]
        [SerializeField] private Look othersLook;
        [SerializeField] private Look yoursLook;

        public event Action<FactionMessageRow> Picked;

        public FactionMessage Message { get; private set; }
        public Button Button => button;
        Sprite _restAvatar;

        void Awake()
        {
            if (avatar) _restAvatar = avatar.sprite;
            if (button) button.onClick.AddListener(() => Picked?.Invoke(this));
        }

        public void Show(in FactionMessage message, string time, SO_ProfileIconList icons)
        {
            Message = message;
            bool own = message.FromSelf;
            if (nameText) nameText.text = message.SenderName;
            if (timeText) timeText.text = time;
            if (bodyText) bodyText.text = message.Text;
            KitVisibility.SetShown(youTag, own);
            Paint(own ? yoursLook : othersLook);
            if (layout) layout.childAlignment = own ? TextAnchor.LowerRight : TextAnchor.LowerLeft;
            if (avatarHolder)
            {
                if (own) avatarHolder.SetAsLastSibling();
                else avatarHolder.SetAsFirstSibling();
            }
            SizeCard();
            if (!avatar) return;
            var sprite = AvatarFor(message.AvatarId, icons);
            // A sender with no known avatar keeps the row's authored placeholder face.
            avatar.sprite = sprite ? sprite : _restAvatar;
            avatar.enabled = avatar.sprite;
        }

        public void SetTime(string time)
        {
            if (timeText) timeText.text = time;
        }

        void Paint(in Look look)
        {
            if (cardBorder) cardBorder.SetColours(look.borderFrom, look.borderTo);
            if (cardFill) cardFill.SetColours(look.fillFrom, look.fillTo);
            if (cardGlow) cardGlow.color = look.glow;
            if (avatarRing) avatarRing.SetColours(look.ringFrom, look.ringTo);
            if (nameText) nameText.color = look.name;
        }

        /// <summary>The card as wide as its words (or its name line), never wider than Max Card Width.</summary>
        void SizeCard()
        {
            if (!card || !bodyText) return;
            float inner = maxCardWidth - cardPadding;
            float words = bodyText.GetPreferredValues(bodyText.text, inner, 0f).x;
            float head = nameText ? nameText.GetPreferredValues(nameText.text).x + headExtra : 0f;
            card.preferredWidth = Mathf.Min(maxCardWidth, Mathf.Max(words, head) + cardPadding);
        }

        static Sprite AvatarFor(int avatarId, SO_ProfileIconList icons)
        {
            if (avatarId < 0 || !icons || icons.ProfileIcons == null) return null;
            foreach (var icon in icons.ProfileIcons)
                if (icon.Id == avatarId) return icon.IconSprite;
            return null;
        }
    }
}
