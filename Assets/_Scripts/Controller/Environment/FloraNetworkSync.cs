using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-authoritative flora replication for a <see cref="Cell"/> — Option B of
    /// Docs/ECOSYSTEM_NETWORK_SYNC.md (snapshot + replicated decisions; the host's
    /// world is NEVER destroyed on a join):
    ///
    ///   - Server: planting decisions run only here (the spawner loops are
    ///     authority-gated). Every planted profile flora becomes a slot in a
    ///     NetworkList — species index (into the cell config's SupportedFloras),
    ///     root position/rotation, domain — and a low-cadence mirror keeps each
    ///     slot's GrowthTicks fresh and flips it to Withered when the flora dies.
    ///   - Client: plants the SAME species at the SAME pose with the SAME domain
    ///     (Flora.UseAuthoredPlacement skips the local random dispersal), then
    ///     fast-forwards growth to the mirrored tick count as a paced bloom-in.
    ///     Late joiners read the whole list on connect (NetworkList initial sync),
    ///     so they reconstruct the cell's current flora population — no reset.
    ///   - Death: a Withered slot runs the same LifeForm death path locally
    ///     (crystal drop + spindle wither) — continuity + mass conservation on
    ///     every peer.
    ///
    /// Fidelity contract (deliberate, documented): structures match in species,
    /// place, domain, and approximate SIZE — not byte-identical shape. Growth
    /// consults the LOCAL spatial index (TryReserve against local occupancy,
    /// which includes client-local trails), so shape is emergent per peer by
    /// construction; a shared seed cannot fix that and is not attempted.
    ///
    /// Like <see cref="CellNetworkSync"/>/<see cref="FaunaNetworkSync"/>, this
    /// component is OPTIONAL and inert when never network-spawned — offline and
    /// tool scenes behave exactly as before.
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

        [SerializeField] Cell cell;

        [Tooltip("Server-side mirror interval for growth ticks + death flags. Lower = " +
                 "clients track size/death sooner, higher = less bandwidth.")]
        [Min(0.25f)] [SerializeField] float serverMirrorIntervalSeconds = 2f;

        [Tooltip("Cap on Grow() cycles a client fast-forwards when reconstructing a " +
                 "long-lived plant (one per frame). Bounds worst-case join cost; the " +
                 "live-prism budget and Frenzy gate keep applying during catch-up.")]
        [Min(1)] [SerializeField] int maxCatchUpGrowthTicks = 200;

        NetworkList<FloraSlotData> _slots;

        // Server: slot index -> live instance (removed once mirrored as Withered).
        readonly Dictionary<int, Flora> _serverFlora = new();
        // Client: slot index -> reconstructed local instance.
        readonly Dictionary<int, Flora> _clientFlora = new();
        // Client: events arriving before the cell config is ready are ignored;
        // the catch-up pass reads the final list state once config exists.
        bool _clientReady;

        void Awake()
        {
            if (!cell) cell = GetComponent<Cell>();
            _slots = new NetworkList<FloraSlotData>();
        }

        // ------------------------------------------------------------------
        //  Server: plant registration (called from the canonical spawn seam)
        // ------------------------------------------------------------------

        /// <summary>
        /// Registers a freshly planted flora for replication. Safe from ANY caller:
        /// no-ops on clients, offline, on cells without this component, and for flora
        /// not in the cell profile's SupportedFloras (e.g. conveyor-released flora —
        /// those stay peer-local, documented v1).
        /// </summary>
        public static void ServerOnPlanted(Cell host, Flora prefab, Flora instance)
        {
            if (!host || !prefab || !instance) return;
            if (!host.TryGetComponent(out FloraNetworkSync sync)) return;
            sync.ServerRegister(prefab, instance);
        }

        void ServerRegister(Flora prefab, Flora instance)
        {
            if (!IsSpawned || !IsServer) return;

            int species = ResolveSpeciesIndex(prefab);
            if (species < 0) return;

            var slot = new FloraSlotData
            {
                Species = species,
                Position = instance.transform.position,
                Rotation = instance.transform.rotation,
                Domain = (int)instance.domain,
                GrowthTicks = 0,
                State = (byte)SlotState.Alive,
            };

            // Reuse the first non-alive slot so hours-long sessions don't grow the
            // list (and the late-join payload) without bound.
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].State != (byte)SlotState.Alive)
                {
                    _slots[i] = slot;
                    _serverFlora[i] = instance;
                    return;
                }
            }

            _slots.Add(slot);
            _serverFlora[_slots.Count - 1] = instance;
        }

        int ResolveSpeciesIndex(Flora prefab)
        {
            var profile = cell && cell.Config ? cell.Config.SpawnProfile : null;
            if (!profile || profile.SupportedFloras == null) return -1;
            for (int i = 0; i < profile.SupportedFloras.Count; i++)
            {
                var cfg = profile.SupportedFloras[i];
                if (cfg && cfg.FloraPrefab == prefab) return i;
            }
            return -1;
        }

        // ------------------------------------------------------------------
        //  Lifecycle
        // ------------------------------------------------------------------

        public override void OnNetworkSpawn()
        {
            _slots.OnListChanged += OnSlotsChanged;

            if (IsServer)
                StartCoroutine(ServerMirrorCoroutine());
            else
                StartCoroutine(ClientCatchUpCoroutine());
        }

        public override void OnNetworkDespawn()
        {
            if (_slots != null)
                _slots.OnListChanged -= OnSlotsChanged;
        }

        // ------------------------------------------------------------------
        //  Server: growth/death mirror
        // ------------------------------------------------------------------

        IEnumerator ServerMirrorCoroutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.25f, serverMirrorIntervalSeconds));
            var deadIndices = new List<int>();

            while (true)
            {
                yield return wait;
                if (!IsSpawned) yield break;

                deadIndices.Clear();
                foreach (var pair in _serverFlora)
                {
                    int idx = pair.Key;
                    var flora = pair.Value;
                    if (idx < 0 || idx >= _slots.Count) { deadIndices.Add(idx); continue; }

                    var slot = _slots[idx];
                    if (!flora || flora.IsDying)
                    {
                        slot.State = (byte)SlotState.Withered;
                        _slots[idx] = slot;
                        deadIndices.Add(idx);
                        continue;
                    }

                    ushort ticks = (ushort)Mathf.Clamp(flora.GrowthTicks, 0, ushort.MaxValue);
                    if (slot.GrowthTicks != ticks)
                    {
                        slot.GrowthTicks = ticks;
                        _slots[idx] = slot;
                    }
                }

                for (int i = 0; i < deadIndices.Count; i++)
                    _serverFlora.Remove(deadIndices[i]);
            }
        }

        // ------------------------------------------------------------------
        //  Client: reconstruction
        // ------------------------------------------------------------------

        IEnumerator ClientCatchUpCoroutine()
        {
            // The species table lives on the cell config, which is assigned during the
            // scene's OnInitializeGame flow — a bounded scene-load-window wait (no SOAP
            // event exists for config assignment).
            while (cell == null || !cell.HasConfigAssigned)
                yield return null;

            _clientReady = true;
            for (int i = 0; i < _slots.Count; i++)
                ApplySlot(i, _slots[i]);
        }

        void OnSlotsChanged(NetworkListEvent<FloraSlotData> e)
        {
            if (IsServer || !_clientReady) return;
            if (e.Type != NetworkListEvent<FloraSlotData>.EventType.Add &&
                e.Type != NetworkListEvent<FloraSlotData>.EventType.Insert &&
                e.Type != NetworkListEvent<FloraSlotData>.EventType.Value)
                return;
            if (e.Index < 0 || e.Index >= _slots.Count) return;

            ApplySlot(e.Index, _slots[e.Index]);
        }

        void ApplySlot(int index, FloraSlotData slot)
        {
            if (slot.State == (byte)SlotState.Withered)
            {
                // Run the same death path locally: crystal drop + spindle wither —
                // nothing pops out, mass conserved on this peer too.
                if (_clientFlora.TryGetValue(index, out var dying) && dying)
                    dying.ApplyReplicatedDeath();
                _clientFlora.Remove(index);
                return;
            }

            if (slot.State != (byte)SlotState.Alive) return;

            if (_clientFlora.TryGetValue(index, out var existing) && existing)
            {
                // Growth top-up: converge toward the server's size when the local
                // plant has fallen behind (e.g. differing Frenzy hold windows).
                int deficit = slot.GrowthTicks - existing.GrowthTicks;
                if (deficit > 0)
                    existing.FastForwardGrowth(Mathf.Min(deficit, maxCatchUpGrowthTicks));
                return;
            }

            var prefab = ResolveSpeciesPrefab(slot.Species);
            if (!prefab)
            {
                CSDebug.LogWarning($"[FloraNetworkSync] Cell '{cell.name}': no species prefab for replicated slot {index} (species {slot.Species}).");
                return;
            }

            var flora = Instantiate(prefab, slot.Position, slot.Rotation);
            flora.domain = (Domains)slot.Domain;
            flora.UseAuthoredPlacement = true;
            flora.Initialize(cell);
            flora.FastForwardGrowth(Mathf.Min((int)slot.GrowthTicks, maxCatchUpGrowthTicks));

            CellLifeSpawnerBase.RegisterSpawned(cell, flora.gameObject);
            _clientFlora[index] = flora;
        }

        Flora ResolveSpeciesPrefab(int species)
        {
            var profile = cell && cell.Config ? cell.Config.SpawnProfile : null;
            if (!profile || profile.SupportedFloras == null) return null;
            if (species < 0 || species >= profile.SupportedFloras.Count) return null;
            var cfg = profile.SupportedFloras[species];
            return cfg ? cfg.FloraPrefab : null;
        }
    }

    /// <summary>
    /// Atomic flora slot — one replicated plant decision plus its mirrored growth/death
    /// state, in a single NetworkList entry (the <see cref="CrystalSlotData"/> pattern:
    /// every field a client needs arrives together in one change callback).
    /// </summary>
    public struct FloraSlotData : INetworkSerializable, System.IEquatable<FloraSlotData>
    {
        public int Species;
        public Vector3 Position;
        public Quaternion Rotation;
        public int Domain;
        public ushort GrowthTicks;
        public byte State;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            using (serializer.IsReader
                ? CosmicShore.Utility.PerformanceBenchmark.NetMarkers.Deserialize.Auto()
                : CosmicShore.Utility.PerformanceBenchmark.NetMarkers.Serialize.Auto())
            {
                serializer.SerializeValue(ref Species);
                serializer.SerializeValue(ref Position);
                serializer.SerializeValue(ref Rotation);
                serializer.SerializeValue(ref Domain);
                serializer.SerializeValue(ref GrowthTicks);
                serializer.SerializeValue(ref State);
            }
        }

        public bool Equals(FloraSlotData other) =>
            Species == other.Species &&
            Position.Equals(other.Position) &&
            Rotation.Equals(other.Rotation) &&
            Domain == other.Domain &&
            GrowthTicks == other.GrowthTicks &&
            State == other.State;

        public override bool Equals(object obj) => obj is FloraSlotData other && Equals(other);
        public override int GetHashCode() => Species ^ Position.GetHashCode() ^ (GrowthTicks << 8) ^ State;
    }
}
