using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.HangerBuilder
{
    public class Bay003 : MonoBehaviour, IDataPersistence
    {
        [Tooltip("String used to save and load pilot.")]
        public string pilot = "";
        [Tooltip("String used to save and load ship.")]
        public string ship = "";

        [SerializeField] private GameObject shipPrefab; 
        [SerializeField] private GameObject pilotPrefab;

        void Start()
        {
            shipPrefab.SetActive(true);
            pilotPrefab.SetActive(true);
            DataPersistenceManager.Instance.LoadHanger();
        }

        public void LoadData(HangerData data)
        {
            this.pilot = data.Bay003Pilot;
            this.ship = data.Bay003Ship;
        }

        public void SaveData(ref HangerData data)
        {
            data.Bay003Pilot = this.pilot;
            data.Bay003Ship = this.ship;
        }

        private void OnDisable()
        {
            DataPersistenceManager.Instance.SaveHanger();
        }

        #region Ignore me
        public void LoadData(GameData data)
        {
            //Not used here
        }
        public void SaveData(ref GameData data)
        {
            //Not used here
        }
        #endregion

    }
}


