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
            Debug.Log("Pilot loaded " + pilotLoaded);
            Debug.Log("Ship loaded " + shipLoaded);
            Debug.Log("Trail loaded " + trailLoaded);
        }
    }
}