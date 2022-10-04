using TailGlider.Utility.Singleton;
using Firebase;

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

    private void Initialize()
    {
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                app = Firebase.FirebaseApp.DefaultInstance;
                // TODO: keeping analytics disabled for now until we get FireBase working on iOS
                //analyticsEnabled = true;
                LogAppOpen();
            }
            else
            {
                UnityEngine.Debug.LogError(System.String.Format(
                  "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
            }
        });
    }
}