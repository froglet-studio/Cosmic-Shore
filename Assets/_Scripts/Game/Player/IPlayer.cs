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
        ShipClassType ShipClass { get; }
        Teams Team { get; }
        string Name { get; }
        string PlayerUUID { get; }
        IShip Ship { get; }
        InputController InputController { get; }
        IInputStatus InputStatus { get; }

        bool IsActive { get; }
        bool IsAIModeActivated { get; }

        void Initialize(InitializeData data, IShip ship);
        void ToggleActive(bool active);
        void ToggleGameObject(bool toggle);
        void ToggleAutoPilotMode(bool toggle);
        /// <summary>
        /// If true -> stationary mode is activated. false -> deactivated
        /// </summary>
        void ToggleStationaryMode(bool toggle);
        /// <summary>
        /// If true -> pause input status. false -> unpause otherwise.
        /// </summary>
        void ToggleInputStatus(bool toggle);

        [System.Serializable]
        public class InitializeData
        {
            [FormerlySerializedAs("ShipType")] public ShipClassType ShipClass;
            public Teams Team;
            public string PlayerName;
            public string PlayerUUID;
            
            [Tooltip("If true, the player-ship will spawn as AI")]
            public bool EnableAIPilot;
            
            [Tooltip("If true, then only this player-ship will spawn")]
            public bool AllowSpawning;
        }
    }
}
