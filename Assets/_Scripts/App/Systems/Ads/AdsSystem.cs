using System;
using CosmicShore.Integrations.Firebase.Controller;
using UnityEngine;
using UnityEngine.Advertisements;

namespace CosmicShore.App.Systems.Ads
{
    public class AdsSystem : MonoBehaviour, IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
    {
        // Organization Core ID: 4037077
        [SerializeField] string _androidGameId;
        [SerializeField] string _iOSGameId;
#pragma warning disable CS0414 // Ignore the not used warning - since we only use one of these two behind a precompile flag
        [SerializeField] string _androidAdUnitId = "Rewarded_Android";
        [SerializeField] string _iOSAdUnitId = "Rewarded_iOS";
#pragma warning restore CS0414
        [SerializeField] bool _skipAdForDevelopment = true;

        private string _adUnitId; // These will fall back to android for unsupported platforms
        private string _gameId;

        public static Action AdInitializationComplete;
        public static Action AdInitializationFailed;
        public static Action AdLoaded;
        public delegate void OnAdFailedToLoad(string adUnitId, UnityAdsLoadError error, string message);
        public static event OnAdFailedToLoad AdFailedToLoad;
        public delegate void OnAdShowClick(string adUnitId);
        public static event OnAdShowClick AdShowClick;
        public delegate void OnAdShowStart(string adUnitId);
        public static event OnAdShowStart AdShowStart;
        public delegate void OnAdShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState);
        public static event OnAdShowComplete AdShowComplete;
        public delegate void OnAdShowFailure(string adUnitId, UnityAdsShowError error, string message);
        public static event OnAdShowFailure AdShowFailure;
        void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            AdLoaded += FirebaseAnalyticsController.LogEventAdImpression;
        }

        private void OnDisable()
        {
            AdLoaded -= FirebaseAnalyticsController.LogEventAdImpression;
        }

        public void Initialize()
        {
            // Default to android settings
            _adUnitId = _androidAdUnitId;
            _gameId = _androidGameId;

#if UNITY_IOS
           _adUnitId = _iOSAdUnitId;
           _gameId = _iOSGameId;
#endif

            Debug.Log($"InitializeAds: OnBeforeAdvertisement.Initialize - _gameId:{_gameId}");
            Advertisement.Initialize(_gameId, _skipAdForDevelopment, this);
            Debug.Log($"InitializeAds: OnAfterAdvertisement.Initialize - _gameId:{_gameId}");
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
            if (_skipAdForDevelopment)
            {
                OnUnityAdsShowComplete(_adUnitId, UnityAdsShowCompletionState.COMPLETED);
                return;
            }

            Advertisement.Show(_adUnitId, this);
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
            AdFailedToLoad?.Invoke(adUnitId, error, message);
        }

        public void OnUnityAdsShowClick(string adUnitId)
        {
            Debug.Log($"AdsManager.OnUnityAdsShowClick - adUnitId: {adUnitId}");
            AdShowClick?.Invoke(adUnitId);
        }
        public void OnUnityAdsShowStart(string adUnitId)
        {
            Debug.Log($"AdsManager.OnUnityAdsShowStart - adUnitId: {adUnitId}");
            AdShowStart?.Invoke(adUnitId);
        }
        public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
        {
            Debug.Log($"AdsManager.OnUnityAdsShowComplete - adUnitId: {adUnitId}, completionState: {showCompletionState}");
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            AdShowComplete?.Invoke(adUnitId, showCompletionState);
        }
        public void OnUnityAdsShowFailure(string adUnitId, UnityAdsShowError error, string message)
        {
            Debug.Log($"AdsManager.OnUnityAdsShowFailure - adUnitId: {adUnitId}, error: {error}, message: {message}");
            AdShowFailure?.Invoke(adUnitId, error, message);
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
            Debug.Log($"AdsManager.OnUnityAdsDidStart - placementId: {placementId}, showResult: {showResult}");

            switch (showResult)
            {
                case ShowResult.Finished:
                    Debug.Log("The ad was successfully shown.");
                    OnUnityAdsShowComplete(_adUnitId, UnityAdsShowCompletionState.COMPLETED);
                    break;
                case ShowResult.Skipped:
                    Debug.Log("The ad was skipped before reaching the end.");
                    break;
                case ShowResult.Failed:
                    Debug.LogError("The ad failed to be shown.");
                    break;
            }
        }

        /*
        public void OnAdShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
        {
            Debug.Log("GameManager.OnAdShowComplete");
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            if (showCompletionState.Equals(UnityAdsShowCompletionState.COMPLETED))
            {
                Debug.Log("Unity Ads Rewarded Ad Completed. Extending game.");

            }
            if (showCompletionState.Equals(UnityAdsShowCompletionState.SKIPPED))
            {
                Debug.Log("Unity Ads Rewarded Ad SKIPPED due to ad failure. Extending game.");

            }
        }

        public void OnAdShowFailure(string adUnitId, UnityAdsShowError error, string message)
        {
            // Give them the benefit of the doubt and just pass through to the ad completion logic
            OnAdShowComplete(adUnitId, UnityAdsShowCompletionState.SKIPPED);
        }
        */
    }
}