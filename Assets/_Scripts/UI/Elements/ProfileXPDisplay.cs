using CosmicShore.App.Profile;
using TMPro;
using UnityEngine;

namespace CosmicShore.App.UI.Elements
{
    /// <summary>
    /// Lightweight component that displays the player's XP on the Profile Screen (top right).
    /// Listens to PlayerDataService profile changes to stay up to date.
    /// </summary>
    public class ProfileXPDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text xpText;

        void OnEnable()
        {
            if (PlayerDataService.Instance != null)
            {
                PlayerDataService.Instance.OnProfileChanged += OnProfileChanged;
                UpdateDisplay(PlayerDataService.Instance.GetXP());
            }
        }

        void OnDisable()
        {
            if (PlayerDataService.Instance != null)
                PlayerDataService.Instance.OnProfileChanged -= OnProfileChanged;
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
