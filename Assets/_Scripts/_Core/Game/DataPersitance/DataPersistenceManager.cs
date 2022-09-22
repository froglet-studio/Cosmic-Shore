using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TailGlider.Utility.Singleton;
using System;
using Newtonsoft.Json;

namespace StarWriter.Core 
{
    public class DataPersistenceManager : SingletonPersistent<DataPersistenceManager>
    {
        [Header("File Storage Config")]

        [SerializeField] private string gameFileName = "gamedata.json";
        [SerializeField] private string hangerFileName = "hangerdata.json";
        [SerializeField] private string playerDataFileName = "playerdata.dat";

        private GameData gameData;

        private HangarData hangarData;

        private PlayerData playerData;

        private List<IDataPersistence> dataPersistenceObjects; //Only GameData values use IDataPersistence currently

        private FileDataHandler dataHandler;
        //public static DataPersistenceManager Instance { get; private set; }

        public override void Awake()
        {
            base.Awake();

            this.dataHandler = new FileDataHandler(Application.persistentDataPath, gameFileName, hangerFileName, playerDataFileName);
        }

        private void Start()
        {

            this.dataPersistenceObjects = FindAllDataPersistenceObjects();
            LoadGameData();
            LoadHangerData(); //TODO should this only be loaded upon entering the hangar scene and saved on exiting
            //LoadCurrentPlayer(); //TODO relook at this
        }

        #region Hangar Data
        /// <summary>
        /// Sets HangarData to default values
        /// </summary>
        public void NewHanger()
        {
            this.hangarData = new HangarData();
        }
        /// <summary>
        /// Sends HangarData to be saved to DataHandler
        /// </summary>
        public void SaveHangarData(HangarData data)
        {
            // Get most recent changes to hangarData
            UpDateHangarData(data);

            // Save data to disk using the file data handler
            dataHandler.SaveHangar(hangarData);

            Debug.Log("HangarData Saved. " + hangarData.PlayerBuilds.Keys);
        }
        /// <summary>
        /// Gets HangarData to be loaded from DataHandler, if HangarData is null Creates a new default HangarData
        /// </summary>
        public HangarData LoadHangerData() //why was this set to internal
        {
            
            // Load saved data from disk using the file data handler
            this.hangarData = dataHandler.LoadHanger();

            // Create default values if GameData is null
            if (this.hangarData == null)
            {
                Debug.Log("HangarData not found while attempting to load.  Created a new HangarData file.");
                NewHanger();
            }

            return hangarData;
            //Debug.Log("Pilot in Bay001 is " + hangarData.Bay001Pilot); ;
        }

        public void UpDateHangarData(HangarData updatedData)
        {
            this.hangarData = updatedData;
        }
        #endregion

        #region Player Data
        public void NewPlayer()
        {
            this.playerData = new PlayerData();
        }

        public void LoadCurrentPlayer()
        {
            // Load saved data from disk using the file data handler
            this.playerData = dataHandler.LoadCurrentPlayer();

            // Create default values if GameData is null
            if (this.playerData == null)
            {
                Debug.Log("PlayerData not found while attempting to load.  Created a new PlayerData file.");
                NewPlayer();
            }

            // Push Loaded PlayerData out to scripts requiring it

            //**********************************************************************************************************
            //TODO push data to the Player and player stats and the hangar for favorite build to display first
            //foreach (IDataPersistence Obj in dataPersistenceObjects)
            //{
            //    Obj.LoadData(gameData);
            //}
            Debug.Log("Loaded Player. " + playerData.playerName);
        }

        public void SaveCurrentPlayer()
        {
            // Push Loaded gamedata out to scripts to update it
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            //player.GetComponent<Player>().SaveData(ref playerData);

            // Save data to disk using the file data handler
            dataHandler.SaveGame(gameData);

            Debug.Log("Game Saved. " + gameData.testNumber);
        }
        #endregion

        #region Game Data
        /// <summary>
        /// Sets GameData to default values
        /// </summary>
        public void NewGame()
        {
            this.gameData = new GameData();
        }
        /// <summary>
        /// Gets GameData to be loaded from DataHandler, if GameData is null Creates a new default GameData
        /// </summary>
        public void LoadGameData()
        {
            // Load saved data from disk using the file data handler
            this.gameData = dataHandler.LoadGame();

            // Create default values if GameData is null
            if (this.gameData == null)
            {
                Debug.Log("GameData not found while attempting to load.  Created a new GameData file.");
                NewGame();
            }

            // Push Loaded gamedata out to scripts requiring it
            foreach (IDataPersistence Obj in dataPersistenceObjects)
            {
                Obj.LoadData(gameData);
            }
            Debug.Log("Loaded Game. " + gameData.testNumber);
        }
        /// <summary>
        /// Sends GameData to be saved to DataHandler
        /// </summary>
        public void SaveGame()
        {
            // Push Loaded gamedata out to scripts to update it
            foreach (IDataPersistence Obj in dataPersistenceObjects)
            {
                Obj.SaveData(ref gameData);
            }

            // Save data to disk using the file data handler
            dataHandler.SaveGame(gameData);

            Debug.Log("Game Saved. " + gameData.testNumber);
        }
        
        /// <summary>
        /// Only use IDataPersistence for Game, Audio, Graphics Settings Data
        /// Finds all IDataPersistence components located on Monobehaviors
        /// </summary>
        /// <returns>List<IDataPersistence></returns>
        public List<IDataPersistence> FindAllDataPersistenceObjects()
        {
            IEnumerable<IDataPersistence> dataPersistenceObjects = FindObjectsOfType<MonoBehaviour>().OfType<IDataPersistence>();

            return new List<IDataPersistence>(dataPersistenceObjects);
        }
        #endregion

        /// <summary>
        /// Saves GamaData on shutdown
        /// </summary>
        private void OnApplicationQuit()
        {
            SaveGame();
        }
    }
}


