using UnityEngine;
using TailGlider.Utility.Singleton;

namespace StarWriter.Core 
{
    public class DataPersistenceManager : SingletonPersistent<DataPersistenceManager>
    {
        [Header("File Storage Config")]

        [SerializeField] string gameFileName = "gamedata.json";
        [SerializeField] string hangerFileName = "hangerdata.json";
        [SerializeField] string playerFileName = "playerdata.json";
        [SerializeField] string leaderboardFileName = "leaderboarddata.json";

        GameData gameData;
        HangarData hangarData;
        PlayerData playerData;

        private void Start()
        {
            LoadGameData();
            LoadHangerData();   // TODO need to test. TODO this only be loaded upon entering the hangar scene and saved on exiting
            LoadPlayer();       // TODO need to test. TODO relook at this
        }

        #region Hangar Data
        /// <summary>
        /// Gets HangarData to be loaded from DataHandler, if HangarData is null Creates a new default HangarData
        /// </summary>
        public HangarData LoadHangerData() //why was this set to internal
        {
            FileDataHandler<HangarData> dataHandler = new FileDataHandler<HangarData>(Application.persistentDataPath, hangerFileName);

            // Load saved data from disk using the file data handler
            this.hangarData = dataHandler.Load();

            // Create default values if GameData is null
            if (hangarData == null)
            {
                Debug.Log("HangarData not found while attempting to load.  Created a new HangarData file.");
                hangarData = new HangarData();
            }

            Debug.Log($"Loaded Game - Game Test Number: {gameData.testNumber}");

            return hangarData;
        }

        /// <summary>
        /// Sends HangarData to be saved to DataHandler
        /// </summary>
        public void SaveHangar(HangarData data)
        {
            UpdateHangarData(data);
            SaveHangar();
        }

        public void SaveHangar()
        {
            // Save data to disk using the file data handler
            FileDataHandler<HangarData> dataHandler = new FileDataHandler<HangarData>(Application.persistentDataPath, hangerFileName);
            dataHandler.Save(hangarData);

            Debug.Log($"Saved Hangar - PlayerBuilds: {hangarData.PlayerBuilds}");
        }

        public void UpdateHangarData(HangarData updatedData)
        {
            hangarData = updatedData;
        }
        #endregion


        #region Player Data
        public PlayerData LoadPlayer()
        {
            FileDataHandler<PlayerData> dataHandler = new FileDataHandler<PlayerData>(Application.persistentDataPath, playerFileName);

            // Load saved data from disk using the file data handler
            playerData = dataHandler.Load();

            // Create default values if GameData is null
            if (playerData == null)
            {
                Debug.Log("PlayerData not found while attempting to load.  Created a new PlayerData file.");
                playerData = new PlayerData();
            }

            //*********************************************************************************************************\\
            // Push Loaded PlayerData out to scripts requiring it
            // TODO push data to the Player and player stats and the hangar for favorite build to display first
            //*********************************************************************************************************//

            Debug.Log("Loaded Player. " + playerData.playerName);

            return playerData;
        }

        /// <summary>
        /// Sends PlayerData to be saved to DataHandler
        /// </summary>
        public void SavePlayer(PlayerData playerData)
        {
            // TODO: Push Loaded gamedata out to scripts to update it?
            this.playerData = playerData;
            SavePlayer();
        }

        public void SavePlayer()
        {
            // Save data to disk using the file data handler
            FileDataHandler<PlayerData> dataHandler = new FileDataHandler<PlayerData>(Application.persistentDataPath, playerFileName);
            dataHandler.Save(playerData);

            Debug.Log($"Saved Player - Player Name: {playerData.playerName}");
        }
        #endregion

        #region Game Data
        /// <summary>
        /// Gets GameData to be loaded from DataHandler, if GameData is null Creates a new default GameData
        /// </summary>
        public GameData LoadGameData()
        {
            FileDataHandler<GameData> dataHandler = new FileDataHandler<GameData>(Application.persistentDataPath, gameFileName);

            // Load saved data from disk using the file data handler
            this.gameData = dataHandler.Load();

            // Create default values if GameData is null
            if (gameData == null)
            {
                Debug.Log("GameData not found while attempting to load.  Created a new GameData file.");
                gameData = new GameData();
            }

            Debug.Log($"Loaded Game - Game Test Number: {gameData.testNumber}");

            return gameData;
        }

        /// <summary>
        /// Sends GameData to be saved to DataHandler
        /// </summary>
        public void SaveGame(GameData gameData)
        {
            this.gameData = gameData;
            SaveGame();
        }
        
        public void SaveGame()
        {
            // Save data to disk using the file data handler
            FileDataHandler<GameData> dataHandler = new FileDataHandler<GameData>(Application.persistentDataPath, gameFileName);
            dataHandler.Save(gameData);

            Debug.Log($"Saved Game - Game Test Number: {gameData.testNumber}");
        }
        #endregion
    }
}