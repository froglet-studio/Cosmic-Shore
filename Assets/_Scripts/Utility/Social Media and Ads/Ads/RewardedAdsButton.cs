using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Advertisements;
using StarWriter.Core;

public class RewardedAdsButton : MonoBehaviour
{
    [SerializeField] Button _showAdButton;
    [SerializeField] AdsManager adsManager;

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