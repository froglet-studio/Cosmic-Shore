using CosmicShore.Game.IO;
using CosmicShore.Game.UI;
using CosmicShore.Utility;
using UnityEngine;


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

        public bool IsActive { get; }

        public void Initialize(InitializeData data);
        public void ToggleActive(bool active);
        public void ToggleGameObject(bool toggle);

        [System.Serializable]
        public class InitializeData
        {
            public ShipClassType ShipType;
            public Teams Team;
            public IShip Ship;
            public string PlayerName;
            public string PlayerUUID;
        }
    }
}
