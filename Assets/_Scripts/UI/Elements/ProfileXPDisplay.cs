using CosmicShore.UI;
using Reflex.Attributes;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Lightweight component that displays the player's XP on the Profile Screen (top right).
    /// Listens to PlayerDataService profile changes to stay up to date.
    /// </summary>
    public class ProfileXPDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text xpText;
        [Inject] private PlayerDataService playerDataService;

        void Start()
        {
            playerDataService.OnProfileChanged += OnProfileChanged;
            UpdateDisplay(playerDataService.GetXP());
        }

        void OnDisable()
        {
            if (playerDataService != null)
                playerDataService.OnProfileChanged -= OnProfileChanged;
        }

        void OnProfileChanged(PlayerProfileData data)
        {
            UpdateDisplay(data.xp);
        }

        void UpdateDisplay(int xp)
        {
            if (xpText != null)
                xpText.text = $"{xp} XP";
        }
    }
}
