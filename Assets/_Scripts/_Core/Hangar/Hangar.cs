using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;
using System;

namespace StarWriter.Core.HangerBuilder
{
    public class Hangar : SingletonPersistent<Hangar>
    {
        HangarData hangarData;

        PlayerBuild currentPlayerBuild;
        [SerializeField] string build = "DefaultPlayerBuild001";

        public List<GameObject> hangerBays;

        public GameObject selectedBay;

        private int bayIndex = 0;

        public int BayIndex { get => bayIndex; }

        public HangarData CurrentHangarData { get => hangarData;  }

        private void OnEnable()
        {

        }

        private void OnDisable()
        {
            DataPersistenceManager.Instance.SaveHangarData(hangarData); //removed till V3.0 or rdy
        }

        void Start()
        {
            this.hangarData = DataPersistenceManager.Instance.LoadHangerData(); //removed till V3.0 or rdy
            SetActiveBay();
            currentPlayerBuild = new PlayerBuild();
            hangarData.PlayerBuilds.TryGetValue(build, out currentPlayerBuild);
            Debug.Log("DefaultPlayerBuild001 PlayerBuild Pilot is " + currentPlayerBuild.Pilot);
        }

        public void OnShipButtonPressed(int idx) //TODO really OnBayButtonPressed
        {
            bayIndex = idx;
            SetActiveBay();
            ChangePlayerBuild();
        }
        private void SetActiveBay()
        {          
            foreach (GameObject bay in hangerBays)
            {
                bay.gameObject.SetActive(false);
            }
            selectedBay = hangerBays[bayIndex];
            selectedBay.gameObject.SetActive(true);            
        }
        private void ChangePlayerBuild()
        {
            switch (bayIndex)
            {
                case 1:
                    build = "DefaultPlayerBuild001";
                    break;
                case 2:
                    build = "DefaultPlayerBuild002";
                    break;
                case 3:
                    build = "DefaultPlayerBuild003";
                    break;
            }
            hangarData.PlayerBuilds.TryGetValue(build, out currentPlayerBuild);
            Debug.Log("Current Player Build is " + currentPlayerBuild);
            Debug.Log("Current pilot is " + currentPlayerBuild.Pilot);
            Debug.Log("Current ship is " + currentPlayerBuild.Ship);
            Debug.Log("Current trail is " + currentPlayerBuild.Trail);
        }

        public string GetCurrentPlayerBuildsPilot()
        {
            string pilot = currentPlayerBuild.Pilot;
            return pilot;
        }

        public string GetCurrentPlayerBuildShip()
        {
            string ship = currentPlayerBuild.Ship;
            return ship;
        }

        public string GetCurrentPlayerBuildTrail()
        {
            string trail = currentPlayerBuild.Trail;
            return trail;
        }
        
        //#region Persistent Data
        //public void LoadData(HangarData data)
        //{
        //    this.hangarData = data;
        //}

        //public void SaveData(ref HangarData data)
        //{
        //    data = this.hangarData;
        //}
        //#endregion
    }
}

