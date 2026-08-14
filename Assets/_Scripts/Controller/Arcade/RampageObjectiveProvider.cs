using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Rampage: the arena's nearest contested OMNI crystal, and nothing
    /// else, ever.
    ///
    /// Rampage is the one mode where the crystal is not a pickup but a TRIGGER - a Dolphin banks
    /// skim energy in the forest and the crystal is the only thing that discharges it as the jaw
    /// blast (DOLPHIN_ENERGY_ECONOMY.md §1). How many are in the arena IS the mode's intensity
    /// (twice the roster at 1, exactly one at 4 - see RAMPAGE.md), so "where is the nearest one
    /// right now" is the question the whole match is played around, and it is the one thing a
    /// pilot deep in a cactus thicket cannot answer by looking.
    ///
    /// <para><b>Why the filter is the whole point.</b> <see cref="Crystal.Active"/> holds EVERY
    /// live crystal, and this arena is full of ones that are not the objective:</para>
    /// <list type="bullet">
    ///   <item><b>Lifeform hearts.</b> Every flora and fauna carries one and drops it on death
    ///   (the every-lifeform-drops-a-crystal invariant), and this mode's whole verb is killing
    ///   flora - so the arena is constantly raining elemental crystals.</item>
    ///   <item><b>Seeded TEAM crystals.</b> The Dolphin's Charge ability plants one every 30 s,
    ///   and only its own domain may collect it.</item>
    /// </list>
    /// <para>A nearest-live-crystal scan would therefore spend the match swinging onto whatever
    /// cactus just died two hundred units away, which is worse than no arrow: it actively points
    /// the pilot away from the thing they are racing for.</para>
    ///
    /// <para>The discriminator is <see cref="Crystal.CrystalManager"/>, set ONLY by
    /// <see cref="CrystalManager.SpawnWithDomain"/>. A manager-spawned crystal is the arena's -
    /// it respawns inside the nucleus forever and is replicated to every peer. Hearts and seeded
    /// crystals are plain <c>Instantiate</c>s and carry no manager, so one test separates them
    /// all. <see cref="Crystal.CanBeCollected"/> is then applied so that if a future variant ever
    /// spawns per-domain managed crystals (as HexRace does), the arrow still only names one this
    /// pilot may actually take.</para>
    ///
    /// Deliberately NOT <see cref="HexRaceObjectiveProvider"/>, which filters to the local
    /// player's own DOMAIN: Rampage's crystal is neutral (<c>spawnCrystalWithPlayerDomain: 0</c> ⇒
    /// <c>Domains.Blue</c>) so a strict domain-equality test would reject the only objective in
    /// the match and the arrow would never appear at all.
    ///
    /// Event-driven: the scan runs on demand (initial call + each
    /// <see cref="ElementalCrystalImpactor.OnCrystalCollected"/> + whenever the cached target goes
    /// null), steady-state <see cref="TryGetObjective"/> is an O(1) cache read, and a recompute
    /// walks the in-memory registry - never a FindObjectsByType scene scan, never a per-frame
    /// allocation.
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
                // Only re-scan when the crystal we were naming is GONE. A collection does not
                // qualify: the manager moves the same Crystal object to its new spot
                // (CrystalManager.UpdateCrystalPos), so the cached transform is already the
                // right answer and the arrow follows it there. Deliberately NOT dirtying on
                // Crystal.IsExploding - that flag stays true for 0.5 s AFTER the respawn has
                // repositioned the crystal, so honouring it would blank the arrow for half a
                // second while the crystal sits at the very place it is pointing to.
                if (_cachedCrystal == null)
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

                var localPlayer = gameData != null ? gameData.LocalPlayer : null;
                var localTransform = localPlayer?.Vessel?.Transform;
                var localDomain = localPlayer?.Domain ?? Domains.Blue;

                float bestSqr = float.MaxValue;
                Crystal best = null;

                for (int i = 0; i < count; i++)
                {
                    var crystal = crystals[i];
                    if (crystal == null) continue;

                    // THE filter: manager-spawned only. Excludes every lifeform heart the food
                    // web drops and every team crystal a Dolphin seeds.
                    if (crystal.CrystalManager == null) continue;

                    // And only one this pilot could actually collect.
                    if (!crystal.CanBeCollected(localDomain)) continue;

                    // No local vessel yet (spawn chain still running): any managed crystal is a
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
