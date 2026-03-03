using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility.Trailer
{
    public enum TrailerCameraType
    {
        ChaseBehind,
        SideTracking,
        FrontHeroShot,
        HighOrbit,
        LowAngleHero,
        SlowOrbit
    }

    [System.Serializable]
    public class TrailerCameraSetup
    {
        [Tooltip("Camera behavior type")]
        public TrailerCameraType cameraType;

        [Tooltip("Label used in filenames and editor UI")]
        public string label = "Camera";

        [Tooltip("Distance from the vessel")]
        [Range(5f, 100f)]
        public float distance = 25f;

        [Tooltip("Height offset relative to the vessel")]
        [Range(-20f, 40f)]
        public float heightOffset = 5f;

        [Tooltip("Lateral offset (positive = right)")]
        [Range(-30f, 30f)]
        public float lateralOffset;

        [Tooltip("Orbit speed in degrees per second (for orbit cameras)")]
        [Range(5f, 120f)]
        public float orbitSpeed = 30f;

        [Tooltip("Follow smoothing (lower = tighter)")]
        [Range(0.01f, 1f)]
        public float smoothTime = 0.2f;

        [Tooltip("Enable this camera for recording")]
        public bool enabled = true;
    }

    [CreateAssetMenu(
        fileName = "TrailerCameraConfig",
        menuName = "ScriptableObjects/Trailer/Trailer Camera Config")]
    public class TrailerCameraConfigSO : ScriptableObject
    {
        [Header("Camera Setups")]
        [Tooltip("List of trailer camera angles. 5-6 recommended.")]
        public List<TrailerCameraSetup> cameraSetups = new()
        {
            new TrailerCameraSetup
            {
                cameraType = TrailerCameraType.ChaseBehind,
                label = "Chase",
                distance = 20f,
                heightOffset = 6f,
                smoothTime = 0.25f
            },
            new TrailerCameraSetup
            {
                cameraType = TrailerCameraType.SideTracking,
                label = "Side",
                distance = 18f,
                heightOffset = 3f,
                lateralOffset = 15f,
                smoothTime = 0.15f
            },
            new TrailerCameraSetup
            {
                cameraType = TrailerCameraType.FrontHeroShot,
                label = "Front",
                distance = 25f,
                heightOffset = 2f,
                smoothTime = 0.3f
            },
            new TrailerCameraSetup
            {
                cameraType = TrailerCameraType.HighOrbit,
                label = "HighOrbit",
                distance = 35f,
                heightOffset = 20f,
                orbitSpeed = 20f,
                smoothTime = 0.4f
            },
            new TrailerCameraSetup
            {
                cameraType = TrailerCameraType.LowAngleHero,
                label = "LowHero",
                distance = 15f,
                heightOffset = -5f,
                smoothTime = 0.2f
            },
            new TrailerCameraSetup
            {
                cameraType = TrailerCameraType.SlowOrbit,
                label = "SlowOrbit",
                distance = 22f,
                heightOffset = 8f,
                orbitSpeed = 15f,
                smoothTime = 0.35f
            }
        };

        [Header("Recording Settings")]
        [Tooltip("Duration of each clip in seconds")]
        [Range(1f, 30f)]
        public float clipDurationSeconds = 5f;

        [Tooltip("Capture resolution width")]
        public int captureWidth = 1920;

        [Tooltip("Capture resolution height")]
        public int captureHeight = 1080;

        [Tooltip("Target frames per second for capture")]
        [Range(24, 120)]
        public int targetFPS = 60;

        [Tooltip("Anti-aliasing samples for render textures (1, 2, 4, 8)")]
        [Range(1, 8)]
        public int antiAliasing = 4;

        [Header("Output")]
        [Tooltip("Root output folder relative to project root. Clips saved under subfolders per session.")]
        public string outputFolder = "TrailerCaptures";

        [Header("UI Control")]
        [Tooltip("Hide the game UI layer during recording")]
        public bool hideUILayer = true;

        [Header("Trigger")]
        [Tooltip("Automatically start recording when the game ends")]
        public bool recordOnGameEnd = true;

        [Tooltip("Delay after game end before recording starts (seconds)")]
        [Range(0f, 5f)]
        public float recordingStartDelay = 0.5f;
    }
}
