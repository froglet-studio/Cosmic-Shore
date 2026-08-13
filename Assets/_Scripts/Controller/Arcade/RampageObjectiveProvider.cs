using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Rampage: the arena's single contested crystal.
    ///
    /// Rampage is the one mode where the crystal is not a pickup but a TRIGGER - a Dolphin banks
    /// skim energy in the forest and the crystal is the only thing that discharges it as the jaw
    /// blast (DOLPHIN_ENERGY_ECONOMY.md §1). There is exactly one in the arena, so "where is it
    /// right now" is the question the whole match is played around, and it is the one thing a
    /// pilot deep in a cactus thicket cannot answer by looking.
    ///
    /// Deliberately NOT <see cref="HexRaceObjectiveProvider"/>, which filters to crystals in the
    /// local player's own domain. HexRace gives every player their own crystal; Rampage spawns
    /// ONE neutral crystal (<c>spawnCrystalWithPlayerDomain: 0</c> ⇒ <c>Domains.Blue</c>) that
    /// everybody may collect, so a domain filter here would reject the only objective in the
    /// match and the arrow would never appear at all.
    ///
    /// Event-driven, same shape as the HexRace provider: the scan runs on demand (initial call +
    /// each <see cref="ElementalCrystalImpactor.OnCrystalCollected"/> + whenever the cached
    /// target goes null or starts exploding), steady-state <see cref="TryGetObjective"/> is an
    /// O(1) cache read, and a recompute walks the in-memory <see cref="Crystal.Active"/> registry
    /// - never a FindObjectsByType scene scan, never a per-frame allocation.
    /// </summary>
    public class RampageObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Header("Dependencies")]
        [Inject] GameDataSO gameData;

        Transform _cachedTarget;
        Crystal _cachedCrystal;
        bool _dirty = true;
        bool _subscribed;

        static readonly ProfilerMarker s_TryGetObjectiveMarker =
            new("RampageObjectiveProvider.TryGetObjective");
        static readonly ProfilerMarker s_RecomputeTargetMarker =
            new("RampageObjectiveProvider.RecomputeTarget");

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
                // The cached crystal can go invalid between collection events: destroyed on
                // respawn, or already exploding mid-animation. Recompute without waiting for
                // the event in both cases.
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

                var crystals = Crystal.Active;
                int count = crystals.Count;
                if (count == 0) return;

                // Nearest live crystal. The arena authors exactly one, so in practice this is a
                // one-element walk; scanning for the nearest keeps the provider honest if a
                // future intensity ever seeds more.
                var localVessel = gameData != null ? gameData.LocalPlayer?.Vessel : null;
                var localTransform = localVessel?.Transform;

                float bestSqr = float.MaxValue;
                Crystal best = null;

                for (int i = 0; i < count; i++)
                {
                    var crystal = crystals[i];
                    if (crystal == null || crystal.IsExploding) continue;

                    // No local vessel yet (spawn chain still running): any live crystal is a
                    // better answer than hiding the arrow.
                    if (localTransform == null)
                    {
                        best = crystal;
                        break;
                    }

                    float sqr = (crystal.transform.position - localTransform.position).sqrMagnitude;
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
