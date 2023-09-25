using PlayFab;
using PlayFab.EconomyModels;
using StarWriter.Utility.Singleton;
using System.Collections.Generic;
using UnityEngine;
using _Scripts._Core.Playfab_Models;

public class CatalogManager : SingletonPersistent<CatalogManager>
{
    // Items list, configurable via inspector
    // static List<PlayFab.EconomyModels.CatalogItem> CatalogItems;
    // static List<PlayFab.EconomyModels..CatalogItem> InventoryItems;
    private static PlayerInventory _playerInventory; 
    
    PlayFabEconomyInstanceAPI playFabEconomyInstanceAPI;

    // Bootstrap the whole thing
    public void Start()
    {
        _playerInventory ??= new PlayerInventory();
        AuthenticationManager.OnLoginSuccess += InitializePlayFabEconomyAPI;
        AuthenticationManager.OnLoginSuccess += LoadCatalog;
        AuthenticationManager.OnLoginSuccess += LoadInventory;
        AuthenticationManager.OnRegisterSuccess += GrantStartingInventory;

    }

    #region Initialize PlayFab Economy API with Auth Context

    /// <summary>
    /// Load Catalog Items
    /// Instantiate PlayFab Economy API with auth context
    /// Querying catalog and inventory item don't need to fetch auth context from Authentication manager everytime.
    /// </summary>
    void InitializePlayFabEconomyAPI()
    {
        playFabEconomyInstanceAPI = new PlayFabEconomyInstanceAPI(AuthenticationManager.PlayerAccount.AuthContext);
    }

    #endregion

    #region Catalog Operations
    
    /// <summary>
    /// Load Catalog Items
    /// Get all catalog items
    /// </summary>
    void LoadCatalog()
    {
        playFabEconomyInstanceAPI.SearchItems(
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
            error => 
            { 
                Debug.Log(error.ErrorDetails); 
            }
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
            playFabEconomyInstanceAPI.AddInventoryItems(
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
                error =>
                {
                    Debug.LogError(error.GenerateErrorReport());
                }
            );
        }
    }

    /// <summary>
    /// Load All Inventory Items
    /// Get a list of inventory item ids and request each item's detail via Get Items Request 
    /// </summary>
    public void LoadInventory()
    {
        playFabEconomyInstanceAPI.GetInventoryItems(
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
            error =>
            {
                Debug.LogError(error.GenerateErrorReport());
            }
        );
    }

    /// <summary>
    /// Get Inventory Item
    /// Request inventory item by item id
    /// </summary>
    public void GetInventoryItem(InventoryItemReference itemReference)
    {
        playFabEconomyInstanceAPI.GetItem(
            new GetItemRequest()
            {
                Id = itemReference.Id
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
            (PlayFabError error) =>
            {
                Debug.LogError(error.GenerateErrorReport());
            }
        );
    }

    /// <summary>
    /// Add Items to Inventory
    /// Add shinny new stuff! Any type of item from currency to vessel and ship upgrades
    /// </summary>
    public void AddInventoryItem(InventoryItemReference itemReference, int amount)
    {
            playFabEconomyInstanceAPI.AddInventoryItems(
                new AddInventoryItemsRequest()
                {
                    Item = itemReference,
                    Amount = amount
                }, (result) =>
                {
                    var name = nameof(CatalogManager);
                    Debug.Log($"{name} - add inventory item success.");
                    Debug.Log($"{name} - add inventory item id: {itemReference.Id} amount: {amount.ToString()}");
                    // Etag can be used for multiple sources or users to modify the same item simultaneously without conflict
                    // Debug.Log($"{name} - add inventory item etag: {result.ETag}");
                    // Debug.Log($"{name} - add inventory item idempotency id: {result.IdempotencyId}");
                    LoadInventory();
                }, (error) =>
                {
                    Debug.Log(error.GenerateErrorReport());
                }
            );
    }
    
    
    // public void 
    
    #endregion

    #region In-game Purchases

    /// <summary>
    /// Purchase Item
    /// Buy in-game item with virtual currency (Shards)
    /// </summary>
    public void PurchaseItem(string itemId, string currencyId, int itemAmount, int currencyAmount)
    {
        
        playFabEconomyInstanceAPI.PurchaseInventoryItems(
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
            error =>
            {
                Debug.LogError(error.GenerateErrorReport());
            }
        );
    }
    
    #endregion
}