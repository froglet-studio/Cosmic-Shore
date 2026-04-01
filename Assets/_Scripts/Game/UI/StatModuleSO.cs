using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Defines a single stat that can be displayed on the scoreboard.
    /// Uses direct property binding instead of string-based reflection.
    /// Create instances via: Right-click > Create > CosmicShore > Stats > Stat Module
    /// </summary>
    [CreateAssetMenu(fileName = "NewStatModule", menuName = "CosmicShore/Stats/Stat Module", order = 1)]
    public class StatModuleSO : ScriptableObject
    {
        [Header("Display")]
        [Tooltip("The label shown on the scoreboard (e.g., 'Clean Crystals')")]
        public string Label = "Stat Name";
        
        [Tooltip("Icon displayed next to the stat")]
        public Sprite Icon;
        
        [Header("Formatting")]
        [Tooltip("How to format the value for display")]
        public ValueFormatType FormatType = ValueFormatType.Integer;
        
        [Tooltip("Custom format string (only used if FormatType = Custom). Use {0} as placeholder")]
        public string CustomFormat = "{0}";
        
        [Header("Data Binding (Set by Editor)")]
        [Tooltip("Internal: Property path for binding. Set automatically by editor.")]
        [HideInInspector]
        public string PropertyPath = "";
        
        [Tooltip("Internal: Expected tracker type. Set automatically by editor.")]
        [HideInInspector]
        public string TrackerTypeName = "";
    }
    
    public enum ValueFormatType 
    { 
        Integer,              // "42"
        Float1Decimal,        // "42.5"
        Float2Decimals,       // "42.53"
        TimeSeconds,          // "42.53s"
        Percentage,           // "42%"
        Custom                // Use CustomFormat string
    }
}