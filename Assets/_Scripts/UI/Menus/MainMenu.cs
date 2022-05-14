using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarWriter.Core.UI
{
    /// <summary>
    /// Provides high level functionality to panels in the main menu scene
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public GameObject Game_Options_Panel;

        public void OnClickPlayGame()
        {
            GameManager.Instance.OnClickPlayButton();
        }

        public void OnPressButtonOptions()
        {
            Debug.Log("Game Options Pressed");
            Game_Options_Panel.SetActive(true);
            gameObject.SetActive(false);
        }

        
    }
}


