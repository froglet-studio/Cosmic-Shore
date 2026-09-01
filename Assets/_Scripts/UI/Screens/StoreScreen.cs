using CosmicShore.UI;
using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using CosmicShore.Utility;
using Reflex.Attributes;

namespace CosmicShore.UI
{
    public class StoreScreen : View
    {
        [Inject] CaptainManager _captainManager;
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
        [SerializeField] bool ShowGamePurchasing;

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

            // Crystals come from PlayerDataService - the live, cloud-persisted wallet the
            // Hangar and every reward payout use. This screen used to read the PlayFab
            // inventory shelf instead, which has been disabled for as long as PlayFab has,
            // so its balance was 0 forever AND never refreshed (the catalog events that
            // drove it cannot fire). Two screens, two answers, for one wallet.
            PlayerDataService.OnCrystalBalanceChanged += HandleCrystalBalanceChanged;
            UpdateCrystalBalance();
        }

        void OnDisable()
        {
            //CaptainManager.OnLoadCaptainData -= UpdateView;
            CatalogManager.OnLoadInventory -= UpdateView;
            CatalogManager.OnInventoryChange -= UpdateTicketBalance;

            PlayerDataService.OnCrystalBalanceChanged -= HandleCrystalBalanceChanged;
        }

        void HandleCrystalBalanceChanged(int _) => UpdateCrystalBalance();

        /// <summary>The live wallet. 0 before the profile service exists, which is a real
        /// state during boot rather than an error.</summary>
        static int CurrentCrystalBalance =>
            PlayerDataService.Instance != null ? PlayerDataService.Instance.GetCrystalBalance() : 0;

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
            CSDebug.Log($"PopulateCaptainPurchaseCards, unfiltered: {captains.Count}");

            // Filter out owned captains
            captains = captains.Where(x => !CatalogManager.Inventory.captains.Contains(x)).ToList();
            CSDebug.Log($"PopulateCaptainPurchaseCards, excluding purchased: {captains.Count}");

            // Filter out unencountered captains
            captains = captains
                .Where(x =>
                {
                    var captain = _captainManager.GetCaptainByName(x.Name);

                    // If this assert is triggered, it suggests a configuration mismatch between playfab and scriptable objects
                    Assert.AreNotEqual(captain, null, $"Could not find captain while loading store catalog - Captain Name: {x.Name}");
                    
                    return captain != null && captain.Encountered == true;
                })
                .ToList();

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
            if (!ShowGamePurchasing)
                return;

            if (gameCardsPopulated)
                return;

            // Get all purchaseable games
            var games = CatalogManager.StoreShelve.games.Values.ToList();
            CSDebug.Log($"PopulategamePurchaseCards, unfiltered: {games.Count}");

            // Filter out owned games
            games = games.Where(x => !CatalogManager.Inventory.games.Contains(x)).ToList();
            CSDebug.Log($"PopulateGamePurchaseCards, excluding purchased: {games.Count}");

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
                        row = GamePurchaseRows[rowIndex];
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
            if (!CrystalBalance) return;

            // Only roll the counter while the screen is live; StartCoroutine on a disabled
            // object throws, and OnEnable can run before the object is fully active.
            if (isActiveAndEnabled)
                StartCoroutine(UpdateBalanceCoroutine());
            else
                CrystalBalance.text = CurrentCrystalBalance.ToString();
        }

        IEnumerator UpdateBalanceCoroutine()
        {
            // The text may hold a placeholder/empty value (e.g. before the balance is first
            // seeded), so parse defensively rather than throwing a FormatException that would
            // abort the coroutine.
            int.TryParse(CrystalBalance.text, out var crystalBalance);
            var newCrystalBalance = CurrentCrystalBalance;
            var delta = crystalBalance - newCrystalBalance;
            var duration = 1f;
            var elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                CrystalBalance.text = ((int)(crystalBalance - (delta * elapsedTime / duration))).ToString();
                yield return null;
                elapsedTime += Time.unscaledDeltaTime;
            }

            // Re-read rather than settling on the value captured a second ago: a reward can
            // land mid-roll, and the final frame must be the wallet, not the stale target.
            CrystalBalance.text = CurrentCrystalBalance.ToString();
        }
    }
}