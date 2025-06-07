using CosmicShore.Game.IO;
using CosmicShore.Game.UI;
using CosmicShore.Utility;


namespace CosmicShore.Game
{
    public interface IPlayer : ITransform
    {
        public ShipTypes ShipType { get; set; }
        public Teams Team { get; }
        public string PlayerName { get; }
        public string PlayerUUID { get; }
        public IShip Ship { get; }
        public InputController InputController { get; }
        public GameCanvas GameCanvas { get; }
        public bool IsActive { get; }

        public void Initialize(InitializeData data);
        public void ToggleActive(bool active);
        public void InitializeShip(ShipTypes shipType, Teams team);
        public void ToggleGameObject(bool toggle);

        [System.Serializable]
        public struct InitializeData
        {
            public ShipTypes DefaultShipType;
            public Teams Team;
            public string PlayerName;
            public string PlayerUUID;
            public string Name;
        }
    }
}
