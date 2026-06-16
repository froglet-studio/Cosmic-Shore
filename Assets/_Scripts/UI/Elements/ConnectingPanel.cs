using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Connecting panel shown for a mandatory window at the start of each game.
    /// Displays a random background sprite plus the current game's details
    /// (mode name, intensity, player count). The animated "connecting" text is
    /// driven by a sibling <see cref="ConnectingDotsAnimator"/>.
    /// Shown/hidden by <see cref="MiniGameHUDView.ToggleConnectingPanel"/>.
    /// </summary>
    public class ConnectingPanel : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite[] backgroundSprites;
        [Tooltip("Game details line(s): mode name, intensity, players in the room.")]
        [SerializeField] private TMP_Text detailsText;

        void OnEnable()
        {
            if (backgroundImage != null && backgroundSprites is { Length: > 0 })
            {
                backgroundImage.sprite = backgroundSprites[Random.Range(0, backgroundSprites.Length)];
            }
        }

        /// <summary>
        /// Sets the game-details text shown on the panel (e.g. mode name + intensity + players).
        /// No-op when no details label is wired.
        /// </summary>
        public void SetDetails(string details)
        {
            if (detailsText != null)
                detailsText.text = details;
        }

        /// <summary>
        /// Wires the panel's content references when the panel is created at runtime
        /// (no authored prefab). See <see cref="MiniGameHUDView.EnsureConnectingPanel"/>.
        /// </summary>
        public void InitializeRuntime(Image background, TMP_Text details)
        {
            backgroundImage = background;
            detailsText = details;
        }
    }
}
