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
        [SerializeField] PurchaseConfirmationModal PurchaseConfirmationModal;
        [SerializeField] Button PurchaseConfirmationButton;

        [Header("Game Purchasing")] 
        [SerializeField] PurchaseGameCard PurchaseGamePrefab;
        [SerializeField] List<HorizontalLayoutGroup> GamePurchaseRows;

        [Header("Daily Challenge and Faction Tickets")]
        //[SerializeField] PurchaseGameplayTicketCard FactionMissionTicketCard;
        [SerializeField] PurchaseGameplayTicketCard DailyChallengeTicketCard;

        bool captainCardsPopulated = false;

        void Start()
        {
            CatalogManager.OnLoadInventory += UpdateView;
        }

        public override void UpdateView()
        {
            UpdateCrystalBalance();
            //FactionMissionTicketCard.SetVirtualItem(CatalogManager.Instance.GetFactionTicket());
            DailyChallengeTicketCard.SetVirtualItem(CatalogManager.Instance.GetDailyChallengeTicket());
            PopulateCaptainPurchaseCards();
        }

        void PopulateCaptainPurchaseCards()
        {
            if (captainCardsPopulated)
                return;

            var captains = CatalogManager.StoreShelve.captains;

            foreach (var row in CaptainPurchaseRows)
            {
                foreach (Transform child in row.transform)
                {
                    Destroy(child.gameObject);
                }

                int i = 0;
                foreach (var captain in captains.Values)
                {
                    if (i > 2) break;
                    i++;
                    var purchaseTicketCard = Instantiate(PurchaseCaptainPrefab);
                    purchaseTicketCard.ConfirmationModal = PurchaseConfirmationModal;
                    purchaseTicketCard.ConfirmationButton = PurchaseConfirmationButton;
                    purchaseTicketCard.SetVirtualItem(captain);
                    purchaseTicketCard.transform.SetParent(row.transform, false);

                }
            }

            captainCardsPopulated = true;
        }


        void PopulateGamePurchaseCards()
        {

        }

        void UpdateCrystalBalance()
        {
            CrystalBalance.text = CatalogManager.Instance.GetCrystalBalance().ToString();
        }
    }
}