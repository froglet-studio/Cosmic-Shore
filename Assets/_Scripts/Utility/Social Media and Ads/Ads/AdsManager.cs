using StarWriter.Core;
using UnityEngine;
using UnityEngine.Advertisements;

public class AdsManager : MonoBehaviour, IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener, IUnityAdsListener
{
    // Organization Core ID: 4037077
    [SerializeField] string _androidGameId;
    [SerializeField] string _iOSGameId;
    [SerializeField] string _androidAdUnitId = "Rewarded_Android";
    [SerializeField] string _iOSAdUnitId = "Rewarded_iOS";
    [SerializeField] bool _testMode = true;

    private string _adUnitId; // This will remain null for unsupported platforms
    private string _gameId;

    public delegate void OnAdInitializationComplete();
    public static event OnAdInitializationComplete AdInitializationComplete;
    public delegate void OnAdInitializationFailed();
    public static event OnAdInitializationFailed AdInitializationFailed;
    public delegate void OnAdLoaded();
    public static event OnAdLoaded AdLoaded;
    public delegate void OnAdFailedToLoad(string adUnitId, UnityAdsLoadError error, string message);
    public static event OnAdFailedToLoad adFailedToLoad;
    public delegate void OnAdShowClick(string adUnitId);
    public static event OnAdShowClick adShowClick;
    public delegate void OnAdShowStart(string adUnitId);
    public static event OnAdShowStart adShowStart;
    public delegate void OnAdShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState);
    public static event OnAdShowComplete adShowComplete;
    public delegate void OnAdShowFailure(string adUnitId, UnityAdsShowError error, string message);
    public static event OnAdShowFailure adShowFailure;

    void Awake()
    {
        InitializeAds();
    } 

    public void InitializeAds()
    {
        #if UNITY_IOS
            _adUnitId = _iOSAdUnitId;
        #elif UNITY_ANDROID
            _adUnitId = _androidAdUnitId;
        #endif

        _gameId = (Application.platform == RuntimePlatform.IPhonePlayer)
            ? _iOSGameId
            : _androidGameId;

        Debug.Log($"InitializeAds: OnBeforeAdvertisement.Initialize - _gameId:{_gameId}, _testMode:{_testMode}");
        Advertisement.Initialize(_gameId, _testMode, true, this);
        Advertisement.AddListener(this);
        Debug.Log($"InitializeAds: OnAfterAdvertisement.Initialize - _gameId:{_gameId}, _testMode:{_testMode}");
    }

    // Load content to the Ad Unit:
    public void LoadAd()
    {
        // IMPORTANT! Only load content AFTER initialization (in this example, initialization is handled in a different script).
        Debug.Log($"Loading Ad: _adUnitId:{_adUnitId}");
        Advertisement.Load(_adUnitId, this);
    }

    // Implement a method to execute when the user clicks the button:
    public void ShowAd()
    {   
        Advertisement.Show(_adUnitId, this);
    }

    private void HandleShowResult(ShowResult result)
    {

    }

    public void OnInitializationComplete()
    {
        Debug.Log("AdsManager.OnInitializationComplete");

        AdInitializationComplete?.Invoke();
    }

    public void OnInitializationFailed(UnityAdsInitializationError error, string message)
    {
        Debug.Log($"AdsManager.OnInitializationFailed - error: {error}, message:  {message}");
        AdInitializationFailed?.Invoke();
    }

    // If the ad successfully loads, add a listener to the button and enable it:
    public void OnUnityAdsAdLoaded(string adUnitId)
    {
        Debug.Log("AdsManager.OnUnityAdsAdLoaded - adUnitId: " + adUnitId);
        AdLoaded?.Invoke();
    }

    public void OnUnityAdsFailedToLoad(string adUnitId, UnityAdsLoadError error, string message)
    {
        Debug.Log($"AdsManager.OnUnityAdsFailedToLoad - adUnitId:{adUnitId}, error: {error}, message: {message}");
        adFailedToLoad?.Invoke(adUnitId, error, message);
    }

    public void OnUnityAdsShowClick(string adUnitId) 
    {
        Debug.Log($"AdsManager.OnUnityAdsShowClick - adUnitId: {adUnitId}");
        adShowClick?.Invoke(adUnitId);
    }
    public void OnUnityAdsShowStart(string adUnitId) 
    {
        Debug.Log($"AdsManager.OnUnityAdsShowStart - adUnitId: {adUnitId}");
        adShowStart?.Invoke(adUnitId);
    }
    public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
    {
        Debug.Log($"AdsManager.OnUnityAdsShowComplete - adUnitId: {adUnitId}, completionState: {showCompletionState}");
        adShowComplete?.Invoke(adUnitId, showCompletionState);
    }
    public void OnUnityAdsShowFailure(string adUnitId, UnityAdsShowError error, string message)
    {
        Debug.Log($"AdsManager.OnUnityAdsShowFailure - adUnitId: {adUnitId}, error: {error}, message: {message}");
        adShowFailure?.Invoke(adUnitId, error, message);
    }

    public void OnUnityAdsReady(string placementId)
    {
        Debug.Log($"AdsManager.OnUnityAdsReady - placementId: {placementId}");
    }

    public void OnUnityAdsDidError(string message)
    {
        Debug.Log($"AdsManager.OnUnityAdsDidError - message: {message}");
    }

    public void OnUnityAdsDidStart(string placementId)
    {
        Debug.Log($"AdsManager.OnUnityAdsDidStart - placementId: {placementId}");
    }

    public void OnUnityAdsDidFinish(string placementId, ShowResult showResult)
    {
        switch (showResult)
        {
            case ShowResult.Finished:
                Debug.Log("The ad was successfully shown.");
                OnUnityAdsShowComplete(_adUnitId, UnityAdsShowCompletionState.COMPLETED);
                //
                // YOUR CODE TO REWARD THE GAMER
                // Give coins etc.
                break;
            case ShowResult.Skipped:
                Debug.Log("The ad was skipped before reaching the end.");
                break;
            case ShowResult.Failed:
                Debug.LogError("The ad failed to be shown.");
                break;
        }
    }
}