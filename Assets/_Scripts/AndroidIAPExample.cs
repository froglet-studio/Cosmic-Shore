using PlayFab;
using PlayFab.ClientModels;
using PlayFab.EconomyModels;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

public class AndroidIAPExample : MonoBehaviour, IDetailedStoreListener
{
    // Items list, configurable via inspector
    private List<PlayFab.ClientModels.CatalogItem> Catalog;

    // The Unity Purchasing system
    private static IStoreController m_StoreController;

    static string PlayerId;
    static PlayFabAuthenticationContext m_AuthenticationContext;
    static string EntityType;
    static List<PlayFab.EconomyModels.CatalogItem> catalogItems;

    // Bootstrap the whole thing
    public void Start()
    {
        // Make PlayFab log in
        Login();
        
    }
    public void GrantShards()
    {
        InventoryItemReference inventoryItemReference = new InventoryItemReference();

        PlayFab.EconomyModels.SearchItemsRequest searchItemsRequest = new();
        searchItemsRequest.AuthenticationContext = m_AuthenticationContext;
        PlayFabEconomyAPI.SearchItems(
            searchItemsRequest,
            (SearchItemsResponse response) =>
            {

                catalogItems = response.Items;
                Debug.Log(catalogItems);
                foreach (var item in catalogItems)
                {
                    Debug.Log("   Title: " + item.Title);
                    Debug.Log("   Content Type: " + item.ContentType);
                    foreach (var description in item.Description.Values)
                    {
                        Debug.Log("   Description: " + description);
                    }
                    Debug.Log("   DefaultStackId: " + item.DefaultStackId);
                    Debug.Log("   Id: " + item.Id);
                }

                inventoryItemReference.StackId = catalogItems[0].DefaultStackId;
                inventoryItemReference.Id = catalogItems[0].Id;

                PlayFab.EconomyModels.EntityKey entityKey = new PlayFab.EconomyModels.EntityKey();
                entityKey.Id = PlayerId;
                entityKey.Type = EntityType;

                PlayFab.EconomyModels.AddInventoryItemsRequest request = new AddInventoryItemsRequest();
                //request.
                request.AuthenticationContext = m_AuthenticationContext;
                request.Amount = 100;
                request.Item = inventoryItemReference;
                request.Entity = entityKey;

                PlayFab.PlayFabEconomyAPI.AddInventoryItems(request, OnAddInventoryItemSuccess, OnAddInventoryItemError);
            },
            (PlayFabError error) => { Debug.Log(error.ErrorDetails); });
    }

    void OnAddInventoryItemError(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());
    }

    void OnAddInventoryItemSuccess(AddInventoryItemsResponse response)
    {
        Debug.Log("OnAddInventoryItemSuccess");
        foreach (var transactionId in response.TransactionIds)
        {
            Debug.Log($"transaction id: {transactionId}");
        }

        PlayFab.EconomyModels.GetInventoryItemsRequest getItemsRequest = new GetInventoryItemsRequest();
        getItemsRequest.AuthenticationContext = m_AuthenticationContext;
        PlayFab.PlayFabEconomyAPI.GetInventoryItems(
            getItemsRequest,
            (GetInventoryItemsResponse response) =>
            {
                Debug.Log("GetInventoryItemsResponse: " + response.Items);

                foreach (var item in response.Items)
                {
                    PlayFab.PlayFabEconomyAPI.GetItem(
                        new GetItemRequest()
                        {
                            Id = item.Id 
                        },
                        (GetItemResponse response) => 
                        {
                            Debug.Log("   Id: " + response.Item.Id);
                            foreach (var key in response.Item.Title.Keys)
                            {
                                Debug.Log("   Title Key: " + key);
                                Debug.Log("   Title: " + response.Item.Title[key]);
                            }
                            Debug.Log("   Type: " + response.Item.Type);
                            Debug.Log("   Image Count: " + response.Item.Images.Count);
                            Debug.Log("   Content Type: " + item.Amount);
                        },
                        (PlayFabError error) => 
                        {
                            Debug.LogError(error.GenerateErrorReport());
                        }
                    );
                }
            },
            (PlayFabError error) => { Debug.Log(error.ErrorDetails); });
    }

    public void BuyTomahawk()
    {
        PlayFab.EconomyModels.EntityKey entityKey = new PlayFab.EconomyModels.EntityKey();
        entityKey.Id = PlayerId;
        entityKey.Type = EntityType;

        PurchaseInventoryItemsRequest purchaseInventoryItemsRequest = new PurchaseInventoryItemsRequest()
        {
            Amount = 1,
            Item = new InventoryItemReference() { Id = "1fc47256-60e1-4976-b66e-71feb8e56372" },
            Entity = entityKey,
            AuthenticationContext = m_AuthenticationContext,
            PriceAmounts = new List<PurchasePriceAmount> { new PurchasePriceAmount() { ItemId= "017bbd32-c4f7-486b-ab86-f899fda1f4ca", Amount= 10 } },
        };
        PlayFab.PlayFabEconomyAPI.PurchaseInventoryItems(
            purchaseInventoryItemsRequest,
            (PurchaseInventoryItemsResponse response) =>
            {
                Debug.Log("Successfully purchased Tomahawk");
            },
            (PlayFabError error) =>
            {
                Debug.LogError(error.GenerateErrorReport());
            }
        );
    }


    public void OnGUI()
    {
        // This line just scales the UI up for high-res devices
        // Comment it out if you find the UI too large.
        GUI.matrix = Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity, new Vector3(3, 3, 3));

        // if we are not initialized, only draw a message
        if (!IsInitialized)
        {
            GUILayout.Label("Initializing IAP and logging in...");
            return;
        }

        // Draw menu to purchase items
        foreach (var item in Catalog)
        {
            if (GUILayout.Button("Buy " + item.DisplayName))
            {
                // On button click buy a product
                BuyProductID(item.ItemId);
            }
        }
    }

    // This is invoked manually on Start to initiate login ops
    void Login()
    {
#if (UNITY_ANDROID || UNITY_EDITOR)
        // Login with Android ID
        PlayFabClientAPI.LoginWithAndroidDeviceID(new LoginWithAndroidDeviceIDRequest()
        {
            CreateAccount = true,
            AndroidDeviceId = SystemInfo.deviceUniqueIdentifier
        }, result => {
            Debug.Log($"Logged in: {result.PlayFabId}");
            // Refresh available items
            RefreshIAPItems();
            PlayerId = result.EntityToken.Entity.Id; // result.PlayFabId;
            m_AuthenticationContext = result.AuthenticationContext;
            EntityType = result.EntityToken.Entity.Type;
            Debug.Log($"Entity Type: {EntityType}");
            Debug.Log($"PlayerId: {PlayerId}");
            GrantShards();
        }, error => Debug.LogError(error.GenerateErrorReport()));
#endif
#if UNITY_IOS
        PlayFabClientAPI.LoginWithIOSDeviceID(new LoginWithIOSDeviceIDRequest()
        {
            CreateAccount = true,
            DeviceId = SystemInfo.deviceUniqueIdentifier
            //iOSDeviceId = SystemInfo.deviceUniqueIdentifier
        }, result => {
            Debug.Log($"Logged in: {result.PlayFabId}");
            // Refresh available items
            RefreshIAPItems();
            PlayerId = result.EntityToken.Entity.Id; // result.PlayFabId;
            m_AuthenticationContext = result.AuthenticationContext;
            EntityType = result.EntityToken.Entity.Type;
            Debug.Log($"Entity Type: {EntityType}");
            Debug.Log($"PlayerId: {PlayerId}");
            GrantShards();
        }, error => Debug.LogError(error.GenerateErrorReport()));
#endif

    }

    private void RefreshIAPItems()
    {
        PlayFabClientAPI.GetCatalogItems(new GetCatalogItemsRequest(), result => {
            Catalog = result.Catalog;

            // Make UnityIAP initialize
            InitializePurchasing();
        }, error => Debug.LogError(error.GenerateErrorReport()));
    }

    // This is invoked manually on Start to initialize UnityIAP
    public void InitializePurchasing()
    {
        // If IAP is already initialized, return gently
        if (IsInitialized) return;

        // Create a builder for IAP service
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.GooglePlay));

        // Register each item from the catalog
        foreach (var item in Catalog)
        {
            builder.AddProduct(item.ItemId, ProductType.Consumable);
        }

        // Trigger IAP service initialization
        UnityPurchasing.Initialize(this, builder);
    }

    // We are initialized when StoreController and Extensions are set and we are logged in
    public bool IsInitialized
    {
        get
        {
            return m_StoreController != null && Catalog != null;
        }
    }

    // This is automatically invoked automatically when IAP service is initialized
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        m_StoreController = controller;
    }

    // This is automatically invoked automatically when IAP service failed to initialized
    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.Log("OnInitializeFailed InitializationFailureReason:" + error);
    }

    // This is automatically invoked automatically when purchase failed
    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.Log(string.Format("OnPurchaseFailed: FAIL. Product: '{0}', PurchaseFailureReason: {1}", product.definition.storeSpecificId, failureReason));
    }

    // This is invoked automatically when successful purchase is ready to be processed
    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
    {
        // NOTE: this code does not account for purchases that were pending and are
        // delivered on application start.
        // Production code should account for such case:
        // More: https://docs.unity3d.com/ScriptReference/Purchasing.PurchaseProcessingResult.Pending.html

        if (!IsInitialized)
        {
            return PurchaseProcessingResult.Complete;
        }

        // Test edge case where product is unknown
        if (e.purchasedProduct == null)
        {
            Debug.LogWarning("Attempted to process purchase with unknown product. Ignoring");
            return PurchaseProcessingResult.Complete;
        }

        // Test edge case where purchase has no receipt
        if (string.IsNullOrEmpty(e.purchasedProduct.receipt))
        {
            Debug.LogWarning("Attempted to process purchase with no receipt: ignoring");
            return PurchaseProcessingResult.Complete;
        }

        Debug.Log("Processing transaction: " + e.purchasedProduct.transactionID);

        // Deserialize receipt
        var googleReceipt = GooglePurchase.FromJson(e.purchasedProduct.receipt);

        // Invoke receipt validation
        // This will not only validate a receipt, but will also grant player corresponding items
        // only if receipt is valid.
        PlayFabClientAPI.ValidateGooglePlayPurchase(new ValidateGooglePlayPurchaseRequest()
        {
            // Pass in currency code in ISO format
            CurrencyCode = e.purchasedProduct.metadata.isoCurrencyCode,
            // Convert and set Purchase price
            PurchasePrice = (uint)(e.purchasedProduct.metadata.localizedPrice * 100),
            // Pass in the receipt
            ReceiptJson = googleReceipt.PayloadData.json,
            // Pass in the signature
            Signature = googleReceipt.PayloadData.signature
        }, result => Debug.Log("Validation successful!"),
           error => Debug.Log("Validation failed: " + error.GenerateErrorReport())
        );

        return PurchaseProcessingResult.Complete;
    }

    // This is invoked manually to initiate purchase
    void BuyProductID(string productId)
    {
        // If IAP service has not been initialized, fail hard
        if (!IsInitialized) throw new Exception("IAP Service is not initialized!");

        // Pass in the product id to initiate purchase
        m_StoreController.InitiatePurchase(productId);
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.Log("OnInitializeFailed: " + message);
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        Debug.Log("OnPurchaseFailed: " + failureDescription);
    }
}

// The following classes are used to deserialize JSON results provided by IAP Service
// Please, note that JSON fields are case-sensitive and should remain fields to support Unity Deserialization via JsonUtilities
public class JsonData
{
    // JSON Fields, ! Case-sensitive

    public string orderId;
    public string packageName;
    public string productId;
    public long purchaseTime;
    public int purchaseState;
    public string purchaseToken;
}

public class PayloadData
{
    public JsonData JsonData;

    // JSON Fields, ! Case-sensitive
    public string signature;
    public string json;

    public static PayloadData FromJson(string json)
    {
        var payload = JsonUtility.FromJson<PayloadData>(json);
        payload.JsonData = JsonUtility.FromJson<JsonData>(payload.json);
        return payload;
    }
}

public class GooglePurchase
{
    public PayloadData PayloadData;

    // JSON Fields, ! Case-sensitive
    public string Store;
    public string TransactionID;
    public string Payload;

    public static GooglePurchase FromJson(string json)
    {
        var purchase = JsonUtility.FromJson<GooglePurchase>(json);
        purchase.PayloadData = PayloadData.FromJson(purchase.Payload);
        return purchase;
    }
}