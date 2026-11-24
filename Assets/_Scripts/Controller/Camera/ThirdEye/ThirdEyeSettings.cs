using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI Diagnose panel's (Third Eye's) tuning, per machine: saved as JSON in PlayerPrefs (<see cref="Key"/>), never in an asset.
    /// Every overlay starts OFF so a fresh install costs nothing until you ask for it.
    /// </summary>
    [Serializable]
    public sealed class ThirdEyeSettings
    {
        public const string Key = "CosmicShore.ThirdEye.Settings";

        // The one diagnose camera is Free fly (the lava lamp's FreeFlyCamera, its own speeds); this is its field of
        // view. There are no Chase / Follow / Orbit distances (Yash, 2026-11-22): a hull is watched through the game's
        // own camera. Old saved settings may still carry those fields; JsonUtility ignores them.
        public float Fov = 70f;

        // Session-only (never saved, Yash 2026-11-01): the overlays start off every run.
        [NonSerialized] public bool ShowThinking, ShowLabels, ShowTrails;
        public float TrailSeconds = 8f;

        public void Clamp()
        {
            Fov = Mathf.Clamp(Fov, 20f, 120f);
            TrailSeconds = Mathf.Clamp(TrailSeconds, 1f, 30f);
        }

        public static ThirdEyeSettings Load()
        {
            var s = new ThirdEyeSettings();
            string json = PlayerPrefs.GetString(Key, null);
            if (!string.IsNullOrEmpty(json))
            {
                try { JsonUtility.FromJsonOverwrite(json, s); }
                catch (ArgumentException) { s = new ThirdEyeSettings(); }
            }
            s.Clamp();
            return s;
        }

        public void Save()
        {
            Clamp();
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
