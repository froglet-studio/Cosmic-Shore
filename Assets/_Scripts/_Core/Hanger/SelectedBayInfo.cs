using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace StarWriter.Core
{
    public class SelectedBayInfo : MonoBehaviour
    {
        public TMP_Text pilotText;
        public TMP_Text shipText;

        public string pilotsInfo;

        public string shipInfo;

        // Start is called before the first frame update
        void Start()
        {

        }

        private void OnEnable()
        {
            Hanger.onChangeBay += UpdateSelectedBayInfo;
        }

        private void OnDisable()
        {
            Hanger.onChangeBay -= UpdateSelectedBayInfo;
        }

        private void UpdateSelectedBayInfo(int bayIndex)
        {
            GameObject selectedBay = Hanger.Instance.hangerBays[bayIndex].gameObject;
            GameObject selectedPilot = selectedBay.GetComponentInChildren<Pilot>().gameObject;
            Debug.Log(selectedPilot.name);
            //pilotsInfo = selectedPilot.GetComponents<Pilot>()

            GameObject selectedShip = selectedBay.GetComponentInChildren<Pilot>().gameObject;
        }
    }
}

