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

    public delegate void OnWatchAdEvent();
    public static event OnWatchAdEvent onWatchAd;

    private void OnEnable()
    {
        GameManager.onPlayGame += ResetButtons;
        GameManager.onDeath += OnDeath;
    }

    private void OnDisable()
    {
        GameManager.onPlayGame -= ResetButtons;
        GameManager.onDeath -= OnDeath;
    }

    private void Awake()
    {
        ResetButtons();
    }

    public void ResetButtons()
    {
        watchAdButton.gameObject.SetActive(false);
        declineAdButton.gameObject.SetActive(true);
        bedazzledWatchAdButton.gameObject.SetActive(true);
    }

    public void OnClickWatchAdButton()
    {
        Debug.Log("Ad requested");
        ResetButtons();
        //GameManager.Instance.ExtendGame();
    }

    public void OnClickDeclineAdButton()
    {
        Debug.Log("Ad declined");
        ResetButtons();
        onDeclineAd?.Invoke();
    }

    private void OnDeath()
    {
        var bedazzled = ScoringManager.IsScoreBedazzleWorthy;
        
        adsManager.LoadAd();
        bedazzledWatchAdButton.gameObject.SetActive(bedazzled);
        watchAdButton.gameObject.SetActive(!bedazzled);
        declineAdButton.gameObject.SetActive(true);
    }
}