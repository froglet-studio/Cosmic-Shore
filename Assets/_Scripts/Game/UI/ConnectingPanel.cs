using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Displays a random sprite and tip text each time the panel is enabled.
    /// Attach to the connecting panel GameObject inside MiniGameHUD.
    /// </summary>
    public class ConnectingPanel : MonoBehaviour
    {
        [SerializeField] private SO_ConnectingPanelSpriteList spriteList;
        [SerializeField] private Image displayImage;

        [Header("Tips")]
        [Tooltip("Tip text element on the connecting panel (add in scene).")]
        [SerializeField] private TMP_Text tipText;

        private SO_GameModeTips _activeTips;

        /// <summary>
        /// Set the tips SO for the current game mode before the panel is enabled.
        /// Called by MiniGameHUD when the connecting panel is shown.
        /// </summary>
        public void SetTips(SO_GameModeTips tips)
        {
            _activeTips = tips;
        }

        private void OnEnable()
        {
            if (displayImage == null)
                displayImage = GetComponentInChildren<Image>();

            if (spriteList != null && displayImage != null)
            {
                var sprite = spriteList.GetRandomSprite();
                if (sprite != null)
                    displayImage.sprite = sprite;
            }

            if (tipText != null && _activeTips != null)
            {
                var tip = _activeTips.GetRandomTip();
                tipText.text = string.IsNullOrEmpty(tip) ? string.Empty : tip;
                tipText.gameObject.SetActive(!string.IsNullOrEmpty(tip));
            }
            else if (tipText != null)
            {
                tipText.gameObject.SetActive(false);
            }
        }
    }
}
