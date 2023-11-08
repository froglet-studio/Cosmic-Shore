using System.Collections.Generic;
using System.Runtime.CompilerServices;
using _Scripts._Core.Enums;
using _Scripts._Core.Playfab_Models.Economy;
using _Scripts._Core.Playfab_Models.Player_Models;
using JetBrains.Annotations;
using PlayFab;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Scenes.TestScenes.Playfab_Sandbox_Test
{
    public class CatalogView : MonoBehaviour
    {
        [Header("Catalog Buttons")]
        [SerializeField] private Button purchaseVesselButton;
        [SerializeField] private Button loadCatalogItemsButton;

        [Header("Inventory Buttons")] 
        [SerializeField] private Button grantStartingItemsButton;
        [SerializeField] private Button loadInventoryButton;
        [SerializeField] private Button loadVesselDataButton;

        private static VesselDataAccessor vesselDataAccessor;

        private static List<VesselData> vesselDataList;
        // test strings
        const string MantaShipUpgrade1Id = "6b5264af-4645-4aaa-8228-3b35ed379585";
        const string MantaShipUpgrade2Id = "806f1840-a0de-4463-8b56-4b43b07c3d5a";
        const string VesselShardId = "06bcebb1-dc41-49a8-82b0-96a15ced7c1c";
        private const string crystalId = "88be4041-cc48-4231-8595-d440b371d015";
        
    
        // Start is called before the first frame update
        void Start()
        {
            vesselDataAccessor ??= new VesselDataAccessor();
            vesselDataList ??= new List<VesselData>();
            
            purchaseVesselButton.onClick.AddListener(PurchaseUpgradeTest);
            grantStartingItemsButton.onClick.AddListener(GrantStartingInventoryTest);
            loadCatalogItemsButton.onClick.AddListener(GetCatalogItemsTest);
            loadInventoryButton.onClick.AddListener(LoadInventoryTest);
            loadVesselDataButton.onClick.AddListener(LoadVesselDataTest);
        }

        private void OnEnable()
        {
            CatalogManager.OnGettingPlayFabErrors += ProcessCatalogErrors;
        }

        private void OnDisable()
        {
            CatalogManager.OnGettingPlayFabErrors -= ProcessCatalogErrors;
        }

        /// <summary>
        /// Purchase Item Test
        /// TODO: Currently Manta Ship Upgrade 1 is not purchasable, figure out why.
        /// Buy a test vessel using shards, the amount of shards should be the exact price tag on the test vessel shards
        /// </summary>
        private void PurchaseUpgradeTest()
        {
            // The currency calculation for currency should be done before passing shard to purchase inventory API, otherwise it will get "Invalid Request" error.
            // testing upgrade 1 purchasing
            var vesselShard1 = new VirtualItemModel{Id = VesselShardId, Amount = 5};
            var mantaSpaceUpgrade1 = new VirtualItemModel { Id = MantaShipUpgrade1Id, Amount = 1 };
            // Parameter order note: item first, currency second
            CatalogManager.Instance.PurchaseItem(mantaSpaceUpgrade1, vesselShard1);

            SaveVesselData(Vessels.Space, MantaShipUpgrade1Id, 1 );
            Debug.LogFormat("{0} - {1}: vessel info {2} saved to local storage.", nameof(CatalogView), nameof(PurchaseUpgradeTest), nameof(mantaSpaceUpgrade1));
        
            // TODO: Should have ship upgrade level detection here, if level 1 upgrade is not purchased, you can't buy level 2 or higher.
        
            // testing upgrade 2 purchasing
            var vesselShard2 = new VirtualItemModel{Id = VesselShardId, Amount = 10};
            var mantaSpaceUpgrade2 = new VirtualItemModel { Id = MantaShipUpgrade2Id, Amount = 1 };
 
            CatalogManager.Instance.PurchaseItem(mantaSpaceUpgrade2, vesselShard2);
            SaveVesselData(Vessels.Space, MantaShipUpgrade2Id, 2 );
            Debug.LogFormat("{0} - {1}: vessel info {2} saved to local storage.", nameof(CatalogView), nameof(PurchaseUpgradeTest), nameof(mantaSpaceUpgrade2));
        }

        private void SaveVesselData(Vessels vessel, string vesselId, int upgradeLevel)
        {
            VesselData vesselData = new(vesselId, upgradeLevel);
            vesselDataList.Add(vesselData);
            vesselDataAccessor.Save(vessel, vesselDataList);
        }

        /// <summary>
        /// Grant Starting Inventory (Working)
        /// Experimental method - should be handled by 
        /// Nothing magical here, default item quantity is 100, Granted when player created their account.
        /// </summary>
        private void GrantStartingInventoryTest()
        {
            // For now it's 100 vessel shards
            var vesselShard = new VirtualItemModel
            {
                Id = VesselShardId,
                ContentType = nameof(VirtualItemContentTypes.VesselShard),
                Amount = 100
            };
            var startingItems = new List<VirtualItemModel> { vesselShard };
            CatalogManager.Instance.GrantStartingInventory(startingItems);
        }

        /// <summary>
        /// Get Catalog Items Test (Working)
        /// </summary>
        private void GetCatalogItemsTest()
        {
            // var filter = "ContentType eq 'Vessel' and tags/any(t: t eq 'Rhino')";
        
            // Default filter is "", which means load without filter
            CatalogManager.Instance.LoadCatalogItems();
        }
    
        /// <summary>
        /// Load Inventory Test (Working)
        /// Update to see the contents inside player inventory
        /// </summary>
        private void LoadInventoryTest()
        {
            CatalogManager.Instance.LoadPlayerInventory();
        }

        private void LoadVesselDataTest()
        {
            var vesselUpgradeLevels = vesselDataAccessor.Load();
            foreach (var level in vesselUpgradeLevels)
            {
                Debug.LogFormat("{0} - {1}: vessel: {2}  loaded.", nameof(CatalogView), nameof(PurchaseUpgradeTest), level.Key);
                foreach (var data in level.Value)
                {
                    Debug.LogFormat("{0} - {1}: vessel id: {2} upgrade level {3} loaded.", nameof(CatalogView), nameof(PurchaseUpgradeTest), data.vesselId, data.upgradeLevel);
                }
            }
        }

        /// <summary>
        /// Process Catalog Errors
        /// </summary>
        /// <param name="error">PlayFab Error</param>
        private void ProcessCatalogErrors(PlayFabError error)
        {
            switch (error.Error)
            {
                // Purchasing Inventory Items errors (some overlap with other APIs)
                case PlayFabErrorCode.DatabaseThroughputExceeded: 
                    Debug.LogError("Database is out of storage, it couldn't take more.");
                    break;
                case PlayFabErrorCode.InsufficientFunds:
                    Debug.LogError("Sorry, but you don't have enough currency to buy the stuff.");
                    break;
                case PlayFabErrorCode.ItemNotFound:
                    Debug.LogError("No such item in our store.");
                    break;
                
                // Search Items errors
                case PlayFabErrorCode.NotImplemented:
                    Debug.LogError("PlayFab folks said this is not implemented. Not sure what it means.");
                    break;
                
                // Get Inventory Items errors
                case PlayFabErrorCode.AccountDeleted:
                    Debug.LogError("Your account is deleted :(");
                    break;
                
                
                
                default:
                    Debug.LogError("Unknown Nightmare under store or inventory operations.");
                    break;
            
            }
        }
    }
}
