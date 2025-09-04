using CosmicShore.Game.IO;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Serialization;


namespace CosmicShore.Game
{
    public interface IPlayer : ITransform
    {
        ShipClassType ShipClass { get; }
        Teams Team { get; }
        string Name { get; }
        string PlayerUUID { get; }
        IShip Ship { get; }
        InputController InputController { get; }
        IInputStatus InputStatus { get; }

        bool IsActive { get; }

        void Initialize(InitializeData data, IShip ship);
        void ToggleActive(bool active);
        void ToggleGameObject(bool toggle);
        void ToggleAutoPilotMode(bool toggle);
        void ToggleStationaryMode(bool toggle);
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
