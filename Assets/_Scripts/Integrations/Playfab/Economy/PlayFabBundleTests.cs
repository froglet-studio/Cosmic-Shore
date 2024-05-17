using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.Economy;
using UnityEngine;

namespace _Scripts.Integrations.PlayFab.PlayFabTests
{
    public class PlayFabBundleTests : MonoBehaviour
    {
        private void Start()
        {
            AuthenticationManager.OnLoginSuccess += ShowBundles;
            CatalogManager.OnGettingBundleId += GrantElementalCrystals;
        }

        private void OnDisable()
        {
            AuthenticationManager.OnLoginSuccess -= ShowBundles;
            CatalogManager.OnGettingBundleId -= GrantElementalCrystals;
        }

        private void ShowBundles()
        {
            CatalogManager.Instance.GetBundles();
        }

        private void GrantElementalCrystals(string bundleId)
        {
            CatalogManager.Instance.PurchaseBundle(bundleId, 5);
        }
    }
}
