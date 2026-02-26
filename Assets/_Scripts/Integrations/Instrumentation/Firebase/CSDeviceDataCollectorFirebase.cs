using CosmicShore.Integrations.Instrumentation;
using Firebase.Analytics;
using System.Threading.Tasks;
using UnityEngine;
using CosmicShore.Utility.Recording;

namespace CosmicShore.Integrations.Instrumentation
{
    public class CSDeviceDataCollectorFirebase : IDeviceAnalyzale
    {
        public async Task InitSDK()
        {
            await Task.Delay(1);    // Hide console warning until this is connected
            CSDebug.Log("CSDeviceDataCollectorFirebase - Initializing Training Data Collector.");
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