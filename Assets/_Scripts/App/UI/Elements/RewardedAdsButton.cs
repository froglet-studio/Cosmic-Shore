using CosmicShore.App.Systems.Ads;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class RewardedAdsButton : MonoBehaviour
    {
        [SerializeField] Button _showAdButton;
        [SerializeField] AdsSystem adsManager;

        void Awake()
        {
            _showAdButton.onClick.AddListener(adsManager.ShowAd);
        }

        void OnDestroy()
        {
            // Clean up the button listeners:
            _showAdButton.onClick.RemoveAllListeners();
        }
    }
}