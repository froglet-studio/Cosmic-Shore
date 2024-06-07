using CosmicShore.Integrations.Enums;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Integrations.PlayFab.PlayerModels;
using CosmicShore.Integrations.PlayFab.Utility;
using PlayFab;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scenes.TestScenes.Playfab_Sandbox_Test
{
    public class CatalogView : MonoBehaviour
    {
        [Header("Catalog Buttons")]
        [SerializeField] Button purchaseCaptainButton;
        [SerializeField] Button purchaseShardsButton;
        [SerializeField] Button loadCatalogItemsButton;

        [Header("Inventory Buttons")] 
        [SerializeField] Button grantStartingItemsButton;
        [SerializeField] Button loadInventoryButton;
        [SerializeField] Button loadCaptainDataButton;
        [SerializeField] Button removeInvCollectionButton;

        // Vessel Data related instances
        private static GuideDataAccessor _guideDataAccessor;
        private static List<GuideData> _guideDataList;
        
        // test strings
        const string MantaShipUpgrade1Id = "6b5264af-4645-4aaa-8228-3b35ed379585";
        const string MantaShipUpgrade2Id = "806f1840-a0de-4463-8b56-4b43b07c3d5a";
        const string ElementalCrystalId = "06bcebb1-dc41-49a8-82b0-96a15ced7c1c";
        const string CrystalId = "51392e05-9072-43a9-ae2d-4a3335dbf313";

        private static CatalogManager CatalogManager => CatalogManager.Instance;
    
        void Start()
        {
            _guideDataAccessor ??= new GuideDataAccessor();
            _guideDataList ??= new List<GuideData>();
        }

        void OnEnable()
        {
            purchaseCaptainButton.onClick.AddListener(PurchaseUpgradeTest);
            grantStartingItemsButton.onClick.AddListener(GrantStartingInventoryTest);
            loadCatalogItemsButton.onClick.AddListener(GetCatalogItemsTest);
            loadInventoryButton.onClick.AddListener(LoadInventoryTest);
            loadCaptainDataButton.onClick.AddListener(LoadCaptainData);
            purchaseShardsButton.onClick.AddListener(PurchaseShardsWithCrystalTest);
            removeInvCollectionButton.onClick.AddListener(RemoveInventoryCollectionTest);
            
            PlayFabUtility.GettingPlayFabErrors += ProcessCatalogErrors;
            
        }

        void OnDisable()
        {
            PlayFabUtility.GettingPlayFabErrors -= ProcessCatalogErrors;
            
            purchaseCaptainButton.onClick.RemoveListener(PurchaseUpgradeTest);
            grantStartingItemsButton.onClick.RemoveListener(GrantStartingInventoryTest);
            loadCatalogItemsButton.onClick.RemoveListener(GetCatalogItemsTest);
            loadInventoryButton.onClick.RemoveListener(LoadInventoryTest);
            loadCaptainDataButton.onClick.RemoveListener(LoadCaptainData);
            purchaseShardsButton.onClick.RemoveListener(PurchaseShardsWithCrystalTest);
            removeInvCollectionButton.onClick.RemoveListener(RemoveInventoryCollectionTest);
        }

        /// <summary>
        /// Purchase Item Test
        /// TODO: Currently Manta Ship Upgrade 1 is not purchasable, figure out why.
        /// Buy a test captain using shards, the amount of shards should be the exact price tag on the test captain shards
        /// </summary>
        void PurchaseUpgradeTest()
        {
            // The currency calculation for currency should be done before passing shard to purchase inventory API, otherwise it will get "Invalid Request" error.
            // testing upgrade 1 purchasing
            var elementalCrystal1 = new ItemPrice{ ItemId = ElementalCrystalId, Amount = 5};
            var mantaSpaceUpgrade1 = new VirtualItem { ItemId = MantaShipUpgrade1Id, ContentType = nameof(CaptainLevel.Upgrade3), Amount = 1 };
            
            // Parameter order note: item first, currency second
            CatalogManager.PurchaseItem(mantaSpaceUpgrade1, elementalCrystal1);

            SaveCaptainData(CaptainLevel.Upgrade3, MantaShipUpgrade1Id);
            Debug.LogFormat("{0} - {1}: captain info {2} saved to local storage.", nameof(CatalogView), nameof(PurchaseUpgradeTest), nameof(mantaSpaceUpgrade1)); 
            
            // TODO: Should have ship upgrade level detection here, if level 1 upgrade is not purchased, you can't buy level 2 or higher.
        
            // testing upgrade 2 purchasing
            var captainShard2 = new ItemPrice{ItemId = ElementalCrystalId, Amount = 10};
            var mantaSpaceUpgrade2 = new VirtualItem { ItemId = MantaShipUpgrade2Id, Amount = 1 };
 
            CatalogManager.PurchaseItem(mantaSpaceUpgrade2, vesselShard2);
            SaveGuideData(GuideLevel.Upgrade3, MantaShipUpgrade2Id, 2 );
            Debug.LogFormat("{0} - {1}: vessel info {2} saved to local storage.", nameof(CatalogView), nameof(PurchaseUpgradeTest), nameof(mantaSpaceUpgrade2));
            LoadGuideData();
        }
        
        /// <summary>
        /// Purchase Shards With Crystal Test
        /// 1 Crystal for 10 Shards
        /// </summary>
        void PurchaseShardsWithCrystalTest()
        {
            var shardPrice = new ItemPrice { ItemId = CrystalId, Amount = 1 };
            var elementalCrystals = new VirtualItem { ItemId = ElementalCrystalId, Amount = 10 };

            
            CatalogManager.PurchaseItem(vesselShards, shardPrice);
        }

        /// <summary>
        /// Grant Starting Inventory (Working)
        /// Experimental method - should be handled by 
        /// Nothing magical here, default item quantity is 100, Granted when player created their account.
        /// </summary>
        void GrantStartingInventoryTest()
        {
            // For now it's 100 elemental crystals
            var elementalCrystal = new VirtualItem
            {
                ItemId = ElementalCrystalId,
                ContentType = nameof(ContentTypes.CaptainXP),
                Amount = 100
            };
            var crystals = new VirtualItem
            {
                ItemId = CrystalId,
                ContentType = "Currency",
                Amount = 10
            };
            var startingItems = new List<VirtualItem> { vesselShard, crystals };
            CatalogManager.GrantStartingInventory(startingItems);
        }

        /// <summary>
        /// Get Catalog Items Test (Working)
        /// </summary>
        void GetCatalogItemsTest()
        {
            // var filter = "ContentType eq 'Captain' and tags/any(t: t eq 'Rhino')";
        
            // Default filter is "", which means load without filter
            CatalogManager.LoadCatalogItems();
        }
    
        /// <summary>
        /// Load Inventory Test (Working)
        /// Update to see the contents inside player inventory
        /// </summary>
        void LoadInventoryTest()
        {
            CatalogManager.LoadPlayerInventory();
        }

        /// <summary>
        /// Save Captain Data (Working)
        /// </summary>
        /// <param name="captainLevel">Captain's Upgrade Level</param>
        /// <param name="captainId">Captain ID</param>
        void SaveCaptainData(CaptainLevel captainLevel, string captainId)
        {
            GuideData guideData = new(guideId, upgradeLevel);
            _guideDataList.Add(guideData);
            _guideDataAccessor.Save(guideLevel, _guideDataList);
        }
        
        /// <summary>
        /// Load Captain Data (working)
        /// </summary>
        void LoadCaptainData()
        {
            var guideUpgradeLevels = _guideDataAccessor.Load();
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
        void RemoveInventoryCollectionTest()
        {
            CatalogManager.GetInventoryCollectionIds();
        }

        /// <summary>
        /// Remove Inventory Collection
        /// </summary>
        /// <param name="collectionIds">Collection Id List</param>
        void RemoveInventoryCollection(List<string> collectionIds)
        {
            foreach (var id in collectionIds)
            {
                Debug.LogFormat("{0} - {1} collection id: .", nameof(CatalogView), nameof(RemoveInventoryCollection));
                CatalogManager.DeleteInventoryCollection(id);
            }
        }

        /// <summary>
        /// Process Catalog Errors
        /// </summary>
        /// <param name="error">PlayFab Error</param>
        void ProcessCatalogErrors(PlayFabError error)
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
            // TODO: Update captain knowledge via PlayerDataController
            // CatalogManager.Instance.GrantCaptainXP(20, ShipTypes.Manta, Element.Space);
        }
    }
}
