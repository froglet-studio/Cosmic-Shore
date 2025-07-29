using CosmicShore.Game.IO;
using CosmicShore.Utility;
using UnityEngine.Serialization;


namespace CosmicShore.Game
{
    public interface IPlayer : ITransform
    {
        ShipClassType ShipClass { get; }
        Teams Team { get; }
        string PlayerName { get; }
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

        [System.Serializable]
        public class InitializeData
        {
            [FormerlySerializedAs("ShipType")] public ShipClassType ShipClass;
            public Teams Team;
            public string PlayerName;
            public string PlayerUUID;
            public bool EnableAIPilot;
            public bool AllowSpawning;
        }
    }
}
