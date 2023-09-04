using Firebase;
using Firebase.Analytics;
using StarWriter.Utility.Singleton;

public class AnalyticsManager : SingletonPersistent<AnalyticsManager>
{
    private FirebaseApp app;

    private bool analyticsEnabled = false;

    public override void Awake()
    {
        base.Awake();
        Initialize();
    }

    public void LogLevelStart()
    {
        if (analyticsEnabled)
        {
            Firebase.Analytics.FirebaseAnalytics.LogEvent(Firebase.Analytics.FirebaseAnalytics.EventLevelStart);
        }
    }

    public void LogAdImpression()
    {
        if (analyticsEnabled)
        {
            Firebase.Analytics.FirebaseAnalytics.LogEvent(Firebase.Analytics.FirebaseAnalytics.EventAdImpression);
        }
    }

    public void LogAppOpen()
    {
        if (analyticsEnabled)
        {
            Firebase.Analytics.FirebaseAnalytics.LogEvent(Firebase.Analytics.FirebaseAnalytics.EventAppOpen);
        }
    }

    public void LogGamePlayStart(MiniGames mode, ShipTypes ship, int playerCount, int intensity)
    {
        if (analyticsEnabled)
        {
            Parameter[] parameters =
            {
                new Parameter(FirebaseAnalytics.ParameterLevel, mode.ToString()),
                new Parameter(FirebaseAnalytics.ParameterCharacter, ship.ToString()),
                new Parameter("PlayerCount", playerCount),
                new Parameter("Intensity", intensity),
            };
            Firebase.Analytics.FirebaseAnalytics.LogEvent(Firebase.Analytics.FirebaseAnalytics.EventLevelStart, parameters);
        }
    }

    public void LogGamePlayEnd(MiniGames mode, ShipTypes ship, int playerCount, int intensity, int highScore)
    {
        if (analyticsEnabled)
        {
            Parameter[] parameters =
            {
                new Parameter(FirebaseAnalytics.ParameterLevel, mode.ToString()),
                new Parameter(FirebaseAnalytics.ParameterCharacter, ship.ToString()),
                new Parameter("PlayerCount", playerCount),
                new Parameter("Intensity", intensity),
                new Parameter("HighScore", highScore),
            };
            Firebase.Analytics.FirebaseAnalytics.LogEvent(Firebase.Analytics.FirebaseAnalytics.EventLevelEnd, parameters);
        }
    }

    void Initialize()
    {

#if UNITY_ANDROID  // TODO: keeping analytics disabled on iOS for now until we resolve dependency issues
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                app = Firebase.FirebaseApp.DefaultInstance;
                
                analyticsEnabled = true;
                LogAppOpen();
            }
            else
            {
                UnityEngine.Debug.LogError(System.String.Format(
                  "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
            }
        });
        /**/
#endif
    }
}