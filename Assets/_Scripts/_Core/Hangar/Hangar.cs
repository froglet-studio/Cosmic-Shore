using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;

namespace StarWriter.Core.HangerBuilder
{
    public class Hangar : SingletonPersistent<Hangar>
    {
        [SerializeField] Dictionary<string, SO_Ship> ships = new Dictionary<string, SO_Ship>();

        [SerializeField] int SelectedBayIndex = 0;

        public List<SO_Ship> bayShips;

        public SO_Ship selectedShip { get; private set; }

        public int BayIndex { get => SelectedBayIndex; }

        private void Start()
        {
            if (!PlayerPrefs.HasKey("ShipName"))
                PlayerPrefs.SetString("ShipName", "Manta");


        }
    }
}