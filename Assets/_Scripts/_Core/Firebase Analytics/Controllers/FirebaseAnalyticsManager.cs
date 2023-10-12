using _Scripts._Core.Firebase_Models;
using Firebase;
using Firebase.Analytics;
using StarWriter.Utility.Singleton;
using UnityEngine;

public class FirebaseAnalyticsManager : SingletonPersistent<FirebaseAnalyticsManager>
{
    private FirebaseApp app;
    private FirebaseApp _testApp;
    // private M
    private const string TestAppName = "EditorTest";
    private bool _analyticsEnabled = false;
    private FirebaseGameplayModel _firebaseGameplayModel;
    private FirebaseUserModel _firebaseUserModel;
    

    public override void Awake()
    {
        base.Awake();
        InitializeFirebase();
        SetUserProperty();
    }
    #region User Configurations

    /// <summary>
    /// Set User Property
    /// Set Device Unique Identifier as user Unique ID and send to Firebase Analytics
    /// </summary>
    public void SetUserProperty()
    {
        _firebaseUserModel ??= new FirebaseUserModel();
        _firebaseUserModel.UniqueId ??= SystemInfo.deviceUniqueIdentifier;
        Debug.Log($"{nameof(FirebaseAnalyticsManager)} - {nameof(SetUserProperty)} - {nameof(_firebaseUserModel.UniqueId)} - {_firebaseUserModel.UniqueId}");
        FirebaseAnalytics.SetUserProperty(nameof(_firebaseUserModel.UniqueId), _firebaseUserModel.UniqueId);
    }

    /// <summary>
    /// Get User Property
    /// Set Device Unique Identifier as user Unique ID and send to Firebase Analytics
    /// </summary>
    public void GetUserProperty()
    {
        // TODO: get user properties from Firebase Remote Configuration
    }
    #endregion
    
    #region Analytics Controls

    /// <summary>
    /// Data Collection Switch
    /// Controls Firebase data collection
    /// </summary>
    public void DataCollectionSwitch(bool isOn)
    {
        FirebaseAnalytics.SetAnalyticsCollectionEnabled(isOn);
    }
    #endregion
    
    #region Firebase Initialization
    /// <summary>
    /// Initialize Firebase
    /// Android requires Google Play Services to be up-to-date, check for update before the Firebase initialization
    /// IOS check also?
    /// </summary>
    void InitializeFirebase()
    {
        // Check for analytics dependencies before performing analytic events
        AnalyticsDependenciesCheck();
#if UNITY_ANDROID  
        InitializeGooglePlayService();
#endif
        
#if UNITY_IOS
        // TODO: IOS pre-requisites for firebase
            InitializeIOSPrerequisites();
#endif
        
#if UNITY_EDITOR
        InitializeUnityEditMode();
#endif

    }

    /// <summary>
    /// Initialize Google Play Service
    /// Android requires Google Play Services to be up-to-date, check for update before the Firebase initialization 
    /// </summary>
    void InitializeGooglePlayService()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available) {
                // Create and hold a reference to your FirebaseApp,
                // where app is a Firebase.FirebaseApp property of your application class.
                app = FirebaseApp.DefaultInstance;

                // Set a flag here to indicate whether Firebase is ready to use by your app.
                _analyticsEnabled = true;
            } else {
                UnityEngine.Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
                _analyticsEnabled = false;
                // Firebase Unity SDK is not safe to use here.
            }
        });
        
    }
    
    /// <summary>
    /// Initialize IOS pre-requisites
    /// TODO: IOS pre-requisites for firebase
    /// </summary>
    void InitializeIOSPrerequisites()
    {
        
    }
    

    /// <summary>
    /// Initialize Unity Edit Mode
    /// Testing in Unity Edit Mode using a separate Firebase App instance from the default one
    /// So that the data collected in testing does not effect the ones from final game builds
    /// </summary>
    void InitializeUnityEditMode()
    {
        // FirebaseApp.Create(TestAppName);
    }

    /// <summary>
    /// Analytics Dependency Check
    /// Check for Firebase analytics dependencies
    /// </summary>
    void AnalyticsDependenciesCheck()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
        });
    }
    
    #endregion

    #region Firebase Gameplay Analytics Events
    
    /// <summary>
    /// On Log Level Start
    /// Log level start event
    /// </summary>
    public void OnLogLevelStart()
    {
        if (!_analyticsEnabled) return;
        
        // TODO: add Log Level Start Event
        //FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelStart);
        
    }

    public void OnLogGamePlayStart(MiniGames mode, ShipTypes ship, int playerCount, int intensity)
    {
        if (!_analyticsEnabled) return;
        
        /*
        Parameter[] parameters =
        {
            new Parameter(FirebaseAnalytics.ParameterLevel, mode.ToString()),
            new Parameter(FirebaseAnalytics.ParameterCharacter, ship.ToString()),
            new Parameter("PlayerCount", playerCount),
            new Parameter("Intensity", intensity),
        };
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelStart, parameters);
        */
        
    }

    public void LogGamePlayEnd(GameplayStages gameplayStages)
    {
        if (!_analyticsEnabled) return;
        
        /*
        Parameter[] parameters =
        {
            new Parameter(FirebaseAnalytics.ParameterLevel, mode.ToString()),
            new Parameter(FirebaseAnalytics.ParameterCharacter, ship.ToString()),
            new Parameter("PlayerCount", playerCount),
            new Parameter("Intensity", intensity),
            new Parameter("HighScore", highScore),
        };
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelEnd, parameters);
        */
        
    }
    #endregion

    #region Ads Measurement Events

    public void OnLogAdImpression()
    {
        if (!_analyticsEnabled) return;
        {
            //FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAdImpression);
        }
    }

    public void OnLogAppOpen()
    {
        if (!_analyticsEnabled) return;
        
        //FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAppOpen);
        
    }

    #endregion

   
}