using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;
using System.Linq;

namespace StarWriter.Core.HangerBuilder
{
    public class Hangar : SingletonPersistent<Hangar>
    {
        Dictionary<string, Ship> ships = new Dictionary<string, Ship>();

        [SerializeField] int SelectedBayIndex = 0;
        [SerializeField] public List<Ship> ShipPrefabs;

        public Ship selectedShip { get; private set; }

        public int BayIndex { get => SelectedBayIndex; }

        private void Start()
        {
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