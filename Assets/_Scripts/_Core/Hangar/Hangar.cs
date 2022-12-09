using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;
using System.Linq;

namespace StarWriter.Core.HangerBuilder
{
    public class Hangar : SingletonPersistent<Hangar>
    {
        //Dictionary<string, SO_Ship> ships = new Dictionary<string, SO_Ship>();
        Dictionary<string, Ship> ships = new Dictionary<string, Ship>();

        [SerializeField] int SelectedBayIndex = 0;
        [SerializeField] public List<Ship> ShipPrefabs;
        [SerializeField] public List<SO_Ship> allShips;
        [SerializeField] public List<SO_Ship> bayShips;

        public Ship selectedShip { get; private set; }

        public int BayIndex { get => SelectedBayIndex; }

        private void Start()
        {
            //foreach (SO_Ship ship in allShips)
            //    ships.Add(ship.Name, ship);

            foreach (var ship in ShipPrefabs)
                ships.Add(ship.name, ship);

            if (!PlayerPrefs.HasKey("ShipName") || PlayerPrefs.GetString("ShipName") == "Manta")
                PlayerPrefs.SetString("ShipName", "GreenManta");
        }
        public Ship LoadPlayerShip()
        {
            return Instantiate(ships[PlayerPrefs.GetString("ShipName")]);
        }
        public Ship LoadAIShip()
        {
            if (PlayerPrefs.GetString("ShipName") == "GreenManta")
                return Instantiate(ships["RedMantaAI"]);
            else
                return Instantiate(ships["GreenMantaAI"]);
        }
    }
}