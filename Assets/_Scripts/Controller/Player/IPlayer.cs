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
        /// Broader than <see cref="IsLocalUser"/> by exactly one case: a non-AI Player whose
        /// NetworkObject is not spawned, so <c>IsSpawned</c> is false and <see cref="IsLocalUser"/>
        /// reports false for a human. The legacy non-networked single-player spawn path that used
        /// to produce that state was deleted 2026-07-20 (solo is now a Relay host with AI backfill);
        /// the clause is kept deliberately so no future spawn path can slip a human past a platform
        /// system by not being network-spawned.
        ///
        /// Use this — never <see cref="IsLocalUser"/> — for anything that must hold in EVERY game
        /// mode, so a mode cannot opt out of a platform system by using another spawn path. The
        /// prism occlusion corridor (Docs/PRISM_ANIMATION.md §4.7) binds on exactly this.
        /// </summary>
        bool IsLocalPilot { get; }
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
