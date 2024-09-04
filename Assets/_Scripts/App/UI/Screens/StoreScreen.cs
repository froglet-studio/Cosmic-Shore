using CosmicShore.App.UI.Modals;
using CosmicShore.App.UI.Views;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Models;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.Ui.Menus
{
    public class StoreScreen : View
    {
        [Header("Crystal Balance")]
        [SerializeField] TMP_Text CrystalBalance;
        [SerializeField] TMP_Text TicketBalance;

        [Header("Captain Purchasing")]
        [SerializeField] GameObject CaptainPurchaseSection;
        [SerializeField] PurchaseCaptainCard PurchaseCaptainPrefab;
        [SerializeField] List<HorizontalLayoutGroup> CaptainPurchaseRows;
        [SerializeField] PurchaseConfirmationModal PurchaseConfirmationModal;
        [SerializeField] Button PurchaseConfirmationButton;
        [SerializeField] int CaptainsPerRow = 3;
        [SerializeField] int MaxCaptainRows = 2;

        [Header("Game Purchasing")]
        [SerializeField] GameObject GamePurchaseSection;
        [SerializeField] PurchaseGameCard PurchaseGamePrefab;
        [SerializeField] List<HorizontalLayoutGroup> GamePurchaseRows;
        [SerializeField] int GamesPerRow = 2;
        [SerializeField] int MaxGameRows = 2;

        [Header("Daily Challenge and Faction Tickets")]
        //[SerializeField] PurchaseGameplayTicketCard FactionMissionTicketCard;
        [SerializeField] PurchaseGameplayTicketCard DailyChallengeTicketCard;

        bool captainCardsPopulated = false;
        bool gameCardsPopulated = false;

        void OnEnable()
        {
            //CaptainManager.OnLoadCaptainData += UpdateView;
            CatalogManager.OnLoadInventory += UpdateView;
            CatalogManager.OnInventoryChange += UpdateTicketBalance;
            CatalogManager.OnCurrencyBalanceChange += UpdateCrystalBalance;
        }

        void OnDisable()
        {
            //CaptainManager.OnLoadCaptainData -= UpdateView;
            CatalogManager.OnLoadInventory -= UpdateView;
            CatalogManager.OnInventoryChange -= UpdateTicketBalance;
            CatalogManager.OnCurrencyBalanceChange -= UpdateCrystalBalance;
        }

        void Start()
        {
            // Clear out placeholder captain cards
            foreach (var row in CaptainPurchaseRows)
            {
                foreach (Transform child in row.transform)
                {
                    Destroy(child.gameObject);
                }
            }

            // Clear out placeholder game cards
            foreach (var row in GamePurchaseRows)
            {
                foreach (Transform child in row.transform)
                {
                    Destroy(child.gameObject);
                }
            }

            // If the scene is reloaded after playing a game, the catalog is already loaded, so the events wont fire
            if (CatalogManager.CatalogLoaded)
                UpdateView();
        }

        public override void UpdateView()
        {
            UpdateCrystalBalance();
            UpdateTicketBalance();
            PopulateDailyChallengeTicketCard();
            PopulateCaptainPurchaseCards();
            PopulateGamePurchaseCards();
        }

        void PopulateDailyChallengeTicketCard()
        {
            DailyChallengeTicketCard.ConfirmationModal = PurchaseConfirmationModal;
            DailyChallengeTicketCard.ConfirmationButton = PurchaseConfirmationButton;
            DailyChallengeTicketCard.SetVirtualItem(CatalogManager.Instance.GetDailyChallengeTicket());
        }

        void PopulateCaptainPurchaseCards()
        {
            if (captainCardsPopulated)
                return;

            // Get all purchaseable captains
            var captains = CatalogManager.StoreShelve.captains.Values.ToList();
            Debug.Log($"PopulateCaptainPurchaseCards, unfiltered: {captains.Count}");

            // Filter out owned captains
            captains = captains.Where(x => !CatalogManager.Inventory.captains.Contains(x)).ToList();
            Debug.Log($"PopulateCaptainPurchaseCards, excluding purchased: {captains.Count}");

            // Filter out unencountered captains
            captains = captains.Where(x => CaptainManager.Instance.GetCaptainByName(x.Name).Encountered == true).ToList();
            Debug.Log($"PopulateCaptainPurchaseCards, excluding not encountered: {captains.Count}");

            // if no captains, hide captains section
            if (captains.Count == 0)
                CaptainPurchaseSection.SetActive(false);

            var captainIndex = 0;
            var rowIndex = 0;
            var row = CaptainPurchaseRows[rowIndex];
            while (captainIndex < CaptainsPerRow*MaxCaptainRows && captainIndex < captains.Count && rowIndex < MaxCaptainRows)
            {
                var captain = captains[captainIndex];

                var purchaseCaptainCard = Instantiate(PurchaseCaptainPrefab);
                purchaseCaptainCard.ConfirmationModal = PurchaseConfirmationModal;
                purchaseCaptainCard.ConfirmationButton = PurchaseConfirmationButton;
                purchaseCaptainCard.SetVirtualItem(captain);
                purchaseCaptainCard.transform.SetParent(row.transform, false);

                captainIndex++;
                if (captainIndex % CaptainsPerRow == 0 && captainIndex != captains.Count) // Second check is to prevent an empty row from being displayed
                {
                    rowIndex++;
                    if (rowIndex < MaxCaptainRows)
                    {
                        row = CaptainPurchaseRows[rowIndex];
                        row.gameObject.SetActive(true);
                    }
                }
            }

            captainCardsPopulated = true;
        }

        void PopulateGamePurchaseCards()
        {
            if (gameCardsPopulated)
                return;

            // Get all purchaseable games
            var games = CatalogManager.StoreShelve.games.Values.ToList();
            Debug.Log($"PopulategamePurchaseCards, unfiltered: {games.Count}");

            // Filter out owned games
            games = games.Where(x => !CatalogManager.Inventory.games.Contains(x)).ToList();
            Debug.Log($"PopulateGamePurchaseCards, excluding purchased: {games.Count}");

            // if no games, hide games section
            if (games.Count == 0)
                GamePurchaseSection.SetActive(false);

            var gameIndex = 0;
            var rowIndex = 0;
            var row = GamePurchaseRows[rowIndex];
            while (gameIndex < GamesPerRow * MaxGameRows && gameIndex < games.Count && rowIndex < MaxGameRows)
            {
                var game = games[gameIndex];

                var purchaseGameCard = Instantiate(PurchaseGamePrefab);
                purchaseGameCard.ConfirmationModal = PurchaseConfirmationModal;
                purchaseGameCard.ConfirmationButton = PurchaseConfirmationButton;
                purchaseGameCard.SetVirtualItem(game);
                purchaseGameCard.transform.SetParent(row.transform, false);

                gameIndex++;
                if (gameIndex % GamesPerRow == 0)
                {
                    rowIndex++;
                    if (rowIndex < MaxGameRows && gameIndex != games.Count) // Second check is to prevent an empty row from being displayed
                    {
                        row = CaptainPurchaseRows[rowIndex];
                        row.gameObject.SetActive(true);
                    }
                }
            }

            gameCardsPopulated = true;
        }

        void UpdateTicketBalance()
        {
            TicketBalance.text = CatalogManager.Instance.GetDailyChallengeTicketBalance().ToString();
        }

        void UpdateCrystalBalance()
        {
            StartCoroutine(UpdateBalanceCoroutine());
        }

        IEnumerator UpdateBalanceCoroutine()
        {
            var crystalBalance = int.Parse(CrystalBalance.text);
            var newCrystalBalance = CatalogManager.Instance.GetCrystalBalance();
            Debug.Log($"UpdateBalanceCoroutine - initial Balance: {crystalBalance}, new Balance: {newCrystalBalance}");
            var delta = crystalBalance- newCrystalBalance;
            var duration = 1f;
            var elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                CrystalBalance.text = ((int)(crystalBalance - (delta * elapsedTime / duration))).ToString();
                yield return null;
                elapsedTime += Time.unscaledDeltaTime;
            }
            CrystalBalance.text = CatalogManager.Instance.GetCrystalBalance().ToString();   
        }
    }
}