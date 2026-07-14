using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The per-vessel state home for the elemental ability system: quantitative multipliers and
    /// level-threshold qualitative unlocks, driven by this vessel's own ResourceSystem levels and
    /// configured by the vessel class's ElementalAbilityMapSO (Resources/ElementalAbilityMaps/).
    ///
    /// Lives on the vessel root; created lazily via VesselStatus.ElementalAbilityHandler
    /// (the ResourceSystem GetOrAdd pattern) so no prefab wiring is required. State must live
    /// here and never on the shared ShipActionSO assets (multiplayer: last-initializer-wins).
    ///
    /// Executors read <see cref="Multiplier"/> at use-time (per-shot / per-frame) and gate
    /// qualitative behavior on <see cref="IsUpgradeActive"/>. AI reaches the same executors with
    /// its own IVesselStatus, so upgrades apply to AI with zero extra work.
    ///
    /// NOTE (multiplayer, Phase 2): unlock state is currently derived from the LOCAL
    /// ResourceSystem, which does not replicate. That is acceptable for quantitative scaling
    /// (actions execute owner-side and their outputs replicate), but outcome-affecting unlocks
    /// (piercing / shielded prisms / domain-sparing explosions) need the replicated unlock bits
    /// on VesselStatus before they ship — see Docs/ElementalAbilitySystem/ARCHITECTURE.md §3.4.
    /// </summary>
    public class R_VesselElementalAbilityHandler : MonoBehaviour
    {
        IVesselStatus _status;
        ResourceSystem _resources;
        ElementalAbilityMapSO _map;
        bool _initialized;

        readonly Dictionary<Element, bool> _unlocked = new();

        /// <summary>Raised when an element's qualitative upgrade turns on or off.</summary>
        public event Action<Element, bool> OnUpgradeStateChanged;

        static readonly Element[] AllElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        public ElementalAbilityMapSO Map => _map;

        /// <summary>Idempotent — safe to call again on vessel swap / re-init.</summary>
        public void Initialize(IVesselStatus status)
        {
            if (status == null) return;

            Detach();

            _status = status;
            _resources = status.ResourceSystem;
            _map = ElementalAbilityMapSO.LoadFor(status.VesselType);
            _initialized = true;

            if (_map == null || _resources == null) return;

            _resources.OnElementLevelChange += HandleElementLevelChanged;

            // Seed unlock state from current levels (no event for already-crossed thresholds).
            foreach (var element in AllElements)
                HandleElementLevelChanged(element, _resources.GetLevel(element));
        }

        void OnDestroy() => Detach();

        void Detach()
        {
            if (_resources != null)
                _resources.OnElementLevelChange -= HandleElementLevelChanged;
            _unlocked.Clear();
            _initialized = false;
        }

        /// <summary>
        /// Quantitative multiplier for the ability parameter this element owns. Exactly 1 at the
        /// resting level, at MultiplierAtFullLevel at integer level 10, floored at MinMultiplier.
        /// Returns 1 for unmapped elements / vessels without a map.
        /// </summary>
        public float Multiplier(Element element)
        {
            var entry = _map ? _map.GetEntry(element) : null;
            if (entry == null) return 1f;
            return ElementalScaling.Multiplier(_status, element,
                entry.MultiplierAtFullLevel, entry.MinMultiplier);
        }

        /// <summary>
        /// True while this element's qualitative upgrade is active, per the map's latch policy
        /// (unlock at ≥ UnlockLevel; Relock policy turns off below RelockBelowLevel).
        /// </summary>
        public bool IsUpgradeActive(Element element)
            => _unlocked.TryGetValue(element, out var active) && active;

        public bool IsInitialized => _initialized;

        void HandleElementLevelChanged(Element element, int level)
        {
            var entry = _map ? _map.GetEntry(element) : null;
            if (entry == null) return;

            bool current = IsUpgradeActive(element);
            bool next = current;

            if (!current && level >= entry.UnlockLevel)
                next = true;
            else if (current && entry.LatchPolicy == UnlockLatchPolicy.Relock
                             && level < entry.RelockBelowLevel)
                next = false;

            if (next == current) return;

            _unlocked[element] = next;
            OnUpgradeStateChanged?.Invoke(element, next);
        }
    }
}
