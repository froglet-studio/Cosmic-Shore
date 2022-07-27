using UnityEngine;
using UnityEngine.Advertisements;

public class AdsInitializer : MonoBehaviour, IUnityAdsInitializationListener
{
    // Organization Core ID: 4037077
    [SerializeField] string _androidGameId;
    [SerializeField] string _iOSGameId;
    [SerializeField] bool _testMode = true;
    [SerializeField] RewardedAdsButton _rewardedAdsButton;
    private string _gameId;

    void Awake()
    {
        InitializeAds();
    } 

    public void InitializeAds()
    {
        _gameId = (Application.platform == RuntimePlatform.IPhonePlayer)
            ? _iOSGameId
            : _androidGameId;
        Debug.Log($"InitializeAds: OnBeforeAdvertisement.Initialize - _gameId:{_gameId}, _testMode:{_testMode}");
        Advertisement.Initialize(_gameId, _testMode, true, this);
        Debug.Log($"InitializeAds: OnAfterAdvertisement.Initialize - _gameId:{_gameId}, _testMode:{_testMode}");
    }

    public void OnInitializationComplete()
    {
        Debug.Log("Unity Ads initialization complete.");
        // For now, we're loading the ad on game over instead of here
        // _rewardedAdsButton.LoadAd();
    }

    public void OnInitializationFailed(UnityAdsInitializationError error, string message)
    {
        Debug.Log($"Unity Ads Initialization Failed: {error.ToString()} - {message}");
    }
}