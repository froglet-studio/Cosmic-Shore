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
        R_VesselActionHandler _netActions;
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
            _netActions = status.ActionHandler;
            _initialized = true;

            // Remote peers learn unlock flips through the replicated bits (their local
            // ResourceSystem copies drift and must not gate outcomes).
            if (_netActions)
                _netActions.NetElementUnlocks.OnValueChanged += HandleNetUnlocksChanged;

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
            if (_netActions)
                _netActions.NetElementUnlocks.OnValueChanged -= HandleNetUnlocksChanged;
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
        /// In a networked session, non-owner peers resolve from the replicated unlock bits so
        /// every machine destroys the same prisms; the owner (and offline play) uses the
        /// locally derived state.
        /// </summary>
        public bool IsUpgradeActive(Element element)
        {
            if (_netActions && _netActions.IsSpawned && !_netActions.IsOwner)
                return (_netActions.NetElementUnlocks.Value & BitFor(element)) != 0;
            return LocalUpgradeActive(element);
        }

        public bool IsInitialized => _initialized;

        bool LocalUpgradeActive(Element element)
            => _unlocked.TryGetValue(element, out var active) && active;

        static byte BitFor(Element element)
        {
            int i = (int)element - 1; // Charge=1 → bit 0 … Time=4 → bit 3
            return i is >= 0 and < 4 ? (byte)(1 << i) : (byte)0;
        }

        void HandleElementLevelChanged(Element element, int level)
        {
            var entry = _map ? _map.GetEntry(element) : null;
            if (entry == null) return;

            bool current = LocalUpgradeActive(element);
            bool next = current;

            if (!current && level >= entry.UnlockLevel)
                next = true;
            else if (current && entry.LatchPolicy == UnlockLatchPolicy.Relock
                             && level < entry.RelockBelowLevel)
                next = false;

            if (next == current) return;

            _unlocked[element] = next;

            // Owner publishes the flip so remote peers (whose element levels drift) resolve
            // outcome-affecting upgrades identically. Single-writer: only the owner writes.
            if (_netActions && _netActions.IsSpawned && _netActions.IsOwner)
            {
                byte bits = _netActions.NetElementUnlocks.Value;
                var bit = BitFor(element);
                _netActions.NetElementUnlocks.Value =
                    next ? (byte)(bits | bit) : (byte)(bits & ~bit);
            }

            OnUpgradeStateChanged?.Invoke(element, next);
        }

        void HandleNetUnlocksChanged(byte previous, byte current)
        {
            // The owner already raised its event from the local derivation; this relay is for
            // remote peers' HUD/VFX so unlock flips are visible everywhere.
            if (_netActions && _netActions.IsOwner) return;

            byte changed = (byte)(previous ^ current);
            foreach (var element in AllElements)
            {
                var bit = BitFor(element);
                if ((changed & bit) == 0) continue;
                OnUpgradeStateChanged?.Invoke(element, (current & bit) != 0);
            }
        }
    }
}
