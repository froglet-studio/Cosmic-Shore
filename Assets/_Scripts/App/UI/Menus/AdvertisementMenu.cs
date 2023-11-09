using UnityEngine;
using UnityEngine.UI;
using StarWriter.Core;

public class AdvertisementMenu : MonoBehaviour
{
    public RewardedAdsButton watchAdButton;
    public Button declineAdButton;
    public AdsManager adsManager;

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

    void ShowIncentivizedAdPanel()
    {
        if (GameManager.Instance.DeathCount < 2)
        {
            adsManager.LoadAd();
            watchAdButton.gameObject.SetActive(true);
            declineAdButton.gameObject.SetActive(true);
        }
    }
}