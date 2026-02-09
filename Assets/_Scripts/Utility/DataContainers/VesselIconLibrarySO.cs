using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Cinematics
{
    /// <summary>
    /// Stores sprite/icon references for all vessel types in the game.
    /// Designers can assign vessel icons here for use in UI displays.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselIconLibrary",
        menuName = "ScriptableObjects/Cinematics/Vessel Icon Library")]
    public class VesselIconLibrarySO : ScriptableObject
    {
        [Serializable]
        public class VesselIconEntry
        {
            [Tooltip("The vessel type this icon represents")]
            public VesselClassType vesselType;
            
            [Tooltip("Icon/sprite for this vessel type")]
            public Sprite vesselIcon;
            
            [Tooltip("Optional: Alternative icon for winner display")]
            public Sprite winnerIcon;
            
            [Tooltip("Optional: Color tint for this vessel")]
            public Color vesselColor = Color.white;
        }

        [SerializeField] private List<VesselIconEntry> vesselIcons = new List<VesselIconEntry>();
        
        [Header("Fallback")]
        [Tooltip("Default icon if vessel type not found")]
        [SerializeField] private Sprite defaultIcon;

        /// <summary>
        /// Get vessel icon for a specific vessel type
        /// </summary>
        public Sprite GetVesselIcon(VesselClassType vesselType, bool useWinnerIcon = false)
        {
            foreach (var entry in vesselIcons)
            {
                if (entry.vesselType == vesselType)
                {
                    if (useWinnerIcon && entry.winnerIcon != null)
                        return entry.winnerIcon;
                    
                    return entry.vesselIcon != null ? entry.vesselIcon : defaultIcon;
                }
            }

            Debug.LogWarning($"No icon found for vessel type: {vesselType}. Using default icon.");
            return defaultIcon;
        }

        /// <summary>
        /// Get vessel color tint for a specific vessel type
        /// </summary>
        public Color GetVesselColor(VesselClassType vesselType)
        {
            foreach (var entry in vesselIcons)
            {
                if (entry.vesselType == vesselType)
                    return entry.vesselColor;
            }

            return Color.white;
        }

        /// <summary>
        /// Check if icon exists for vessel type
        /// </summary>
        public bool HasIcon(VesselClassType vesselType)
        {
            foreach (var entry in vesselIcons)
            {
                if (entry.vesselType == vesselType && entry.vesselIcon != null)
                    return true;
            }

            return false;
        }
    }
}