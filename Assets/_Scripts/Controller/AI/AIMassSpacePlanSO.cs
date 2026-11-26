using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Each hull's Mass-Space AI (Docs/AI_SYSTEM/AI_PLAN.md, plan steps <c>mass-arcade</c> and <c>space-arcade</c>):
    /// the ability cycle and steering its autopilot flies with in a Mass-Space mode (its own Mass and Space games
    /// and Brood Rush: <see cref="ModePreviewLibrarySO.CategoryOf"/> is <see cref="ArcadeCategory.MassSpace"/>).
    /// Every other mode keeps the prefab's own <c>AIPilot</c> values, so a Time or Charge AI never changes here.
    /// <para>
    /// Learned, never set by hand: <c>Tools/Compute/evaluators/massspace_ai_search.py</c> searches each hull in its
    /// OWN Mass and Space games on the volume books (never in an arena game), and
    /// <c>Tools/Build/massspace_ai_plan.py</c> writes the winners here (<c>--check</c> is the drift gate).
    /// </para>
    /// <para>
    /// A row replaces the prefab's ability list (the cycler presses each entry for Duration, then waits Cooldown)
    /// and scales the autopilot's aggressiveness and throttle (both ends of the skill lerp, so the intensity
    /// ladder stays). A hull with no row flies exactly as authored.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "AIMassSpacePlan", menuName = "ScriptableObjects/AI/Mass-Space AI Plan")]
    public class AIMassSpacePlanSO : ScriptableObject
    {
        public const string ResourcePath = "AIMassSpacePlan";

        [Serializable]
        public class Row
        {
            [Tooltip("The hull this AI flies.")]
            public VesselClassType Hull;

            [Tooltip("The ability cycle in a Mass-Space mode, replacing the prefab's AIPilot list (empty = press nothing).")]
            public List<AIAbility> Abilities = new();

            [Tooltip("Multiplies the AIPilot's defaultAggressivenessHigh and Low (how hard it turns onto its target).")]
            [Min(0f)] public float AggressivenessScale = 1f;

            [Tooltip("Multiplies the AIPilot's defaultThrottleHigh and Low (each capped at 1).")]
            [Min(0f)] public float ThrottleScale = 1f;
        }

        [SerializeField] List<Row> rows = new();

        public IReadOnlyList<Row> Rows => rows;

        static AIMassSpacePlanSO _cached;
        static bool _looked;

        /// <summary>The hull's row when <paramref name="mode"/> is a Mass-Space mode and the plan has one.</summary>
        public static bool TryGetRow(VesselClassType hull, GameModes mode, out Row row)
        {
            row = null;
            if (ModePreviewLibrarySO.CategoryOf(mode) != ArcadeCategory.MassSpace) return false;
            if (!_looked)
            {
                _cached = Resources.Load<AIMassSpacePlanSO>(ResourcePath);
                _looked = true;
            }
            if (!_cached) return false;
            foreach (var r in _cached.rows)
                if (r != null && r.Hull == hull) { row = r; return true; }
            return false;
        }
    }
}
