using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.App.Ui.Menus
{
    public class StoreMenu : MonoBehaviour
    {
        [Header("Daily Reward")] 
        [SerializeField] private Button claimDailyRewardButton;
        
        [Header("Ship Purchasing")]
        [SerializeField] private Button buyShipButton;
        
        [Header("MiniGame Purchasing")] 
        [SerializeField] private Button buyMiniGameButton;
 
        [Header("Vessel Upgrade Purchasing")] 
        [SerializeField] private Button buyVesselUpgradeButton;

        // Upon claiming daily reward, the button non-clickable here on the menu
        // (Back-end) Notify the server for cool down time
        private bool _isDailyRewardClaimed = false;

        private void Start()
        {
            // TODO: Not sure why the button instance is null upon summoning store menu 
            claimDailyRewardButton.onClick.AddListener(ClaimDailyReward_OnClick);
            buyShipButton.onClick.AddListener(BuyShip_OnClick);
            buyMiniGameButton.onClick.AddListener(BuyMiniGame_OnClick);
            buyVesselUpgradeButton.onClick.AddListener(BuyVesselUpgrade_OnClick);
        }

        private void ClaimDailyReward_OnClick()
        {
            if (!_isDailyRewardClaimed)
            {
                claimDailyRewardButton.interactable = true;
                Debug.LogFormat("{0} - {1} claiming daily reward.", nameof(StoreMenu), nameof(ClaimDailyReward_OnClick));
                // TODO: back-end daily reward cool down.
                _isDailyRewardClaimed = true;
            }
            else
            {
                // Deactivate the claim daily reward button
                claimDailyRewardButton.interactable = false;
                Debug.LogFormat("{0} - {1} daily reward claimed.", nameof(StoreMenu), nameof(ClaimDailyReward_OnClick));
            }
            
        }

        private void BuyShip_OnClick()
        {
            Debug.LogFormat("{0} - {1} buying a ship - urchine.", nameof(StoreMenu), nameof(BuyShip_OnClick));
            // TODO: back-end buy ship 
        }

        private void BuyMiniGame_OnClick()
        {
            Debug.LogFormat("{0} - {1} buying a mini game.", nameof(StoreMenu), nameof(BuyMiniGame_OnClick));
            // TODO: back-end buy mini game
        }

        void BuyVesselUpgrade_OnClick()
        {
            Debug.LogFormat("{0} - {1} buying a vessel upgrade.", nameof(StoreMenu), nameof(BuyVesselUpgrade_OnClick));
            // TODO: back-end buy a vessel upgrade.
        }
    }
}
