using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.HangerBuilder
{
    public class Bay : MonoBehaviour
    {
        Hangar hangar;

        public ShipConfiguration playerBuild;

        public bool pilotLoaded = false;
        public bool shipLoaded = false;
        public bool trailLoaded = false;

        SO_Pilot pilotSO;

        SO_Ship_Base shipSO;

        SO_Trail_Base trailSO;

        [SerializeField] private GameObject shipHardPoint;
        [SerializeField] private GameObject pilotHardPoint;
        //[SerializeField] private GameObject trailHardPoint;

        

        void Start()
        {
            hangar = Hangar.Instance;
            playerBuild = new ShipConfiguration();
            InitializeBay();
        }
        private void InitializeBay()
        {
            TryToLoadShip(hangar.GetCurrentPlayerBuildShip());
            TryToLoadTrail(hangar.GetCurrentPlayerBuildTrail());

            Debug.Log("Pilot loaded " + pilotLoaded);
            Debug.Log("Ship loaded " + shipLoaded);
            Debug.Log("Trail loaded " + trailLoaded);
            Debug.Log("Pilot's name " + pilotSO.CharacterName);
            Debug.Log("Ship's name is " + shipSO.ShipName);
            Debug.Log("Trail loaded is " + trailSO.TrailName);
        }

        private void TryToLoadPilot(string pilot)
        {
            bool success = hangar.GetComponent<ListOfAllPilots>().AllPilots.TryGetValue(pilot, out SO_Pilot tempPilotSO);
            pilotLoaded = success;
            pilotSO = tempPilotSO;
        }

        private void TryToLoadShip(string pilot)
        {
            bool success = hangar.GetComponent<ListOfAllShips>().AllShips.TryGetValue(pilot, out SO_Ship_Base tempShipSO);
            shipLoaded = success;
            shipSO = tempShipSO;
        }

        private void TryToLoadTrail(string pilot)
        {
            bool success = hangar.GetComponent<ListOfAllPilots>().AllPilots.TryGetValue(pilot, out SO_Pilot tempPilotSO);
            pilotLoaded = success;
            pilotSO = tempPilotSO;
        }

    }
}

