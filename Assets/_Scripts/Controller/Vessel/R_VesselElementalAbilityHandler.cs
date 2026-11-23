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
            {
                _netActions.NetElementUnlocks.OnValueChanged += HandleNetUnlocksChanged;
                _netActions.OwnershipHandedOver += AdoptHandover;
            }

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
            {
                _netActions.NetElementUnlocks.OnValueChanged -= HandleNetUnlocksChanged;
                _netActions.OwnershipHandedOver -= AdoptHandover;
            }
            _unlocked.Clear();
            _initialized = false;
        }

        // Multiplier(Element) is REMOVED. It answered "how much does this element scale things?"
        // with no way to say WHICH thing, so it was readable from anywhere and scoped to nothing.
        // A parameter that scales with an element declares an ElementalFloat next to itself and
        // calls EvaluateLive(status). This handler owns the QUALITATIVE half only — IsUpgradeActive,
        // which is legitimately a fact about the element rather than an unlabelled number.

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

        /// <summary>
        /// This vessel's INTEGER level for <paramref name="element"/>, clamped to 0..15, and the
        /// SAME number on every peer: the owner (and offline play) reads its own ResourceSystem,
        /// a remote peer reads the owner's published <c>NetElementLevels</c>. Use this — never a
        /// local <c>GetLevel</c> — wherever an element continuously scales an OUTCOME that other
        /// machines also simulate (a wake's width, a debuff's bite). Integer resolution is the
        /// price of agreement, and it is the resolution the HUD flowers already show.
        /// </summary>
        public int ReplicatedLevel(Element element)
        {
            int nibble = (int)element - 1;
            if (nibble is < 0 or > 3) return 0;
            if (_netActions && _netActions.IsSpawned && !_netActions.IsOwner)
                return (_netActions.NetElementLevels.Value >> (nibble * 4)) & 0xF;
            return _resources ? Mathf.Clamp(_resources.GetLevel(element), 0, 15) : 0;
        }

        // The level events only fire on a CHANGE, and a vessel's first levels are often seeded
        // before its NetworkObject spawns (arena StartingElements land in VesselController
        // .Initialize), when an owner-write is not yet possible. So the owner re-asserts every
        // frame; PublishLevel writes only when a nibble actually differs, so a settled vessel
        // costs four integer compares and no network traffic.
        void LateUpdate()
        {
            if (!_initialized || !_resources || !_netActions
                || !_netActions.IsSpawned || !_netActions.IsOwner) return;
            foreach (var element in AllElements)
                PublishLevel(element, _resources.GetLevel(element));
            PublishBase();
        }

        // The hull's base levels, for whichever machine takes it over next (ElementHandover).
        void PublishBase()
        {
            var local = ElementHandover.Pack(_resources.BaseLevel(Element.Charge), _resources.BaseLevel(Element.Mass),
                                             _resources.BaseLevel(Element.Space), _resources.BaseLevel(Element.Time));
            bool ever = _netActions.NetElementBasePublished.Value;
            if (!ElementHandover.ShouldPublish(_netActions.NetElementBase.Value, local, ever)) return;
            _netActions.NetElementBase.Value = local;
            if (!ever) _netActions.NetElementBasePublished.Value = true;
        }

        /// <summary>
        /// This machine has just taken the hull over (<see cref="R_VesselActionHandler.OwnershipHandedOver"/>): its own copy of
        /// the levels is whatever its replica held, so it adopts the last owner's published base levels and latched unlocks
        /// before it simulates or publishes anything (<see cref="ElementHandover"/>). The unlocks go first, so the level
        /// writes below run the latch from the state the last owner had.
        /// </summary>
        void AdoptHandover()
        {
            if (!_resources || !_netActions || !_netActions.NetElementBasePublished.Value) return;
            byte bits = _netActions.NetElementUnlocks.Value;
            foreach (var element in AllElements)
            {
                bool was = LocalUpgradeActive(element), now = (bits & BitFor(element)) != 0;
                _unlocked[element] = now;
                if (was != now) OnUpgradeStateChanged?.Invoke(element, now);
            }
            var levels = _netActions.NetElementBase.Value;
            _resources.ClearPendingElementalLoss();
            foreach (var element in AllElements)
                _resources.SetElementLevel(element, ElementHandover.Get(levels, element));
        }

        void PublishLevel(Element element, int level)
        {
            if (!_netActions || !_netActions.IsSpawned || !_netActions.IsOwner) return;
            int nibble = (int)element - 1;
            if (nibble is < 0 or > 3) return;
            int shift = nibble * 4;
            int packed = _netActions.NetElementLevels.Value;
            int next = (packed & ~(0xF << shift)) | (Mathf.Clamp(level, 0, 15) << shift);
            if (next != packed) _netActions.NetElementLevels.Value = (ushort)next;
        }

        void HandleElementLevelChanged(Element element, int level)
        {
            // Published BEFORE the map gate below: a level is a fact about the vessel whether or
            // not this element has an upgrade entry.
            PublishLevel(element, level);

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
