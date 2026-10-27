using System;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One message in the Port's faction feed (Docs/MENU_REDESIGN_B.md section 90; the Rev 27 boards): the sender's
    /// avatar, name and time, then the words. Your own messages carry a neutral bar and YOU (never the selection blue:
    /// Docs/UI_COLOR_RULES.md). Pressing a row, or A / Y on it, opens its actions.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Faction Message Row")]
    public class FactionMessageRow : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image avatar;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("YOU, shown on your own messages.")]
        [SerializeField] private CanvasGroup youTag;
        [Tooltip("The thin bar down the left of your own messages.")]
        [SerializeField] private CanvasGroup selfBar;
        [Tooltip("The row's fill: a quiet surface behind your own messages, clear behind everyone else's.")]
        [SerializeField] private CanvasGroup selfFill;

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
            if (nameText) nameText.text = message.SenderName;
            if (timeText) timeText.text = time;
            if (bodyText) bodyText.text = message.Text;
            KitVisibility.SetShown(youTag, message.FromSelf);
            KitVisibility.SetShown(selfBar, message.FromSelf);
            KitVisibility.SetShown(selfFill, message.FromSelf);
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

        static Sprite AvatarFor(int avatarId, SO_ProfileIconList icons)
        {
            if (avatarId < 0 || !icons || icons.ProfileIcons == null) return null;
            foreach (var icon in icons.ProfileIcons)
                if (icon.Id == avatarId) return icon.IconSprite;
            return null;
        }
    }
}
