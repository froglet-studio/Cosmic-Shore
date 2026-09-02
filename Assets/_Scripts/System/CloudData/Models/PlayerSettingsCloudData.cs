using System;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persists player settings/preferences to UGS Cloud Save.
    /// Mirrors GameSetting fields so settings roam across devices.
    ///
    /// JSON example:
    /// {
    ///   "MusicEnabled": true,
    ///   "SFXEnabled": true,
    ///   "HapticsEnabled": true,
    ///   "InvertYEnabled": false,
    ///   "InvertThrottleEnabled": false,
    ///   "JoystickVisualsEnabled": true,
    ///   "MusicLevel": 0.8,
    ///   "SFXLevel": 1.0,
    ///   "HapticsLevel": 1.0,
    ///   "ModifiedUtcTicks": 638600000000000000
    /// }
    ///
    /// <c>ModifiedUtcTicks</c> is the last-writer-wins stamp GameSetting compares against its local
    /// PlayerPrefs stamp before applying this snapshot (0 = written by a build that predates the
    /// stamp, or never saved). Absent from a legacy payload, Newtonsoft leaves it 0.
    /// </summary>
    [Serializable]
    public class PlayerSettingsCloudData
    {
        public bool MusicEnabled = true;
        public bool SFXEnabled = true;
        public bool HapticsEnabled = true;
        public bool InvertYEnabled;
        public bool InvertThrottleEnabled;
        public bool JoystickVisualsEnabled = true;
        public float MusicLevel = 1.0f;
        public float SFXLevel = 1.0f;
        public float HapticsLevel = 1.0f;
        public long ModifiedUtcTicks;
    }
}
