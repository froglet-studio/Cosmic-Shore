using UnityEngine;
using System.Collections.Generic;

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
    
    /// <summary>
    /// ⭐ NEW: Available AI behaviors for cinematic sequences.
    /// Designer-friendly enum instead of typing strings.
    /// </summary>
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
        [Range(0f, 5f)]
        public float duration = 2f;
        
        [Tooltip("Speed multiplier during victory lap")]
        [Range(1f, 3f)]
        public float speedMultiplier = 1.5f;
        
        [Tooltip("Enhance trail renderer intensity (future feature)")]
        public bool enhanceTrail = true;
        
        [Tooltip("In multiplayer, fade loser's vessel trail (future feature)")]
        public bool fadeLoserTrail = true;
    }

    [System.Serializable]
    public class CinematicText
    {
        [Tooltip("Text to display (e.g., 'Brilliant!', 'Perfect!', 'Victory!')")]
        public string displayText = "Brilliant!";
        
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
        
        public VictoryLapSettings victoryLapSettings = new VictoryLapSettings();
        
        [Header(" Victory Lap Toast Message")]
        [Tooltip("Toast message to show during victory lap (e.g., 'GREAT JOB!', 'AMAZING!', 'WINNER!')")]
        public string scoreRevealToastString = "GREAT JOB!";
        
        [Tooltip("Enable the pop-up toast message during victory lap")]
        public bool showVictoryToast = true;

        [Header("AI Control")]
        [Tooltip("Take control away from player and give to AI during cinematic")]
        public bool setLocalVesselToAI = true;
        
        [Tooltip(" AI behavior during cinematic - Choose from dropdown!")]
        public AICinematicBehaviorType aiCinematicBehavior = AICinematicBehaviorType.MoveForward;

        [Header("Camera System")]
        [Tooltip("List of camera setups to cycle through during cinematic")]
        public List<CinematicCameraSetup> cameraSetups = new List<CinematicCameraSetup>();
        
        [Tooltip("Randomly select from available cameras instead of sequential")]
        public bool randomizeCameraSelection = false;

        [Header("Cinematic Text")]
        [Tooltip("Texts to display during score reveal based on performance")]
        public List<CinematicText> cinematicTexts = new List<CinematicText>();

        [Header("Timing")]
        [Tooltip("Total delay before showing end screen (includes victory lap + cinematic)")]
        public float delayBeforeEndScreen = 4f;
        
        [Tooltip("Quick transition time between cameras")]
        [Range(0.1f, 2f)]
        public float cameraTransitionTime = 0.5f;

        [Header("Multiplayer Features (Future)")]
        [Tooltip("Show multiple vessel images in score panel")]
        public bool showMultipleVessels = true;
        
        [Tooltip("Animate vessels on podium")]
        public bool animateVesselPodium = false;

        /// <summary>
        /// Get cinematic text based on player score
        /// </summary>
        public string GetCinematicTextForScore(int score)
        {
            if (cinematicTexts == null || cinematicTexts.Count == 0)
                return "Victory!";

            // Filter texts that meet the score threshold
            var validTexts = new List<CinematicText>();
            foreach (var text in cinematicTexts)
            {
                if (score >= text.scoreThreshold)
                    validTexts.Add(text);
            }

            if (validTexts.Count == 0)
                return cinematicTexts[0].displayText;

            // Weighted random selection
            float totalWeight = 0f;
            foreach (var text in validTexts)
                totalWeight += text.selectionWeight;

            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var text in validTexts)
            {
                currentWeight += text.selectionWeight;
                if (randomValue <= currentWeight)
                    return text.displayText;
            }

            return validTexts[0].displayText;
        }

        /// <summary>
        /// Get next camera setup (sequential or random)
        /// </summary>
        public CinematicCameraSetup GetNextCameraSetup(ref int currentIndex)
        {
            if (cameraSetups == null || cameraSetups.Count == 0)
                return null;

            if (randomizeCameraSelection)
            {
                currentIndex = Random.Range(0, cameraSetups.Count);
            }
            else
            {
                currentIndex = (currentIndex + 1) % cameraSetups.Count;
            }

            return cameraSetups[currentIndex];
        }
    }
}