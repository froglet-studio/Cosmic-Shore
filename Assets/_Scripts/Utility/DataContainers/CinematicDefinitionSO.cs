using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

namespace CosmicShore.Game.Cinematics
{
    public enum CinematicCameraType
    {
        StaticFrontView,      // Camera stops, vessel moves forward
        StaticSideView,       // Camera stops at side angle
        FollowBehind,         // Camera follows from behind
        CircleAround,         // Camera circles around vessel
        ZoomOut,              // Camera zooms out dramatically
        Custom                // For future custom camera behaviors
    }

    public enum AICinematicBehaviorType
    {
        MoveForward,    // Simple forward flight (most common)
        Loop,           // Perform loop maneuver
        Drift,          // Drift while moving
        Spiral,         // Spiral upward
        BarrelRoll,     // Barrel roll (future)
        FlyBy,          // Victory fly-by (future)
        HoverSpin       // Hover and spin (future)
    }

    [System.Serializable]
    public class CinematicCameraSetup
    {
        [Tooltip("Type of camera behavior during cinematic")]
        public CinematicCameraType cameraType;
        [Tooltip("Duration this camera setup stays active")]
        public float duration = 2f;
        [Tooltip("Camera distance from vessel")]
        public float distance = 20f;
        [Tooltip("Camera height offset")]
        public float heightOffset = 5f;
        [Tooltip("For circle camera - rotation speed")]
        public float rotationSpeed = 30f;
        [Tooltip("For zoom - target distance")]
        public float zoomTargetDistance = 50f;
    }

    [System.Serializable]
    public class VictoryLapSettings
    {
        [Tooltip("Duration player maintains control after game ends")]
        [Range(0f, 5f)] public float duration = 2f;
        [Tooltip("Speed multiplier during victory lap")]
        [Range(1f, 3f)] public float speedMultiplier = 1.5f;
        [Tooltip("Enhance trail renderer intensity (future feature)")]
        public bool enhanceTrail = true;
        [Tooltip("In multiplayer, fade loser's vessel trail (future feature)")]
        public bool fadeLoserTrail = true;
    }

    [System.Serializable]
    public class ToastAnimationSettings
    {
        [Tooltip("How high the toast pops up")]
        public float yOffset = 5f;
        [Tooltip("Delay before showing toast")]
        public float delay = 0.5f;
        [Tooltip("Duration toast stays visible")]
        public float duration = 1.5f;
    }

    [System.Serializable]
    public class ScoreRevealAnimationSettings
    {
        [Header("Slide Animation")]
        [Tooltip("Starting X position for slide-in")]
        public float startX = -1200f;
        [Tooltip("Ending X position for slide-in")]
        public float endX = 0f;
        [Tooltip("Duration of slide animation")]
        public float slideDuration = 0.6f;
        [Tooltip("Easing curve for slide animation")]
        public Ease slideEase = Ease.OutCubic;
        
        [Header("Casino Counter")]
        [Tooltip("Duration of casino counter animation")]
        public float casinoCounterDuration = 2f;
    }

    [System.Serializable]
    public class CinematicText
    {
        [Tooltip("Text to display in the Score Reveal Screen")]
        public string scoreRevealDisplayText = "Brilliant!";
        [Tooltip("Minimum score threshold to trigger this text")]
        public int scoreThreshold = 0;
        [Tooltip("Weight for random selection if multiple texts qualify")]
        [Range(0f, 1f)]
        public float selectionWeight = 1f;
    }

    [CreateAssetMenu(
        fileName = "CinematicDefinition",
        menuName = "ScriptableObjects/Cinematics/Cinematic Definition")]
    public class CinematicDefinitionSO : ScriptableObject
    {
        [Header("Victory Lap")]
        [Tooltip("Enable victory lap phase where player maintains control")]
        public bool enableVictoryLap = true;
        public VictoryLapSettings victoryLapSettings = new();
        
        [Header("Victory Lap Toast Message")]
        [Tooltip("Toast message to show during victory lap")]
        public string scoreRevealToastString;
        [Tooltip("Enable the pop-up toast message during victory lap")]
        public bool showVictoryToast = true;
        [Tooltip("Toast animation settings")]
        public ToastAnimationSettings toastSettings = new();
        
        [Header("AI Control")]
        [Tooltip("Take control away from player and give to AI during cinematic")]
        public bool setLocalVesselToAI = true;
        [Tooltip("AI behavior during cinematic - Choose from dropdown!")]
        public AICinematicBehaviorType aiCinematicBehavior = AICinematicBehaviorType.MoveForward;
        
        [Header("Camera System")]
        [Tooltip("List of camera setups to cycle through during cinematic")]
        public List<CinematicCameraSetup> cameraSetups = new List<CinematicCameraSetup>();
        [Tooltip("Randomly select from available cameras instead of sequential")]
        public bool randomizeCameraSelection = false;
        
        [Header("Score Reveal")]
        [Tooltip("Animation settings for score reveal UI")]
        public ScoreRevealAnimationSettings scoreRevealSettings = new();
        
        [Header("Cinematic Text")]
        [Tooltip("Texts to display during score reveal based on performance")]
        public List<CinematicText> cinematicTexts = new List<CinematicText>();
        
        [Header("Timing")]
        [Tooltip("Total delay before showing end screen (includes victory lap + cinematic)")]
        public float delayBeforeEndScreen = 4f;
        [Tooltip("Quick transition time between cameras")]
        [Range(0.1f, 2f)]
        public float cameraTransitionTime = 0.5f;
        [Tooltip("Duration of connecting panel transition")]
        public float connectingPanelDuration = 1f;

        /// <summary>
        /// Get cinematic text based on player score
        /// </summary>
        public string GetCinematicTextForScore(int score)
        {
            if (cinematicTexts == null || cinematicTexts.Count == 0)
                return "Victory!";

            // Filter texts that meet the score threshold
            var validTexts = cinematicTexts.Where(text => score >= text.scoreThreshold).ToList();

            if (validTexts.Count == 0)
                return cinematicTexts[0].scoreRevealDisplayText;

            // Weighted random selection
            var totalWeight = validTexts.Sum(text => text.selectionWeight);

            var randomValue = Random.Range(0f, totalWeight);
            var currentWeight = 0f;

            foreach (var text in validTexts)
            {
                currentWeight += text.selectionWeight;
                if (randomValue <= currentWeight)
                    return text.scoreRevealDisplayText;
            }

            return validTexts[0].scoreRevealDisplayText;
        }
    }
}