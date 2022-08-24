using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;

namespace StarWriter.Core
{
    public class Hanger : SingletonPersistent<Hanger>
    {
        public List<GameObject> hangerBays;

        //public Dictionary<string, GameObject> AvailableShips;

        private GameObject selectedBay;

        //public GameObject SelectedBay { get => selectedBay; set => selectedBay = value; }

        public delegate void OnChangeBayEvent(int bayIndex);
        public static event OnChangeBayEvent onChangeBay;

        // Start is called before the first frame update
        void Start()
        {
            SetActiveBay(0);
        }

        private void SetActiveBay(int idx)
        {
            foreach(GameObject bay in hangerBays)
            {
                bay.gameObject.SetActive(false);
            }
            selectedBay = hangerBays[idx];
            selectedBay.gameObject.SetActive(true); 
            onChangeBay?.Invoke(idx);
        }

        public void OnShipButtonPressed(int idx)
        {
            SetActiveBay(idx);
        }
    }
}

