using System.Collections.Generic;
using _Scripts._Core.Playfab_Models.Authentication;
using JetBrains.Annotations;
using PlayFab;
using PlayFab.EconomyModels;
using StarWriter.Utility.Singleton;
using UnityEngine;

namespace _Scripts._Core.Playfab_Models.Economy
{
    public class CatalogManager : SingletonPersistent<CatalogManager>
    {
        private static InventoryModel _playerInventory; 
    
        static PlayFabEconomyInstanceAPI _playFabEconomyInstanceAPI;

        private static string _shardId;

        // Bootstrap the whole thing
        public void Start()
        {
            _playerInventory ??= new InventoryModel();
            AuthenticationManager.OnLoginSuccess += InitializePlayFabEconomyAPI;
            // AuthenticationManager.OnLoginSuccess += LoadCatalogItems;
            AuthenticationManager.OnLoginSuccess += LoadPlayerInventory;
            // AuthenticationManager.OnRegisterSuccess += GrantStartingInventory;
        }

        public void OnDestroy()
        {
            AuthenticationManager.OnLoginSuccess -= InitializePlayFabEconomyAPI;
            // AuthenticationManager.OnLoginSuccess -= LoadCatalogItems;
            AuthenticationManager.OnLoginSuccess -= LoadPlayerInventory;
            // AuthenticationManager.OnRegisterSuccess -= GrantStartingInventory;
        }

        #region Initialize PlayFab Economy API with Auth Context

        /// <summary>
        /// Initialize PlayFab Economy API
        /// Instantiate PlayFab Economy API with auth context
        /// </summary>
        static void InitializePlayFabEconomyAPI()
        {
            if (AuthenticationManager.PlayerAccount.AuthContext == null)
            {
                Debug.LogWarning($"Current Player has not logged in yet.");
                return;
            }
            // Null check for PlayFab Economy API instance
            _playFabEconomyInstanceAPI??= new PlayFabEconomyInstanceAPI(AuthenticationManager.PlayerAccount.AuthContext);
            Debug.LogFormat("{0} - {1}: PlayFab Economy API initialized.", nameof(CatalogManager), nameof(OnLoadingCatalogItems));
        }

        #endregion

        #region Catalog Operations
    
        /// <summary>
        /// Load Catalog Items
        /// Get all catalog items
        /// </summary>
        public void LoadCatalogItems(in string filter = "")
        {
            _playFabEconomyInstanceAPI.SearchItems(
                new()
                {
                    Filter = filter//
                },
                OnLoadingCatalogItems,
                HandleErrorReport
            );
        }

        /// <summary>
        /// On Loading Catalog Items Callback
        /// Load catalog items to local memory
        /// </summary>
        /// <param name="response"></param>
        private void OnLoadingCatalogItems(SearchItemsResponse response)
        {
            // _playerInventory.CatalogItems = response.Items;

            if (response == null)
            {
                Debug.LogWarningFormat("{0} - {1}: Unable to get catalog item.", nameof(CatalogManager), nameof(OnLoadingCatalogItems));
                return;
            }

            if (response.Items.Count == 0)
            {
                Debug.LogWarningFormat("{0} - {1}: No catalog items are available.", nameof(CatalogManager), nameof(OnLoadingCatalogItems));
                return;
            }
            
            Debug.LogFormat("{0} - {1}: Catalog items Loaded.", nameof(CatalogManager), nameof(OnLoadingCatalogItems));
            foreach (var item in response.Items)
            {
                Debug.LogFormat("   CatalogManager - Inventory Id: {0} title: {1} content type: {2}", item.Id, item.Title, item.ContentType);
                Debug.LogFormat("   CatalogManager - tags: {0}", string.Join(",", item.Tags));
            }
        }
    
    
        #endregion

        #region Inventory Operations

        /// <summary>
        /// Grant Starting Inventory
        /// </summary>
        /// <param name="startingItems">Starting Items List</param>
        public void GrantStartingInventory(in List<VirtualItemModel> startingItems)
        {
            // const int amount = 100;
            foreach (var virtualItem in startingItems)
            {
                _playFabEconomyInstanceAPI.AddInventoryItems(
                    new AddInventoryItemsRequest()
                    {
                        // AuthenticationContext = AccountManager.AuthenticationContext,
                        Amount = virtualItem.Amount,
                        Item = new InventoryItemReference
                        {
                            Id = virtualItem.Id
                        }
                    },
                    OnGrantStartingInventory,
                    HandleErrorReport
                );
            }
        }

        private void OnGrantStartingInventory(AddInventoryItemsResponse response)
        {
            if (response == null)
            {
                Debug.LogWarningFormat("{0} - {1}: Unable to get catalog item or no inventory items are available.", nameof(CatalogManager), nameof(OnGrantStartingInventory));
                return;
            }
            Debug.Log("CatalogManager - On Add Inventory Item Success.");
            foreach (var transactionId in response.TransactionIds)
            {
                // Transaction ID is the ascending order of the players transaction
                Debug.Log($"CatalogManager - transaction id: {transactionId}");
            }
        }
        

        /// <summary>
        /// Load All Inventory Items
        /// Get a list of inventory item ids and request each item's detail via Get Items Request 
        /// </summary>
        public void LoadPlayerInventory()
        {
            _playFabEconomyInstanceAPI.GetInventoryItems(
                new GetInventoryItemsRequest
                {
                },
                OnLoadingPlayerInventory,
                HandleErrorReport
            );
        }

        private void OnLoadingPlayerInventory(GetInventoryItemsResponse response)
        {
            if (response == null || response.Items?.Count == 0)
            {
                Debug.LogWarningFormat("{0} - {1}: Unable to get catalog item or no inventory items are available.", nameof(CatalogManager), nameof(OnLoadingCatalogItems));
                return;
            }
            
            Debug.Log("CatalogManager - Get Inventory Items success.");

            foreach (var item in response.Items)
            {
                var name = nameof(CatalogManager);
                Debug.LogFormat("{0} - {1}: id: {2} amount: {3} loaded.", nameof(CatalogManager), nameof(OnLoadingCatalogItems), item.Id, item.Amount.ToString());
            }
        }

        /// <summary>
        /// Get Inventory Item
        /// Request inventory item by item id
        /// </summary>
        //public void GetInventoryItem([NotNull] InventoryItemReference itemReference)
        public void GetInventoryItem([NotNull] in VirtualItemModel virtualItemModel)
        {
            _playFabEconomyInstanceAPI.GetItem(
                new GetItemRequest()
                {
                    Id = virtualItemModel.Id
                },
                OnLoadingPlayerInventory,
                HandleErrorReport
            );
        }

        private void OnLoadingPlayerInventory(GetItemResponse response)
        {
            if (response == null)
            {
                Debug.LogWarningFormat("{0} - {1}: no response on adding inventory item.", nameof(CatalogManager), nameof(GetInventoryItem));
                return;
            }

            if (response.Item == null)
            {
                Debug.LogWarningFormat("{0} - {1}: no inventory item.", nameof(CatalogManager), nameof(GetInventoryItem));
                return;
            }
                    
            Debug.Log("   CatalogManager - Id: " + response.Item.Id);
            foreach (var key in response.Item.Title.Keys)
            {
                Debug.Log("   CatalogManager - Title Key: " + key);
                Debug.Log("   CatalogManager - Title: " + response.Item.Title[key]);
            }
            Debug.Log("   CatalogManager - Type: " + response.Item.Type);
            Debug.Log("   CatalogManager - Image Count: " + response.Item.Images.Count);
            Debug.Log("   CatalogManager - Content Type: " + response.Item.ContentType);
        }

        /// <summary>
        /// Add Items to Inventory
        /// Add shinny new stuff! Any type of item from currency to vessel and ship upgrades
        /// </summary>
        //public void AddInventoryItem([NotNull] InventoryItemReference itemReference, int amount)
        public void AddInventoryItem([NotNull] VirtualItemModel virtualItemModel)
        {
            _playFabEconomyInstanceAPI.AddInventoryItems(
                new AddInventoryItemsRequest()
                {
                    Item = new InventoryItemReference() { Id = virtualItemModel.Id },
                    Amount = virtualItemModel.Amount
                }, (result) =>
                {
                    if(result == null)
                    {
                        Debug.LogWarningFormat("{0} - {1}: no result.", nameof(CatalogManager), nameof(AddInventoryItem));
                        return;
                    }

                    Debug.LogFormat("{0} - {1}: item added to player inventory.", nameof(CatalogManager), nameof(AddInventoryItem));
                    // Etag can be used for multiple sources or users to modify the same item simultaneously without conflict
                    // Debug.Log($"{name} - add inventory item etag: {result.ETag}");
                    // Debug.Log($"{name} - add inventory item idempotency id: {result.IdempotencyId}");
                    LoadPlayerInventory();
                }, HandleErrorReport
            );
        }
    
    
        // public void 
    
        #endregion

        #region In-game Purchases

        /// <summary>
        /// Purchase Item
        /// Buy in-game item with virtual currency (Shards, Crystals)
        /// </summary>
        public void PurchaseItem([NotNull] VirtualItemModel item, [NotNull] VirtualItemModel currency)
        {
        
            _playFabEconomyInstanceAPI.PurchaseInventoryItems(
                new()
                {
                    Amount = item.Amount,
                    Item = new() 
                    { 
                        Id = item.Id
                    },
                    PriceAmounts = new List<PurchasePriceAmount>
                    {
                        new PurchasePriceAmount() 
                        { 
                            ItemId = currency.Id, 
                            Amount = currency.Amount 
                        }
                    },
                },
                response =>
                {
                    Debug.Log($"CatalogManager - Purchase success.");
                },
                HandleErrorReport
            );
        }
    
        #endregion
    
        #region Situation Handling

        /// <summary>
        /// Handle Error Report
        /// </summary>
        /// <param name="error">PlayFab Error</param>
        private void HandleErrorReport(PlayFabError error)
        {
            Debug.LogErrorFormat("{0} - {1}", nameof(CatalogManager), error.ErrorMessage);
        }
        #endregion
    }
}