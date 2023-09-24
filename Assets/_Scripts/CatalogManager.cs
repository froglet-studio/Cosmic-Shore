using PlayFab;
using PlayFab.EconomyModels;
using StarWriter.Utility.Singleton;
using System.Collections.Generic;
using UnityEngine;
using PlayFab.ClientModels;
using _Scripts._Core.Playfab_Models;

public class CatalogManager : SingletonPersistent<CatalogManager>
{
    // Items list, configurable via inspector
    static List<PlayFab.EconomyModels.CatalogItem> CatalogItems;
    static List<PlayFab.EconomyModels.CatalogItem> InventoryItems;
    PlayFabEconomyInstanceAPI playFabEconomyInstanceAPI;

    // Bootstrap the whole thing
    public void Start()
    {
        AuthenticationManager.LoginSuccess += InitializePlayFabEconomyAPI;
        AuthenticationManager.LoginSuccess += LoadCatalog;
    }

    #region Initialize PlayFab Economy API with Auth Context

    /// <summary>
    /// Load Catalog Items
    /// Instantiate PlayFab Economy API with auth context
    /// Querying catalog and inventory item don't need to fetch auth context from Authentication manager everytime.
    /// </summary>
    void InitializePlayFabEconomyAPI(object sender, LoginResult result)
    {
        playFabEconomyInstanceAPI = new PlayFabEconomyInstanceAPI(AuthenticationManager.PlayerAccount.AuthContext);
    }

    #endregion

    #region Catalog Operations
    
    /// <summary>
    /// Load Catalog Items
    /// Get all catalog items
    /// </summary>
    void LoadCatalog(object sender, LoginResult result)
    {
        playFabEconomyInstanceAPI.SearchItems(
            new()
            {
                // AuthenticationContext = AuthenticationManager.PlayerAccount.AuthContext,
                Filter = "ContentType eq 'Vessel' and tags/any(t: t eq 'Rhino')"
            },
            response =>
            {
                CatalogItems = response.Items;
                Debug.Log(CatalogItems);
                foreach (var item in CatalogItems)
                {
                    Debug.Log("   CatalogManager - Id: " + item.Id);
                    Debug.Log("   CatalogManager - Title: " + item.Title);
                    Debug.Log("   CatalogManager - Content Type: " + item.ContentType);
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
    /// Nothing magical here, default item quantity is 100
    /// </summary>
    void GrantStartingInventory(int amount = 100)
    {
        for (int i = 0; i < CatalogItems.Count; i++)
        {
            playFabEconomyInstanceAPI.AddInventoryItems(
                new AddInventoryItemsRequest()
                {
                    // AuthenticationContext = AccountManager.AuthenticationContext,
                    Amount = amount,
                    Item = new InventoryItemReference
                    {
                        Id = CatalogItems[i].Id
                    }
                },
                response =>
                {
                    Debug.Log("CatalogManager - OnAddInventoryItemSuccess");
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
    void LoadInventory()
    {
        playFabEconomyInstanceAPI.GetInventoryItems(
            new GetInventoryItemsRequest
            {
                // AuthenticationContext = AccountManager.AuthenticationContext,
            },
            response =>
            {
                Debug.Log("CatalogManager - GetInventoryItemsResponse: " + response.Items);

                List<string> itemIds = new();
                foreach (var item in response.Items)
                {
                    itemIds.Add(item.Id);
                }

                playFabEconomyInstanceAPI.GetItems(
                    new GetItemsRequest()
                    {
                        Ids = itemIds
                    },
                    response =>
                    {
                        InventoryItems = response.Items;

                        foreach (var item in response.Items)
                        {
                            Debug.Log("   CatalogManager - Id: " + item.Id);
                            foreach (var key in item.Title.Keys)
                            {
                                Debug.Log("   CatalogManager - Title Key: " + key);
                                Debug.Log("   CatalogManager - Title: " + item.Title[key]);
                            }
                            Debug.Log("   CatalogManager - Type: " + item.Type);
                            Debug.Log("   CatalogManager - Image Count: " + item.Images.Count);
                            Debug.Log("   CatalogManager - Content Type: " + item.ContentType);
                        }
                    },
                    error =>
                    {
                        Debug.LogError(error.GenerateErrorReport());
                    });
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
    public void GetInventoryItem(string inventoryItemId)
    {
        playFabEconomyInstanceAPI.GetItem(
            new GetItemRequest()
            {
                Id = inventoryItemId
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
    public void AddInventoryItem(int amount)
    {
        foreach (var item in InventoryItems)
        {
            playFabEconomyInstanceAPI.AddInventoryItems(
                new AddInventoryItemsRequest()
                {
                    Amount = amount,
                    Item = new InventoryItemReference
                    {
                        Id = item.Id,
                        StackId = item.DefaultStackId
                    }
                }, (result) =>
                {
                    var name = nameof(CatalogManager);
                    Debug.Log($"{name} - add inventory item success.");
                    Debug.Log($"{name} - add inventory item id: {item.Id}");
                    // Etag can be used for multiple sources or users to modify the same item simultaneously without conflict
                    Debug.Log($"{name} - add inventory item etag: {result.ETag}");
                    Debug.Log($"{name} - add inventory item idempotency id: {result.IdempotencyId}");
                }, (error) =>
                {
                    Debug.Log(error.GenerateErrorReport());
                }
            );
        }
        
    }
    

    /// <summary>
    /// Purchase Item
    /// Buy in-game item with virtual currency (Shards)
    /// </summary>
    public void PurchaseItem(string itemId, string currencyId, int itemAmount, int currencyAmount)
    {
        
        playFabEconomyInstanceAPI.PurchaseInventoryItems(
            new()
            {
                // AuthenticationContext = AccountManager.AuthenticationContext,

                Amount = itemAmount,
                Item = new() 
                { 
                    Id = itemId
                },
                // Entity = new()
                // {
                //     // Id = AccountManager.AuthenticationContext.PlayFabId,
                //     // Type = AccountManager.AuthenticationContext.EntityType
                // },
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