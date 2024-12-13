using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using Firebase.Analytics;

namespace CosmicShore._Scripts.Integrations.Instrumentation.Firebase
{
    public class CSDeviceDataCollectorFirebase : IDeviceAnalyzale
    {
        public void InitSDK()
        {
            
        }

        public void LogEventAppOpen()
        {
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.AppOpen);
        }

        public void LogEventAppClose()
        {
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.AppClose);
        }
    }
}