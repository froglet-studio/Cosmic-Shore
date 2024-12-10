using CosmicShore._Scripts.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSDeviceDataCollector : IDeviceAnalyzale
    {
        private readonly CSDeviceDataCollectorFirebase  _deviceDataCollectorFirebase = new ();
        public void InitSDK()
        {
            Debug.Log("CSDeviceDataCollector - Initializing Arcade Data Collector.");
        }

        public void LogEventAppOpen()
        {
            Debug.Log("CSArcadeDataCollector - Triggering app open event.");
            _deviceDataCollectorFirebase.LogEventAppOpen();
        }

        public void LogEventAppClose()
        {
            Debug.Log("CSArcadeDataCollector - Triggering app close event.");
            _deviceDataCollectorFirebase.LogEventAppClose();
        }
    }
}