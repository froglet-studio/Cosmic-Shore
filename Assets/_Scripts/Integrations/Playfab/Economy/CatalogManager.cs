using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.Utility;
using CosmicShore.Utility.Singleton;
using PlayFab;
using PlayFab.ClientModels;
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

        public static Dictionary<string, string> Bundles { get; private set; } = new();
        public static event Action<string> OnGettingBundleId;
        static event Action OnLoadCatalogSuccess;   // Use an event to prevent a race condition - Inventory Loading requires the full catalog to have been loaded

        int _dailyRewardIndex;

        const string DailyRewardStoreID = "63d59c05-2b86-4843-8e9b-61c07ab121ad";
        const string ClaimDailyRewardTime = "ClaimDailyRewardTime";
        
        
        void Start()
        {
            Debug.Log("CatalogManager.Start");
            AuthenticationManager.OnLoginSuccess += InitializePlayFabEconomyAPI;
            AuthenticationManager.OnLoginSuccess += LoadAllCatalogItems;
            OnLoadCatalogSuccess += LoadPlayerInventory;
            OnLoadCatalogSuccess += GetDailyRewardStore;

            NetworkMonitor.NetworkConnectionLost += Inventory.LoadFromDisk;
        }

        public void OnDestroy()
        {
            AuthenticationManager.OnLoginSuccess -= InitializePlayFabEconomyAPI;
            AuthenticationManager.OnLoginSuccess -= LoadAllCatalogItems;
            OnLoadCatalogSuccess -= LoadPlayerInventory;
            OnLoadCatalogSuccess -= GetDailyRewardStore;

            NetworkMonitor.NetworkConnectionLost -= Inventory.LoadFromDisk;
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
            LoadCatalogItems();
        }

        /// <summary>
        /// Load Catalog Items
        /// Get all catalog items
        /// </summary>
        public void LoadCatalogItems(string filter = "")
        {
            var request = new SearchItemsRequest();
            request.Filter = filter;
            
            _playFabEconomyInstanceAPI.SearchItems(request,
                OnLoadingCatalogItems,
                PlayFabUtility.HandleErrorReport
            );
        }

        /// <summary>
        /// On Loading Catalog Items Callback
        /// Load catalog items to local memory
        /// </summary>
        /// <param name="response">Search Items Response</param>
        void OnLoadingCatalogItems(SearchItemsResponse response)
        {
            if (response == null)
            {
                Debug.LogWarningFormat("{0} - {1}: Unable to get catalog item.", nameof(CatalogManager), nameof(OnLoadingCatalogItems));
                return;
            }

            if (response.Items.Count == 0)
            {
                Debug.LogWarningFormat("{0} - {1}: No store items are available. Please check out PlayFab dashboard to fillout store items", nameof(CatalogManager), nameof(OnLoadingCatalogItems));
                return;
            }
            
            Debug.LogFormat("{0} - {1}: Catalog items Loaded.", nameof(CatalogManager), nameof(OnLoadingCatalogItems));
            StoreShelve = new()
            {
                crystals = new(),
                classes = new(),
                games = new()
            };

            foreach (var item in response.Items)
            {
                Debug.LogFormat("   CatalogManager - Inventory Id: {0} title: {1} content type: {2}", item.Id, item.Title, item.ContentType);
                Debug.LogFormat("   CatalogManager - tags: {0}", string.Join(",", item.Tags));
                Debug.LogFormat("   CatalogManager - Type: {0}", item.Type);
                Debug.LogFormat("   CatalogManager - ContentType: {0}", item.ContentType);
                var converted = ConvertCatalogItemToVirtualItem(item);
                AddToStoreShelve(item.ContentType, converted);
            }

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
                default:
                    Debug.LogWarningFormat("CatalogManager - AddToStoreSelves: item content type is not part of the store.");
                    break;
            }
        }



        #endregion

        #region Inventory Operations

        // TODO: Captain XP is now part of player data, not a store item. Should re-wire the API calls from PlayerDataController.
        // public void GrantCaptainXP(int amount, ShipTypes shipClass, Element element)
        // {
        //     string shardItemId = "";
        //     Debug.Log($"Captain Knowledge Length: {Catalog.CaptainXP.Count}");
        //     foreach (var captainXP in Catalog.CaptainXP)
        //     {
        //         Debug.Log($"Next Captain: {captainXP.Name}");
        //         foreach (var tag in captainXP.Tags)
        //             Debug.Log($"Captain Knowledge Tags: {tag}");
        //
        //         if (captainXP.Tags.Contains(shipClass.ToString()) && captainXP.Tags.Contains(element.ToString()))
        //         {
        //             Debug.Log($"Found matching Captain Shard");
        //             shardItemId = captainXP.ItemId;
        //             break;
        //         }
        //     }
        //
        //     if (string.IsNullOrEmpty(shardItemId))
        //     {
        //         Debug.LogError($"{nameof(CatalogManager)}.{nameof(GrantCaptainXP)} - Error Granting Shards. No matching captain shard found in catalog - shipClass:{shipClass}, element:{element}");
        //         return;
        //     }
        //
        //     var request = new AddInventoryItemsRequest();
        //     request.Amount = amount;
        //     request.Item = new InventoryItemReference() { Id = shardItemId };
        //     
        //     _playFabEconomyInstanceAPI.AddInventoryItems(
        //         request,
        //         OnGrantShards,
        //         HandleErrorReport
        //     );
        // }

        /// <summary>
        /// On Grant Shards
        /// </summary>
        /// <param name="response"></param>
        void OnGrantShards(AddInventoryItemsResponse response)
        {
            if (response == null)
            {
                Debug.LogWarningFormat($"{nameof(CatalogManager)}.{nameof(OnGrantShards)}: received a null response.");
                return;
            }
            Debug.Log("CatalogManager - On grant Shards Success.");
            Debug.LogFormat("CatalogManager - transaction ids: {0}", string.Join(",", response.TransactionIds));
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
        }
        

        /// <summary>
        /// Load All Inventory Items
        /// Get a list of inventory item ids and request each item's detail via Get Items Request 
        /// </summary>
        public void LoadPlayerInventory()
        {
            Debug.Log("CatalogManager.LoadPlayerInventory");
            var request = new GetInventoryItemsRequest();
            
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
            if (response == null || response.Items?.Count == 0)
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
                var virtualItem = ConvertInventoryItemToVirtualItem(item);
                AddToInventory(virtualItem);
            }

            Inventory.SaveToDisk();
        }

        void ClearLocalInventoryOnLoading()
        {
            if (Inventory == null) return;
            
            Inventory.games.Clear();
            Inventory.captainUpgrades.Clear();
            Inventory.crystals.Clear();
            Inventory.shipClasses.Clear();
            Inventory.captains.Clear();
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
                default:
                    Debug.LogWarningFormat("{0} - {1} - Item Content Type not related to player inventory items, such as Stores and Subscriptions.", nameof(CatalogManager), nameof(AddToInventory));
                    break;
            }
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
            foreach (var key in response.Item.Title.Keys)
            {
                Debug.Log("   CatalogManager - Title Key: " + key);
                Debug.Log("   CatalogManager - Title: " + response.Item.Title[key]);
            }
            Debug.LogFormat("   CatalogManager - Type: {0} Image Count: {1} Content Type: {2} ", response.Item.Type, response.Item.Images.Count.ToString(), response.Item.ContentType);
        }

        /// <summary>
        /// Add Items to Inventory
        /// Add shinny new stuff! Any type of item from currency to captain and ship upgrades
        /// </summary>
        //public void AddInventoryItem([NotNull] InventoryItemReference itemReference, int amount)
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
        }

        /// <summary>
        /// Remove All Inventory Items
        /// </summary>
        /// <param name="collectionId">Collection Id</param>
        public void DeleteInventoryCollection(string collectionId)
        {
            var request = new DeleteInventoryCollectionRequest();
            request.CollectionId = collectionId;
            
            _playFabEconomyInstanceAPI.DeleteInventoryCollection(
                request, 
                (response) =>
                {
                    if (response == null)
                    {
                        Debug.LogWarningFormat("{0} - {1} No responses when removing all inventory collections.", nameof(CatalogManager), nameof(DeleteInventoryCollection));
                        return;
                    }
                    Debug.LogFormat("{0} - {1} All inventory collections removed.", nameof(CatalogManager), nameof(DeleteInventoryCollection));
                },
                PlayFabUtility.HandleErrorReport
                );
        }

        /// <summary>
        /// Get Inventory Collection Ids
        /// </summary>
        public void GetInventoryCollectionIds()
        {
            //Get Inventory Collection Ids. Up to 50 Ids can be returned at once.
            //You can use continuation tokens to paginate through results that return greater than the limit.
            //It can take a few seconds for new collection Ids to show up.

            var request = new GetInventoryCollectionIdsRequest();
            _playFabEconomyInstanceAPI.GetInventoryCollectionIds(
                request,
                OnGettingInventoryCollectionIds,
                PlayFabUtility.HandleErrorReport
                );
        }

        private void OnGettingInventoryCollectionIds(GetInventoryCollectionIdsResponse response)
        {
            if (response == null)
            {
                Debug.LogWarningFormat("{0} - {1} No responses.", nameof(CatalogManager), nameof(GetInventoryCollectionIds));
                return;
            }

            if (response.CollectionIds == null)
            {
                Debug.LogWarningFormat("{0} - {1} No inventory collection ids returned.", nameof(CatalogManager), nameof(GetInventoryCollectionIds));
                return;
            }

        }
        
        #endregion

        #region In-game Purchases

        /// <summary>
        /// Purchase Item
        /// Buy in-game item with virtual currency (Shards, Crystals)
        /// </summary>
        public void PurchaseItem(VirtualItem item, ItemPrice price)
        {
            // The currency calculation for currency should be done before passing item and price to purchase inventory item API, otherwise it will get "Invalid Request" error.
            _playFabEconomyInstanceAPI.PurchaseInventoryItems(
                new()
                {
                    Amount = item.Amount,
                    Item = new() 
                    { 
                        Id = item.ItemId
                    },
                    PriceAmounts = new List<PurchasePriceAmount>
                    {
                        new PurchasePriceAmount() 
                        { 
                            ItemId = price.ItemId, 
                            Amount = price.Amount 
                        }
                    },
                },
                response =>
                {
                    Debug.Log($"CatalogManager - Purchase success.");
                },
                PlayFabUtility.HandleErrorReport
            );
        }

        

        /// <summary>
        /// Claim Daily Reward
        /// </summary>
        public void ClaimDailyReward()
        {
            if (StoreShelve.dailyRewards is null || StoreShelve.dailyRewards.Count == 0) return;
            
            
        }

        /// <summary>
        /// Get Daily Reward Store defined in PlayFab Economy
        /// TODO: Might need to put on the chopping block since daily reward contents are moved to bundles.
        /// </summary>
        private void GetDailyRewardStore()
        {
            var store = new StoreReference { Id = DailyRewardStoreID };
            var request = new SearchItemsRequest{ Store = store };
            _playFabEconomyInstanceAPI.SearchItems(request, OnGettingDailyRewardStore, PlayFabUtility.HandleErrorReport);
        }

        /// <summary>
        /// On Successfully Getting Daily Reward Store
        /// TODO: Might need to put on the chopping block since daily reward contents are moved to bundles.
        /// </summary>
        /// <param name="result"></param>
        private void OnGettingDailyRewardStore(SearchItemsResponse result)
        {
            if (result == null)
            {
                Debug.Log("Catalog manager - OnGettingDailyRewardStore() - no result.");
                return;
            }

            foreach (var storeItem in result.Items)
            {
                StoreShelve.dailyRewards.Add(storeItem.Id, ConvertCatalogItemToVirtualItem(storeItem));
                StoreShelve.allItems.TryAdd(storeItem.Id, ConvertCatalogItemToVirtualItem(storeItem));  // Daily Reward may have already been added to all Items when loading the catalog
            }

            foreach (var dailyReward in StoreShelve.dailyRewards.Values)
            {
                Debug.Log($"Catalog manager - OnGettingDailyRewardStore() - the stored daily rewards is: " +
                          $"id: {dailyReward.ItemId} " +
                          $"title: {dailyReward.Name}");
            }
        }

        #endregion

        #region Model Conversion
        /// <summary>
        /// Convert PlayFab Price to Cosmic Shore Custom Price model
        /// TODO: Should be put on model or a conversion services instead of Catalog Manager
        /// </summary>
        /// <param name="price"></param>
        /// <returns></returns>
        ItemPrice PlayFabToCosmicShorePrice(CatalogPriceOptions price)
        {
            ItemPrice itemPrice = new();
            itemPrice.ItemId = price.Prices[0].Amounts[0].ItemId;
            itemPrice.Amount = price.Prices[0].Amounts[0].Amount;
            return itemPrice;
        }
        
        /// <summary>
        /// Convert PlayFab Catalog Item To Cosmic Shore Virtual Item model
        /// TODO: Should be put on model or a conversion services instead of Catalog Manager
        /// </summary>
        /// <param name="catalogItem"></param>
        /// <returns></returns>
        VirtualItem ConvertCatalogItemToVirtualItem(CatalogItem catalogItem)
        {
            VirtualItem virtualItem = new();
            virtualItem.ItemId = catalogItem.Id;
            Debug.Log($"catalogItem.Title[\"NEUTRAL\"]: {catalogItem.Title["NEUTRAL"]}");
            virtualItem.Name = catalogItem.Title["NEUTRAL"];
            virtualItem.Description = catalogItem.Description.TryGetValue("NEUTRAL", out var description)? description : "No Description";
            virtualItem.ContentType = catalogItem.ContentType;
            //virtualItem.BundleContents = catalogItem.Contents;
            //virtualItem.priceModel = PlayfabToCosmicShorePrice(catalogItem.PriceOptions);
            virtualItem.Tags = catalogItem.Tags;
            virtualItem.Type = catalogItem.Type;
            //virtualItem.Amount = catalogItem.PriceOptions.Prices[0].Amounts[0].
            return virtualItem;
        }

        /// <summary>
        /// Load Cosmic Shore Virtual Item details for the corresponding PlayFab Inventory Item by looking it up on the store shelf
        /// We load it from the store shelf since PF's inventory API doesn't return all of the expected fields (e.g the item's name)
        /// TODO: Should be put on model or a conversion services instead of Catalog Manager
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        VirtualItem ConvertInventoryItemToVirtualItem(InventoryItem item)
        {
            return StoreShelve.allItems[item.Id];
        }
        #endregion

        #region Bundle Handling

        /// <summary>
        /// Get Bundles
        /// Returns SeearchItemsResponse that contains bundle id if request is successful, title and other information.
        /// </summary>
        /// <param name="filter">A filter string to query PlayFab bundle information</param>
        public void GetBundles(string filter = "type eq 'bundle'")
        {
            _playFabEconomyInstanceAPI ??=
                new (AuthenticationManager.PlayFabAccount.AuthContext);
            var request = new SearchItemsRequest
            {
                Filter = filter
            };
            _playFabEconomyInstanceAPI.SearchItems(request, OnGettingBundlesSuccess, PlayFabUtility.HandleErrorReport);
        }

        /// <summary>
        /// On Getting Bundle Success Delegate
        /// Add bundle titles as keys and bundle ids as values to memory
        /// Invoke an action "testBundleId" for testing purpose
        /// </summary>
        /// <param name="response">Search Item Response</param>
        private void OnGettingBundlesSuccess(SearchItemsResponse response)
        {
            if (response is null) {Debug.Log("CatalogManager.GetBundle() - no response");return;}

            var items = string.Join(" bundle: ", response.Items.Select(i => i.Id.ToString() + " " + i.Title.Values.FirstOrDefault()));
            Debug.Log($"CatalogManager.GetBundle() - bundle: {items}");

            Bundles ??= new();
            
            foreach (var bundle in response.Items)
            {
                Bundles.TryAdd(bundle.Title.Values.FirstOrDefault() ?? "Nameless Bundle", bundle.Id);
            }

            string testBundleId;
            Bundles.TryGetValue("Test Bundle", out testBundleId);
            
            // TODO: This one is for testing, can be changed to any bundle id you want later
            if (string.IsNullOrEmpty(testBundleId)) {Debug.Log($"CatalogManager.GetBundle() - Test Bundle Id is not here");return;}
            Debug.Log($"CatalogManager.GetBundles() - Test Bundle Id: {testBundleId}");
            OnGettingBundleId?.Invoke(testBundleId);
        }

        // public void BuyBundle()
        // {
        //     _playFabEconomyInstanceAPI ??=
        //         new(AuthenticationManager.PlayFabAccount.AuthContext);
        //
        //     // ItemPurchaseRequest;
        //     // PurchaseItemRequest;
        //     // AddInventoryItemsRequest;
        //     // PurchaseInventoryItemsRequest;
        //     // UpdateInventoryItemsRequest;
        // }
        /// <summary>
        /// Purchase a bundle
        /// </summary>
        /// <param name="bundleId"></param>
        /// <param name="quantity"></param>
        public void PurchaseBundle(string bundleId, uint quantity)
        {
            const string annotation = "Bundle Purchase";
            
            quantity = VerifyQuantity(quantity);
            
            _playFabEconomyInstanceAPI ??=
                new(AuthenticationManager.PlayFabAccount.AuthContext);
            
            var itemRequest = new ItemPurchaseRequest
            {
                ItemId = bundleId,
                Quantity = quantity,
                Annotation = annotation
            };

            var startPurchaseRequest = new StartPurchaseRequest { Items = new(){itemRequest} };
            
            PlayFabClientAPI.StartPurchase(startPurchaseRequest, OnPurchaseBundleSuccess, PlayFabUtility.HandleErrorReport);
        }

        /// <summary>
        /// On Purchasing Bundle Success
        /// </summary>
        /// <param name="result"></param>
        private void OnPurchaseBundleSuccess(StartPurchaseResult result)
        {
            if (result is null) return;
            
            Debug.Log($"CatalogManager.PurchaseBundle() - {result.OrderId} remaining balance: {result.VirtualCurrencyBalances}");
            PayBundle(result.OrderId);
            
        }

        /// <summary>
        /// A helper function to verify item quantity, if it exceeds 25, clamp to 25
        /// </summary>
        /// <param name="quantity"></param>
        /// <returns></returns>
        private static uint VerifyQuantity(uint quantity)
        {
            return quantity > 25 ? 25 : quantity;
        }

        /// <summary>
        /// Pay For a Bundle
        /// TODO: The bundle Id is not legit for purchase as an item, needs further investigation on how to handle bundles in PlayFab
        /// </summary>
        /// <param name="orderId"></param>
        private void PayBundle(string orderId)
        {
            var payPurchaseRequest = new PayForPurchaseRequest { OrderId = orderId };
            PlayFabClientAPI.PayForPurchase(payPurchaseRequest, OnPayBundleSuccess, PlayFabUtility.HandleErrorReport);
        }

        /// <summary>
        /// On Paying Bundle Success Delegate
        /// </summary>
        /// <param name="result"></param>
        private void OnPayBundleSuccess(PayForPurchaseResult result)
        {
            if (result is null) return;
            
            Debug.Log($"CatalogManager.PayBundle() - {result.OrderId} purchase currency:{result.PurchaseCurrency} status:{result.Status}");
            var balance = string.Join(" ", result.VirtualCurrency.Select(i => i.Key + " " + i.Value));
            Debug.Log($"CatalogManager.BayBundle() - current virtual currency balance: {balance}");
        }
        
        #endregion
    }
}