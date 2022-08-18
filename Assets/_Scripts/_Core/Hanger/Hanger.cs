using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;
using System.Collections;

namespace StarWriter.Core
{
    public class Hanger : SingletonPersistent<Hanger>
    {
        public List<Bay> hangerBays;

        private Bay selectedBay;

        public delegate void OnChangeBayEvent(int bayIndex);
        public static event OnChangeBayEvent onChangeBay;

        // Start is called before the first frame update
        void Start()
        {
            SetActiveBay(1);
        }

        // Update is called once per frame
        void Update()
        {

        }

        private void SetActiveBay(int idx)
        {
            foreach(Bay bay in hangerBays)
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

