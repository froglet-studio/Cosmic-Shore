using System;

namespace CosmicShore.App.Systems.CloudData.Models
{
    /// <summary>
    /// Persists player settings/preferences to UGS Cloud Save.
    /// Enables settings to roam across devices via cloud sync.
    ///
    /// JSON example:
    /// {
    ///   "MusicVolume": 0.8,
    ///   "SfxVolume": 1.0,
    ///   "Sensitivity": 0.5,
    ///   "InvertY": false,
    ///   "PreferredVessel": "Squirrel",
    ///   "TutorialCompleted": true,
    ///   "NotificationsEnabled": true
    /// }
    /// </summary>
    [Serializable]
    public class PlayerSettingsCloudData
    {
        public float MusicVolume = 0.8f;
        public float SfxVolume = 1.0f;
        public float Sensitivity = 0.5f;
        public bool InvertY;
        public string PreferredVessel = "";
        public bool TutorialCompleted;
        public bool NotificationsEnabled = true;
    }
}
