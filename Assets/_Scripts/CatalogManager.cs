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

    // Bootstrap the whole thing
    public void Start()
    {
        AuthenticationManager.LoginSuccess += LoadCatalog;
    }

    void LoadCatalog(object sender, LoginResult result)
    {
        PlayFabEconomyAPI.SearchItems(
            new()
            {
                AuthenticationContext = AuthenticationManager.PlayerAccount.AuthContext,
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

    // TODO: Add title data key of starting catalog item ids

    void GrantStartingInventory()
    {
        for (int i = 0; i < CatalogItems.Count; i++)
        {
            PlayFabEconomyAPI.AddInventoryItems(
                new AddInventoryItemsRequest()
                {
                    // AuthenticationContext = AccountManager.AuthenticationContext,
                    Amount = 100,
                    Item = new InventoryItemReference
                    {
                        Id = CatalogItems[i].Id
                    },
                    Entity = new PlayFab.EconomyModels.EntityKey()
                    {
                        Id = AuthenticationManager.PlayerAccount.AuthContext.EntityId,
                        Type = AuthenticationManager.PlayerAccount.AuthContext.EntityType
                    }
                },
                response =>
                {
                    Debug.Log("CatalogManager - OnAddInventoryItemSuccess");
                    foreach (var transactionId in response.TransactionIds)
                    {
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

    void LoadInventory()
    {
        PlayFabEconomyAPI.GetInventoryItems(
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

                PlayFabEconomyAPI.GetItems(
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

    public void GetInventoryItem(string inventoryItemId)
    {
        PlayFabEconomyAPI.GetItem(
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

    public void BuyTomahawk()
    {
        string tomahawkId = "1fc47256-60e1-4976-b66e-71feb8e56372";
        string shardId = "017bbd32-c4f7-486b-ab86-f899fda1f4ca";
        PlayFabEconomyAPI.PurchaseInventoryItems(
            new()
            {
                // AuthenticationContext = AccountManager.AuthenticationContext,

                Amount = 1,
                Item = new() 
                { 
                    Id = tomahawkId
                },
                Entity = new()
                {
                    // Id = AccountManager.AuthenticationContext.PlayFabId,
                    // Type = AccountManager.AuthenticationContext.EntityType
                },
                PriceAmounts = new List<PurchasePriceAmount>
                {
                    new PurchasePriceAmount() 
                    { 
                        ItemId = shardId, 
                        Amount = 10 
                    }
                },
            },
            response =>
            {
                Debug.Log("CatalogManager - Successfully purchased Tomahawk");
            },
            error =>
            {
                Debug.LogError(error.GenerateErrorReport());
            }
        );
    }
}