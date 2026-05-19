using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for HexRace: the local player's next crystal — the
    /// closest live <see cref="Crystal"/> to the player's vessel.
    ///
    /// Event-driven: the closest-crystal scan runs on demand (initial call +
    /// each <see cref="ElementalCrystalImpactor.OnCrystalCollected"/> event +
    /// whenever the cached target becomes null or exploding). Steady-state
    /// <see cref="TryGetObjective"/> is an O(1) cache lookup — no per-frame
    /// FindObjects, no per-frame allocation, no per-frame scan over crystals.
    /// </summary>
    public class HexRaceObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Header("Dependencies")]
        [Inject] GameDataSO gameData;

        Transform _cachedTarget;
        Crystal _cachedCrystal;
        bool _dirty = true;
        bool _subscribed;

        static readonly ProfilerMarker s_TryGetObjectiveMarker =
            new("HexRaceObjectiveProvider.TryGetObjective");
        static readonly ProfilerMarker s_RecomputeTargetMarker =
            new("HexRaceObjectiveProvider.RecomputeTarget");

        void OnEnable()
        {
            ElementalCrystalImpactor.OnCrystalCollected += HandleCrystalCollected;
            _subscribed = true;
            _dirty = true;
        }

        void OnDisable()
        {
            if (_subscribed)
            {
                ElementalCrystalImpactor.OnCrystalCollected -= HandleCrystalCollected;
                _subscribed = false;
            }
        }

        void HandleCrystalCollected(string _) => _dirty = true;

        public bool TryGetObjective(out Transform target)
        {
            using (s_TryGetObjectiveMarker.Auto())
            {
                // The cached crystal can become invalid between collection
                // events: collected by something other than the local skimmer,
                // explicitly destroyed, or already exploding mid-animation.
                // Detect those and force a recompute without waiting for an
                // event.
                if (_cachedCrystal == null || _cachedCrystal.IsExploding)
                    _dirty = true;

                if (_dirty)
                    RecomputeTarget();

                target = _cachedTarget;
                return target != null;
            }
        }

        void RecomputeTarget()
        {
            using (s_RecomputeTargetMarker.Auto())
            {
                _dirty = false;
                _cachedTarget = null;
                _cachedCrystal = null;

                if (gameData == null) return;

                var localVessel = gameData.LocalPlayer?.Vessel;
                if (localVessel == null) return;

                var crystals = FindObjectsByType<Crystal>(FindObjectsSortMode.None);
                if (crystals == null || crystals.Length == 0) return;

                var origin = localVessel.Transform.position;
                float bestSqr = float.MaxValue;
                Crystal best = null;

                for (int i = 0; i < crystals.Length; i++)
                {
                    var crystal = crystals[i];
                    if (crystal == null || crystal.IsExploding) continue;

                    float sqr = (crystal.transform.position - origin).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = crystal;
                    }
                }

                if (best != null)
                {
                    _cachedCrystal = best;
                    _cachedTarget = best.transform;
                }
            }
        }
    }
}
