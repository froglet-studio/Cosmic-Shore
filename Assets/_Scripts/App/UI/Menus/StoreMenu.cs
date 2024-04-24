using UnityEngine;
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
 
        [Header("Guide Upgrade Purchasing")] 
        [SerializeField] private Button buyGuideUpgradeButton;

        // Upon claiming daily reward, the button non-clickable here on the menu
        // (Back-end) Notify the server for cool down time
        private bool _isDailyRewardClaimed = false;
        

        public void ClaimDailyReward_OnClick()
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

        public void BuyShip_OnClick()
        {
            Debug.LogFormat("{0} - {1} buying a ship - urchine.", nameof(StoreMenu), nameof(BuyShip_OnClick));
            // TODO: back-end buy ship 
        }

        public void BuyMiniGame_OnClick()
        {
            Debug.LogFormat("{0} - {1} buying a mini game.", nameof(StoreMenu), nameof(BuyMiniGame_OnClick));
            // TODO: back-end buy mini game
        }

        public void BuyGuideUpgrade_OnClick()
        {
            Debug.LogFormat("{0} - {1} buying a guide upgrade.", nameof(StoreMenu), nameof(BuyGuideUpgrade_OnClick));
            // TODO: back-end buy a guide upgrade.
        }
    }
}
