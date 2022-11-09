using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace StarWriter.Core
{
    public class LoadSaveTest : MonoBehaviour
        {
        public TextMeshProUGUI testText;
        public int testNumber = 0;
        public GameData gameData;

        private void Awake()
        {
            testText = this.GetComponent<TextMeshProUGUI>();

            if (gameData == null)
                gameData = new GameData();
        }

        public void LoadData()
        {
            gameData.LoadData();
            Debug.Log($"Loaded Game Data - testNumber: {gameData.testNumber}, firstLifeHighScore: {gameData.firstLifeHighScore}");
        }

        public void SaveData()
        {
            gameData.SaveData();
            Debug.Log($"Saved Game Data - testNumber: {gameData.testNumber}, firstLifeHighScore: {gameData.firstLifeHighScore}");
        }
    }
}