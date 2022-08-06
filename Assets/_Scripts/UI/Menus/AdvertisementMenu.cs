using UnityEngine;
using UnityEngine.UI;
using StarWriter.Core;

public class AdvertisementMenu : MonoBehaviour
{
    public RewardedAdsButton watchAdButton;
    public Button declineAdButton;
    public RewardedAdsButton bedazzledWatchAdButton;
    public AdsManager adsManager;

    public delegate void OnDeclineAdEvent();
    public static event OnDeclineAdEvent onDeclineAd;

    private void OnEnable()
    {
        GameManager.onDeath += ShowIncentivizedAdPanel;
    }

    private void OnDisable()
    {
        GameManager.onDeath -= ShowIncentivizedAdPanel;
    }

    private void Awake()
    {
        watchAdButton.gameObject.SetActive(true);
        declineAdButton.gameObject.SetActive(true);
        bedazzledWatchAdButton.gameObject.SetActive(false);
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

    private void ShowIncentivizedAdPanel()
    {
        if (GameManager.Instance.DeathCount < 2)
        {
            var bedazzled = ScoringManager.IsScoreBedazzleWorthy;

            adsManager.LoadAd();
            bedazzledWatchAdButton.gameObject.SetActive(bedazzled);
            watchAdButton.gameObject.SetActive(!bedazzled);
            declineAdButton.gameObject.SetActive(true);
        }
    }
}