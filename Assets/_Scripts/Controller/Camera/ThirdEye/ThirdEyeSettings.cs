using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public enum ThirdEyeCameraMode { Chase, Follow, Free }
    public enum ThirdEyeFreeMode { Fly, Orbit }

    /// <summary>
    /// The AI Diagnose panel's (Third Eye's) tuning, per machine: saved as JSON in PlayerPrefs (<see cref="Key"/>), never in an asset.
    /// Every overlay starts OFF so a fresh install costs nothing until you ask for it.
    /// </summary>
    [Serializable]
    public sealed class ThirdEyeSettings
    {
        public const string Key = "CosmicShore.ThirdEye.Settings";

        public ThirdEyeCameraMode Mode = ThirdEyeCameraMode.Free;
        public ThirdEyeFreeMode FreeMode = ThirdEyeFreeMode.Fly;

        public float Fov = 70f;
        public float ChaseDistance = 30f, ChaseHeight = 8f, ChaseLookAhead = 40f, ChaseSmoothing = 8f;
        public float FollowDistance = 90f, FollowHeight = 35f, FollowSmoothing = 3f;
        // Free fly is the lava lamp's FreeFlyCamera (its own speeds); these are orbit's.
        public float OrbitDistance = 80f, LookSensitivity = 0.15f;

        public bool ShowThinking, ShowLabels, ShowTrails;
        public float TrailSeconds = 8f;

        public void Clamp()
        {
            Fov = Mathf.Clamp(Fov, 20f, 120f);
            ChaseDistance = Mathf.Clamp(ChaseDistance, 2f, 400f);
            ChaseHeight = Mathf.Clamp(ChaseHeight, -100f, 200f);
            ChaseLookAhead = Mathf.Clamp(ChaseLookAhead, 0f, 400f);
            ChaseSmoothing = Mathf.Clamp(ChaseSmoothing, 0.5f, 30f);
            FollowDistance = Mathf.Clamp(FollowDistance, 5f, 1000f);
            FollowHeight = Mathf.Clamp(FollowHeight, -200f, 500f);
            FollowSmoothing = Mathf.Clamp(FollowSmoothing, 0.5f, 30f);
            OrbitDistance = Mathf.Clamp(OrbitDistance, 2f, 2000f);
            LookSensitivity = Mathf.Clamp(LookSensitivity, 0.02f, 2f);
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
