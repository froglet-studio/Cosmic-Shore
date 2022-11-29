using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.HangerBuilder 
{
    public class ListOfAllShips : MonoBehaviour
    {
        public List<SO_Ship> AllSO_Ships;
        [SerializeField] List<string> shipNames;
        public Dictionary<string, SO_Ship> AllShips;


        // Start is called before the first frame update
        void Start()
        {
            IntializeAllPilotsDictionary();
        }

        private void IntializeAllPilotsDictionary()
        {
            int idx = 0;
            AllShips = new Dictionary<string, SO_Ship>();

            foreach (SO_Ship ship in AllSO_Ships)
            {
                //Add ship to list
                shipNames.Add(ship.Name);
                //add ship to dictionary
                AllShips.Add(shipNames[idx], AllSO_Ships[idx]);
                idx++;
            }
        }

        public SO_Ship GetShipSOFromAllShips(string pilotName)
        {
            AllShips.TryGetValue(pilotName, out SO_Ship shipToReturn);
            return shipToReturn;
        }
    }
}