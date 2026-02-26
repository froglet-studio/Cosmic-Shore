using CosmicShore.Core;
using System.Threading.Tasks;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    public class CSDeviceDataCollector : IDeviceAnalyzale
    {
        private readonly IDeviceAnalyzale _deviceDataCollectorFirebase = new CSDeviceDataCollectorFirebase();
        public async Task InitSDK()
        {
            await Task.Delay(1);    // Hide console warning until this is connected
            CSDebug.Log("CSDeviceDataCollector - Initializing Arcade Data Collector.");
        }

        public void LogEventAppOpen()
        {
            CSDebug.Log("CSArcadeDataCollector - Triggering app open event.");
            _deviceDataCollectorFirebase.LogEventAppOpen();
        }

        public void LogEventAppClose()
        {
            CSDebug.Log("CSArcadeDataCollector - Triggering app close event.");
            _deviceDataCollectorFirebase.LogEventAppClose();
        }
    }
}