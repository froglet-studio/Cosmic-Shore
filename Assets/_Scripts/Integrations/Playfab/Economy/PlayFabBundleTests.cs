using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.CloudScripts;
using CosmicShore.Integrations.PlayFab.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Scripts.Integrations.PlayFab.PlayFabTests
{
    public class PlayFabBundleTests : MonoBehaviour
    {
        private static CatalogManager CatalogManager => CatalogManager.Instance;
        private static DailyRewardHandler DailyRewardHandler => DailyRewardHandler.Instance;
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
            CatalogManager.GetBundles();
        }

        private void GrantElementalCrystals(string bundleId)
        {
            // _catalogManager.PurchaseBundle(bundleId, 5);
            if (string.IsNullOrEmpty(bundleId))
            {
                Debug.LogError("PlayFabBundleTests-GrantElementalCrystals() - test bundle id is null");
                return;
            }
            DailyRewardHandler.GrantBundle(new[]{bundleId});
        }
    }
}
