using System.Collections.Generic;
using CosmicShore.Game.IO;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Serialization;


namespace CosmicShore.Game
{
    public interface IPlayer : ITransform
    {
        Domains Domain { get; }
        string Name { get; }
        string PlayerUUID { get; }
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
        /// In singleplayer mode, true when not initialized as AI,
        /// In multiplayer mode -> always false.
        /// </summary>
        public bool IsSinglePlayerOwner { get; }
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
        /// Local User in singleplayer is the player providing input, not AI.
        /// In Multiplayer, it is the Owner Client providing input.
        /// </summary>
        bool IsLocalUser { get; }
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
            public Domains domain;
            public string PlayerName;
            
            [Tooltip("If true, the player-vessel will spawn as AI")]
            public bool IsAI;
            
            [Tooltip("If true, then only this player-vessel will spawn")]
            public bool AllowSpawning;
        }
    }
}
