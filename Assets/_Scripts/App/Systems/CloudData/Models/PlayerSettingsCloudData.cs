using System;

namespace CosmicShore.App.Systems.CloudData.Models
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
    ///   "HapticsLevel": 1.0
    /// }
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
    }
}
