using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TailGlider.Utility.Singleton;
using System;

//[RequireComponent()]
namespace StarWriter.Core.HangerBuilder
{
    public class Hanger : SingletonPersistent<Hanger>, IDataPersistence
    {
        HangerData hangerData;

        PlayerBuild currentPlayerBuild;
        [SerializeField] string build = "DefaultPlayerBuild001";

        public List<GameObject> hangerBays;

        public GameObject selectedBay;

        private int bayIndex = 0;

        public int BayIndex { get => bayIndex; }
        public HangerData HangerData { get => hangerData;  }

        void Start()
        {
            DataPersistenceManager.Instance.LoadHanger();
            SetActiveBay();
            currentPlayerBuild = new PlayerBuild();
            hangerData.PlayerBuilds.TryGetValue(build, out currentPlayerBuild);
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
            hangerData.PlayerBuilds.TryGetValue(build, out currentPlayerBuild);
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
        private void OnDisable()
        {
            DataPersistenceManager.Instance.SaveHanger();
        }
        #region Persistent Data
        public void LoadData(HangerData data)
        {
            this.hangerData = data;
        }

        public void SaveData(ref HangerData data)
        {
            data = this.hangerData;
        }
        public void LoadData(GameData data)
        {
            
        }

        public void SaveData(ref GameData data)
        {
            
        }
        #endregion


    }
}

