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
        SO_Ship shipSO;
        SO_Trail_Base trailSO;

        [SerializeField] GameObject shipHardPoint;
        [SerializeField] GameObject pilotHardPoint;

        void Start()
        {
            hangar = Hangar.Instance;
            playerBuild = new ShipConfiguration();
            InitializeBay();
        }
        void InitializeBay()
        {
            TryToLoadShip(hangar.GetCurrentPlayerBuildShip());
            TryToLoadTrail(hangar.GetCurrentPlayerBuildTrail());

            Debug.Log("Pilot loaded " + pilotLoaded);
            Debug.Log("Ship loaded " + shipLoaded);
            Debug.Log("Trail loaded " + trailLoaded);
            Debug.Log("Pilot's name " + pilotSO.CharacterName);
            Debug.Log("Ship's name is " + shipSO.Name);
            Debug.Log("Trail loaded is " + trailSO.TrailName);
        }

        void TryToLoadPilot(string pilot)
        {
            bool success = hangar.GetComponent<ListOfAllPilots>().AllPilots.TryGetValue(pilot, out SO_Pilot tempPilotSO);
            pilotLoaded = success;
            pilotSO = tempPilotSO;
        }

        void TryToLoadShip(string pilot)
        {
            bool success = hangar.GetComponent<ListOfAllShips>().AllShips.TryGetValue(pilot, out SO_Ship tempShipSO);
            shipLoaded = success;
            shipSO = tempShipSO;
        }

        void TryToLoadTrail(string pilot)
        {
            bool success = hangar.GetComponent<ListOfAllPilots>().AllPilots.TryGetValue(pilot, out SO_Pilot tempPilotSO);
            pilotLoaded = success;
            pilotSO = tempPilotSO;
        }
    }
}

