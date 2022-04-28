using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Provides high level functionality to panels in the main menu scene
/// </summary>
public class MainMenu : MonoBehaviour
{
    public GameObject Game_Settings_Panel;

    public void OnPressButtonStartGame()
    {
        SceneManager.LoadScene(1);
    }

    public void OnPressButtonOptions()
    {
        Debug.Log("Game Options Pressed");
        Game_Settings_Panel.SetActive(true);
        gameObject.SetActive(false);
    }
}
