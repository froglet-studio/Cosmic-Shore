using CosmicShore.Integrations.PlayFab.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.Ui.Menus
{
    public class StoreMenu : MonoBehaviour
    {
        [SerializeField] TMP_Text CrystalBalance;

        [Header("Daily Reward")] 
        [SerializeField] Button claimDailyRewardButton;
        
        [Header("Ship Purchasing")]
        [SerializeField] Button buyShipButton;
        
        [Header("MiniGame Purchasing")] 
        [SerializeField] Button buyMiniGameButton;
 
        [Header("Captain Upgrade Purchasing")] 
        [SerializeField] Button buyCaptainUpgradeButton;

        [SerializeField] PurchaseGameplayTicketCard FactionMissionTicketCard;
        [SerializeField] PurchaseGameplayTicketCard DailyChallengeTicketCard;

        // Upon claiming daily reward, the button non-clickable here on the menu
        // (Back-end) Notify the server for cool down time
        bool _isDailyRewardClaimed = false;


        void Start()
        {
            CatalogManager.OnLoadInventory += InitializeView;
        }

        void InitializeView()
        {
            UpdateCrystalBalance();
            FactionMissionTicketCard.SetVirtualItem(CatalogManager.Instance.GetFactionTicket());
            Debug.Log(DailyChallengeTicketCard.name);
            DailyChallengeTicketCard.SetVirtualItem(CatalogManager.Instance.GetFactionTicket());
            //DailyChallengeTicketCard.SetVirtualItem(CatalogManager.Instance.GetDailyChallengeTicket());
        }

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

        public void BuyCaptainUpgrade_OnClick()
        {
            Debug.LogFormat("{0} - {1} buying a captain upgrade.", nameof(StoreMenu), nameof(BuyCaptainUpgrade_OnClick));
            // TODO: back-end buy a captain upgrade.
        }

        void UpdateCrystalBalance()
        {
            CrystalBalance.text = CatalogManager.Instance.GetCrystalBalance().ToString();
        }
    }
}