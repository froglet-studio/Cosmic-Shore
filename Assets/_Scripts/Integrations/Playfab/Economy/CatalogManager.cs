using System.Collections.Generic;
using CosmicShore.Integrations.Playfab.Authentication;
using CosmicShore.Integrations.Playfab.Utility;
using CosmicShore.Utility.Singleton;
using PlayFab;
using PlayFab.EconomyModels;
using UnityEngine;
using CatalogItem = PlayFab.EconomyModels.CatalogItem;

namespace CosmicShore.Integrations.Playfab.Economy
{
    public class CatalogManager : SingletonPersistent<CatalogManager>
    {
        // PlayFab Economy API instance
        static PlayFabEconomyInstanceAPI _playFabEconomyInstanceAPI;

        // Player inventory and items
        public static StoreShelve StoreShelve { get; private set; } = new();

        public static Inventory Inventory { get; private set; } = new();
        // private static string _shardId;

        private int _dailyRewardIndex;

        private const string DailyRewardStoreID = "63d59c05-2b86-4843-8e9b-61c07ab121ad";
        private const string ClaimDailyRewardTime = "ClaimDailyRewardTime";
        
        
        private void Start()
        {
            AuthenticationManager.OnLoginSuccess += InitializePlayFabEconomyAPI;
            AuthenticationManager.OnLoginSuccess += LoadAllCatalogItems;
            AuthenticationManager.OnLoginSuccess += LoadPlayerInventory;

            AuthenticationManager.OnLoginSuccess += GetDailyRewardStore;
        }


        public void OnDestroy()
        {
            AuthenticationManager.OnLoginSuccess -= InitializePlayFabEconomyAPI;
            AuthenticationManager.OnLoginSuccess -= LoadAllCatalogItems;
            AuthenticationManager.OnLoginSuccess -= LoadPlayerInventory;
            
            AuthenticationManager.OnLoginSuccess -= GetDailyRewardStore;
            StoreShelve = null;
            Inventory = null;
            // AuthenticationManager.OnRegisterSuccess -= GrantStartingInventory;
        }

        #region Initialize PlayFab Economy API with Auth Context

        /// <summary>
        /// Initialize PlayFab Economy API
        /// Instantiate PlayFab Economy API with auth context
        /// </summary>
        void InitializePlayFabEconomyAPI()
        {
            if (AuthenticationManager.PlayFabAccount.AuthContext == null)
            {
                Debug.LogWarning($"Current Player has not logged in yet.");
                return;
            }
            // Null check for PlayFab Economy API instance
            _playFabEconomyInstanceAPI??= new PlayFabEconomyInstanceAPI(AuthenticationManager.PlayFabAccount.AuthContext);
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
        private void OnLoadingCatalogItems(SearchItemsResponse response)
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
                ships = new(),
                miniGames = new()
            };

            foreach (var item in response.Items)
            {
                Debug.LogFormat("   CatalogManager - Inventory Id: {0} title: {1} content type: {2}", item.Id, item.Title, item.ContentType);
                Debug.LogFormat("   CatalogManager - tags: {0}", string.Join(",", item.Tags));
                Debug.LogFormat("   CatalogManager - Type: {0}", item.Type);
                Debug.LogFormat("   CatalogManager - ContentType: {0}", item.ContentType);
                var converted = ConvertToStoreItem(item);
                AddToStoreShelve(item.ContentType, converted);
            }
        }

        private void AddToStoreShelve(string contentType, VirtualItem item)
        {
            switch (contentType)
            {
                case "Crystal":
                    StoreShelve.crystals.Add(item);
                    break;
                case "Ship":
                    StoreShelve.ships.Add(item);
                    break;
                case "MiniGame":
                    StoreShelve.miniGames.Add(item);
                    break;
                default:
                    Debug.LogWarningFormat("CatalogManager - AddToStoreSelves: item content type is not part of the store.");
                    break;
            }
        }

        

        #endregion

        #region Inventory Operations

        // TODO: Guide knowledge is now part of player data, not a store item. Should re-wire the API calls from PlayerDataController.
        // public void GrantGuideKnowledge(int amount, ShipTypes shipClass, Element element)
        // {
        //     string shardItemId = "";
        //     Debug.Log($"Guide Knowledge Length: {Catalog.GuideKnowledge.Count}");
        //     foreach (var guideKnowledge in Catalog.GuideKnowledge)
        //     {
        //         Debug.Log($"Next Guide: {guideKnowledge.Name}");
        //         foreach (var tag in guideKnowledge.Tags)
        //             Debug.Log($"Guide Knowledge Tags: {tag}");
        //
        //         if (guideKnowledge.Tags.Contains(shipClass.ToString()) && guideKnowledge.Tags.Contains(element.ToString()))
        //         {
        //             Debug.Log($"Found matching Guidew Shard");
        //             shardItemId = guideKnowledge.ItemId;
        //             break;
        //         }
        //     }
        //
        //     if (string.IsNullOrEmpty(shardItemId))
        //     {
        //         Debug.LogError($"{nameof(CatalogManager)}.{nameof(GrantGuideKnowledge)} - Error Granting Shards. No matching guide shard found in catalog - shipClass:{shipClass}, element:{element}");
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
        private void OnGrantShards(AddInventoryItemsResponse response)
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
        private void OnGrantStartingInventory(AddInventoryItemsResponse response)
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
        private void OnGettingInventoryItems(GetInventoryItemsResponse response)
        {
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
                var virtualItem = ConvertToInventoryItem(item);
                AddToInventory(virtualItem);
            }
        }

        private void ClearLocalInventoryOnLoading()
        {
            if (Inventory == null) return;
            
            Inventory.miniGames.Clear();
            Inventory.guideUpgrades.Clear();
            Inventory.crystals.Clear();
            Inventory.ships.Clear();
            Inventory.guides.Clear();
        }
        
        private void AddToInventory(VirtualItem item)
        {
            switch (item.ContentType)
            {
                case "Guide":
                    Debug.LogFormat("{0} - {1} - Adding Guide",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.guides.Add(item);
                    break;
                case "ShipClass":
                    Debug.LogFormat("{0} - {1} - Adding Ship",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.ships.Add(item);
                    break;
                case "GuideUpgrade":
                    Debug.LogFormat("{0} - {1} - Adding Upgrade",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.guideUpgrades.Add(item);
                    break;
                case "MiniGame":
                    Debug.LogFormat("{0} - {1} - Adding MiniGame",nameof(CatalogManager), nameof(AddToInventory));
                    Inventory.miniGames.Add(item);
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
        /// Add shinny new stuff! Any type of item from currency to guide and ship upgrades
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
        /// </summary>
        private void GetDailyRewardStore()
        {
            var store = new StoreReference { Id = DailyRewardStoreID };
            var request = new SearchItemsRequest{ Store = store };
            _playFabEconomyInstanceAPI.SearchItems(request, OnGettingDailyRewardStore, PlayFabUtility.HandleErrorReport);
        }

        private void OnGettingDailyRewardStore(SearchItemsResponse result)
        {
            if (result == null)
            {
                Debug.Log("Catalog manager - OnGettingDailyRewardStore() - no result.");
                return;
            }

            foreach (var storeItem in result.Items)
            {
                StoreShelve.dailyRewards.Add(ConvertToStoreItem(storeItem));
            }

            foreach (var dailyReward in StoreShelve.dailyRewards)
            {
                Debug.Log($"Catalog manager - OnGettingDailyRewardStore() - the stored daily rewards is: " +
                          $"id: {dailyReward.ItemId} " +
                          $"title: {dailyReward.Name}");
            }
        }

        #endregion

        #region Model Conversion
        ItemPrice PlayFabToCosmicShorePrice(CatalogPriceOptions price)
        {
            ItemPrice itemPrice = new();
            itemPrice.ItemId = price.Prices[0].Amounts[0].ItemId;
            itemPrice.Amount = price.Prices[0].Amounts[0].Amount;
            return itemPrice;
        }
        
        VirtualItem ConvertToStoreItem(CatalogItem catalogItem)
        {
            VirtualItem virtualItem = new();
            virtualItem.ItemId = catalogItem.Id;
            Debug.Log($"catalogItem.Title[\"NEUTRAL\"]: {catalogItem.Title["NEUTRAL"]}");
            virtualItem.Name = catalogItem.Title["NEUTRAL"];
            virtualItem.Description = catalogItem.Description.TryGetValue("NEUTRAL",out var description)? description : "No Description";
            virtualItem.ContentType = catalogItem.ContentType;
            //virtualItem.BundleContents = catalogItem.Contents;
            //virtualItem.priceModel = PlayfabToCosmicShorePrice(catalogItem.PriceOptions);
            virtualItem.Tags = catalogItem.Tags;
            virtualItem.Type = catalogItem.Type;
            //virtualItem.Amount = catalogItem.PriceOptions.Prices[0].Amounts[0].
            return virtualItem;
        }

        VirtualItem ConvertToInventoryItem(InventoryItem item)
        {
            var virtualItem = new VirtualItem();
            virtualItem.ItemId = item.Id;
            virtualItem.Type = item.Type;
            virtualItem.Amount = item.Amount;

            return virtualItem;
        }
        #endregion

        // #region Situation Handling
        //
        // /// <summary>
        // /// Handle Error Report
        // /// </summary>
        // /// <param name="error">PlayFab Error</param>
        // private void HandleErrorReport(PlayFabError error)
        // {
        //     OnGettingPlayFabErrors?.Invoke(error);
        //     // Keep the error message here if there will be unit tests.
        //     Debug.LogErrorFormat("{0} - error code: {1} message: {2}", nameof(CatalogManager), error.Error, error.ErrorMessage);
        // }
        // #endregion
    }
}