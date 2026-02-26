using System;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    /// <summary>
    /// Manages Unity IAP for the "Support Us" feature.
    /// This is a stub that will be completed when Unity IAP is fully configured
    /// with product IDs and store setup.
    ///
    /// To complete the integration:
    /// 1. Import Unity IAP package via Package Manager
    /// 2. Configure products in Unity Dashboard / App Store Connect / Google Play Console
    /// 3. Implement IDetailedStoreListener interface
    /// 4. Add product IDs and purchase flow
    /// </summary>
    public class IAPManager : MonoBehaviour
    {
        public static IAPManager Instance { get; private set; }

        [Header("IAP Configuration")]
        [Tooltip("Product ID for the support/donation purchase")]
        [SerializeField] private string supportProductId = "com.cosmicshore.support_tier1";

        [Tooltip("Whether IAP has been initialized")]
        public bool IsInitialized { get; private set; }

        public event Action<bool> OnPurchaseComplete;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            InitializeIAP();
        }

        /// <summary>
        /// Initialize Unity IAP.
        /// TODO: Implement with UnityPurchasing.Initialize() when IAP package is imported.
        /// </summary>
        void InitializeIAP()
        {
            // Stub: Unity IAP initialization will go here
            // var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            // builder.AddProduct(supportProductId, ProductType.Consumable);
            // UnityPurchasing.Initialize(this, builder);

            CSDebug.Log("[IAPManager] IAP stub initialized. Unity IAP package required for full implementation.");
            IsInitialized = false;
        }

        /// <summary>
        /// Initiates a support/donation purchase.
        /// </summary>
        public void InitiateSupportPurchase()
        {
            if (!IsInitialized)
            {
                CSDebug.LogWarning("[IAPManager] IAP not initialized. Cannot process purchase.");
                // TODO: Show UI message that purchases are not available yet
                return;
            }

            // TODO: Implement with m_StoreController.InitiatePurchase(supportProductId)
            CSDebug.Log($"[IAPManager] Initiating purchase for: {supportProductId}");
        }

        /// <summary>
        /// Called when a purchase completes successfully.
        /// TODO: Implement IDetailedStoreListener.ProcessPurchase
        /// </summary>
        void HandlePurchaseSuccess(string productId)
        {
            CSDebug.Log($"[IAPManager] Purchase successful: {productId}");
            OnPurchaseComplete?.Invoke(true);
        }

        /// <summary>
        /// Called when a purchase fails.
        /// TODO: Implement IDetailedStoreListener.OnPurchaseFailed
        /// </summary>
        void HandlePurchaseFailure(string productId, string reason)
        {
            CSDebug.LogWarning($"[IAPManager] Purchase failed: {productId} - {reason}");
            OnPurchaseComplete?.Invoke(false);
        }
    }
}
