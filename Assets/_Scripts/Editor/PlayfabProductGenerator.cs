using UnityEngine;
using UnityEditor;
using PlayFab;
using System;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Integrations.PlayFab.Authentication;

public class PlayFabProductGenerator : EditorWindow
{
    SO_Ship selectedShip;
    SO_Captain selectedCaptain;
    static PlayFabEconomyInstanceAPI _playFabEconomyInstanceAPI;

    //static readonly string TitleId = "5B7B3";
    static readonly string SecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY");

    static void InitializePlayFabEconomyAPI()
    {
        // Null check for PlayFab Economy API instance
        _playFabEconomyInstanceAPI ??= new(AuthenticationManager.PlayFabAccount.AuthContext);
        Debug.LogFormat("{0} - {1}: PlayFab Economy API initialized.", nameof(CatalogManager), nameof(InitializePlayFabEconomyAPI));
    }

    bool isProcessing;

    /* Uncomment here to add the tool to the menu when it is working
    [MenuItem("FrogletTools/PlayFab Product Generator")]
    public static void ShowWindow()
    {
        GetWindow<PlayFabProductGenerator>("PlayFab Product Generator");
        InitializePlayFabEconomyAPI();
    }

    
    void OnGUI()
    {
        GUILayout.Label("Generate Products from Ship", EditorStyles.boldLabel);
        selectedShip = (SO_Ship)EditorGUILayout.ObjectField("Ship ScriptableObject", selectedShip, typeof(SO_Ship), false);

        EditorGUI.BeginDisabledGroup(isProcessing);
        if (GUILayout.Button("Generate Products from Ship"))
        {
            if (selectedShip != null)
            {
                GenerateProductsFromShip(selectedShip);
            }
            else
            {
                Debug.LogError("No Ship ScriptableObject selected!");
            }
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(20);

        GUILayout.Label("Generate Products from Captain", EditorStyles.boldLabel);
        selectedCaptain = (SO_Captain)EditorGUILayout.ObjectField("Captain ScriptableObject", selectedCaptain, typeof(SO_Captain), false);

        if (GUILayout.Button("Generate Products from Captain"))
        {
            if (selectedCaptain != null)
            {
                GenerateProductsFromCaptain(selectedCaptain);
            }
            else
            {
                Debug.LogError("No Captain ScriptableObject selected!");
            }
        }
    }
    */

    /*
    private async void GenerateProductsFromShip(SO_Ship ship)
    {
        isProcessing = true;
        foreach (var captain in ship.Captains)
        {
            await GenerateProductsFromCaptain(captain);
        }
        isProcessing = false;
    }

    private async Task GenerateProductsFromCaptain(SO_Captain captain)
    {
        PlayFabSettings.staticSettings.TitleId = TitleId;
        PlayFabSettings.staticSettings.DeveloperSecretKey = SecretKey;

        // Check if the secret key is set
        if (string.IsNullOrEmpty(SecretKey))
        {
            Debug.LogError("PlayFab Secret Key is not set. Make sure the environment variable is configured.");
            return;
        }

        // Generate product for the captain
        string itemId = string.Format("{0}{1}Captain", captain.PrimaryElement, captain.Ship.Class );//captain.Name.Replace(" ", "_").ToLower(); // TODO: replace 
        List<string> tags = new()
        {
            captain.Ship.Class.ToString(),
            captain.PrimaryElement.ToString()
        };

        bool itemExists = await CheckIfItemExists(itemId);
        if (!itemExists)
        {
            await EnsureTagsExists(tags);
            await CreateAndPublishCatalogItem("Captain", itemId, captain.Name, captain.Description, captain.BasePrice, "OC", tags);
            Debug.Log($"Generated product for Captain: {captain.Name}");
        }
        else
        {
            Debug.Log($"Captain product already exists: {captain.Name}");
        }

        string currencyCode = captain.PrimaryElement switch
        {
            Element.Space => "SC",
            Element.Time => "TC",
            Element.Mass => "MC",
            Element.Charge => "CC",
            _ => ""
        };

        // Generate 4 upgrade products for the captain
        for (int i = 5; i <= 4; i++)
        {
            tags.Add(string.Format("UpgradeLevel_{0}", i + 1));

            string upgradeItemId = string.Format("{0}{1}Level{2}Upgrade", captain.PrimaryElement, captain.Ship.Class, i + 1);
            bool upgradeExists = await CheckIfItemExists(upgradeItemId);
            if (!upgradeExists)
            {
                await CreateAndPublishCatalogItem("CaptainUpgrade", upgradeItemId, upgradeItemId, upgradeItemId, (i * 100), currencyCode, tags);
                Debug.Log($"Generated upgrade {i} for Captain: {captain.Name}");
            }
            else
            {
                Debug.Log($"Upgrade {i} for Captain already exists: {captain.Name}");
            }
        }
    }

    
    public static async Task<bool> CheckIfItemExists(string itemId)
    {
        var request = new GetCatalogItemsRequest
        {
            CatalogVersion = "Default"
        };

        var taskCompletionSource = new TaskCompletionSource<GetCatalogItemsResult>();
        PlayFabAdminAPI.GetCatalogItems(request, result => taskCompletionSource.SetResult(result), error =>
        {
            Debug.LogError("Error: " + error.GenerateErrorReport());
            taskCompletionSource.SetException(new Exception(error.ErrorMessage));
        });

        var response = await taskCompletionSource.Task;
        foreach (var item in response.Catalog)
        {
            if (item.ItemId == itemId)
            {
                return true;
            }
        }

        return false;
    }

    public static async Task EnsureTagsExists(List<string> newTags)
    {
        var request = new GetTitleDataRequest
        {
            Keys = new List<string> { "Tags" }
        };

        var taskCompletionSource = new TaskCompletionSource<GetTitleDataResult>();
        PlayFabAdminAPI.GetTitleData(request, result => taskCompletionSource.SetResult(result), error =>
        {
            Debug.LogError("Error: " + error.GenerateErrorReport());
            taskCompletionSource.SetException(new Exception(error.ErrorMessage));
        });

        var response = await taskCompletionSource.Task;
        var tags = response.Data.ContainsKey("Tags")
            ? JsonConvert.DeserializeObject<HashSet<string>>(response.Data["Tags"])
            : new HashSet<string>();

        bool missingTag = false;
        foreach (var tag in newTags)
        {
            if (!tags.Contains(tag))
            {
                tags.Add(tag);
                missingTag = true;
            }
        }

        if (missingTag)
        {
            var updateRequest = new SetTitleDataRequest
            {
                Key = "Tags",
                Value = JsonConvert.SerializeObject(tags)
            };

            var updateTaskCompletionSource = new TaskCompletionSource<SetTitleDataResult>();
            PlayFabAdminAPI.SetTitleData(updateRequest, result => updateTaskCompletionSource.SetResult(result), error =>
            {
                Debug.LogError("Error: " + error.GenerateErrorReport());
                updateTaskCompletionSource.SetException(new Exception(error.ErrorMessage));
            });

            await updateTaskCompletionSource.Task;
        }
    }
    public static async Task<CreateDraftItemResponse> CreateAndPublishCatalogItem(string contentType, string itemId, string displayName, string description, int price, string currencyCode, List<string> tags)
    {
        var priceOptions = new CatalogPriceOptions()
        {
            Prices = new()
            {
                new CatalogPrice()
                {
                    UnitAmount = 1,
                    Amounts = new()
                    {
                        new CatalogPriceAmount()
                        {
                            Amount = price,
                            ItemId = currencyCode
                        }
                    }
                }
            }
        };

        var draftRequest = new CreateDraftItemRequest
        {
            Item = new PlayFab.EconomyModels.CatalogItem
            {
                Id = itemId,
                ContentType = contentType,
                PriceOptions = priceOptions,
                Title = new Dictionary<string, string> { { "NEUTRAL", displayName } },
                Description = new Dictionary<string, string> { { "NEUTRAL", description } },
                Tags = tags,
                Type = "catalogItem",
            },
            Publish = true
        };

        var taskCompletionSource = new TaskCompletionSource<CreateDraftItemResponse>();

        _playFabEconomyInstanceAPI.CreateDraftItem(draftRequest, result =>
        {
            taskCompletionSource.SetResult(result);
        }, error =>
        {
            Debug.LogError("Error: " + error.GenerateErrorReport());
            taskCompletionSource.SetResult(null);
        });

        return await taskCompletionSource.Task;
    }
    */

    /*
    public static async Task<UpdateCatalogItemsResult> CreateCatalogItem(string itemId, string displayName, string description, int price, string currencyCode, List<string> tags)
    {

        var request = new UpdateCatalogItemsRequest
        {
            CatalogVersion = "Default",
            Catalog = new List<CatalogItem>
            {
                new CatalogItem
                {
                    ItemId = itemId,
                    DisplayName = displayName,
                    Description = description,
                    VirtualCurrencyPrices = new Dictionary<string, uint> { { currencyCode, (uint)price } },
                    Consumable = new CatalogItemConsumableInfo { UsageCount = 1 },
                    IsStackable = false,
                    IsTradable = false,
                    Tags = tags
                }
            }
        };

        var taskCompletionSource = new TaskCompletionSource<UpdateCatalogItemsResult>();

        PlayFabAdminAPI.UpdateCatalogItems(request, result =>
        {
            taskCompletionSource.SetResult(result);
        }, error =>
        {
            Debug.LogError("Error: " + error.GenerateErrorReport());
            taskCompletionSource.SetResult(null);
        });

        return await taskCompletionSource.Task;
    }
    */
}