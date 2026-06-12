using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Connecting panel that displays a random background sprite on enable.
    /// Used by MiniGameHUDView during the pre-game connecting phase.
    /// </summary>
    public class ConnectingPanel : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite[] backgroundSprites;

        void OnEnable()
        {
            if (backgroundImage != null && backgroundSprites is { Length: > 0 })
            {
                backgroundImage.sprite = backgroundSprites[Random.Range(0, backgroundSprites.Length)];
            }
        }
    }
}
