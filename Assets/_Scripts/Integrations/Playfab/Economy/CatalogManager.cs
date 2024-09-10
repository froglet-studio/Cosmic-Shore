using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.CloudScripts;
using CosmicShore.Integrations.Playfab.Utility;
using CosmicShore.Integrations.PlayFab.Utility;
using CosmicShore.Models;
using CosmicShore.Utility.Singleton;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.CloudScriptModels;
using PlayFab.EconomyModels;
using UnityEngine;
using CatalogItem = PlayFab.EconomyModels.CatalogItem;

namespace CosmicShore.Integrations.PlayFab.Economy
{
    public class CatalogManager : SingletonPersistent<CatalogManager>
    {
        // PlayFab Economy API instance
        static PlayFabEconomyInstanceAPI _playFabEconomyInstanceAPI;

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

        int GrantedCrystalAmount;
        Element GrantedCrystalElement;

        void Start()
        {
            Debug.Log("CatalogManager.Start");
            AuthenticationManager.OnLoginSuccess += InitializePlayFabEconomyAPI;
            AuthenticationManager.OnLoginSuccess += LoadAllCatalogItems;
            OnLoadCatalogSuccess += LoadPlayerInventory;
            OnLoadInventory += GrantStartingInventoryIfInventoryIsEmpty;

            NetworkMonitor.OnNetworkConnectionLost += Inventory.LoadFromDisk;
        }

        public void OnDestroy()
        {
            AuthenticationManager.OnLoginSuccess -= InitializePlayFabEconomyAPI;
            AuthenticationManager.OnLoginSuccess -= LoadAllCatalogItems;
            OnLoadCatalogSuccess -= LoadPlayerInventory;
            OnLoadInventory -= GrantStartingInventoryIfInventoryIsEmpty;

            NetworkMonitor.OnNetworkConnectionLost -= Inventory.LoadFromDisk;
        }

        #region Initialize PlayFab Economy API with Auth Context

        /// <summary>
        /// Initialize PlayFab Economy API
        /// Instantiate PlayFab Economy API with auth context
        /// </summary>
        void InitializePlayFabEconomyAPI()
        {
            // Null check for PlayFab Economy API instance
            _playFabEconomyInstanceAPI ??= new (AuthenticationManager.PlayFabAccount.AuthContext);
            Debug.LogFormat("{0} - {1}: PlayFab Economy API initialized.", nameof(CatalogManager), nameof(InitializePlayFabEconomyAPI));
        }

        #endregion

        #region Catalog Operations

        /// <summary>
        /// Load Catalog Items
        /// Get all catalog items
        /// </summary>
        public void LoadAllCatalogItems()
        {
            List<CatalogItem> allCatalogItems = new List<CatalogItem>();
            LoadCatalogItemsRecursive(allCatalogItems);
        }

        /// <summary>
        /// Load Catalog Items recursively if there are more than playfab's limit of 50 results
        /// </summary>
        void LoadCatalogItemsRecursive(List<CatalogItem> allCatalogItems, string filter = "", Action callback = null, string continuationToken="")
        {
            var request = new SearchItemsRequest
            {
                Filter = filter,
                ContinuationToken = continuationToken,
                Count = 50
            };

            _playFabEconomyInstanceAPI.SearchItems(request, result =>
            {
                // Add the current set of items to the list
                allCatalogItems.AddRange(result.Items);

                // Check if there's a continuation token
                if (!string.IsNullOrEmpty(result.ContinuationToken))
                {
                    // If there is, make another request to get the next page of results
                    LoadCatalogItemsRecursive(allCatalogItems, filter, callback, result.ContinuationToken);
                }
                else
                {
                    // If no more tokens, we've retrieved all items
                    Debug.Log($"Total catalog items retrieved: {allCatalogItems.Count}");
                    callback?.Invoke();
                    OnLoadingCatalogItemsRecursive(allCatalogItems);
                }
            },
            PlayFabUtility.HandleErrorReport);
        }

        /// <summary>
        /// Callback invoked when the recursive api calls have finished
        /// </summary>
        /// <param name="allCatalogItems">List of all discovered catalog items</param>
        void OnLoadingCatalogItemsRecursive(List<CatalogItem> allCatalogItems)
        {
            if (allCatalogItems == null)
            {
                Debug.LogWarningFormat("{0} - {1}: Unable to get catalog item.", nameof(CatalogManager), nameof(OnLoadingCatalogItemsRecursive));
                return;
            }

            if (allCatalogItems.Count == 0)
            {
                Debug.LogWarningFormat("{0} - {1}: No store items are available. Please check out PlayFab dashboard to fillout store items", nameof(CatalogManager), nameof(OnLoadingCatalogItemsRecursive));
                return;
            }

            Debug.LogFormat("{0} - {1}: Catalog items Loaded: Count:{2}.", nameof(CatalogManager), nameof(OnLoadingCatalogItemsRecursive), allCatalogItems.Count);
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
                Debug.LogFormat("   CatalogManager - title: {0}, content type: {1}, tags:{2}", item.Title["NEUTRAL"], item.ContentType, string.Join(",", item.Tags));
                var converted = ModelConversionService.ConvertCatalogItemToVirtualItem(item);
                AddToStoreShelve(item.ContentType, converted);
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
                    Debug.Log($"   AddToStoreShelve Ticket - Title: {item.Name}, ContentType:{item.ContentType}, Type:{item.Type}");
                    StoreShelve.tickets.Add(item.ItemId, item);
                    if (item.Name == "Daily Challenge Ticket")
                        StoreShelve.DailyChallengeTicket = item;
                    else if (item.Name == "Faction Mission Ticket")
                        StoreShelve.FactionMissionTicket = item;

                    Debug.Log("Ticket Product Found - name: " + item.Name +", " + item.Amount);
                    break;
                default:
                    Debug.LogWarningFormat($"CatalogManager - AddToStoreSelves: item content type is not part of the store, {item.Name}, {item.ContentType}");
                    break;
            }
        }
        #endregion

        #region Inventory Operations

        public void GrantElementalCrystals(int amount, Element element)
        {
            string crystalItemId = "";
            Debug.Log($"GrantElementalCrystals: amount: {amount}, element:{element}");
            foreach (var elementalCrystal in StoreShelve.crystals.Values)
            {
                Debug.Log($"Crystal: {elementalCrystal.Name}");
                foreach (var tag in elementalCrystal.Tags)
                    Debug.Log($"Crystal Tags: {tag}");

                if (elementalCrystal.Tags.Contains(element.ToString()))
                {
                    Debug.Log($"Found matching Crystal");
                    crystalItemId = elementalCrystal.ItemId;
                    break;
                }
            }

            if (string.IsNullOrEmpty(crystalItemId))
            {
                Debug.LogError($"{nameof(CatalogManager)}.{nameof(GrantElementalCrystals)} - Error Granting Crystals. No matching crystal found in catalog - element:{element}");
                return;
            }

            GrantedCrystalAmount = amount;
            GrantedCrystalElement = element;

            var request = new AddInventoryItemsRequest
            {
                Amount = amount,
                Item = new InventoryItemReference() { Id = crystalItemId }
            };

            _playFabEconomyInstanceAPI.AddInventoryItems(
                request,
                OnGrantElementalCrystals,
                PlayFabUtility.HandleErrorReport
            );
        }

        /// <summary>
        /// On Grant Shards
        /// </summary>
        /// <param name="response"></param>
        void OnGrantElementalCrystals(AddInventoryItemsResponse response)
        {
            if (response == null)
            {
                Debug.LogWarningFormat($"{nameof(CatalogManager)}.{nameof(OnGrantElementalCrystals)}: received a null response.");
                return;
            }

            foreach (var elementalCrystal in Inventory.crystals)
            {
                if (elementalCrystal.Tags.Contains(GrantedCrystalElement.ToString()))
                {
                    elementalCrystal.Amount += GrantedCrystalAmount;
                    break;
                }
            }

            Debug.Log("CatalogManager - OnGrantElementalCrystals Success.");
            Debug.LogFormat("CatalogManager - transaction ids: {0}", string.Join(",", response.TransactionIds));
        }


        void GrantStartingInventoryIfInventoryIsEmpty()
        {
            if (Inventory.allItems.Count == 0)
                GrantStartingInventory(startingInventory);
        }

        /// <summary>
        /// Grant Starting Inventory
        /// </summary>
        /// <param name="startingItems">Starting Items List</param>
        public void GrantStartingInventory(List<VirtualItem> startingItems)
        {
            var request = new AddInventoryItemsRequest();
            // const int amount = 100;
            foreach (var virtualItem in startingItems)
            {
                request.Item = new() { Id = virtualItem.ItemId };
                request.Amount = virtualItem.Amount;
                
                _playFabEconomyInstanceAPI.AddInventoryItems(
                    request,
                    OnGrantStartingInventory,
                    PlayFabUtility.HandleErrorReport
                );
            }
        }

        /// <summary>
        /// On Granting Starting Inventory
        /// </summary>
        /// <param name="response">Add Inventory Items Response</param>
        void OnGrantStartingInventory(AddInventoryItemsResponse response)
        {
            if (response == null)
            {
                Debug.LogWarningFormat("{0} - {1}: Unable to get catalog item or no inventory items are available.", nameof(CatalogManager), nameof(OnGrantStartingInventory));
                return;
            }
            Debug.Log("CatalogManager - On Add Inventory Item Success.");
            Debug.LogFormat("CatalogManager - transaction ids: {0}", string.Join(",", response.TransactionIds));


            // TODO: verify ownership of expected items to grant, update player data to have inventory granted flag set


            OnInventoryChange?.Invoke();
        }
        

        /// <summary>
        /// Load All Inventory Items
        /// Get a list of inventory item ids and request each item's detail via Get Items Request 
        /// </summary>
        public void LoadPlayerInventory()
        {
            Debug.Log("CatalogManager.LoadPlayerInventory");
            var request = new GetInventoryItemsRequest();
            request.Count = 50; // TODO: need to recursively load all like we don in the catalog
            //request.CustomTags
            
            _playFabEconomyInstanceAPI.GetInventoryItems(
                request,
                OnGettingInventoryItems,
                PlayFabUtility.HandleErrorReport
            );
        }

        /// <summary>
        /// On Loading Player Inventory
        /// </summary>
        /// <param name="response">Get Inventory Items Response</param>
        void OnGettingInventoryItems(GetInventoryItemsResponse response)
        {
            Debug.Log("CatalogManager.OnGettingInventoryItems");

            // If no inventory items no need to process the response.
            if (response == null)
            {
                Debug.LogWarningFormat("{0} - {1}: Unable to get catalog item or no inventory items are available.", nameof(CatalogManager), nameof(OnGettingInventoryItems));
                return;
            }
            
            Debug.Log("CatalogManager - Get Inventory Items success.");

            // Clear out previous loaded inventory, make sure no duplicates.
            ClearLocalInventoryOnLoading();

            // Iterate through the response, convert PlayFab item to virtual item, and add to inventory
            foreach (var item in response.Items)
            {
                Debug.LogFormat("{0} - {1}: id: {2} amount: {3} content type: {4} loaded.", 
                    nameof(CatalogManager), 
                    nameof(OnGettingInventoryItems), 
                    item.Id, item.Amount.ToString(), item.Type);

                var virtualItem = ModelConversionService.ConvertInventoryItemToVirtualItem(item);
                



                if (virtualItem != null)   // Can be null if inventory item no longer exists in the catalog
                    AddToInventory(virtualItem);
            }

            foreach (var crystal in Inventory.crystals)
            {
                Debug.Log($"Crystal: {crystal.Name}, Balance: {crystal.Amount}");
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
                    Debug.LogFormat("{0} - {1} - Adding Captain", nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.captains.Add(item);
                    break;
                case "Class":
                    Debug.LogFormat("{0} - {1} - Adding Ship",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.shipClasses.Add(item);
                    break;
                case "CaptainUpgrade":
                    Debug.LogFormat("{0} - {1} - Adding Upgrade",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.captainUpgrades.Add(item);
                    break;
                case "Game":
                    Debug.LogFormat("{0} - {1} - Adding MiniGame",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.games.Add(item);
                    break;
                case "Crystal":
                    Debug.LogFormat("{0} - {1} - Adding Crystal",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.crystals.Add(item);
                    break;
                case "Ticket":
                    Debug.LogFormat("{0} - {1} - Adding Ticket",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.tickets.Add(item);
                    break;
                default:
                    Debug.LogWarningFormat("{0} - {1} - Item Content Type not related to player inventory items, such as Stores and Subscriptions: {2}", nameof(CatalogManager), nameof(AddToInventory), item.ContentType);
                    break;
            }

            Inventory.allItems.Add(item);
        }

        /// <summary>
        /// Get Catalog Item
        /// </summary>
        /// <param name="virtualItem"></param>
        public void GetCatalogItem(VirtualItem virtualItem)
        {
            var request = new GetItemRequest();
            
            _playFabEconomyInstanceAPI.GetItem(
                request,
                OnGettingCatalogItem,
                PlayFabUtility.HandleErrorReport
            );
        }

        /// <summary>
        /// On Loading Player Inventory
        /// </summary>
        /// <param name="response">Get Item Response</param>
        private void OnGettingCatalogItem(GetItemResponse response)
        {
            if (response == null)
            {
                Debug.LogWarningFormat("{0} - {1}: no response on adding inventory item.", nameof(CatalogManager), nameof(OnGettingCatalogItem));
                return;
            }

            if (response.Item == null)
            {
                Debug.LogWarningFormat("{0} - {1}: no inventory item.", nameof(CatalogManager), nameof(OnGettingCatalogItem));
                return;
            }
                    
            Debug.Log("   CatalogManager - Id: " + response.Item.Id);
        }

        /// <summary>
        /// Add Items to Inventory
        /// Add shinny new stuff! Any type of item from currency to captain and ship upgrades
        /// </summary>
        public void AddInventoryItem(VirtualItem virtualItem)
        {
            var request = new AddInventoryItemsRequest();
            request.Item = new() { Id = virtualItem.ItemId };
            
            _playFabEconomyInstanceAPI.AddInventoryItems(
                request,
                OnAddingInventoryItem, 
                PlayFabUtility.HandleErrorReport);
        }

        private void OnAddingInventoryItem(AddInventoryItemsResponse response)
        {
            if(response == null)
            {
                Debug.LogWarningFormat("{0} - {1}: no result.", nameof(CatalogManager), nameof(AddInventoryItem));
                return;
            }

            Debug.LogFormat("{0} - {1}: item added to player inventory.", nameof(CatalogManager), nameof(AddInventoryItem));
            OnInventoryChange?.Invoke();
        }
        
        #endregion

        #region In-game Purchases

        public void PurchaseCaptainUpgrade(Captain captain, Action successCallback = null, Action failureCallback = null)
        {
            // Find the upgrade
            var elementTag = captain.PrimaryElement.ToString();
            var shipTypeTag = captain.Ship.Class.ToString();
            var upgradeLevelTag = "UpgradeLevel_" + (captain.Level+1);

            Debug.Log($"PurchaseCaptainUpgrade - elementTag:{elementTag},shipTypeTag:{shipTypeTag},upgradeLevelTag:{upgradeLevelTag}");

            foreach (var upgrade in StoreShelve.captainUpgrades.Values)
            {
                Debug.Log($"PurchaseCaptainUpgrade - upgrade:{upgrade.Name}, tags:{JsonConvert.SerializeObject(upgrade.Tags)}");
                Debug.Log($"PurchaseCaptainUpgrade {upgrade.Tags.Contains(elementTag)},{upgrade.Tags.Contains(shipTypeTag)},{upgrade.Tags.Contains(upgradeLevelTag)}");

                if (upgrade.Tags.Contains(elementTag) && upgrade.Tags.Contains(shipTypeTag) && upgrade.Tags.Contains(upgradeLevelTag))
                {
                    Debug.Log($"PurchaseCaptainUpgrade - found a match, attempting purchase");

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
                Debug.LogWarning($"CatalogManager - Attempt to PurchaseItem when max amount already owned. Item:{item.Name}, Owned:{ownedItem.Amount}.");
                return;
            }

            // The currency calculation for currency should be done before passing item and price to purchase inventory item API, otherwise it will get "Invalid Request" error.
            _playFabEconomyInstanceAPI.PurchaseInventoryItems(
                new()
                {
                    Amount = price.UnitAmount,
                    Item = new() 
                    { 
                        Id = item.ItemId
                    },
                    PriceAmounts = new List<PurchasePriceAmount>
                    {
                        new() 
                        { 
                            ItemId = price.ItemId,
                            Amount = price.Amount 
                        }
                    },
                },
                result =>
                {
                    UpdateCurrencyBalance(price.ItemId, price.Amount * -1);
                    if (item.ContentType == "Ticket") item.Amount += 1;
                    AddToInventory(item);
                    Inventory.SaveToDisk();
                    OnInventoryChange?.Invoke();
                    Debug.Log($"CatalogManager - Purchase success.");
                    successCallback?.Invoke();
                },
                error =>
                {
                    PlayFabUtility.HandleErrorReport(error);
                    failureCallback?.Invoke();
                }
            );
        }
        #endregion

        public VirtualItem GetCaptainUpgrade(Captain captain)
        {
            Debug.Log($"GetCaptainUpgrade - Element:{captain.PrimaryElement}");
            Debug.Log($"GetCaptainUpgrade - Class:{captain.Ship.Class}");
            Debug.Log($"GetCaptainUpgrade - Level:{ "UpgradeLevel_" + (captain.Level + 1)}");

            return StoreShelve.captainUpgrades.Values.FirstOrDefault(x => x.Tags.Contains(captain.PrimaryElement.ToString()) &&
                                                                          x.Tags.Contains(captain.Ship.Class.ToString()) &&
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

            DailyRewardHandler.Instance.PlayDailyChallenge(OnPlayDailyChallengeSuccess);
        }
        
        /// <summary>
        /// Play Daily Challenge function execution successful result
        /// Returns playDailyChallengeResult { bool CanPlay, int remainingBalance}
        /// </summary>
        /// <param name="result">Function execution result</param>
        private void OnPlayDailyChallengeSuccess(ExecuteFunctionResult result)
        {
            Debug.Log("DailyRewardHandler - OnPlayDailyChallengeSuccess");
            if (result.FunctionResultTooLarge ?? false)
            {
                Debug.LogError("Cloud script - This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                return;
            }
            
            AddToInventory(GetDailyChallengeTicket());
            Inventory.SaveToDisk();
            OnInventoryChange?.Invoke();
            
            // TODO: Invoke the result if needed for the UI and Daily Reward System
            
            Debug.Log($"Cloud script - The {result.FunctionName} function took {result.ExecutionTimeMilliseconds} to complete");
            Debug.Log($"Cloud script - Result: {result.FunctionResult}");
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
                Debug.Log($"GetCrystalBalance - {crystal.Type}:{crystal.Name}:{crystal.Amount}");
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
                Debug.Log($"RewardClaimed - {crystal.Type}:{crystal.Name}:{value}");
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
    }
}