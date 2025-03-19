using System;
using System.Collections;


#if !UNITY_WEBGL
using CosmicShore.Integrations.Firebase.Controller;
#endif
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
            StartCoroutine(InitializeCoroutine());
        }

        private void OnEnable()
        {
#if !UNITY_WEBGL
            AdLoaded += FirebaseAnalyticsController.LogEventAdImpression;
#endif
        }

        private void OnDisable()
        {
#if !UNITY_WEBGL
            AdLoaded -= FirebaseAnalyticsController.LogEventAdImpression;
#endif
        }

        IEnumerator InitializeCoroutine()
        {
            yield return new WaitForSeconds(1.5f);

            Initialize();
        }

        void Initialize()
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
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
#endif
        }

        // Load content to the Ad Unit:
        public void LoadAd()
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
            // IMPORTANT! Only load content AFTER initialization (in this example, initialization is handled in a different script).
            Debug.Log($"Loading Ad: _adUnitId:{_adUnitId}");
            Advertisement.Load(_adUnitId, this);
#endif        
        }

        // Implement a method to execute when the user clicks the button:
        public void ShowAd()
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
            if (_skipAdForDevelopment)
            {
                OnUnityAdsShowComplete(_adUnitId, UnityAdsShowCompletionState.COMPLETED);
                return;
            }

            Advertisement.Show(_adUnitId, this);
#endif
        }

        public void OnInitializationComplete()
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID

            Debug.Log("AdsManager.OnInitializationComplete");

            AdInitializationComplete?.Invoke();
#endif
        }

        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
            Debug.Log($"AdsManager.OnInitializationFailed - error: {error}, message:  {message}");
            AdInitializationFailed?.Invoke();
#endif
        }

        // If the ad successfully loads, add a listener to the button and enable it:
        public void OnUnityAdsAdLoaded(string adUnitId)
        {

#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
            Debug.Log("AdsManager.OnUnityAdsAdLoaded - adUnitId: " + adUnitId);
            AdLoaded?.Invoke();
#endif
        }

        public void OnUnityAdsFailedToLoad(string adUnitId, UnityAdsLoadError error, string message)
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
            Debug.Log($"AdsManager.OnUnityAdsFailedToLoad - adUnitId:{adUnitId}, error: {error}, message: {message}");
            AdFailedToLoad?.Invoke(adUnitId, error, message);
#endif
        }

        public void OnUnityAdsShowClick(string adUnitId)
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
            Debug.Log($"AdsManager.OnUnityAdsShowClick - adUnitId: {adUnitId}");
            AdShowClick?.Invoke(adUnitId);
#endif
        }
        public void OnUnityAdsShowStart(string adUnitId)
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
            Debug.Log($"AdsManager.OnUnityAdsShowStart - adUnitId: {adUnitId}");
            AdShowStart?.Invoke(adUnitId);
#endif
        }
        public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
            Debug.Log($"AdsManager.OnUnityAdsShowComplete - adUnitId: {adUnitId}, completionState: {showCompletionState}");
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            AdShowComplete?.Invoke(adUnitId, showCompletionState);
#endif
        }
        public void OnUnityAdsShowFailure(string adUnitId, UnityAdsShowError error, string message)
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
            Debug.Log($"AdsManager.OnUnityAdsShowFailure - adUnitId: {adUnitId}, error: {error}, message: {message}");
            AdShowFailure?.Invoke(adUnitId, error, message);
#endif
        }

        public void OnUnityAdsReady(string placementId)
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
            Debug.Log($"AdsManager.OnUnityAdsReady - placementId: {placementId}");
#endif
        }

        public void OnUnityAdsDidError(string message)
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
            Debug.Log($"AdsManager.OnUnityAdsDidError - message: {message}");
#endif
        }

        public void OnUnityAdsDidStart(string placementId)
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
            Debug.Log($"AdsManager.OnUnityAdsDidStart - placementId: {placementId}");
#endif
        }

        public void OnUnityAdsDidFinish(string placementId, ShowResult showResult)
        {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
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
#endif
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