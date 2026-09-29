using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for SkimRace: the local player's next crystal - the
    /// closest live <see cref="Crystal"/> in the local player's own domain.
    /// SkimRace gives every player a crystal in their own domain, so the
    /// other-domain crystals belonging to AI opponents are never a valid
    /// objective for the local player and must be skipped.
    ///
    /// Event-driven: the closest-crystal scan runs on demand (initial call +
    /// each <see cref="ElementalCrystalImpactor.OnCrystalCollected"/> event +
    /// whenever the cached target becomes null or exploding). Steady-state
    /// <see cref="TryGetObjective"/> is an O(1) cache lookup, and a recompute
    /// iterates the in-memory <see cref="Crystal.Active"/> registry - never a
    /// FindObjectsByType scene scan and never a per-frame allocation.
    /// </summary>
    public class SkimRaceObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Header("Dependencies")]
        [Inject] GameDataSO gameData;

        Transform _cachedTarget;
        Crystal _cachedCrystal;
        bool _dirty = true;
        bool _subscribed;

        static readonly ProfilerMarker s_TryGetObjectiveMarker =
            new("SkimRaceObjectiveProvider.TryGetObjective");
        static readonly ProfilerMarker s_RecomputeTargetMarker =
            new("SkimRaceObjectiveProvider.RecomputeTarget");

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
                // events: explicitly destroyed, or already exploding
                // mid-animation. Detect those and force a recompute without
                // waiting for an event.
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

                var localPlayer = gameData.LocalPlayer;
                var localVessel = localPlayer?.Vessel;
                if (localVessel == null) return;

                // Iterate the live-crystal registry instead of FindObjectsByType. The scene
                // scan cost 19-25ms on every burst frame: ObjectiveIndicator polls this each
                // frame and re-dirties while the cached crystal is exploding, so a collection
                // burst triggered a full scene scan + array alloc every frame.
                var crystals = Crystal.Active;
                int count = crystals.Count;
                if (count == 0) return;

                // SkimRace gives every player a crystal in their own domain, so
                // only a crystal matching the local player's domain is a valid
                // objective. Without this filter the closest crystal is often
                // an AI opponent's, and the indicator hooks onto a crystal the
                // local player can neither reach nor collect.
                var localDomain = localPlayer.Domain;
                var origin = localVessel.Transform.position;
                float bestSqr = float.MaxValue;
                Crystal best = null;

                for (int i = 0; i < count; i++)
                {
                    var crystal = crystals[i];
                    if (crystal == null || crystal.IsExploding) continue;
                    if (crystal.ownDomain != localDomain) continue;

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
