using System;
using System.Collections.Generic;
using CosmicShore.UI;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class Player : NetworkBehaviour, IPlayer
    {
        [FormerlySerializedAs("miniGameData")] [SerializeField]
        GameDataSO gameData;

        [Inject] private PlayerDataService _injectedPlayerDataService;

        // Fallback to static singleton — Netcode-spawned Players (host's own player)
        // bypass Reflex's auto-injection since they're instantiated by NetworkManager,
        // not Instantiate() inside an injected scope.
        private PlayerDataService playerDataService
            => _injectedPlayerDataService != null
                ? _injectedPlayerDataService
                : PlayerDataService.Instance;

        public NetworkVariable<VesselClassType> NetDefaultVesselType = new(VesselClassType.Random, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Domains> NetDomain = new(Domains.Jade, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<FixedString128Bytes> NetName = new(string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<ulong> NetVesselId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> NetIsAI = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> NetAvatarId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Server-write mirror of the owner's NetDomain. Exists because Netcode 2.x
        /// owner-write NetworkVariable replication is unreliable in MPPM — the spawn-
        /// deserialization swallows the field initializer and subsequent owner writes
        /// don't always reach the server. Owners explicitly push their team to this
        /// var via SyncDomainToServer_ServerRpc, so the server (and the score card,
        /// which reads RoundStats.Domain seeded from this var) always has the
        /// authoritative value the owner actually picked. Default Jade so even if
        /// the ServerRpc never fires, the score card still has a valid color.
        /// </summary>
        public NetworkVariable<Domains> NetServerDomain = new(Domains.Jade, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public Domains Domain { get; private set; } = Domains.Jade;

        /// <summary>
        /// Owner-side helper: sets the local NetDomain (so the local vessel/crystal
        /// systems pick up the new team color immediately) AND pushes the value to
        /// the server's authoritative NetServerDomain. Use this from any UI that
        /// changes the player's team (TeamSelectionPanel, MenuVesselSelectionPanel
        /// Controller, ArcadeGameConfigureModal) instead of writing NetDomain.Value
        /// directly — the direct-write path doesn't replicate to the server in
        /// MPPM, leaving the score card's team color stuck on the field-initializer
        /// default.
        /// </summary>
        public void RequestDomainChange(Domains domain)
        {
            if (!IsOwner) return;
            NetDomain.Value = domain;
            if (!IsServer)
                SyncDomainToServer_ServerRpc(domain);
            else if (NetServerDomain.Value != domain)
                NetServerDomain.Value = domain;
        }

        /// <summary>
        /// Owner-only ServerRpc that mirrors the owner's chosen NetDomain into the
        /// server-write NetServerDomain. Server validates by checking the sender
        /// owns this NetworkObject (RequireOwnership=true). The score card's
        /// authoritative team color flows from this NetworkVariable through
        /// RoundStats.Domain in PrepareForNewScene.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        void SyncDomainToServer_ServerRpc(Domains domain)
        {
            if (NetServerDomain.Value != domain)
                NetServerDomain.Value = domain;

            // Mirror to local Player.Domain so server-side systems that read
            // Player.Domain (NetworkCrystalManager domain assignment, score
            // tracker, etc.) see the right value without waiting for the next
            // OnNetDomainChanged tick.
            Domain = domain;

            // Mirror to RoundStats.Domain too if the component is already spawned —
            // makes the score card on remote clients refresh immediately.
            if (RoundStats is RoundStats rs && rs.IsSpawned)
                rs.Domain = domain;
        }

        /// <summary>
        /// Changes the player's domain at runtime. Used by shape mode to match
        /// the player's prism color to the collided shape's domain.
        /// </summary>
        public void SetDomain(Domains newDomain)
        {
            Domain = newDomain;
        }
        public string Name { get; private set; }
        public int AvatarId { get; private set; }
        public string PlayerUUID => Name;
        public ulong PlayerNetId => NetworkObjectId;
        /// <summary>
        /// Remarks, this VesselNetId will be set by server
        /// through a network variable during initialization
        /// of vessel and player.
        /// </summary>
        public ulong VesselNetId { get; private set; }
        public ulong OwnerClientNetId => OwnerClientId;
        public IVessel Vessel { get; private set; }
        public bool IsActive { get; private set; }
        public bool AutoPilotEnabled => Vessel.VesselStatus.AutoPilotEnabled;
        public bool IsInitializedAsAI { get; private set; }

        bool _spawnEventRaised;

        private InputController _inputController;
        public InputController InputController
        {
            get
            {
                if (!_inputController)
                    _inputController = gameObject.GetOrAdd<InputController>();
                return _inputController;
            }
        }

        private RoundStats _roundStats;
        public IRoundStats RoundStats
        {
            get
            {
                if (!_roundStats)
                    _roundStats = gameObject.GetOrAdd<RoundStats>();
                return _roundStats;
            }
        }
        public IInputStatus InputStatus => InputController.InputStatus;

        public Transform Transform => transform;
        public bool IsMultiplayerOwner => IsSpawned && IsOwner && !IsInitializedAsAI;
        public bool IsNetworkOwner => IsSpawned && IsOwner;
        public bool IsNetworkClient => IsSpawned && !IsOwner;
        public bool IsSinglePlayerOwner => !IsSpawned && !IsInitializedAsAI;
        public bool IsLocalUser => IsMultiplayerOwner || IsSinglePlayerOwner;
       
        IPlayer.InitializeData InitializeData;
        
        public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel)
        {
            InitializeData = data;
            IsInitializedAsAI = InitializeData.IsAI;
            Domain = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
            Name = InitializeData.PlayerName;
            AvatarId = InitializeData.AvatarId;
            InputController.Initialize();
            ToggleInputPause(true);
            Vessel = vessel;
            RoundStats.Name = Name;
            RoundStats.Domain = Domain;
        }

        /// <summary>
        /// TODO -> A temp way to initialize in multiplayer, try for better approach.
        /// </summary>
        public void InitializeForMultiplayerMode(IVessel vessel)
        {
            IsInitializedAsAI = NetIsAI.Value;
            Domain = NetDomain.Value;
            Name = NetName.Value.ToString();
            AvatarId = NetAvatarId.Value;
            Vessel = vessel;

            if (!IsServer)
                return;

            RoundStats.Name = Name;
            RoundStats.Domain = Domain;

            SetGameObjectName();
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"<color=#00FF00>[FLOW-4] [Player] OnNetworkSpawn — OwnerClientId={OwnerClientId}, NetworkObjectId={NetworkObjectId}, IsOwner={IsOwner}, IsServer={IsServer}</color>");
            base.OnNetworkSpawn();

            // Add to game data early so ServerPlayerVesselInitializer can find us.
            gameData.Players.Add(this);

            VesselNetId = NetVesselId.Value;

            // Subscribe BEFORE writes so deferred spawn-event logic in
            // OnNetNameValueChanged / OnNetDefaultVesselTypeChanged catches
            // the first client value replication.
            NetDomain.OnValueChanged += OnNetDomainChanged;
            NetName.OnValueChanged += OnNetNameValueChanged;
            NetDefaultVesselType.OnValueChanged += OnNetDefaultVesselTypeChanged;
            NetVesselId.OnValueChanged += OnNetVesselIdChanged;
            NetAvatarId.OnValueChanged += OnNetAvatarIdChanged;

            // --- Server writes (server-perm vars) ---
            // Domain is NOT assigned here — it is the spawner's responsibility:
            //   AI players:        SpawnAIs() in ServerPlayerVesselInitializerWithAI
            //   Persistent humans: PrepareForNewScene() via FindUnprocessedPlayerByOwnerClientId
            //   New humans:        HandlePlayerNetworkSpawnedAsync() fallback in ServerPlayerVesselInitializer
            // Assigning here caused double-consumption of the DomainAssigner pool for AI players,
            // because Player.OnNetworkSpawn fires synchronously during Spawn() inside SpawnAIs(),
            // wasting a pool slot that SpawnAIs then overwrites.
            if (IsServer)
            {
                NetIsAI.Value = IsInitializedAsAI;
            }

            // --- Owner writes (owner-perm vars: NetName, NetAvatarId, NetDefaultVesselType) ---
            // Only the local human player writes profile data here.
            // AI players share the host's OwnerClientId (IsOwner=true) but must NOT
            // overwrite their names with the human's profile — the AI spawner sets their
            // names separately after spawn. IsLocalUser filters out AI via !IsInitializedAsAI.
            if (IsLocalUser)
            {
                if (playerDataService != null && playerDataService.IsInitialized
                    && playerDataService.CurrentProfile != null)
                {
                    NetName.Value = playerDataService.CurrentProfile.displayName;
                    NetAvatarId.Value = playerDataService.CurrentProfile.avatarId;
                }
                else if (!string.IsNullOrEmpty(gameData.LocalPlayerDisplayName))
                {
                    NetName.Value = gameData.LocalPlayerDisplayName;
                    NetAvatarId.Value = gameData.LocalPlayerAvatarId;
                }
                else
                {
                    NetName.Value = StripPlayerNameSuffix(AuthenticationService.Instance.PlayerName);
                }

                // If profile wasn't ready when we spawned, subscribe so NetName updates
                // when the cloud profile finishes loading.
                if (playerDataService != null)
                    playerDataService.OnProfileChanged += HandleProfileLoadedAfterSpawn;

                // Only set vessel type from gameData if the client hasn't already
                // chosen a vessel via the ArcadeGameConfigureModal (which writes
                // directly to NetDefaultVesselType). This preserves per-client
                // vessel selection in multiplayer.
                if (!IsValidVesselTypeForSpawn(NetDefaultVesselType.Value))
                    NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;

                // Owner-write NetworkVariables can land at default(T) on the owner
                // side post-spawn even when the field initializer specifies a
                // non-default value: Netcode's spawn-message deserialization
                // overwrites m_InternalValue with default(Domains) = Unassigned for
                // owner-write vars on the owner. Without this owner-side write,
                // server's view stays at Unassigned forever (no replication signal,
                // because owner never wrote), the score-card team color falls
                // through to Color.white, and PrepareForNewScene's RPC carries
                // Unassigned into RoundStats.Domain on every client.
                // Re-establish the field-initializer's intended Jade default so
                // owner-write replication picks it up immediately. Subsequent team
                // selections through MenuVesselSelectionPanelController or the
                // ArcadeGameConfigureModal overwrite this normally.
                if (NetDomain.Value == Domains.Unassigned || NetDomain.Value == Domains.None)
                    NetDomain.Value = Domains.Jade;

                // Owner-write NetDomain replication is unreliable in MPPM (Netcode
                // 2.x spawn-deserialization quirk leaves the server-side view at
                // Unassigned). Push the local value to the server via a direct
                // ServerRpc so the server's RoundStats.Domain (server-write,
                // visible to the score card) is always seeded with what the owner
                // actually wrote — regardless of whether NetDomain replication
                // catches up. This is the authoritative path going forward; team
                // selection panels also call SyncDomainToServer_ServerRpc when the
                // user picks.
                if (!IsServer)
                    SyncDomainToServer_ServerRpc(NetDomain.Value);
            }

            // --- Raise spawn event AFTER all local writes ---
            // Server: only when all required values are populated.
            //   Host player (IsOwner && IsServer): name written above → raise now.
            //   Client player (!IsOwner && IsServer): name empty → deferred to
            //   OnNetNameValueChanged when the client's name replicates.
            // Non-server: raise immediately for client-side pair resolution.
            if (IsServer)
            {
                if (!_spawnEventRaised && IsSpawnReady())
                {
                    _spawnEventRaised = true;
                    gameData.OnPlayerNetworkSpawnedUlong.Raise(OwnerClientId);
                }
            }
            else
            {
                gameData.OnPlayerNetworkSpawnedUlong.Raise(OwnerClientId);
            }

            Debug.Log($"<color=#00FF00>[FLOW-4] [Player] OnNetworkSpawn DONE — Name={NetName.Value}, VesselType={NetDefaultVesselType.Value}, Domain={NetDomain.Value}, IsAI={NetIsAI.Value}, SpawnEventRaised={_spawnEventRaised}</color>");

            InputController.Initialize();
        }

        public override void OnNetworkDespawn()
        {
            _spawnEventRaised = false;
            gameData.Players.Remove(this);

            NetDomain.OnValueChanged -= OnNetDomainChanged;
            NetName.OnValueChanged -= OnNetNameValueChanged;
            NetDefaultVesselType.OnValueChanged -= OnNetDefaultVesselTypeChanged;
            NetVesselId.OnValueChanged -= OnNetVesselIdChanged;
            NetAvatarId.OnValueChanged -= OnNetAvatarIdChanged;

            if (playerDataService != null)
                playerDataService.OnProfileChanged -= HandleProfileLoadedAfterSpawn;
        }

        /// <summary>
        /// Fires when the cloud profile finishes loading after Player has already spawned.
        /// Updates NetName/NetAvatarId so the in-game name matches the menu username.
        /// Only the owner writes to these NetworkVariables — other clients read via replication.
        /// </summary>
        private void HandleProfileLoadedAfterSpawn(PlayerProfileData profile)
        {
            if (!IsLocalUser || profile == null) return;
            if (string.IsNullOrEmpty(profile.displayName)) return;

            if (NetName.Value.ToString() != profile.displayName)
                NetName.Value = profile.displayName;
            if (NetAvatarId.Value != profile.avatarId)
                NetAvatarId.Value = profile.avatarId;
        }


        // TODO - Unnecessary usage of two methods, can be replaced with a single method.
        public void ToggleGameObject(bool toggle) => 
            gameObject.SetActive(toggle);

        /// <summary>
        /// Re-initializes a persistent Player for a new game scene.
        /// Player NetworkObjects survive Netcode scene loads (DestroyWithScene=false)
        /// but OnNetworkSpawn() only fires once (initial creation in Auth scene).
        /// This method handles all subsequent scene transitions:
        ///   - Clears stale vessel reference (old vessel destroyed with scene)
        ///   - Updates NetworkVariables to match new game config
        ///   - Syncs local properties from NetworkVariables
        ///   - Re-registers with gameData.Players (cleared by ResetRuntimeData)
        /// Called by ServerPlayerVesselInitializer when discovering persistent Players.
        /// </summary>
        public void PrepareForNewScene()
        {
            Debug.Log($"<color=#00FF00>[FLOW-4] [Player] PrepareForNewScene — OwnerClientId={OwnerClientId}, NetworkObjectId={NetworkObjectId}, IsOwner={IsOwner}</color>");
            // Clear stale references from previous scene.
            // Vessels have destroyWithScene=true and are already destroyed.
            Vessel = null;
            IsActive = false;
            VesselNetId = 0;

            // Reset gameplay stats from previous game.
            // Cleanup() zeroes all stats via property setters, which also
            // update NetworkVariables on the server. Name/Domain are re-set below.
            RoundStats.Cleanup();

            // Server-authoritative reset + domain assignment for the joining/persistent
            // player. NetDomain is owner-write and unreliable in MPPM (Netcode 2.x
            // spawn-deserialization quirk leaves the server-side view at Unassigned).
            // NetServerDomain is server-write, populated by the owner via
            // SyncDomainToServer_ServerRpc — that value is always correct on the
            // server because the owner explicitly pushed it.
            //
            // Domain priority (most-trusted first):
            //   1. NetServerDomain.Value — owner pushed via ServerRpc, definitive
            //   2. NetDomain.Value       — fallback for older code paths that
            //                              haven't been updated to call
            //                              SyncDomainToServer_ServerRpc
            //   3. DomainAssigner        — last resort if neither var has a real team
            //
            // The chosen domain is force-written to RoundStats.Domain (server-write,
            // replicates reliably to all clients) and also broadcast in the RPC so
            // the client-side _local fields align immediately.
            if (IsServer)
            {
                var domain = NetServerDomain.Value;
                string source = "NetServerDomain";

                if (domain == Domains.Unassigned || domain == Domains.None)
                {
                    domain = NetDomain.Value;
                    source = "NetDomain";
                }

                if (domain == Domains.Unassigned || domain == Domains.None)
                {
                    domain = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
                    source = "DomainAssigner";
                }

                Debug.Log($"<color=#FFA500>[FLOW-4] [Player.PrepareForNewScene] Server-authoritative domain " +
                    $"for '{NetName.Value}' (OwnerClientId={OwnerClientId}): " +
                    $"NetServerDomain={NetServerDomain.Value}, NetDomain={NetDomain.Value} → using {domain} " +
                    $"(source={source})</color>");

                if (RoundStats is RoundStats rs && rs.IsSpawned)
                    rs.Domain = domain;

                // Also update NetServerDomain so subsequent reads (and clients
                // reading this var directly) see the canonical chosen value.
                if (NetServerDomain.Value != domain)
                    NetServerDomain.Value = domain;

                ResetStatsLocal_ClientRpc(
                    domain,
                    new Unity.Collections.FixedString64Bytes(NetName.Value.ToString()));
            }

            // Reset input state (joystick positions, throttle, flags).
            InputStatus?.ResetForReplay();

            // Update owner-writable NetworkVariables to match new game config.
            // Always overwrite vessel type so the menu autopilot uses the configured
            // menuVesselClass (e.g. Squirrel) rather than retaining the game vessel.
            // When launching a new game, ArcadeGameConfigureModal.SyncLocalPlayerVesselType()
            // writes the chosen vessel back to NetDefaultVesselType.
            if (IsOwner)
                NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;

            // Refresh NetName/NetAvatarId from the now-loaded profile.
            // OnNetworkSpawn may have run in the Auth scene before the cloud profile
            // finished loading, leaving NetName = UGS default (e.g. "CuteAwakingLightbulb").
            // By the time we enter a game scene, PlayerDataService is initialized and
            // CurrentProfile holds the menu display name (e.g. "dragon").
            if (IsLocalUser && playerDataService != null && playerDataService.IsInitialized
                && playerDataService.CurrentProfile != null)
            {
                var profile = playerDataService.CurrentProfile;
                if (!string.IsNullOrEmpty(profile.displayName) && NetName.Value.ToString() != profile.displayName)
                    NetName.Value = profile.displayName;
                if (NetAvatarId.Value != profile.avatarId)
                    NetAvatarId.Value = profile.avatarId;
            }

            // Reset server-writable NetworkVariables.
            if (IsServer)
                NetVesselId.Value = 0;

            // Force-sync local properties from NetworkVariables.
            // OnValueChanged callbacks only fire on actual changes;
            // if a value happens to be the same, the local property
            // would remain stale without this explicit sync.
            Domain = NetDomain.Value;
            Name = NetName.Value.ToString();
            AvatarId = NetAvatarId.Value;

            // Re-register with gameData (cleared by ResetRuntimeData during scene transition)
            if (!gameData.Players.Contains(this))
                gameData.Players.Add(this);
        }

        public void DestroyPlayer()
        {
            if (IsSpawned)
            {
                if (IsServer)
                    NetworkObject.Despawn(true);
                return;
            }
            Destroy(gameObject);
        }

        /// <summary>
        /// Called on every client (and host) to reset the local <see cref="RoundStats"/>
        /// fields when entering a new scene. RoundStats lives on a persistent Player
        /// NetworkObject (DestroyWithScene=false), so its local <c>_xxxLocal</c> fields
        /// can carry values from the menu scene (or from earlier code that incremented
        /// stats client-side). Cleanup() resets all local fields immediately on this
        /// machine; on the server it also writes 0 through every NetworkVariable, but
        /// Netcode skips replication when the new value equals the current value, so
        /// the client's locally-drifted fields wouldn't otherwise be cleared.
        /// </summary>
        [ClientRpc]
        void ResetStatsLocal_ClientRpc(Domains domain, Unity.Collections.FixedString64Bytes name)
        {
            // RoundStats getter lazy-creates the component. Cleanup() runs through the
            // property setters, which on a non-server client take the !IsSpawned-or-server
            // path that writes only the local field — exactly what we want here.
            // Cleanup() is a default interface method on IRoundStats, so we keep the
            // interface-typed reference for that call and downcast for the concrete
            // RoundStats.NotifyAllStatsChanged() call afterwards.
            var rs = RoundStats;
            if (rs == null) return;

            int beforeOmni = rs.OmniCrystalsCollected;
            int beforeCrystals = rs.CrystalsCollected;
            var beforeDomain = rs.Domain;

            rs.Cleanup();

            // Push the authoritative Domain + Name as well. Cleanup() doesn't touch
            // these (they aren't gameplay stats), but on the client side they can be
            // stale: the initial spawn sync caught a default value before the server
            // wrote n_Domain/n_Name, and subsequent server writes of the same value
            // never replicated. Writing through the property setter on a non-server
            // client updates the local field without re-touching the NetworkVariable.
            rs.Domain = domain;
            rs.Name = name.ToString();

            // Force-fire every OnXxxChanged event so HUD subscribers (score cards)
            // refresh their cached _displayedScore and team color — without this the
            // property setters' !IsSpawned guard suppresses event raising on a spawned
            // client, leaving the card showing menu-mode leftovers indefinitely
            // because n_xxx.OnValueChanged only fires on actual NetworkVariable
            // value changes (and our reset-to-0 may equal the existing server value).
            (rs as RoundStats)?.NotifyAllStatsChanged();

            Debug.Log($"<color=#00FF00>[FLOW-4] [Player] ResetStatsLocal_ClientRpc on '{Name}' " +
                $"(IsServer={IsServer}, IsOwner={IsOwner}) — " +
                $"OmniCrystals: {beforeOmni}→{rs.OmniCrystalsCollected}, " +
                $"Crystals: {beforeCrystals}→{rs.CrystalsCollected}, " +
                $"Domain: {beforeDomain}→{rs.Domain}</color>");
        }

        public void StartPlayer()
        {
            // Vessel can be null on non-host clients when a ClientRpc (e.g. countdown end)
            // arrives before ClientPlayerVesselInitializer has resolved the player-vessel pair.
            // Same transient Netcode state handled by ResetForPlay() below.
            if (Vessel == null)
            {
                Debug.LogWarning($"[Player] StartPlayer called on '{Name}' (NetObjId={NetworkObjectId}) " +
                                 "but Vessel is null — vessel pair not yet initialized. Skipping.");
                return;
            }

            ToggleActive(true);
            Vessel.StartVessel();
            ToggleInputIdle(false);

            if (IsNetworkClient)
                return;

            if (IsInitializedAsAI)
            {
                ToggleAIPilot(true);
                ToggleInputPause(true);
            }
            else
                ToggleInputPause(false);
        }
        

        public void ResetForPlay()
        {
            // Vessel can be null for persistent Players between scene transitions
            // (old vessel destroyed with scene, new vessel not yet spawned).
            Vessel?.ResetForPlay();
            ToggleActive(false);

            if (IsNetworkClient)
                return;

            if (IsInitializedAsAI && Vessel != null)
                ToggleAIPilot(false);

            InputStatus?.ResetForReplay();
        }

        public void ChangeVessel(IVessel vessel) =>
            Vessel = vessel;

        void ToggleActive(bool active) =>
            IsActive = active;

        void ToggleAIPilot(bool toggle) => 
            Vessel.ToggleAIPilot(toggle);
        
        void ToggleInputPause(bool toggle) => 
            InputController.SetPause(toggle);

        void ToggleInputIdle(bool toggle) =>
            InputController.SetIdle(toggle);
        
        void OnNetDomainChanged(Domains previousValue, Domains newValue)
        {
            Domain = newValue;

            // Propagate the new Domain to the server-authoritative RoundStats.Domain so
            // n_Domain replicates the change to all clients. Without this, RoundStats.Domain
            // is only set once during InitializeForMultiplayerMode and can become stale —
            // resulting in clients displaying Color.white on the player score card when
            // a player picks a team after their Player+Vessel pair has already initialized.
            // Use the RoundStats property so the cached reference is lazy-initialized.
            if (IsServer && RoundStats is RoundStats rs && rs.IsSpawned)
                rs.Domain = newValue;
        }

        void OnNetNameValueChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue)
        {
            Name = newValue.ToString();

            // Mirror Name to RoundStats so the score-card lookup key stays in sync when
            // NetName changes (e.g. cloud profile loads after spawn — see
            // HandleProfileLoadedAfterSpawn). Without this, the card was registered under
            // the old name and live updates fail to match.
            // Use the RoundStats property so the cached reference is lazy-initialized.
            if (IsServer && RoundStats is RoundStats rs && rs.IsSpawned)
                rs.Name = Name;

            TryRaiseDeferredSpawnEvent();
        }

        void OnNetDefaultVesselTypeChanged(VesselClassType previousValue, VesselClassType newValue)
        {
            TryRaiseDeferredSpawnEvent();
        }

        /// <summary>
        /// Server-only: when a remote client's owner-written values (name, vessel type)
        /// replicate, check if we can now raise the spawn event that was deferred
        /// in OnNetworkSpawn because the owner block was skipped.
        /// </summary>
        void TryRaiseDeferredSpawnEvent()
        {
            if (IsServer && !_spawnEventRaised && IsSpawnReady())
            {
                _spawnEventRaised = true;
                gameData.OnPlayerNetworkSpawnedUlong.Raise(OwnerClientId);
            }
        }

        void OnNetVesselIdChanged(ulong previousValue, ulong newValue)
        {
            Debug.Log($"<color=#FF00FF>[PLAYER] OnNetVesselIdChanged '{Name}' — prev={previousValue}, new={newValue}, IsServer={IsServer}, IsOwner={IsOwner}</color>");
            VesselNetId = newValue;
            if (newValue == 0)
            {
                Debug.Log($"<color=#FF00FF>[PLAYER] Clearing Vessel+IsActive on '{Name}' (was VesselId={previousValue})</color>");
                Vessel = null;
                IsActive = false;
            }
        }

        void OnNetAvatarIdChanged(int previousValue, int newValue) =>
            AvatarId = newValue;
        
        bool IsSpawnReady() =>
            IsValidVesselTypeForSpawn(NetDefaultVesselType.Value)
            && !string.IsNullOrEmpty(NetName.Value.ToString());

        /// <summary>
        /// Returns true if the vessel type is a concrete, spawnable vessel
        /// (not Random, Any, or the default enum value).
        /// </summary>
        static bool IsValidVesselTypeForSpawn(VesselClassType type) =>
            type != VesselClassType.Random && type != VesselClassType.Any;

        void SetGameObjectName()
        {
            string playerName;
            if (IsInitializedAsAI)
                playerName = "AI";
            else
                playerName = "Player_" + OwnerClientId;
            gameObject.name = playerName;
        }

        /// <summary>
        /// Strips the "#XXXX" suffix that Unity Authentication appends to PlayerName.
        /// e.g. "MyName#1234" → "MyName"
        /// </summary>
        static string StripPlayerNameSuffix(string ugsName)
        {
            if (string.IsNullOrEmpty(ugsName)) return ugsName;
            int hashIndex = ugsName.LastIndexOf('#');
            return hashIndex > 0 ? ugsName.Substring(0, hashIndex) : ugsName;
        }
    }
}
