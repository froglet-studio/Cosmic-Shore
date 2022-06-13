using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core
{
    public class TutorialMenu : MonoBehaviour
    {
        GameManager gameManager;
    
        void Start()
        {
            gameManager = GameManager.Instance;
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

