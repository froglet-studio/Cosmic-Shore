// Ported from Assets/_Scripts/System/Playfab/Economy/CatalogManager.cs (Store unit
// 2026-07-10; supersedes the Arc F 2b-ii 49L shell — the inline Inventory shell moved
// to the real ported Inventory.cs) — the LOCAL economy lanes are structure-verbatim.
// Upstream this manager is in its "[PLAYFAB DISABLED]" state (Start no longer wires
// login/catalog loading; economy is being rebuilt on UGS; pending removal), so the
// PlayFab request/response plumbing (SearchItems / GetInventoryItems /
// AddInventoryItems / PurchaseInventoryItems / GetItem via PlayFabEconomyInstanceAPI
// + ModelConversionService + Newtonsoft) never runs there — those lanes are
// deviation-commented at their call sites, and their local success bodies survive as
// the internal seams fixtures/tests drive (the LeaderboardManager offline-lane
// shape). Everything a live consumer touches — StoreShelve/Inventory routing, the
// purchase guard + local settlement, ticket + crystal balances, currency updates,
// the static events — is REAL.
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Engine;
using CosmicShore.Utility;
using CosmicShore.Data;
using CosmicShore.Engine.Injection;
namespace CosmicShore.Core
{
    public class CatalogManager : SingletonPersistent<CatalogManager>
    {
        [Inject] CaptainManager _captainManager;
        [SerializeField]
        NetworkMonitorDataVariable  _networkMonitorDataVariable;
        NetworkMonitorData _networkMonitorData => _networkMonitorDataVariable.Value;

        // PORT Deviation (Store unit, PlayFab SDK): the PlayFabEconomyInstanceAPI
        // instance + InitializePlayFabEconomyAPI / LoadAllCatalogItems /
        // LoadCatalogItemsRecursive / LoadPlayerInventory / GetCatalogItem /
        // AddInventoryItem / GrantElementalCrystals / GrantStartingInventory request
        // plumbing lived here. No engine SDK — and the upstream "[PLAYFAB DISABLED]"
        // Start means none of it runs there either.

        // Player inventory and items
        public static StoreShelve StoreShelve { get; private set; } = new();

        public static Inventory Inventory { get; private set; } = new();

        public static event Action OnLoadCatalogSuccess;   // Use an event to prevent a race condition - Inventory Loading requires the full catalog to have been loaded
        public static event Action OnLoadInventory;
        public static event Action OnInventoryChange;
        public static event Action OnCurrencyBalanceChange;
        [SerializeField] List<VirtualItem> startingInventory = new();

        public static bool CatalogLoaded { get; private set; }

        public const int MaxDailyChallengeTicketBalance = 5;
        public const int DailyRewardAmount = 100;         // TODO: need to pull this from the server during start

        void Start()
        {
            // [PLAYFAB DISABLED] Economy/catalog will be rebuilt on UGS. Pending removal.
        }

        public void OnDestroy()
        {
            // PORT Deviation (Store unit): upstream also detached the
            // AuthenticationManager.OnLoginSuccess + OnLoadCatalogSuccess/
            // OnLoadInventory chains (only ever wired by the disabled login path).
            _networkMonitorData.OnNetworkLost.OnRaised -= Inventory.LoadFromDisk;
        }

        /// <summary>
        /// The local landing lane of OnLoadingCatalogItemsRecursive — upstream the
        /// PlayFab search response funneled (via ModelConversionService) into exactly
        /// this: shelve every item, mark the catalog loaded, raise the success event.
        /// Fixtures and tests are the callers in the port.
        /// </summary>
        internal void LoadLocalCatalog(List<VirtualItem> allCatalogItems)
        {
            if (allCatalogItems == null)
            {
                CSDebug.LogWarningFormat("{0} - {1}: Unable to get catalog item.", nameof(CatalogManager), nameof(LoadLocalCatalog));
                return;
            }

            if (allCatalogItems.Count == 0)
            {
                CSDebug.LogWarningFormat("{0} - {1}: No store items are available. Please check out PlayFab dashboard to fillout store items", nameof(CatalogManager), nameof(LoadLocalCatalog));
                return;
            }

            CSDebug.LogFormat("{0} - {1}: Catalog items Loaded: Count:{2}.", nameof(CatalogManager), nameof(LoadLocalCatalog), allCatalogItems.Count);
            if (StoreShelve == null)
            {
                StoreShelve = new()
                {
                    crystals = new(),
                    classes = new(),
                    captains = new(),
                    captainUpgrades = new(),
                    games = new(),
                    tickets = new(),
                };
            }

            foreach (var item in allCatalogItems)
            {
                AddToStoreShelve(item.ContentType, item);
            }

            CatalogLoaded = true;
            OnLoadCatalogSuccess?.Invoke();
        }

        void AddToStoreShelve(string contentType, VirtualItem item)
        {
            StoreShelve.allItems.Add(item.ItemId, item);

            switch (contentType)
            {
                case "Crystal":
                    StoreShelve.crystals.Add(item.ItemId, item);
                    break;
                case "Class":
                    StoreShelve.classes.Add(item.ItemId, item);
                    break;
                case "Game":
                    StoreShelve.games.Add(item.ItemId, item);
                    break;
                case "Captain":
                    StoreShelve.captains.Add(item.ItemId, item);
                    break;
                case "CaptainUpgrade":
                    StoreShelve.captainUpgrades.Add(item.ItemId, item);
                    break;
                case "Ticket":
                    CSDebug.Log($"   AddToStoreShelve Ticket - Title: {item.Name}, ContentType:{item.ContentType}, Type:{item.Type}");
                    StoreShelve.tickets.Add(item.ItemId, item);
                    if (item.Name == "Daily Challenge Ticket")
                        StoreShelve.DailyChallengeTicket = item;
                    else if (item.Name == "Faction Mission Ticket")
                        StoreShelve.FactionMissionTicket = item;

                    CSDebug.Log("Ticket Product Found - name: " + item.Name +", " + item.Amount);
                    break;
                default:
                    CSDebug.LogWarningFormat($"CatalogManager - AddToStoreSelves: item content type is not part of the store, {item.Name}, {item.ContentType}");
                    break;
            }
        }

        #region Inventory Operations

        /// <summary>
        /// The local landing lane of OnGettingInventoryItems — clear, add every
        /// item, persist, raise OnLoadInventory. Fixtures and tests are the callers.
        /// </summary>
        internal void LoadLocalInventory(List<VirtualItem> items)
        {
            // Clear out previous loaded inventory, make sure no duplicates.
            ClearLocalInventoryOnLoading();

            foreach (var virtualItem in items)
            {
                if (virtualItem != null)   // Can be null if inventory item no longer exists in the catalog
                    AddToInventory(virtualItem);
            }

            foreach (var crystal in Inventory.crystals)
            {
                CSDebug.Log($"Crystal: {crystal.Name}, Balance: {crystal.Amount}");
            }

            Inventory.SaveToDisk();
            OnLoadInventory?.Invoke();
        }

        void ClearLocalInventoryOnLoading()
        {
            if (Inventory == null) return;

            Inventory.games.Clear();
            Inventory.captainUpgrades.Clear();
            Inventory.crystals.Clear();
            Inventory.shipClasses.Clear();
            Inventory.captains.Clear();
            Inventory.tickets.Clear();
            Inventory.allItems.Clear();
        }

        void AddToInventory(VirtualItem item)
        {
            switch (item.ContentType)
            {
                case "Captain":
                    CSDebug.LogFormat("{0} - {1} - Adding Captain", nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.captains.Add(item);
                    // If we ever own a captain, consider it encountered
                    _captainManager.EncounterCaptain(item.Name);
                    break;
                case "Class":
                    CSDebug.LogFormat("{0} - {1} - Adding Vessel",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.shipClasses.Add(item);
                    break;
                case "CaptainUpgrade":
                    CSDebug.LogFormat("{0} - {1} - Adding Upgrade",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.captainUpgrades.Add(item);
                    break;
                case "Game":
                    CSDebug.LogFormat("{0} - {1} - Adding MiniGame",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.games.Add(item);
                    break;
                case "Crystal":
                    CSDebug.LogFormat("{0} - {1} - Adding Crystal",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.crystals.Add(item);
                    break;
                case "Ticket":
                    CSDebug.LogFormat("{0} - {1} - Adding Ticket",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.tickets.Add(item);
                    break;
                default:
                    CSDebug.LogWarningFormat("{0} - {1} - Item Content Type not related to player inventory items, such as Stores and Subscriptions: {2}", nameof(CatalogManager), nameof(AddToInventory), item.ContentType);
                    break;
            }

            Inventory.allItems.Add(item);
        }

        #endregion

        #region In-game Purchases

        public void PurchaseCaptainUpgrade(Captain captain, Action successCallback = null, Action failureCallback = null)
        {
            // Find the upgrade
            var elementTag = captain.PrimaryElement.ToString();
            var shipTypeTag = captain.Vessel.Class.ToString();
            var upgradeLevelTag = "UpgradeLevel_" + (captain.Level+1);

            CSDebug.Log($"PurchaseCaptainUpgrade - elementTag:{elementTag},shipTypeTag:{shipTypeTag},upgradeLevelTag:{upgradeLevelTag}");

            foreach (var upgrade in StoreShelve.captainUpgrades.Values)
            {
                if (upgrade.Tags.Contains(elementTag) && upgrade.Tags.Contains(shipTypeTag) && upgrade.Tags.Contains(upgradeLevelTag))
                {
                    CSDebug.Log($"PurchaseCaptainUpgrade - found a match, attempting purchase");

                    PurchaseItem(upgrade, upgrade.Price[0], 1, successCallback, failureCallback);
                    break;
                }
            }
        }

        /// <summary>
        /// Purchase Item
        /// Buy in-game item with virtual currency (Shards, Crystals)
        /// </summary>
        public void PurchaseItem(VirtualItem item, ItemPrice price, int maxCount=1, Action successCallback=null, Action failureCallback=null)
        {
            // Prevent over purchasing
            var ownedItem = Inventory.allItems.Where(x => x.ItemId == item.ItemId).FirstOrDefault();
            if (ownedItem != null && ownedItem.Amount >= maxCount)
            {
                CSDebug.LogWarning($"CatalogManager - Attempt to PurchaseItem when max amount already owned. Item:{item.Name}, Owned:{ownedItem.Amount}.");
                return;
            }

            // PORT Deviation (Store unit, PlayFab SDK): upstream issued
            // PurchaseInventoryItems here and settled locally inside its success
            // callback; the failure lane belonged to PlayFab error reports. With no
            // server round-trip the settlement runs directly — the UI's
            // affordability guard (PurchaseItemCard) is unchanged upstream code.
            SettlePurchaseLocally(item, price, successCallback);
        }

        /// <summary>The success-callback body of the upstream PurchaseInventoryItems call, verbatim.</summary>
        internal void SettlePurchaseLocally(VirtualItem item, ItemPrice price, Action successCallback = null)
        {
            UpdateCurrencyBalance(price.ItemId, price.Amount * -1);
            if (item.ContentType == "Ticket") item.Amount += 1;
            AddToInventory(item);
            Inventory.SaveToDisk();
            OnInventoryChange?.Invoke();
            CSDebug.Log($"CatalogManager - Purchase success.");
            successCallback?.Invoke();
        }
        #endregion

        public VirtualItem GetCaptainUpgrade(Captain captain)
        {
            CSDebug.Log($"GetCaptainUpgrade - Element:{captain.PrimaryElement}");
            CSDebug.Log($"GetCaptainUpgrade - Class:{captain.Vessel.Class}");
            CSDebug.Log($"GetCaptainUpgrade - Level:{ "UpgradeLevel_" + (captain.Level + 1)}");

            return StoreShelve.captainUpgrades.Values.FirstOrDefault(x => x.Tags.Contains(captain.PrimaryElement.ToString()) &&
                                                                          x.Tags.Contains(captain.Vessel.Class.ToString()) &&
                                                                          x.Tags.Contains("UpgradeLevel_" + (captain.Level + 1)));
        }

        public VirtualItem GetFactionTicket()
        {
            return StoreShelve.FactionMissionTicket;
        }

        public VirtualItem GetDailyChallengeTicket()
        {
            return StoreShelve.DailyChallengeTicket;
        }

        public void UseDailyChallengeTicket()
        {
            var dcTicket = GetDailyChallengeTicket();
            dcTicket.Amount -= 1;

            // PORT Deviation (Store unit, PlayFab SDK): the cloud function's success
            // callback (OnPlayDailyChallengeSuccess) re-added the ticket + saved +
            // raised OnInventoryChange. The cloud lane never answers (PlayFab inert
            // upstream too), so the callback body did not survive the port.
            DailyRewardHandler.Instance.PlayDailyChallenge(null);
        }

        public int GetCrystalBalance(Element crystalElementType=Element.Omni)
        {
            int balance = 0;
            foreach (var crystal in Inventory.crystals)
            {
                if (crystal.Tags.Contains(crystalElementType.ToString()))
                {
                    balance = crystal.Amount;
                    break;
                }
                CSDebug.Log($"GetCrystalBalance - {crystal.Type}:{crystal.Name}:{crystal.Amount}");
            }

            return balance;
        }

        public int GetDailyChallengeTicketBalance()
        {
            var tickets = Inventory.tickets.FirstOrDefault(x => x.Name == Instance.GetDailyChallengeTicket().Name);

            if (tickets != null)
                return tickets.Amount;

            return 0;
        }


        public void RewardClaimed(Element crystalElementType, int value)
        {
            var crystalId = "";
            foreach (var crystal in Inventory.crystals)
            {
                if (crystal.Tags.Contains(crystalElementType.ToString()))
                {
                    crystalId = crystal.ItemId;
                    break;
                }
                CSDebug.Log($"RewardClaimed - {crystal.Type}:{crystal.Name}:{value}");
            }

            UpdateCurrencyBalance(crystalId, value);
        }

        void UpdateCurrencyBalance(string currencyItemId, int amount)
        {
            foreach (var item in StoreShelve.crystals)
            {
                if (item.Value.ItemId == currencyItemId)
                {
                    item.Value.Amount += amount;
                    OnCurrencyBalanceChange?.Invoke();
                }
            }
        }

        /// <summary>
        /// Reset the process-wide statics (test/menushell-rebuild isolation — the
        /// original engine got this for free from domain reloads).
        /// </summary>
        internal static void ResetLocalEconomy()
        {
            StoreShelve = new();
            Inventory = new();
            CatalogLoaded = false;
        }
    }
}
