using CosmicShore.App.Systems.Ads;
using CosmicShore.App.UI.Elements;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Menus
{
    public class AdvertisementMenu : MonoBehaviour
    {
        public RewardedAdsButton watchAdButton;
        public Button declineAdButton;
        public AdsSystem adsManager;

        public delegate void OnDeclineAdEvent();
        public static event OnDeclineAdEvent onDeclineAd;

        void Awake()
        {
            watchAdButton.gameObject.SetActive(true);
            declineAdButton.gameObject.SetActive(true);
        }

        public void OnClickWatchAdButton()
        {
            Debug.Log("Ad requested");
        }

        public void OnClickDeclineAdButton()
        {
            Debug.Log("Ad declined");
            onDeclineAd?.Invoke();
        }
    }
}