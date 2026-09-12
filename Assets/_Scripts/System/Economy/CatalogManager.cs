using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The catalog and player inventory, minus PlayFab.
    ///
    /// <para><b>What is left is what the player could ever actually see.</b> Every catalog read,
    /// inventory read, grant and purchase in the original was a PlayFab Economy round trip through
    /// <c>_playFabEconomyInstanceAPI</c>, built from
    /// <c>AuthenticationManager.PlayFabAccount.AuthContext</c>. That context was never populated —
    /// <c>AuthenticationManager.Awake()</c> early-returned — and <c>Start()</c> already carried a
    /// <c>[PLAYFAB DISABLED]</c> marker with nothing subscribed, so not one of those calls could
    /// fire. The catalog has therefore always been empty at runtime and every balance zero. The
    /// LOCAL logic over <see cref="StoreShelve"/> and <see cref="Inventory"/> is kept verbatim, so
    /// behaviour is unchanged.</para>
    ///
    /// <para>The class survives PlayFab's removal because 38 call sites across the Store, the
    /// Hangar and the purchase modals name it — see <c>Docs/PLAYFAB_RETIREMENT.md</c> §2a. Those
    /// surfaces are separately de-scoped and fail closed through <c>SO_CommerceAvailability</c>
    /// (STEAM_RELEASE_TASKS R4); this type does not change that posture either way.</para>
    ///
    /// <para><b>Whatever backend replaces this goes behind these same members.</b> The methods that
    /// needed a server are no-ops that say so rather than deletions, because their signatures are
    /// the contract the UI is already written against.</para>
    /// </summary>
    public class CatalogManager : SingletonPersistent<CatalogManager>
    {
        [Inject] CaptainManager _captainManager;

        [SerializeField] NetworkMonitorDataVariable _networkMonitorDataVariable;
        NetworkMonitorData _networkMonitorData => _networkMonitorDataVariable != null ? _networkMonitorDataVariable.Value : null;

        // Statics are reset per domain load: with domain reload disabled a session-1 catalog would
        // otherwise read as live in session 2.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            StoreShelve = new();
            Inventory = new();
            CatalogLoaded = false;
            OnLoadCatalogSuccess = null;
            OnLoadInventory = null;
            OnInventoryChange = null;
            OnCurrencyBalanceChange = null;
        }

        // Player inventory and items
        public static StoreShelve StoreShelve { get; private set; } = new();

        public static Inventory Inventory { get; private set; } = new();

        public static event Action OnLoadCatalogSuccess;   // Use an event to prevent a race condition - Inventory Loading requires the full catalog to have been loaded
        public static event Action OnLoadInventory;
        public static event Action OnInventoryChange;
        public static event Action OnCurrencyBalanceChange;

        public static bool CatalogLoaded { get; private set; }

        public const int MaxDailyChallengeTicketBalance = 5;
        public const int DailyRewardAmount = 100;         // TODO: need to pull this from the server during start

        void Start()
        {
            // The one subscription that never needed PlayFab: fall back to the on-disk inventory
            // when the network drops. The original only ever UNsubscribed this (its subscribe sat
            // in a Start() that had been stubbed out with [PLAYFAB DISABLED]), so it was a
            // one-sided wiring; it is symmetric now.
            if (_networkMonitorData?.OnNetworkLost != null)
                _networkMonitorData.OnNetworkLost.OnRaised += Inventory.LoadFromDisk;
        }

        public void OnDestroy()
        {
            if (_networkMonitorData?.OnNetworkLost != null)
                _networkMonitorData.OnNetworkLost.OnRaised -= Inventory.LoadFromDisk;
        }

        #region Catalog operations — had a backend, now inert

        /// <summary>
        /// Loaded the whole PlayFab catalog, paging 50 at a time. There is no catalog service now,
        /// so the shelf stays empty and <see cref="CatalogLoaded"/> is raised so anything gating on
        /// it (StoreScreen's load path) proceeds to its empty state rather than waiting forever.
        /// </summary>
        public void LoadAllCatalogItems()
        {
            CatalogLoaded = true;
            OnLoadCatalogSuccess?.Invoke();
        }

        /// <summary>Read the player's owned items from the PlayFab inventory service. No backend.</summary>
        public void LoadPlayerInventory()
        {
            OnLoadInventory?.Invoke();
        }

        /// <summary>Fetched one catalog item by id. No backend.</summary>
        public void GetCatalogItem(VirtualItem virtualItem) { }

        #endregion

        #region Inventory operations — local

        /// <summary>Granted crystals server-side. No backend, so nothing is credited.</summary>
        public void GrantElementalCrystals(int amount, Element element) { }

        /// <summary>Granted the starting bundle server-side. No backend.</summary>
        public void GrantStartingInventory(List<VirtualItem> startingItems) { }

        /// <summary>Added one owned item server-side. No backend.</summary>
        public void AddInventoryItem(VirtualItem virtualItem) { }

        void AddToInventory(VirtualItem item)
        {
            switch (item.ContentType)
            {
                case "Captain":
                    Inventory.captains.Add(item);
                    // If we ever own a captain, consider it encountered
                    _captainManager.EncounterCaptain(item.Name);
                    break;
                case "Class":
                    Inventory.shipClasses.Add(item);
                    break;
                case "CaptainUpgrade":
                    Inventory.captainUpgrades.Add(item);
                    break;
                case "Game":
                    Inventory.games.Add(item);
                    break;
                case "Crystal":
                    Inventory.crystals.Add(item);
                    break;
                case "Ticket":
                    Inventory.tickets.Add(item);
                    break;
                default:
                    CSDebug.LogWarningFormat("{0} - {1} - Item Content Type not related to player inventory items, such as Stores and Subscriptions: {2}", nameof(CatalogManager), nameof(AddToInventory), item.ContentType);
                    break;
            }

            Inventory.allItems.Add(item);
        }

        #endregion

        #region Purchases — had a backend, now inert

        /// <summary>
        /// Spent the player's balance through the PlayFab catalog. No backend, so the purchase
        /// cannot complete and the failure callback is what runs. That matches the de-scoped
        /// posture: these surfaces are locked by <c>SO_CommerceAvailability</c> and should not be
        /// reachable at all.
        /// </summary>
        public void PurchaseCaptainUpgrade(Captain captain, Action successCallback = null, Action failureCallback = null)
        {
            failureCallback?.Invoke();
        }

        /// <inheritdoc cref="PurchaseCaptainUpgrade"/>
        public void PurchaseItem(VirtualItem item, ItemPrice price, int maxCount = 1, Action successCallback = null, Action failureCallback = null)
        {
            failureCallback?.Invoke();
        }

        #endregion

        #region Local queries — unchanged

        public VirtualItem GetCaptainUpgrade(Captain captain)
        {
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
            if (dcTicket == null)
                return;

            dcTicket.Amount -= 1;

            DailyRewardHandler.Instance.PlayDailyChallenge(() =>
            {
                AddToInventory(GetDailyChallengeTicket());
                Inventory.SaveToDisk();
                OnInventoryChange?.Invoke();
            });
        }

        public int GetCrystalBalance(Element crystalElementType = Element.Omni)
        {
            int balance = 0;
            foreach (var crystal in Inventory.crystals)
            {
                if (crystal.Tags.Contains(crystalElementType.ToString()))
                {
                    balance = crystal.Amount;
                    break;
                }
            }

            return balance;
        }

        public int GetDailyChallengeTicketBalance()
        {
            var ticket = Instance.GetDailyChallengeTicket();
            if (ticket == null)
                return 0;

            var tickets = Inventory.tickets.FirstOrDefault(x => x.Name == ticket.Name);

            return tickets?.Amount ?? 0;
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

        #endregion
    }
}
