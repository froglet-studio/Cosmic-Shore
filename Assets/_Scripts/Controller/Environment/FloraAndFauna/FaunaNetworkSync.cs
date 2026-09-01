using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-authoritative replication for a single <see cref="Fauna"/> creature - the fauna
    /// counterpart of <see cref="CellNetworkSync"/>: replication only, the creature keeps owning
    /// its behavior. (Docs/ECOSYSTEM_NETWORK_SYNC.md)
    ///
    ///   - Server: runs the ONE simulation exactly as today (goals, feeding, starvation,
    ///     predation, reproduction). The spawn seam (<see cref="ServerSpawn"/>) stamps the
    ///     creature's IDENTITY and spawns the NetworkObject; a sibling server-authoritative
    ///     NetworkTransform replicates the swim.
    ///   - Client: on spawn this component puts the sibling Fauna into PUPPET mode
    ///     (<see cref="Fauna.EnterPuppetMode"/> - no goals/steering/starvation/predation/
    ///     reproduction), then rebuilds the SAME individual from the replicated identity:
    ///     same species config, same element, same variant tuning, therefore the same body
    ///     scale and the same heart size. That last one is gameplay, not cosmetics - a heart's
    ///     world scale IS the collect reward and the live domain fauna buff
    ///     (Docs/ECOSYSTEM.md §40), so a client that re-rolled its own element would pay a
    ///     different price for the same kill.
    ///   - Death: the server's sealed <see cref="Fauna.Die"/> flips the replicated life state;
    ///     every peer then runs the same wither-to-crystal path locally (crystal drop +
    ///     extremities-first wither - the continuity law: nothing pops out). The server
    ///     despawns the spent husk only after its own wither plus a small grace, so a client's
    ///     later-starting wither is never clipped.
    ///
    /// This component is OPTIONAL and inert when never network-spawned: offline scenes, tool
    /// scenes, the freestyle toys and manager-spawned fauna behave exactly as before (the
    /// <see cref="CellNetworkSync"/> optional-component philosophy).
    /// </summary>
    [RequireComponent(typeof(Fauna))]
    public class FaunaNetworkSync : NetworkBehaviour
    {
        enum LifeState : byte
        {
            Alive = 0,
            Dying = 1,
        }

        /// <summary>
        /// Everything a client needs to rebuild THIS individual, written once before
        /// <c>NetworkObject.Spawn</c> so the whole identity rides the spawn payload - no
        /// Blue-then-recolor flicker, no wrong-element heart for a frame, and late joiners
        /// read it straight out of the sync pass with no change callback.
        ///
        /// The species is carried as an INDEX into the host cell's own spawn profile rather
        /// than as an asset reference, because a ScriptableObject reference does not cross the
        /// wire: both peers resolve the same <see cref="CellConfigDataSO"/> for a scene (the
        /// intensity that picks it is itself synced through <c>GameDataSO.GameConfigSynced</c>),
        /// so the same index names the same asset.
        /// </summary>
        public struct FaunaIdentity : INetworkSerializable, System.IEquatable<FaunaIdentity>
        {
            public byte Domain;
            /// <summary>Index into the host cell's <c>SpawnProfile.SupportedFaunas</c>; -1 = unlisted.</summary>
            public sbyte ConfigIndex;
            /// <summary>Index into that config's <c>ElementPalette</c>; -1 = the config's own Variant.</summary>
            public sbyte PaletteIndex;
            public byte Element;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Domain);
                serializer.SerializeValue(ref ConfigIndex);
                serializer.SerializeValue(ref PaletteIndex);
                serializer.SerializeValue(ref Element);
            }

            public bool Equals(FaunaIdentity other) =>
                Domain == other.Domain && ConfigIndex == other.ConfigIndex &&
                PaletteIndex == other.PaletteIndex && Element == other.Element;
        }

        [SerializeField] Fauna fauna;

        [Tooltip("Extra seconds the SERVER waits after its own wither completes before " +
                 "despawning the husk. Clients start their wither up to ~RTT later, so " +
                 "despawning the moment the server finishes would clip the last wither " +
                 "ring on clients - a pop-out the continuity law forbids.")]
        [Min(0f)] [SerializeField] float despawnGraceSeconds = 0.5f;

        readonly NetworkVariable<FaunaIdentity> _netIdentity = new();

        // Alive -> Dying, server-write. A NetworkVariable (not an RPC) so a peer that joins
        // mid-wither sees a withering husk instead of a healthy creature that pops out at the
        // server's despawn. The DEATH STYLE travels with it because the style IS the animation
        // (Docs/ECOSYSTEM.md §26): a jousted creature unravels outward from the hole the
        // jouster left, a starved one inward from its extremities, a devoured one suctions into
        // a mouth. Replicating only "it died" would play the wrong death on every client.
        readonly NetworkVariable<byte> _netLifeState = new((byte)LifeState.Alive);
        readonly NetworkVariable<byte> _netDeathStyle = new((byte)LifeformDeathStyle.Withered);

        bool _appliedReplicatedDeath;

        void Awake()
        {
            if (!fauna) fauna = GetComponent<Fauna>();
        }

        // ------------------------------------------------------------------
        //  Authority - the one rule every ecology decision site checks
        // ------------------------------------------------------------------

        /// <summary>
        /// True when THIS peer runs ecology decisions (spawning, feeding, death, reproduction).
        /// Under the locked EAGER-Relay design the NetworkManager is always listening and the
        /// local player is the server unless they joined a party - so solo play, offline/tool
        /// scenes and party hosts all simulate exactly as today; only party CLIENTS become
        /// puppet renderers.
        /// </summary>
        public static bool IsSimAuthority
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return ComputeIsSimAuthority(nm != null && nm.IsListening, nm != null && nm.IsServer);
            }
        }

        /// <summary>Pure rule behind <see cref="IsSimAuthority"/> (unit-testable): a peer
        /// simulates unless a network session is live and it is not the server.</summary>
        public static bool ComputeIsSimAuthority(bool networkSessionLive, bool isServer) =>
            !networkSessionLive || isServer;

        // ------------------------------------------------------------------
        //  Spawn seam (server)
        // ------------------------------------------------------------------

        /// <summary>
        /// Networks a freshly-instantiated fauna on the server: stamps the replicated identity
        /// and spawns its NetworkObject (destroyWithScene - Netcode scene loads clean the
        /// population up on every peer).
        ///
        /// Safe to call from ANY spawn path: it no-ops on clients, in offline scenes, and for
        /// species whose prefab carries no NetworkObject - which IS the per-species rollout
        /// gate (Docs/ECOSYSTEM_NETWORK_SYNC.md §5). Call it AFTER
        /// <see cref="Fauna.AssignLineage"/>, never before: the lineage bind is what rolls this
        /// individual's element, and the identity being stamped is the result of that roll.
        /// </summary>
        public static void ServerSpawn(Fauna spawned)
        {
            if (!spawned) return;

            // The per-species rollout gate. Authored on the CONFIG rather than inferred from the
            // prefab, because affordability is a property of the POPULATION: one NetworkObject
            // per creature is cheap for 32 sharks and is not for 893 quadfish, and the same
            // prefab serves both. A species with no config (a toy release, a manager-spawned
            // drone) is never replicated.
            var cfg = spawned.SourceConfig;
            if (!cfg || !cfg.NetworkSynced) return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsServer) return;
            if (!spawned.TryGetComponent(out NetworkObject netObj)) return;
            if (netObj.IsSpawned) return;

            if (spawned.TryGetComponent(out FaunaNetworkSync sync))
                sync._netIdentity.Value = sync.CaptureIdentity();

            netObj.Spawn(destroyWithScene: true);
        }

        FaunaIdentity CaptureIdentity()
        {
            var identity = new FaunaIdentity
            {
                Domain = (byte)fauna.Domain,
                ConfigIndex = -1,
                PaletteIndex = -1,
                Element = (byte)fauna.Element,
            };

            var cfg = fauna.SourceConfig;
            var profile = fauna.HostCell && fauna.HostCell.Config
                ? fauna.HostCell.Config.SpawnProfile
                : null;

            if (cfg && profile != null && profile.SupportedFaunas != null)
            {
                int idx = profile.SupportedFaunas.IndexOf(cfg);
                if (idx >= 0 && idx <= sbyte.MaxValue) identity.ConfigIndex = (sbyte)idx;

                // Which palette sibling supplied this individual's tuning block. Element alone
                // is not enough: with SpreadElements the tuning comes from the SIBLING config,
                // and the sibling is what states the body scale and the heart size.
                identity.PaletteIndex = (sbyte)ResolvePaletteIndex(cfg, fauna.VariantTuningForReplication);
            }

            return identity;
        }

        static int ResolvePaletteIndex(FaunaConfigurationSO cfg, FaunaVariantTuning tuning)
        {
            if (tuning == null || cfg == null) return -1;
            if (ReferenceEquals(tuning, cfg.Variant)) return -1;

            var palette = cfg.ElementPalette;
            if (palette == null) return -1;
            for (int i = 0; i < palette.Count && i <= sbyte.MaxValue; i++)
            {
                var sibling = palette[i];
                if (sibling && ReferenceEquals(sibling.Variant, tuning)) return i;
            }
            return -1;
        }

        /// <summary>
        /// Server-side teardown used just before a Netcode scene load (game launch): despawns
        /// every networked fauna registered on <paramref name="host"/> so a fauna spawn message
        /// can never batch into the same tick as the scene-load message (the known client-side
        /// "[Invalid Destroy]" race - the AI-spawn lesson in CLAUDE.md). The whole scene is
        /// unloading for every peer behind a fade, so this is scene teardown - the same context
        /// in which lifeforms are destroyed today - not an in-world pop-out.
        /// </summary>
        public static void ServerDespawnBrood(Cell host)
        {
            if (!host) return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsServer) return;

            // Snapshot: Despawn destroys -> Fauna.OnDestroy unregisters -> mutates LiveFauna.
            var snapshot = new List<Fauna>(host.LiveFauna);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var f = snapshot[i];
                if (!f) continue;
                if (f.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
                    netObj.Despawn(true);
            }
        }

        // ------------------------------------------------------------------
        //  Death replication (wither-to-crystal on every peer)
        // ------------------------------------------------------------------

        /// <summary>
        /// Called by the sealed <see cref="Fauna.Die"/>. On the server this flips the replicated
        /// life state (and the STYLE, which is the animation) so every client runs the same
        /// crystal drop + wither locally; on clients - whose Die was itself replication-driven -
        /// it no-ops.
        /// </summary>
        public void NotifyDied(LifeformDeathStyle style)
        {
            if (!IsSpawned || !IsServer) return;
            _netDeathStyle.Value = (byte)style;
            if (_netLifeState.Value != (byte)LifeState.Dying)
                _netLifeState.Value = (byte)LifeState.Dying;
        }

        /// <summary>
        /// Removal routing for a spent husk (called from the END of the wither/fade by the
        /// species' own removal point). Returns true when the networked path owns the removal:
        /// the SERVER despawns after <see cref="despawnGraceSeconds"/>; a CLIENT does nothing -
        /// its husk is already spent and the server's despawn destroys the object (a client that
        /// destroyed a replicated NetworkObject itself would be an NGO error). Returns false for
        /// a never-spawned fauna so the legacy Destroy path runs unchanged.
        /// </summary>
        public bool HandleHuskRemoval()
        {
            if (!IsSpawned) return false;
            if (!IsServer) return true;

            if (despawnGraceSeconds > 0f && isActiveAndEnabled && gameObject.activeInHierarchy)
                StartCoroutine(DespawnAfterGrace());
            else
                SafeDespawn();
            return true;
        }

        IEnumerator DespawnAfterGrace()
        {
            yield return new WaitForSeconds(despawnGraceSeconds);
            SafeDespawn();
        }

        void SafeDespawn()
        {
            if (!IsSpawned) return;
            var nm = NetworkManager;
            if (nm == null || !nm.IsListening || nm.ShutdownInProgress) return;
            if (nm.IsServer) NetworkObject.Despawn(true);
        }

        // ------------------------------------------------------------------
        //  Combat damage - the one thing a client originates
        // ------------------------------------------------------------------

        /// <summary>
        /// A client shot one of this creature's body prisms. Projectiles are NOT networked (a
        /// bullet is a pooled local object on whichever machine fired it), so the shot exists
        /// only on the shooter's peer - exactly the reason
        /// <c>Player.ReportFaunaKill_ServerRpc</c> exists for the SCORE. This is the same
        /// owner-detects / server-decides round trip for the DAMAGE: the shooter destroys its
        /// own local copy of the prism immediately (the hit has to read instantly), and tells
        /// the server, which destroys the matching prism on the ONE simulation. If that was the
        /// last one, the server's own <see cref="Fauna.OnBodyPrismExploded"/> runs the sealed
        /// death and the wither replicates back to everyone.
        ///
        /// The prism is named by its INDEX in <c>Fauna.BodyPrisms</c>, which is
        /// <c>GetComponentsInChildren</c> order over an identical prefab hierarchy - the same
        /// prism on every peer, and nothing new to keep in sync.
        ///
        /// Without this a client's kill would be a kill on the client's screen alone: the
        /// creature would die there and swim on for everyone else. In Wildlife Liberation,
        /// where shooting creatures IS the mode, that is worse than not syncing at all.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ReportBodyPrismDestroyed_ServerRpc(int prismIndex, string killerName)
        {
            if (!IsServer || !fauna) return;
            fauna.ApplyReplicatedBodyPrismLoss(prismIndex, killerName);
            // Tell every OTHER peer to drop the same prism, so a creature that SURVIVES the hit
            // (a worm colony losing 1 of 26) looks the same everywhere. The shooter already
            // removed its own.
            DestroyBodyPrism_ClientRpc(prismIndex, killerName);
        }

        /// <summary>
        /// Server -> clients: drop the body prism at <paramref name="prismIndex"/> locally. Used
        /// both for the server's own shots and to fan a client's reported shot out to the other
        /// peers. Idempotent - a prism already gone is a no-op, which is what makes the
        /// shooter's own echo harmless.
        /// </summary>
        [ClientRpc]
        void DestroyBodyPrism_ClientRpc(int prismIndex, string killerName)
        {
            if (IsServer || !fauna) return;
            fauna.ApplyReplicatedBodyPrismLoss(prismIndex, killerName);
        }

        /// <summary>
        /// Called on the SERVER from <see cref="Fauna.OnBodyPrismExploded"/> so the server's own
        /// shots reach the clients' copies too.
        /// </summary>
        public void NotifyBodyPrismDestroyed(int prismIndex, string killerName)
        {
            if (!IsSpawned || !IsServer) return;
            DestroyBodyPrism_ClientRpc(prismIndex, killerName);
        }

        // ------------------------------------------------------------------
        //  Spawn / despawn lifecycle
        // ------------------------------------------------------------------

        public override void OnNetworkSpawn()
        {
            _netLifeState.OnValueChanged += OnLifeStateChanged;

            // Server: the spawner already ran the full Initialize + AssignLineage before Spawn -
            // nothing to do. Clients: puppet-ize BEFORE Initialize so the sim halves (goal
            // coroutine, behavior tick, starvation clock) never start, then rebuild the same
            // individual from the replicated identity. Late joiners take this same path - NGO's
            // sync pass delivers the current NetworkVariable values with the spawn.
            if (IsServer) return;
            if (!fauna) return;

            var identity = _netIdentity.Value;
            var host = Cell.FindNearestActiveCell(transform.position);

            fauna.EnterPuppetMode();
            fauna.SetTeam((Domains)identity.Domain);
            fauna.Initialize(host);
            fauna.ApplyReplicatedIdentity(host, ResolveConfig(host, identity),
                                          ResolvePaletteConfig(host, identity),
                                          (Element)identity.Element);

            // Joined mid-death: run the same local death path now (crystal + wither) so the husk
            // withers instead of popping out at the server's despawn.
            if (_netLifeState.Value == (byte)LifeState.Dying)
                ApplyReplicatedDeath();
        }

        public override void OnNetworkDespawn()
        {
            _netLifeState.OnValueChanged -= OnLifeStateChanged;
        }

        void OnLifeStateChanged(byte _, byte next)
        {
            // The server already ran its own Die; only clients mirror it.
            if (IsServer) return;
            if (next == (byte)LifeState.Dying) ApplyReplicatedDeath();
        }

        void ApplyReplicatedDeath()
        {
            if (_appliedReplicatedDeath || !fauna) return;
            _appliedReplicatedDeath = true;
            fauna.ApplyReplicatedDeath((LifeformDeathStyle)_netDeathStyle.Value);
        }

        static SpawnProfileSO ResolveProfile(Cell host) =>
            host && host.Config ? host.Config.SpawnProfile : null;

        static FaunaConfigurationSO ResolveConfig(Cell host, FaunaIdentity identity)
        {
            var profile = ResolveProfile(host);
            if (profile?.SupportedFaunas == null) return null;
            int i = identity.ConfigIndex;
            return i >= 0 && i < profile.SupportedFaunas.Count ? profile.SupportedFaunas[i] : null;
        }

        static FaunaConfigurationSO ResolvePaletteConfig(Cell host, FaunaIdentity identity)
        {
            var cfg = ResolveConfig(host, identity);
            if (!cfg || identity.PaletteIndex < 0) return null;
            var palette = cfg.ElementPalette;
            if (palette == null || identity.PaletteIndex >= palette.Count) return null;
            return palette[identity.PaletteIndex];
        }
    }
}
