using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;
using System.Linq;

namespace StarWriter.Core.HangerBuilder
{
    public class Hangar : SingletonPersistent<Hangar>
    {
        Dictionary<string, SO_Ship> ships = new Dictionary<string, SO_Ship>();

        [SerializeField] int SelectedBayIndex = 0;
        [SerializeField] public List<SO_Ship> allShips;
        [SerializeField] public List<SO_Ship> bayShips;

        public SO_Ship selectedShip { get; private set; }

        public int BayIndex { get => SelectedBayIndex; }

        private void Start()
        {
            foreach (SO_Ship ship in allShips)
                ships.Add(ship.Name, ship);

            if (!PlayerPrefs.HasKey("ShipName"))
                PlayerPrefs.SetString("ShipName", "Manta");
        }
        public SO_Ship LoadPlayerShip()
        {
            return ships[PlayerPrefs.GetString("ShipName")];
        }
        public SO_Ship LoadAIShip()
        {
            if (PlayerPrefs.GetString("ShipName") == "Manta")
                return ships["Manta Red"];
            else
                return ships["Manta Green"];
        }
    }
}