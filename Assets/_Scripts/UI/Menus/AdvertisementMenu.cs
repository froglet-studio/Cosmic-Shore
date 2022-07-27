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
        GameManager.onPlayGame += ResetButtons;
        ScoringManager.onGameOver += OnGameOver;
    }

    private void OnDisable()
    {
        GameManager.onPlayGame -= ResetButtons;
        ScoringManager.onGameOver -= OnGameOver;
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

    private void OnGameOver(bool bedazzled, bool advertisement)
    {
        if (advertisement)
        {
            adsManager.LoadAd();
            bedazzledWatchAdButton.gameObject.SetActive(bedazzled);
            watchAdButton.gameObject.SetActive(bedazzled);
            declineAdButton.gameObject.SetActive(true);
        }
    }
}