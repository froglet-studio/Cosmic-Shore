using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-authoritative flora PLACEMENT replication for a <see cref="Cell"/>
    /// (Docs/ECOSYSTEM_NETWORK_SYNC.md). It replicates the DECISION and nothing else:
    ///
    ///   - Server: planting decisions run only here (the client planting loops are
    ///     authority-gated). Every planted replicated species becomes a slot in a
    ///     <see cref="NetworkList{T}"/> — species index into the cell's own spawn profile,
    ///     root pose, domain, element — and flips to Withered when the plant dies.
    ///   - Client: plants the SAME species at the SAME pose in the SAME domain carrying the
    ///     SAME element, then GROWS IT LOCALLY. Late joiners read the whole list on connect
    ///     (NetworkList initial sync), so they reconstruct the standing population without the
    ///     host's world ever being torn down.
    ///
    /// <b>The fidelity contract is deliberate and is the whole design.</b> Same species, same
    /// place, same domain, same element — NOT the same shape. Growth consults the LOCAL
    /// <c>PrismSpatialIndex</c> (it reserves against this peer's own occupancy, which includes
    /// this peer's own trails), so a plant's form is emergent per peer by construction; a shared
    /// seed could not make two peers' forests identical and is not attempted. That is also why
    /// there is no growth mirror on the wire: a plant is a growth RULE, and replicating the rule's
    /// output would cost continuously for a fidelity nobody asked for.
    ///
    /// Flora carry NO NetworkObject of their own — a forest is thousands of plants and one
    /// NetworkObject each is not affordable, whereas one slot list on the cell is a few bytes per
    /// plant, paid once at planting. That is the structural difference from
    /// <see cref="FaunaNetworkSync"/>, where the creatures MOVE and each needs its own transform
    /// stream.
    ///
    /// Like <see cref="CellNetworkSync"/>, this component is OPTIONAL and inert when never
    /// network-spawned: offline scenes, tool scenes and the freestyle toys behave exactly as
    /// before.
    /// </summary>
    [RequireComponent(typeof(Cell))]
    public class FloraNetworkSync : NetworkBehaviour
    {
        public enum SlotState : byte
        {
            Empty = 0,
            Alive = 1,
            Withered = 2,
        }

        /// <summary>
        /// One planting DECISION. Everything a peer needs to stand the same plant in the same
        /// place: which species (an index into the host cell's own spawn profile — a
        /// ScriptableObject reference does not cross the wire, and both peers resolve the same
        /// cell config for a scene), the root pose, the domain and the ELEMENT. The element is
        /// carried for the same reason the fauna identity carries it: a lifeform is its species
        /// and its element, and the element decides the heart's size, which IS the collect
        /// reward (Docs/ECOSYSTEM.md §40).
        /// </summary>
        public struct FloraSlotData : INetworkSerializable, System.IEquatable<FloraSlotData>
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public sbyte Species;       // index into SpawnProfile.SupportedFloras
            public sbyte PaletteIndex;  // index into that config's ElementPalette; -1 = its own Variant
            public byte Domain;
            public byte Element;
            public byte State;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Position);
                serializer.SerializeValue(ref Rotation);
                serializer.SerializeValue(ref Species);
                serializer.SerializeValue(ref PaletteIndex);
                serializer.SerializeValue(ref Domain);
                serializer.SerializeValue(ref Element);
                serializer.SerializeValue(ref State);
            }

            public bool Equals(FloraSlotData other) =>
                Position == other.Position && Rotation == other.Rotation &&
                Species == other.Species && PaletteIndex == other.PaletteIndex &&
                Domain == other.Domain && Element == other.Element && State == other.State;
        }

        [SerializeField] Cell cell;

        NetworkList<FloraSlotData> _slots;

        // Server: slot index -> the live plant, so a death can find its slot.
        readonly Dictionary<int, Flora> _serverFlora = new();
        // Client: slot index -> the reconstructed local plant.
        readonly Dictionary<int, Flora> _clientFlora = new();

        void Awake()
        {
            if (!cell) cell = GetComponent<Cell>();
            _slots = new NetworkList<FloraSlotData>();
        }

        // ------------------------------------------------------------------
        //  Authority
        // ------------------------------------------------------------------

        /// <summary>
        /// True when THIS peer makes planting decisions. Shares its rule with
        /// <see cref="FaunaNetworkSync.IsSimAuthority"/> rather than restating it — "who
        /// simulates the ecology" is ONE question and a second copy is a second thing to
        /// forget to update.
        /// </summary>
        public static bool IsSimAuthority => FaunaNetworkSync.IsSimAuthority;

        // ------------------------------------------------------------------
        //  Server: planting registration (called from the canonical spawn seam)
        // ------------------------------------------------------------------

        /// <summary>
        /// Registers a freshly planted flora for replication. Safe from ANY caller: no-ops on
        /// clients, offline, on cells without this component, on species that are not rolled out
        /// (<see cref="FloraConfigurationSO.NetworkSynced"/>), and on flora planted outside the
        /// cell's own profile (the freestyle conveyor, the Lifeform Matrix toy) — those stay
        /// peer-local, which is the documented v1 divergence.
        /// </summary>
        public static void ServerOnPlanted(Cell host, FloraConfigurationSO config, Flora instance)
        {
            if (!host || !config || !instance) return;
            if (!config.NetworkSynced) return;
            if (!host.TryGetComponent(out FloraNetworkSync sync)) return;
            sync.ServerRegister(config, instance);
        }

        void ServerRegister(FloraConfigurationSO config, Flora instance)
        {
            if (!IsSpawned || !IsServer || _slots == null) return;

            int species = ResolveSpeciesIndex(config);
            if (species < 0 || species > sbyte.MaxValue) return;

            var slot = new FloraSlotData
            {
                Position = instance.transform.position,
                Rotation = instance.transform.rotation,
                Species = (sbyte)species,
                PaletteIndex = (sbyte)ResolvePaletteIndex(config, instance.VariantTuningForReplication),
                Domain = (byte)instance.domain,
                Element = (byte)instance.Element,
                State = (byte)SlotState.Alive,
            };

            // Reuse a spent slot so an hours-long session cannot grow the list — and therefore
            // the late-join payload — without bound.
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].State == (byte)SlotState.Alive) continue;
                _slots[i] = slot;
                _serverFlora[i] = instance;
                return;
            }

            _serverFlora[_slots.Count] = instance;
            _slots.Add(slot);
        }

        /// <summary>
        /// Marks a plant's slot spent so clients wither their copy. Called from the flora death
        /// path; a plant that was never registered (unreplicated species, offline) is a no-op.
        /// </summary>
        public static void ServerOnDied(Cell host, Flora instance)
        {
            if (!host || !instance) return;
            if (!host.TryGetComponent(out FloraNetworkSync sync)) return;
            sync.ServerUnregister(instance);
        }

        void ServerUnregister(Flora instance)
        {
            if (!IsSpawned || !IsServer || _slots == null) return;

            foreach (var pair in _serverFlora)
            {
                if (pair.Value != instance) continue;
                int i = pair.Key;
                _serverFlora.Remove(i);
                if (i < 0 || i >= _slots.Count) return;
                var slot = _slots[i];
                slot.State = (byte)SlotState.Withered;
                _slots[i] = slot;
                return;
            }
        }

        // ------------------------------------------------------------------
        //  Client: reconstruct
        // ------------------------------------------------------------------

        public override void OnNetworkSpawn()
        {
            if (_slots != null) _slots.OnListChanged += OnSlotsChanged;

            // Late join: OnListChanged does NOT fire for entries that existed before this client
            // subscribed, so read the standing population out of the list by hand. This is what
            // lets a joiner see the host's forest without the host's world being reset.
            if (!IsServer) SyncExistingSlots();
        }

        public override void OnNetworkDespawn()
        {
            if (_slots != null) _slots.OnListChanged -= OnSlotsChanged;
        }

        void SyncExistingSlots()
        {
            if (_slots == null) return;
            for (int i = 0; i < _slots.Count; i++) ApplySlot(i, _slots[i]);
        }

        void OnSlotsChanged(NetworkListEvent<FloraSlotData> e)
        {
            if (IsServer) return;
            switch (e.Type)
            {
                case NetworkListEvent<FloraSlotData>.EventType.Add:
                case NetworkListEvent<FloraSlotData>.EventType.Insert:
                case NetworkListEvent<FloraSlotData>.EventType.Value:
                    ApplySlot(e.Index, e.Value);
                    break;
                case NetworkListEvent<FloraSlotData>.EventType.Clear:
                    _clientFlora.Clear();
                    break;
            }
        }

        void ApplySlot(int index, FloraSlotData slot)
        {
            if (slot.State == (byte)SlotState.Alive)
            {
                if (_clientFlora.TryGetValue(index, out var live) && live) return; // already standing
                var planted = PlantFromSlot(slot);
                if (planted) _clientFlora[index] = planted;
                return;
            }

            // Spent slot: wither this peer's copy through the plant's own death path, so the
            // crystal drops and the spindles evaporate locally (continuity + mass conservation
            // hold per peer). The slot may then be REUSED for a different plant, so drop the
            // mapping either way.
            if (_clientFlora.TryGetValue(index, out var dying))
            {
                _clientFlora.Remove(index);
                if (dying) dying.KillReplicated();
            }
        }

        Flora PlantFromSlot(FloraSlotData slot)
        {
            var config = ResolveConfig(slot.Species);
            if (!config || !config.FloraPrefab) return null;

            var paletteSibling = ResolvePaletteConfig(config, slot.PaletteIndex);
            var tuning = paletteSibling ? paletteSibling.Variant : config.Variant;
            var pick = new LifeformVariantPick<FloraVariantTuning>((Element)slot.Element, tuning);

            // The ordinary planting path, with three things pinned instead of rolled: the DOMAIN
            // (domainOverride), the POSE (spawnPosition/spawnRotation, which Flora.Plant honours
            // in place of its own dispersal), and the ELEMENT (the inherit pick, which
            // RollVariant returns verbatim). Everything after that — the growth rule, the
            // spatial reservations, the shape — is this peer's own.
            return CellLifeSpawnerBase.SpawnFlora(
                cell, config.FloraPrefab, excludedDomain: null, config: config,
                spawnPosition: slot.Position, spawnUp: null, spawnRotation: slot.Rotation,
                inherit: pick, preInitialize: null,
                domainOverride: (Domains)slot.Domain,
                fromReplication: true);
        }

        // ------------------------------------------------------------------
        //  Species resolution
        // ------------------------------------------------------------------

        SpawnProfileSO Profile => cell && cell.Config ? cell.Config.SpawnProfile : null;

        int ResolveSpeciesIndex(FloraConfigurationSO config)
        {
            var profile = Profile;
            return profile?.SupportedFloras == null ? -1 : profile.SupportedFloras.IndexOf(config);
        }

        FloraConfigurationSO ResolveConfig(int index)
        {
            var profile = Profile;
            if (profile?.SupportedFloras == null) return null;
            return index >= 0 && index < profile.SupportedFloras.Count ? profile.SupportedFloras[index] : null;
        }

        static int ResolvePaletteIndex(FloraConfigurationSO config, FloraVariantTuning tuning)
        {
            if (tuning == null || config == null) return -1;
            if (ReferenceEquals(tuning, config.Variant)) return -1;

            var palette = config.ElementPalette;
            if (palette == null) return -1;
            for (int i = 0; i < palette.Count && i <= sbyte.MaxValue; i++)
            {
                var sibling = palette[i];
                if (sibling && ReferenceEquals(sibling.Variant, tuning)) return i;
            }
            return -1;
        }

        static FloraConfigurationSO ResolvePaletteConfig(FloraConfigurationSO config, int paletteIndex)
        {
            if (!config || paletteIndex < 0) return null;
            var palette = config.ElementPalette;
            if (palette == null || paletteIndex >= palette.Count) return null;
            return palette[paletteIndex];
        }
    }
}
