using System.Collections.Generic;
using CosmicShore.Game.IO;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Serialization;


namespace CosmicShore.Game
{
    public interface IPlayer : ITransform
    {
        public static List<IPlayer> NppList { get; }
        VesselClassType VesselClass { get; }
        Domains Domain { get; }
        string Name { get; }
        string PlayerUUID { get; }
        IVessel Vessel { get; }
        InputController InputController { get; }
        IInputStatus InputStatus { get; }
        public bool IsNetworkOwner { get; }

        bool IsActive { get; }
        bool AutoPilotEnabled { get; }
        /// <summary>
        /// If true, it means that this played was marked as AI at initialization
        /// </summary>
        bool IsInitializedAsAI { get; }
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
        void ToggleAutoPilot(bool toggle);
        /// <summary>
        /// If true -> pause input status. false -> unpause otherwise.
        /// </summary>
        void ToggleInputPause(bool toggle);
        void DestroyPlayer();
        void ResetForReplay();

        void SetPoseOfVessel(Pose pose) => Vessel.SetPose(pose);

        [System.Serializable]
        public class InitializeData
        {
            [FormerlySerializedAs("ShipClass")] [FormerlySerializedAs("ShipType")] public VesselClassType vesselClass;
            [FormerlySerializedAs("Team")] public Domains domain;
            public string PlayerName;
            
            [FormerlySerializedAs("EnableAIPilot")] [Tooltip("If true, the player-vessel will spawn as AI")]
            public bool IsAI;
            
            [Tooltip("If true, then only this player-vessel will spawn")]
            public bool AllowSpawning;
        }
    }
}
