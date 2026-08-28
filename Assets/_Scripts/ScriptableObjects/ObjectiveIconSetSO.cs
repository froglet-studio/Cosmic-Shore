using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The fleet-wide map from a mode's <see cref="ScoringMetric"/> to the glyph the HUD's
    /// objective readout draws.
    ///
    /// It is keyed on the METRIC, not on the game mode, and that is the whole design:
    /// <see cref="ScoringMetric"/> is already the platform's single answer to "what is this mode
    /// scored on" - it drives the HUD number, the turn monitor's remaining count, the end
    /// condition and the scoreboard secondary - so a new mode that picks an existing metric gets
    /// its objective icon for free, and only a genuinely new metric ever needs new art.
    /// Adding a per-mode override here would re-open the exact divergence
    /// <see cref="ScoringMetric"/> exists to close.
    ///
    /// Art is authored by <c>Tools/Build/author_objective_icons.py</c>: pure-white line-weight
    /// silhouettes with the shape in the alpha channel, tinted per context by the reader.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ObjectiveIconSet",
        menuName = "ScriptableObjects/UI/Objective Icon Set")]
    public class ObjectiveIconSetSO : ScriptableObject
    {
        /// <summary>Resources path, so a HUD needs no per-scene wiring to find the set.</summary>
        public const string ResourcePath = "ObjectiveIconSet";

        [Serializable]
        public struct Entry
        {
            [Tooltip("The scoring metric this glyph stands for.")]
            public ScoringMetric metric;

            [Tooltip("Pure-white line-weight glyph; the reader tints it.")]
            public Sprite icon;
        }

        [Tooltip("One entry per ScoringMetric. A metric with no entry draws nothing rather than " +
                 "the wrong thing - blank is the honest state.")]
        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        static ObjectiveIconSetSO _cached;

        /// <summary>
        /// The shipped set. Cached, so the per-frame-free readout costs one Resources.Load per
        /// session. Null when the asset is missing, which every caller treats as "draw nothing".
        /// </summary>
        public static ObjectiveIconSetSO Load()
        {
            if (_cached == null) _cached = Resources.Load<ObjectiveIconSetSO>(ResourcePath);
            return _cached;
        }

        /// <summary>The glyph for a metric, or null when the set does not author one.</summary>
        public Sprite For(ScoringMetric metric)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].metric == metric) return entries[i].icon;
            return null;
        }
    }
}
