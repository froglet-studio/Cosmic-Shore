using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public enum ThirdEyeCameraMode { Chase, Follow, Free }
    public enum ThirdEyeFreeMode { Fly, Orbit }

    /// <summary>
    /// The Third Eye's tuning, per machine: saved as JSON in PlayerPrefs (<see cref="Key"/>), never in an asset.
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
        public float FreeSpeed = 120f, FastMultiplier = 4f, OrbitDistance = 80f, LookSensitivity = 0.15f;

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
            FreeSpeed = Mathf.Clamp(FreeSpeed, 1f, 5000f);
            FastMultiplier = Mathf.Clamp(FastMultiplier, 1f, 20f);
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
