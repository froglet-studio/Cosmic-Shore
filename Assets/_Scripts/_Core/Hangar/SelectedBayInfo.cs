using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace StarWriter.Core.HangerBuilder
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
            //Hanger.onChangeBay += UpdateSelectedBayInfo;
        }

        private void OnDisable()
        {
            //Hanger.onChangeBay -= UpdateSelectedBayInfo;
        }

        /*
        private void UpdateSelectedBayInfo(int bayIndex)
        {
            GameObject selectedBay = Hangar.Instance.bayShips[bayIndex].gameObject;
            GameObject selectedPilot = selectedBay.GetComponentInChildren<Pilot>().gameObject;

            pilotsInfo = selectedPilot.GetComponent<Pilot>().PilotName;
            pilotText.text = "Pilot is " + pilotsInfo;

            GameObject selectedShip = selectedBay.GetComponentInChildren<Ship>().gameObject;
            shipInfo = selectedShip.GetComponent<Ship>().ShipName;
            shipText.text = "Ship is " + shipInfo;
        }
        */
    }
}

