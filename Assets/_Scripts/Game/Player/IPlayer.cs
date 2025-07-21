using CosmicShore.Game.IO;
using CosmicShore.Utility;


namespace CosmicShore.Game
{
    public interface IPlayer : ITransform
    {
        public ShipClassType ShipType { get; }
        public Teams Team { get; }
        public string PlayerName { get; }
        public string PlayerUUID { get; }
        public IShip Ship { get; }
        public InputController InputController { get; }
        public IInputStatus InputStatus { get; }

        public bool IsActive { get; }

        public void Initialize(InitializeData data, IShip ship);
        public void ToggleActive(bool active);
        public void ToggleGameObject(bool toggle);

        [System.Serializable]
        public class InitializeData
        {
            public ShipClassType ShipType;
            public Teams Team;
            public string PlayerName;
            public string PlayerUUID;
            public bool EnableAIPilot;
        }
    }
}
