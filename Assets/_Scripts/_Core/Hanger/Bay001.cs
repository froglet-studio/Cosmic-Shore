using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.HangerBuilder
{
    public class Bay001 : MonoBehaviour, IDataPersistence
    {
        [Tooltip("String used to save and load pilot.")]
        public string pilot;
        [Tooltip("String used to save and load ship.")]
        public string ship;

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
            this.pilot = data.Bay001Pilot;
            this.ship = data.Bay001Ship;
        }

        public void SaveData(ref HangerData data)
        {
            data.Bay001Pilot = this.pilot;
            data.Bay001Ship = this.ship;
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

