using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;
using System.Linq;

namespace StarWriter.Core.HangerBuilder
{

    public class Hangar : SingletonPersistent<Hangar>
    {
        [SerializeField] string PlayerShipName = "GreenManta";
        [SerializeField] string AIShipName = "RedMantaAI";

        Dictionary<string, Ship> ships = new Dictionary<string, Ship>();

        [SerializeField] int SelectedBayIndex = 0;
        [SerializeField] public List<Ship> ShipPrefabs;

        public Ship selectedShip { get; private set; }

        public int BayIndex { get => SelectedBayIndex; }

        private void Start()
        {
            foreach (var ship in ShipPrefabs)
                ships.Add(ship.name, ship);
        }
        public Ship LoadPlayerShip()
        {
            return Instantiate(ships[PlayerShipName]);
        }
        public Ship LoadAIShip()
        {
            return Instantiate(ships[AIShipName]);
        }
    }
}