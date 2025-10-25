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
        public bool IsNetworkOwner { get; }
        public bool IsNetworkClient { get; }
        bool IsActive { get; }
        /// <summary>
        /// If true, it means that this played was marked as AI at initialization
        /// </summary>
        bool IsInitializedAsAI { get; }
        bool IsLocalPlayer { get; }
        void InitializeForSinglePlayerMode(InitializeData data, IVessel vessel);
        void ToggleActive(bool active);
        void ToggleGameObject(bool toggle);
        /// <summary>
        /// If true -> stationary mode is activated. false -> deactivated
        /// </summary>
        void ToggleStationaryMode(bool toggle);
        /// <summary>
        /// If true -> start auto pilot mode, false -> stop auto pilot mode
        /// </summary>
        /// <param name="toggle"></param>
        void ToggleAIPilot(bool toggle);
        /// <summary>
        /// If true -> pause input status. false -> unpause otherwise.
        /// </summary>
        void ToggleInputPause(bool toggle);
        void DestroyPlayer();
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
