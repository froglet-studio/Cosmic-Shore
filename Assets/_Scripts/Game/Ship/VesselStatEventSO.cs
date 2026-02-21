using System;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// A SOAP-style scriptable event for a single vessel stat.
    /// Carries display metadata (label, icon, format) alongside the value,
    /// so listeners need no knowledge of where the stat came from.
    ///
    /// Create via: Right-click > Create > CosmicShore > Stats > Vessel Stat Event
    ///
    /// Usage:
    ///   - Assign one asset per tracked stat (e.g. "MaxDriftTime", "CleanStreak")
    ///   - Score trackers call Raise(value) to broadcast the latest value
    ///   - EventDrivenStatsProvider subscribes and caches the value for the scoreboard
    /// </summary>
    [CreateAssetMenu(
        fileName = "StatEvent_New",
        menuName  = "CosmicShore/Stats/Vessel Stat Event",
        order     = 1)]
    public class VesselStatEventSO : ScriptableObject
    {
        [Header("Display")]
        [Tooltip("Label shown on the scoreboard, e.g. 'Longest Drift'")]
        public string Label = "Stat Name";

        [Tooltip("Icon shown next to the stat on the scoreboard")]
        public Sprite Icon;

        [Header("Formatting")]
        public ValueFormatType FormatType = ValueFormatType.Float1Decimal;

        [Tooltip("Only used when FormatType = Custom. Use {0} as the value placeholder.")]
        public string CustomFormat = "{0}";

        // Runtime — not serialized, resets each play session automatically
        // because SOs are reloaded fresh in a build and reset on domain reload in editor.
        [NonSerialized] private float _currentValue;

        public event Action<float> OnRaised;

        /// <summary>
        /// Broadcast a new value for this stat. Called by VesselStatTracker.
        /// </summary>
        public void Raise(float value)
        {
            _currentValue = value;
            OnRaised?.Invoke(value);
        }

        /// <summary>
        /// The most recently raised value. Used by EventDrivenStatsProvider
        /// when it needs to snapshot all stats at game-end time.
        /// </summary>
        public float CurrentValue => _currentValue;

        /// <summary>
        /// Format CurrentValue according to this stat's FormatType.
        /// </summary>
        public string FormattedValue => Format(_currentValue);

        public string Format(float value)
        {
            return FormatType switch
            {
                ValueFormatType.Integer        => ((int)value).ToString(),
                ValueFormatType.Float1Decimal  => $"{value:F1}",
                ValueFormatType.Float2Decimals => $"{value:F2}",
                ValueFormatType.TimeSeconds    => $"{value:F2}s",
                ValueFormatType.Percentage     => $"{(int)value}%",
                ValueFormatType.Custom         => string.IsNullOrEmpty(CustomFormat)
                    ? value.ToString()
                    : string.Format(CustomFormat, value),
                _                              => value.ToString()
            };
        }

        /// <summary>
        /// Reset the cached value — call this when a new match begins.
        /// </summary>
        public void Reset() => _currentValue = 0f;
    }
}