using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public interface IPlayer : ITransform
    {
        Domains Domain { get; }
        string Name { get; }
        /// <summary>
        /// The player's selected profile avatar icon ID.
        /// Used in multiplayer HUD score cards to display the player's avatar.
        /// </summary>
        int AvatarId { get; }
        string PlayerUUID { get; }
        /// <summary>
        /// The player's UGS authentication PlayerId, replicated to every peer. Empty for AI.
        /// This is the real identity - <see cref="PlayerUUID"/> is the display name.
        /// </summary>
        string UgsPlayerId { get; }
        IVessel Vessel { get; }
        InputController InputController { get; }
        IInputStatus InputStatus { get; }
        IRoundStats RoundStats { get; }
        bool IsActive { get; }
        /// <summary>
        /// If true, it means that this played was marked as AI at initialization
        /// </summary>
        bool IsInitializedAsAI { get; }
        /// <summary>
        /// In multiplayer mode, true -> owner client, false -> other clients and AIs
        /// In singleplayer mode, always false.
        /// </summary>
        public bool IsMultiplayerOwner { get; }
        /// <summary>
        /// In multiplayer mode, true -> owner client, can be AI also (in case of server), false -> other client
        /// In singleplayer mode, always false.
        /// </summary>
        public bool IsNetworkOwner { get; }
        /// <summary>
        /// In multiplayer mode, true -> non owner clients, can be AI also, false -> owner client.
        /// </summary>
        public bool IsNetworkClient { get; }
        /// <summary>
        /// The locally-owned, non-AI player - the owner client providing input. Equivalent to
        /// <see cref="IsMultiplayerOwner"/>; there is no offline single-player (every session is a
        /// Relay host, solo or party) and AI shares the host's owner id, so it is excluded.
        /// </summary>
        bool IsLocalUser { get; }
        /// <summary>
        /// The human pilot ON THIS MACHINE — the player whose camera and input this client owns.
        /// Broader than <see cref="IsLocalUser"/> by exactly one case: the legacy NON-NETWORKED
        /// single-player spawn path (<see cref="PlayerSpawner"/> → <c>InitializeForSinglePlayerMode</c>,
        /// used by the single-player minigame scenes) never network-spawns its Player, so
        /// <c>IsSpawned</c> is false there and <see cref="IsLocalUser"/> reports false for a human.
        ///
        /// Use this — never <see cref="IsLocalUser"/> — for anything that must hold in EVERY game
        /// mode, so a mode cannot opt out of a platform system by using the other spawn path. The
        /// prism occlusion corridor (Docs/PRISM_ANIMATION.md §4.7) binds on exactly this.
        /// </summary>
        bool IsLocalPilot { get; }
        /// <summary>
        /// True once THIS player's machine has finished building the arena and is past its own
        /// connecting screen. The arena is built independently on every peer, so only that
        /// player's machine can know - it reports through <see cref="ReportArenaReady"/> and the
        /// answer replicates. A player that is not network-spawned (the legacy single-player
        /// path) is trivially ready: there is no second machine to wait for.
        /// </summary>
        bool IsArenaReady { get; }
        /// <summary>Announce that this machine's arena build is complete. Owner-side; idempotent.</summary>
        void ReportArenaReady();
        /// <summary>
        /// In multiplayer session, this stores the network object id.
        /// </summary>
        ulong PlayerNetId { get; }
        /// <summary>
        /// In multiplayer session, this stores the vessel's network object id.
        /// </summary>
        ulong VesselNetId { get; }
        /// <summary>
        /// Id of the owner client of this player in multiplayer
        /// </summary>
        ulong OwnerClientNetId { get; }
        void InitializeForSinglePlayerMode(InitializeData data, IVessel vessel);
        void InitializeForMultiplayerMode(IVessel vessel);
        void ToggleGameObject(bool toggle);
        void DestroyPlayer();
        void StartPlayer();
        void ResetForPlay();
        void SetPoseOfVessel(Pose pose) => Vessel.SetPose(pose);
        void ChangeVessel(IVessel vessel);

        [System.Serializable]
        public class InitializeData
        {
            public VesselClassType vesselClass;
            public string PlayerName;
            public int AvatarId;

            [Tooltip("If true, the player-vessel will spawn as AI")]
            public bool IsAI;

            [Tooltip("If true, then only this player-vessel will spawn")]
            public bool AllowSpawning;
        }
    }
}
