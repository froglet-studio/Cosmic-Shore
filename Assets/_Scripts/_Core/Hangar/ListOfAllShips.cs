using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.HangerBuilder 
{
    public class ListOfAllShips : MonoBehaviour
    {
        public List<SO_Ship_Base> AllSO_Ships;
        [SerializeField]
        private List<string> shipNames;
        public Dictionary<string, SO_Ship_Base> AllShips;


        // Start is called before the first frame update
        void Start()
        {
            IntializeAllPilotsDictionary();
        }

        private void IntializeAllPilotsDictionary()
        {
            int idx = 0;
            AllShips = new Dictionary<string, SO_Ship_Base>();

            foreach (SO_Ship_Base ship in AllSO_Ships)
            {
                //Add ship to list
                shipNames.Add(ship.ShipName);
                //add ship to dictionary
                AllShips.Add(shipNames[idx], AllSO_Ships[idx]);
                idx++;
            }
        }

        public SO_Ship_Base GetShipSOFromAllShips(string pilotName)
        {
            AllShips.TryGetValue(pilotName, out SO_Ship_Base shipToReturn);
            return shipToReturn;
        }
    }
}

