using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core
{
    public class TutorialMenu : MonoBehaviour
    {
        GameManager gameManager;

        [SerializeField]
        public GameObject tutorialPlayer;
    
        void Start()
        {
            gameManager = GameManager.Instance;
            tutorialPlayer.SetActive(false);
        }

        public void OnClickMainMenu()
        {
            gameManager.ReturnToLobby();
        }

        public void OnClickPlayGame()
        {
            gameManager.RestartGame();
        }
    }
}

