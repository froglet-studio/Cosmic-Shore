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

        [Tooltip("Single tips list asset containing tips for all game modes.")]
        [SerializeField] private SO_GameModeTips tipsList;

        private GameModes _activeMode;

        /// <summary>
        /// Set the current game mode before the panel is enabled so the
        /// correct tips are shown. Called by MiniGameHUD.
        /// </summary>
        public void SetGameMode(GameModes mode)
        {
            _activeMode = mode;
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

            if (tipText != null && tipsList != null)
            {
                var tip = tipsList.GetRandomTip(_activeMode);
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
