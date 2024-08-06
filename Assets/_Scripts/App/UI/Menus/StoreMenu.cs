using CosmicShore.Integrations.PlayFab.Economy;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.Ui.Menus
{
    public class StoreMenu : View
    {
        [Header("Crystal Balance")]
        [SerializeField] TMP_Text CrystalBalance;

        [Header("Captain Purchasing")]
        [SerializeField] PurchaseCaptainCard PurchaseCaptainPrefab;
        [SerializeField] List<HorizontalLayoutGroup> CaptainPurchaseRows;

        [Header("Game Purchasing")] 
        [SerializeField] PurchaseGameCard PurchaseGamePrefab;
        [SerializeField] List<HorizontalLayoutGroup> GamePurchaseRows;

        [Header("Daily Challenge and Faction Tickets")]
        [SerializeField] PurchaseGameplayTicketCard FactionMissionTicketCard;
        [SerializeField] PurchaseGameplayTicketCard DailyChallengeTicketCard;

        void Start()
        {
            CatalogManager.OnLoadInventory += UpdateView;
        }

        public override void UpdateView()
        {
            UpdateCrystalBalance();
            FactionMissionTicketCard.SetVirtualItem(CatalogManager.Instance.GetFactionTicket());
            DailyChallengeTicketCard.SetVirtualItem(CatalogManager.Instance.GetDailyChallengeTicket());


        }

        void PopulateGamePurchaseCards()
        {

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