using System;
using PlayFab;
using PlayFab.EconomyModels;
using StarWriter.Utility.Singleton;
using System.Collections.Generic;
using UnityEngine;
using _Scripts._Core.Playfab_Models;
using JetBrains.Annotations;
using CosmicShore;

public class CatalogManager : SingletonPersistent<CatalogManager>
{
    private static PlayerInventory _playerInventory; 
    
    static PlayFabEconomyInstanceAPI _playFabEconomyInstanceAPI;

    // Bootstrap the whole thing
    public void Start()
    {
        _playerInventory ??= new PlayerInventory();
        AuthenticationManager.OnLoginSuccess += InitializePlayFabEconomyAPI;
        AuthenticationManager.OnLoginSuccess += LoadCatalog;
        AuthenticationManager.OnLoginSuccess += LoadInventory;
        AuthenticationManager.OnRegisterSuccess += GrantStartingInventory;
    }

    public void OnDestroy()
    {
        AuthenticationManager.OnLoginSuccess -= InitializePlayFabEconomyAPI;
        AuthenticationManager.OnLoginSuccess -= LoadCatalog;
        AuthenticationManager.OnLoginSuccess -= LoadInventory;
        AuthenticationManager.OnRegisterSuccess -= GrantStartingInventory;
    }

    #region Initialize PlayFab Economy API with Auth Context

    /// <summary>
    /// Initialize PlayFab Economy API
    /// Instantiate PlayFab Economy API with auth context
    /// </summary>
    void InitializePlayFabEconomyAPI()
    {
        if (AuthenticationManager.PlayerAccount.AuthContext == null)
        {
            Debug.LogWarning($"Current Player has not logged in yet.");
            return;
        }
        // Null check for PlayFab Economy API instance
        _playFabEconomyInstanceAPI??= new PlayFabEconomyInstanceAPI(AuthenticationManager.PlayerAccount.AuthContext);
    }

    #endregion

    #region Catalog Operations
    
    /// <summary>
    /// Load Catalog Items
    /// Get all catalog items
    /// </summary>
    void LoadCatalog()
    {
        _playFabEconomyInstanceAPI.SearchItems(
            new()
            {
                // AuthenticationContext = AuthenticationManager.PlayerAccount.AuthContext,
                Filter = "ContentType eq 'Vessel' and tags/any(t: t eq 'Rhino')"
            },
            response =>
            {
                _playerInventory.CatalogItems = response.Items;
                Debug.Log(_playerInventory.CatalogItems);
                foreach (var item in _playerInventory.CatalogItems)
                {
                    Debug.Log("   CatalogManager - Inventory Id: " + item.Id);
                    Debug.Log("   CatalogManager - Inventory Title: " + item.Title);
                    Debug.Log("   CatalogManager - Inventory Content Type: " + item.ContentType);
                    foreach (var description in item.Description.Values)
                        Debug.Log("   CatalogManager - Description: " + description);                    
                }
            },
            HandleErrorReport
        );
    }
    
    
    #endregion

    #region Inventory Operations
    
    // TODO: Add title data key of starting catalog item ids

    /// <summary>
    /// Grant Starting Inventory Item Quantity
    /// Nothing magical here, default item quantity is 100, Granted when player created their account.
    /// </summary>
    void GrantStartingInventory()
    {
        const int amount = 100;
        foreach (var item in _playerInventory.CatalogItems)
        {
            _playFabEconomyInstanceAPI.AddInventoryItems(
                new AddInventoryItemsRequest()
                {
                    // AuthenticationContext = AccountManager.AuthenticationContext,
                    Amount = amount,
                    Item = new InventoryItemReference
                    {
                        Id = item.Id
                    }
                },
                response =>
                {
                    Debug.Log("CatalogManager - On Add Inventory Item Success");
                    foreach (var transactionId in response.TransactionIds)
                    {
                        // Transaction ID is the ascending order of the players transaction
                        Debug.Log($"CatalogManager - transaction id: {transactionId}");
                    }
                },
                HandleErrorReport
            );
        }
    }

    /// <summary>
    /// Load All Inventory Items
    /// Get a list of inventory item ids and request each item's detail via Get Items Request 
    /// </summary>
    public void LoadInventory()
    {
        _playFabEconomyInstanceAPI.GetInventoryItems(
            new GetInventoryItemsRequest
            {
            },
            response =>
            {
                Debug.Log("CatalogManager - Get Inventory Items success.");

                _playerInventory.InventoryItems = response.Items;

                foreach (var item in _playerInventory.InventoryItems)
                {
                    var name = nameof(CatalogManager);
                    Debug.Log($"{name} - GetInventoryItems - id: {item.Id} type: {item.Type} amount: {item.Amount.ToString()}");
                }
            },
            HandleErrorReport
        );
    }

    /// <summary>
    /// Get Inventory Item
    /// Request inventory item by item id
    /// </summary>
    //public void GetInventoryItem([NotNull] InventoryItemReference itemReference)
    public void GetInventoryItem([NotNull] VirtualItem virtualItem)
    {
        _playFabEconomyInstanceAPI.GetItem(
            new GetItemRequest()
            {
                Id = virtualItem.Id
            },
            (GetItemResponse response) =>
            {
                Debug.Log("   CatalogManager - Id: " + response.Item.Id);
                foreach (var key in response.Item.Title.Keys)
                {
                    Debug.Log("   CatalogManager - Title Key: " + key);
                    Debug.Log("   CatalogManager - Title: " + response.Item.Title[key]);
                }
                Debug.Log("   CatalogManager - Type: " + response.Item.Type);
                Debug.Log("   CatalogManager - Image Count: " + response.Item.Images.Count);
                Debug.Log("   CatalogManager - Content Type: " + response.Item.ContentType);
            },
            HandleErrorReport
        );
    }

    /// <summary>
    /// Add Items to Inventory
    /// Add shinny new stuff! Any type of item from currency to vessel and ship upgrades
    /// </summary>
    //public void AddInventoryItem([NotNull] InventoryItemReference itemReference, int amount)
    public void AddInventoryItem([NotNull] VirtualItem virtualItem, int amount)
    {
        _playFabEconomyInstanceAPI.AddInventoryItems(
                new AddInventoryItemsRequest()
                {
                    Item = new InventoryItemReference() { Id = virtualItem.Id },
                    Amount = amount
                }, (result) =>
                {
                    var name = nameof(CatalogManager);
                    Debug.Log($"{name} - add inventory item success.");
                    Debug.Log($"{name} - add inventory item id: {virtualItem.Id} amount: {amount.ToString()}");
                    // Etag can be used for multiple sources or users to modify the same item simultaneously without conflict
                    // Debug.Log($"{name} - add inventory item etag: {result.ETag}");
                    // Debug.Log($"{name} - add inventory item idempotency id: {result.IdempotencyId}");
                    LoadInventory();
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
    public void PurchaseItem(string itemId, string currencyId, int itemAmount, int currencyAmount)
    {
        
        _playFabEconomyInstanceAPI.PurchaseInventoryItems(
            new()
            {
                Amount = itemAmount,
                Item = new() 
                { 
                    Id = itemId
                },
                PriceAmounts = new List<PurchasePriceAmount>
                {
                    new PurchasePriceAmount() 
                    { 
                        ItemId = currencyId, 
                        Amount = currencyAmount 
                    }
                },
            },
            response =>
            {
                Debug.Log($"CatalogManager - Successfully purchased {itemId}");
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