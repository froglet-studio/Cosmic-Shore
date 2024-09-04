using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.Utility;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.EconomyModels;
using UnityEngine;

namespace CosmicShore.Integrations.Playfab.Economy
{
    public class CatalogBundleHandler : MonoBehaviour
    {
        #region Bundle Handling

        static PlayFabEconomyInstanceAPI _playFabEconomyInstanceAPI;

        public static Dictionary<string, string> Bundles { get; private set; } = new();
        public static event Action<string> OnGettingBundleId;

        /// <summary>
        /// Get Bundles
        /// Returns SeearchItemsResponse that contains bundle id if request is successful, title and other information.
        /// </summary>
        /// <param name="filter">A filter string to query PlayFab bundle information</param>
        public static void GetBundles(string filter = "type eq 'bundle'")
        {
            _playFabEconomyInstanceAPI ??=
                new(AuthenticationManager.PlayFabAccount.AuthContext);
            var request = new SearchItemsRequest
            {
                Filter = filter
            };
            _playFabEconomyInstanceAPI.SearchItems(request, OnGettingBundlesSuccess, PlayFabUtility.HandleErrorReport);
        }

        /// <summary>
        /// On Getting Bundle Success Delegate
        /// Add bundle titles as keys and bundle ids as values to memory
        /// Invoke an action "testBundleId" for testing purpose
        /// </summary>
        /// <param name="response">Search Item Response</param>
        private static void OnGettingBundlesSuccess(SearchItemsResponse response)
        {
            if (response is null) { Debug.Log("CatalogManager.GetBundle() - no response"); return; }

            var items = string.Join(" bundle: ", response.Items.Select(i => i.Id.ToString() + " " + i.Title.Values.FirstOrDefault()));
            Debug.Log($"CatalogManager.GetBundle() - bundle: {items}");
            Bundles ??= new();

            foreach (var bundle in response.Items)
            {
                Bundles.TryAdd(bundle.Title.Values.FirstOrDefault() ?? "Nameless Bundle", bundle.Id);
            }

            string testBundleId;
            Bundles.TryGetValue("Test Bundle", out testBundleId);

            // TODO: This one is for testing, can be changed to any bundle id you want later
            // if (string.IsNullOrEmpty(testBundleId)) {Debug.Log($"CatalogManager.GetBundle() - Test Bundle Id is not here");return;}
            Debug.Log($"CatalogManager.GetBundles() - Test Bundle Id: {testBundleId}");
            OnGettingBundleId?.Invoke(testBundleId);
        }

        /// <summary>
        /// Purchase a bundle
        /// </summary>
        /// <param name="bundleId"></param>
        /// <param name="quantity"></param>
        public static void PurchaseBundle(string bundleId, uint quantity)
        {
            const string annotation = "Bundle Purchase";

            quantity = VerifyQuantity(quantity);

            _playFabEconomyInstanceAPI ??=
                new(AuthenticationManager.PlayFabAccount.AuthContext);

            var itemRequest = new ItemPurchaseRequest
            {
                ItemId = bundleId,
                Quantity = quantity,
                Annotation = annotation
            };

            var startPurchaseRequest = new StartPurchaseRequest { Items = new() { itemRequest } };

            PlayFabClientAPI.StartPurchase(startPurchaseRequest, OnPurchaseBundleSuccess, PlayFabUtility.HandleErrorReport);
        }

        /// <summary>
        /// On Purchasing Bundle Success
        /// </summary>
        /// <param name="result"></param>
        private static void OnPurchaseBundleSuccess(StartPurchaseResult result)
        {
            if (result is null) return;

            Debug.Log($"CatalogManager.PurchaseBundle() - {result.OrderId} remaining balance: {result.VirtualCurrencyBalances}");
            PayBundle(result.OrderId);

        }

        /// <summary>
        /// A helper function to verify item quantity, if it exceeds 25, clamp to 25
        /// </summary>
        /// <param name="quantity"></param>
        /// <returns></returns>
        private static uint VerifyQuantity(uint quantity)
        {
            return quantity > 25 ? 25 : quantity;
        }

        /// <summary>
        /// Pay For a Bundle
        /// TODO: The bundle Id is not legit for purchase as an item, needs further investigation on how to handle bundles in PlayFab
        /// </summary>
        /// <param name="orderId"></param>
        private static void PayBundle(string orderId)
        {
            var payPurchaseRequest = new PayForPurchaseRequest { OrderId = orderId };
            PlayFabClientAPI.PayForPurchase(payPurchaseRequest, OnPayBundleSuccess, PlayFabUtility.HandleErrorReport);
        }

        /// <summary>
        /// On Paying Bundle Success Delegate
        /// </summary>
        /// <param name="result"></param>
        private static void OnPayBundleSuccess(PayForPurchaseResult result)
        {
            if (result is null) return;

            Debug.Log($"CatalogManager.PayBundle() - {result.OrderId} purchase currency:{result.PurchaseCurrency} status:{result.Status}");
            var balance = string.Join(" ", result.VirtualCurrency.Select(i => i.Key + " " + i.Value));
            Debug.Log($"CatalogManager.BayBundle() - current virtual currency balance: {balance}");
        }

        #endregion
    }
}
