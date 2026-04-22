using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    /// <summary>
    /// Manages Unity IAP for the "Support Us" consumable purchase.
    /// Implements IDetailedStoreListener for Unity Purchasing 4.x.
    /// </summary>
    public class IAPManager : MonoBehaviour, IDetailedStoreListener
    {
        public static IAPManager Instance { get; private set; }

        [Header("IAP Configuration")]
        [Tooltip("Product ID for the support/donation purchase (must match UGS Dashboard)")]
        [SerializeField] private string supportProductId = "com.cosmicshore.support_us";

        private IStoreController _storeController;
        private IExtensionProvider _extensionProvider;

        public bool IsInitialized => _storeController != null && _extensionProvider != null;

        public event Action<bool> OnPurchaseComplete;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            InitializeIAP();
        }

        void InitializeIAP()
        {
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            builder.AddProduct(supportProductId, ProductType.Consumable);
            UnityPurchasing.Initialize(this, builder);
        }

        // ── IDetailedStoreListener ──────────────────────────────────

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            CSDebug.Log("[IAPManager] IAP initialized successfully.");
            _storeController = controller;
            _extensionProvider = extensions;
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            CSDebug.LogWarning($"[IAPManager] IAP init failed: {error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            CSDebug.LogWarning($"[IAPManager] IAP init failed: {error} — {message}");
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            if (args.purchasedProduct.definition.id == supportProductId)
            {
                CSDebug.Log("[IAPManager] Support Us purchase successful. Thank you!");
                OnPurchaseComplete?.Invoke(true);
            }

            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            CSDebug.LogWarning($"[IAPManager] Purchase failed: {product.definition.id} — {failureReason}");
            OnPurchaseComplete?.Invoke(false);
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            CSDebug.LogWarning($"[IAPManager] Purchase failed: {product.definition.id} — {failureDescription.message}");
            OnPurchaseComplete?.Invoke(false);
        }

        // ── Public API ──────────────────────────────────────────────

        public void InitiateSupportPurchase()
        {
            if (!IsInitialized)
            {
                CSDebug.LogWarning("[IAPManager] IAP not initialized. Cannot process purchase.");
                return;
            }

            _storeController.InitiatePurchase(supportProductId);
        }
    }
}
