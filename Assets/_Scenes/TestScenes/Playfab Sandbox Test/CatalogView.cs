using System.Collections.Generic;
using CosmicShore.Integrations.Enums;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Integrations.PlayFab.PlayerModels;
using CosmicShore.Integrations.PlayFab.Utility;
using PlayFab;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Scenes.TestScenes.Playfab_Sandbox_Test
{
    public class CatalogView : MonoBehaviour
    {
        [Header("Catalog Buttons")]
        [SerializeField] private Button purchaseVesselButton;
        [SerializeField] private Button purchaseShardsButton;
        [SerializeField] private Button loadCatalogItemsButton;

        [Header("Inventory Buttons")] 
        [SerializeField] private Button grantStartingItemsButton;
        [SerializeField] private Button loadInventoryButton;
        [SerializeField] private Button loadVesselDataButton;
        [SerializeField] private Button removeInvCollectionButton;

        // Vessel Data related instances
        private static GuideDataAccessor guideDataAccessor;
        private static List<GuideData> guideDataList;
        
        // test strings
        const string MantaShipUpgrade1Id = "6b5264af-4645-4aaa-8228-3b35ed379585";
        const string MantaShipUpgrade2Id = "806f1840-a0de-4463-8b56-4b43b07c3d5a";
        const string ElementalCrystalId = "06bcebb1-dc41-49a8-82b0-96a15ced7c1c";
        private const string CrystalId = "51392e05-9072-43a9-ae2d-4a3335dbf313";

        [Inject] private CatalogManager _catalogManager;
    
        // Start is called before the first frame update
        void Start()
        {
            guideDataAccessor ??= new GuideDataAccessor();
            guideDataList ??= new List<GuideData>();
        }

        private void OnEnable()
        {
            purchaseVesselButton.onClick.AddListener(PurchaseUpgradeTest);
            grantStartingItemsButton.onClick.AddListener(GrantStartingInventoryTest);
            loadCatalogItemsButton.onClick.AddListener(GetCatalogItemsTest);
            loadInventoryButton.onClick.AddListener(LoadInventoryTest);
            loadVesselDataButton.onClick.AddListener(LoadGuideData);
            purchaseShardsButton.onClick.AddListener(PurchaseShardsWithCrystalTest);
            removeInvCollectionButton.onClick.AddListener(RemoveInventoryCollectionTest);
            
            PlayFabUtility.GettingPlayFabErrors += ProcessCatalogErrors;
            
        }

        private void OnDisable()
        {
            PlayFabUtility.GettingPlayFabErrors -= ProcessCatalogErrors;
            
            purchaseVesselButton.onClick.RemoveListener(PurchaseUpgradeTest);
            grantStartingItemsButton.onClick.RemoveListener(GrantStartingInventoryTest);
            loadCatalogItemsButton.onClick.RemoveListener(GetCatalogItemsTest);
            loadInventoryButton.onClick.RemoveListener(LoadInventoryTest);
            loadVesselDataButton.onClick.RemoveListener(LoadGuideData);
            purchaseShardsButton.onClick.RemoveListener(PurchaseShardsWithCrystalTest);
            removeInvCollectionButton.onClick.RemoveListener(RemoveInventoryCollectionTest);
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
            var elementalCrystal1 = new ItemPrice{ ItemId = ElementalCrystalId, Amount = 5};
            var mantaSpaceUpgrade1 = new VirtualItem { ItemId = MantaShipUpgrade1Id, ContentType = nameof(GuideLevel.Upgrade3), Amount = 1 };
            
            // Parameter order note: item first, currency second
            _catalogManager.PurchaseItem(mantaSpaceUpgrade1, elementalCrystal1);

            SaveGuideData(GuideLevel.Upgrade3, MantaShipUpgrade1Id, 1 );
            Debug.LogFormat("{0} - {1}: vessel info {2} saved to local storage.", nameof(CatalogView), nameof(PurchaseUpgradeTest), nameof(mantaSpaceUpgrade1)); 
            
            // TODO: Should have ship upgrade level detection here, if level 1 upgrade is not purchased, you can't buy level 2 or higher.
        
            // testing upgrade 2 purchasing
            var vesselShard2 = new ItemPrice{ItemId = ElementalCrystalId, Amount = 10};
            var mantaSpaceUpgrade2 = new VirtualItem { ItemId = MantaShipUpgrade2Id, Amount = 1 };
 
            _catalogManager.PurchaseItem(mantaSpaceUpgrade2, vesselShard2);
            SaveGuideData(GuideLevel.Upgrade3, MantaShipUpgrade2Id, 2 );
            Debug.LogFormat("{0} - {1}: vessel info {2} saved to local storage.", nameof(CatalogView), nameof(PurchaseUpgradeTest), nameof(mantaSpaceUpgrade2));
            LoadGuideData();
        }
        
        /// <summary>
        /// Purchase Shards With Crystal Test
        /// 1 Crystal for 10 Shards
        /// </summary>
        private void PurchaseShardsWithCrystalTest()
        {
            var shardPrice = new ItemPrice { ItemId = CrystalId, Amount = 1 };
            var vesselShards = new VirtualItem { ItemId = ElementalCrystalId, Amount = 10 };

            
            _catalogManager.PurchaseItem(vesselShards, shardPrice);
        }

        /// <summary>
        /// Grant Starting Inventory (Working)
        /// Experimental method - should be handled by 
        /// Nothing magical here, default item quantity is 100, Granted when player created their account.
        /// </summary>
        private void GrantStartingInventoryTest()
        {
            // For now it's 100 vessel shards
            var vesselShard = new VirtualItem
            {
                ItemId = ElementalCrystalId,
                ContentType = nameof(ContentTypes.GuideKnowledge),
                Amount = 100
            };
            var crystals = new VirtualItem
            {
                ItemId = CrystalId,
                ContentType = "Currency",
                Amount = 10
            };
            var startingItems = new List<VirtualItem> { vesselShard, crystals };
            _catalogManager.GrantStartingInventory(startingItems);
        }

        /// <summary>
        /// Get Catalog Items Test (Working)
        /// </summary>
        private void GetCatalogItemsTest()
        {
            // var filter = "ContentType eq 'Vessel' and tags/any(t: t eq 'Rhino')";
        
            // Default filter is "", which means load without filter
            _catalogManager.LoadCatalogItems();
        }
    
        /// <summary>
        /// Load Inventory Test (Working)
        /// Update to see the contents inside player inventory
        /// </summary>
        private void LoadInventoryTest()
        {
            _catalogManager.LoadPlayerInventory();
        }

        /// <summary>
        /// Save Guide Data (Working)
        /// </summary>
        /// <param name="guideLevel">Guide</param>
        /// <param name="guideId">Guide ID</param>
        /// <param name="upgradeLevel">Upgrade Level</param>
        private void SaveGuideData(GuideLevel guideLevel, string guideId, int upgradeLevel)
        {
            GuideData guideData = new(guideId, upgradeLevel);
            guideDataList.Add(guideData);
            guideDataAccessor.Save(guideLevel, guideDataList);
        }
        
        /// <summary>
        /// Load Guide Data (working)
        /// </summary>
        private void LoadGuideData()
        {
            var guideUpgradeLevels = guideDataAccessor.Load();
            foreach (var level in guideUpgradeLevels)
            {
                Debug.LogFormat("{0} - {1}: Guide: {2}  loaded.", nameof(CatalogView), nameof(PurchaseUpgradeTest), level.Key);
                foreach (var data in level.Value)
                {
                    Debug.LogFormat("{0} - {1}: Guide id: {2} upgrade level {3} loaded.", nameof(CatalogView), nameof(PurchaseUpgradeTest), data.guideId, data.upgradeLevel);
                }
            }
        }
        
        /// <summary>
        /// Remove Inventory Collection Test
        /// Normal player account does not have permission to delete inventory collections
        /// </summary>
        private void RemoveInventoryCollectionTest()
        {
            _catalogManager.GetInventoryCollectionIds();
        }

        /// <summary>
        /// Remove Inventory Collection
        /// </summary>
        /// <param name="collectionIds">Collection Id List</param>
        private void RemoveInventoryCollection(List<string> collectionIds)
        {
            foreach (var id in collectionIds)
            {
                Debug.LogFormat("{0} - {1} collection id: .", nameof(CatalogView), nameof(RemoveInventoryCollection));
                _catalogManager.DeleteInventoryCollection(id);
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
                    Debug.LogError("No such item in our store nor your inventory.");
                    break;
                
                // Search Items errors
                case PlayFabErrorCode.NotImplemented:
                    Debug.LogError("PlayFab folks said this is not implemented. Not sure what it means.");
                    break;
                
                // Get Inventory Items errors
                case PlayFabErrorCode.AccountDeleted:
                    Debug.LogError("Your account is deleted :(");
                    break;
                case PlayFabErrorCode.AccountBanned:
                    Debug.LogError("Your account is banned :(");
                    break;
                
                default:
                    Debug.LogErrorFormat("Unknown Nightmare under store or inventory operations. See details: {0}", error.ErrorMessage);
                    break;
            
            }
        }

        public void GrantShards()
        {
            // TODO: Update vessel knowledge via PlayerDataController
            // CatalogManager.Instance.GrantVesselKnowledge(20, ShipTypes.Manta, Element.Space);
        }
    }
}
