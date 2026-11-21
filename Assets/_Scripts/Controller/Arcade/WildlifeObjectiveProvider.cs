using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Wildlife Liberation: the nearest living creature in the arena cell.
    ///
    /// The mode scores exactly one thing - an attributed creature death
    /// (<c>ScoringMetric.LifeformsKilled</c>) - so the arrow names the nearest thing that can
    /// become one. Three concentric cages split the arena into rooms and the wildlife roams all
    /// of them, so "which way is the nearest quarry" is the question a pilot in an emptied room
    /// cannot answer by looking.
    ///
    /// <para><b>No domain filter, on purpose.</b> Wildlife is quarry whatever colour it wears
    /// (WILDLIFE_LIBERATION.md: body-prism kills never cared about colour, and the skyburst's
    /// creature kill is authored <c>sparesOwnDomain: 0</c>). Fauna spawn in the cell's
    /// controlling colour, so a domain filter would hide every creature from whichever team
    /// shares it - the <see cref="BendsObjectiveProvider"/> rule would be exactly wrong here.</para>
    ///
    /// <para><b>A corpse is not quarry.</b> <see cref="Fauna.IsDying"/> is set at the top of the
    /// sealed death and stays set for the whole wither, during which the heart is still embedded
    /// - the same trap that once double-credited a kill. Dying creatures are skipped.</para>
    ///
    /// <para><b>Multiplayer.</b> Fauna are client-local (no NetworkObject; every peer simulates
    /// its own swarm and a client's kill is reported to the server by RPC). The arrow is
    /// therefore built from THIS peer's <see cref="Cell.LiveFauna"/> registry - the very
    /// creatures this pilot can see and shoot - and reads no server-side state.</para>
    ///
    /// <para>Points at the creature's living heart when it has one (it rides the body), else at
    /// the creature's root. Rescans on a short clock rather than every frame: the registry can
    /// hold hundreds of creatures at the low intensities, and creatures move slowly relative to a
    /// Sparrow, so a quarter-second-old choice is still the right one. Between scans the cached
    /// transform is followed live, so the arrow tracks a moving target.</para>
    /// </summary>
    public class WildlifeObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Header("Dependencies")]
        [Inject] GameDataSO gameData;

        const float RescanSeconds = 0.25f;

        Cell _cell;
        Fauna _cachedFauna;
        Transform _cachedTarget;
        float _nextScan;
        bool _hadTarget;

        static readonly ProfilerMarker s_TryGetObjectiveMarker =
            new("WildlifeObjectiveProvider.TryGetObjective");
        static readonly ProfilerMarker s_RecomputeTargetMarker =
            new("WildlifeObjectiveProvider.RecomputeTarget");

        public bool TryGetObjective(out Transform target)
        {
            using (s_TryGetObjectiveMarker.Auto())
            {
                target = null;

                if (gameData == null) return false;
                var vesselTf = gameData.LocalPlayer?.Vessel?.Transform;
                if (vesselTf == null) return false;

                // Force an early rescan the moment the creature we were naming dies or is gone.
                // Only when there WAS one: an empty arena waits for the clock instead of
                // rescanning every frame.
                bool lost = _hadTarget && (!_cachedFauna || _cachedFauna.IsDying || !_cachedTarget);
                if (lost || Time.time >= _nextScan)
                {
                    _nextScan = Time.time + RescanSeconds;
                    RecomputeTarget(vesselTf.position);
                }

                target = _cachedTarget;
                return target != null;
            }
        }

        void RecomputeTarget(Vector3 origin)
        {
            using (s_RecomputeTargetMarker.Auto())
            {
                _cachedFauna = null;
                _cachedTarget = null;
                _hadTarget = false;

                // Re-resolve when the cached cell is gone (scene reload for replay).
                if (!_cell) _cell = Cell.FindNearestActiveCell(origin, sceneCellsOnly: true);
                if (!_cell) return;

                var fauna = _cell.LiveFauna;
                float bestSqr = float.MaxValue;
                Fauna best = null;
                Transform bestTf = null;

                for (int i = 0; i < fauna.Count; i++)
                {
                    var f = fauna[i];
                    if (!f || f.IsDying || !f.isActiveAndEnabled) continue;

                    var heart = f.LivingHeart;
                    var tf = heart ? heart.transform : f.transform;
                    float sqr = (tf.position - origin).sqrMagnitude;
                    if (sqr >= bestSqr) continue;
                    bestSqr = sqr;
                    best = f;
                    bestTf = tf;
                }

                _cachedFauna = best;
                _cachedTarget = bestTf;
                _hadTarget = best != null;
            }
        }
    }
}
