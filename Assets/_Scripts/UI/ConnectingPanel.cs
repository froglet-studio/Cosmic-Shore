using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Displays a random sprite from a configured SO each time the panel is enabled.
    /// Attach to the connecting panel GameObject inside MiniGameHUD.
    /// </summary>
    public class ConnectingPanel : MonoBehaviour
    {
        [SerializeField] private SO_ConnectingPanelSpriteList spriteList;
        [SerializeField] private Image displayImage;

        private void OnEnable()
        {
            if (displayImage == null)
                displayImage = GetComponentInChildren<Image>();

            if (spriteList == null || displayImage == null)
                return;

            var sprite = spriteList.GetRandomSprite();
            if (sprite != null)
                displayImage.sprite = sprite;
        }
    }
}
