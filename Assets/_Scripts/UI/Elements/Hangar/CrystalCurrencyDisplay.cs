using CosmicShore.UI;
using CosmicShore.Core;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Displays the player's crystal currency balance. Updates at runtime whenever
    /// the balance changes (purchase, reward, etc.) via PlayerDataService events.
    /// </summary>
    public class CrystalCurrencyDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text balanceText;

        void OnEnable()
        {
            PlayerDataService.OnCrystalBalanceChanged += OnBalanceChanged;
            VesselUnlockSystem.OnUnlockStateChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            PlayerDataService.OnCrystalBalanceChanged -= OnBalanceChanged;
            VesselUnlockSystem.OnUnlockStateChanged -= Refresh;
        }

        void OnBalanceChanged(int newBalance)
        {
            if (balanceText)
                balanceText.text = newBalance.ToString();
        }

        void Refresh()
        {
            if (balanceText)
                balanceText.text = VesselUnlockSystem.GetCurrencyBalance().ToString();
        }
    }
}
