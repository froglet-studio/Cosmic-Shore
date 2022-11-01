using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;
using System;

namespace StarWriter.Core.HangerBuilder
{
    public class Hangar : SingletonPersistent<Hangar>
    {
        [SerializeField] int bayIndex = 0;

        ShipConfiguration currentPlayerBuild;

        public List<GameObject> hangerBays;

        public GameObject selectedBay;
        
        public int BayIndex { get => bayIndex; }

        HangarData hangarData;
        public HangarData CurrentHangarData { get => hangarData;  }

        void Start()
        {
            this.hangarData = DataPersistenceManager.Instance.LoadHangerData(); //removed till V3.0 or rdy
            SetActiveBay();
            currentPlayerBuild = new ShipConfiguration();
            currentPlayerBuild = hangarData.PlayerBuilds[bayIndex];
            Debug.Log("Default ShipConfiguration Upgrade is " + currentPlayerBuild.Upgrade1);
        }
        public void SaveHangarData()
        {
            DataPersistenceManager.Instance.SaveHangarData(hangarData); //removed till V3.0 or rdy
        }

        public void OnShipButtonPressed(int idx) //TODO really OnBayButtonPressed
        {
            bayIndex = idx;
            SetActiveBay();
            LoadShipConfigurationFromBay();
        }
        private void SetActiveBay()
        {          
            foreach (GameObject bay in hangerBays)
                bay.gameObject.SetActive(false);
            
            selectedBay = hangerBays[bayIndex];
            selectedBay.gameObject.SetActive(true);            
        }
        private void LoadShipConfigurationFromBay()
        {
            currentPlayerBuild = hangarData.PlayerBuilds[bayIndex];
            Debug.Log("Current Player Build is " + currentPlayerBuild);
            Debug.Log("Current upgrade is " + currentPlayerBuild.Upgrade1);
            Debug.Log("Current ship is " + currentPlayerBuild.Ship);
            Debug.Log("Current trail is " + currentPlayerBuild.Trail);
        }

        public string GetCurrentPlayerBuildShip()
        {
            return currentPlayerBuild.Ship;
        }

        public string GetCurrentPlayerBuildTrail()
        {
            return currentPlayerBuild.Trail;
        }
    }
}