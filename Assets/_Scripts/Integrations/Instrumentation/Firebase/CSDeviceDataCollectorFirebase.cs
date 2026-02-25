using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using Firebase.Analytics;
using System.Threading.Tasks;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Integrations.Instrumentation.Firebase
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