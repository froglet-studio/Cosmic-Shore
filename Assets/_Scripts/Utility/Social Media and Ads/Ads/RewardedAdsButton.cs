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

    void OnEnable()
    {
        AdsManager.adShowComplete += OnAdShowComplete;
    }

    void OnDisable()
    {
        AdsManager.adShowComplete -= OnAdShowComplete;
    }

    public void OnAdShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
    {
        if (showCompletionState.Equals(UnityAdsShowCompletionState.COMPLETED))
        {
            Debug.Log("Unity Ads Rewarded Ad Completed. Extending game.");
            // Grant a reward.

            // TODO: THIS IS WHERE WE WOULD EXTEND THE GAME PLAY
            GameManager.Instance.ExtendGame();
        }
    }

    void OnDestroy()
    {
        // Clean up the button listeners:
        _showAdButton.onClick.RemoveAllListeners();
    }
}