using System;
using System.Collections.Generic;
using CosmicShore.Game.Arcade;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Universal stats provider that displays stats from any IStatExposable ScoreTracker.
    /// No string-based reflection - uses direct type-safe binding.
    /// 
    /// Setup:
    /// 1. Add this component to your scoreboard GameObject
    /// 2. Assign your ScoreTracker (must implement IStatExposable)
    /// 3. Add StatModuleSO assets to the list
    /// 4. Each StatModule binds to a specific stat key from the tracker
    /// </summary>
    public class UniversalStatsProvider : ScoreboardStatsProvider
    {
        [Header("Tracker Reference")]
        [Tooltip("The ScoreTracker to pull stats from (must implement IStatExposable)")]
        [SerializeField] private BaseScoreTracker scoreTracker;
        
        [Header("Stat Modules")]
        [Tooltip("Stat modules define what stats to display and how to format them")]
        [SerializeField] private List<StatBinding> statBindings = new List<StatBinding>();
        
        private IStatExposable statExposable;
        private bool isInitialized;
        
        private void Awake()
        {
            InitializeTracker();
        }
        
        private void InitializeTracker()
        {
            if (scoreTracker == null)
            {
                Debug.LogError("[UniversalStatsProvider] No ScoreTracker assigned!");
                return;
            }
            
            statExposable = scoreTracker as IStatExposable;
            
            if (statExposable == null)
            {
                Debug.LogError(
                    $"[UniversalStatsProvider] ScoreTracker '{scoreTracker.GetType().Name}' does not implement IStatExposable! " +
                    "Add IStatExposable interface to your tracker to expose stats."
                );
                return;
            }
            
            isInitialized = true;
        }
        
        public override List<StatData> GetStats()
        {
            var stats = new List<StatData>();
            
            if (!isInitialized)
            {
                InitializeTracker();
                if (!isInitialized) return stats;
            }
            
            // Get current stat values from tracker
            var exposedStats = statExposable.GetExposedStats();
            
            if (exposedStats == null || exposedStats.Count == 0)
            {
                Debug.LogWarning("[UniversalStatsProvider] Tracker returned no stats");
                return stats;
            }
            
            // Build stat data for each binding
            foreach (var binding in statBindings)
            {
                if (binding.StatModule == null)
                {
                    Debug.LogWarning("[UniversalStatsProvider] Null StatModule in binding, skipping");
                    continue;
                }
                
                if (string.IsNullOrEmpty(binding.StatKey))
                {
                    Debug.LogWarning($"[UniversalStatsProvider] StatModule '{binding.StatModule.Label}' has no stat key bound");
                    continue;
                }
                
                // Try to get the value from exposed stats
                if (!exposedStats.TryGetValue(binding.StatKey, out var value))
                {
                    Debug.LogWarning($"[UniversalStatsProvider] Stat key '{binding.StatKey}' not found in tracker");
                    continue;
                }
                
                // Format and add to results
                try
                {
                    var formattedValue = FormatValue(value, binding.StatModule);
                    
                    stats.Add(new StatData
                    {
                        Label = binding.StatModule.Label,
                        Value = formattedValue,
                        Icon = binding.StatModule.Icon
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UniversalStatsProvider] Error formatting stat '{binding.StatModule.Label}': {ex.Message}");
                }
            }
            
            return stats;
        }
        
        /// <summary>
        /// Formats a raw value according to the module's format settings
        /// </summary>
        private string FormatValue(object value, StatModuleSO module)
        {
            if (value == null) return "0";
            
            try
            {
                switch (module.FormatType)
                {
                    case ValueFormatType.Integer:
                        return Convert.ToInt32(value).ToString();
                        
                    case ValueFormatType.Float1Decimal:
                        return $"{Convert.ToSingle(value):F1}";
                        
                    case ValueFormatType.Float2Decimals:
                        return $"{Convert.ToSingle(value):F2}";
                        
                    case ValueFormatType.TimeSeconds:
                        return $"{Convert.ToSingle(value):F2}s";
                        
                    case ValueFormatType.Percentage:
                        return $"{Convert.ToInt32(value)}%";
                        
                    case ValueFormatType.Custom:
                        if (string.IsNullOrEmpty(module.CustomFormat))
                            return value.ToString();
                        return string.Format(module.CustomFormat, value);
                        
                    default:
                        return value.ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UniversalStatsProvider] Format error for '{module.Label}': {ex.Message}");
                return value.ToString();
            }
        }
        
        /// <summary>
        /// Gets available stat keys from the current tracker (for editor use)
        /// </summary>
        public List<string> GetAvailableStatKeys()
        {
            var keys = new List<string>();
            
            if (scoreTracker == null) return keys;
            
            var exposable = scoreTracker as IStatExposable;
            if (exposable == null) return keys;
            
            var stats = exposable.GetExposedStats();
            if (stats != null)
            {
                keys.AddRange(stats.Keys);
                keys.Sort();
            }
            
            return keys;
        }
        
        /// <summary>
        /// Validates that all bindings have valid stat keys
        /// </summary>
        public bool ValidateBindings(out List<string> errors)
        {
            errors = new List<string>();
            
            if (scoreTracker == null)
            {
                errors.Add("No ScoreTracker assigned");
                return false;
            }
            
            var exposable = scoreTracker as IStatExposable;
            if (exposable == null)
            {
                errors.Add($"ScoreTracker '{scoreTracker.GetType().Name}' does not implement IStatExposable");
                return false;
            }
            
            var availableKeys = exposable.GetExposedStats();
            if (availableKeys == null || availableKeys.Count == 0)
            {
                errors.Add("Tracker returned no exposed stats");
                return false;
            }
            
            foreach (var binding in statBindings)
            {
                if (binding.StatModule == null)
                {
                    errors.Add("Binding has null StatModule");
                    continue;
                }
                
                if (string.IsNullOrEmpty(binding.StatKey))
                {
                    errors.Add($"'{binding.StatModule.Label}' has no stat key bound");
                    continue;
                }
                
                if (!availableKeys.ContainsKey(binding.StatKey))
                {
                    errors.Add($"'{binding.StatModule.Label}' references invalid key '{binding.StatKey}'");
                }
            }
            
            return errors.Count == 0;
        }
        
        private void OnValidate()
        {
            // Reinitialize when properties change in editor
            isInitialized = false;
        }
    }
    
    /// <summary>
    /// Binds a StatModule to a specific stat key from the tracker
    /// </summary>
    [System.Serializable]
    public class StatBinding
    {
        [Tooltip("The stat module (defines label, icon, formatting)")]
        public StatModuleSO StatModule;
        
        [Tooltip("The key from the tracker's GetExposedStats() dictionary")]
        public string StatKey;
    }
}