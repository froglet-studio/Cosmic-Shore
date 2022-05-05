using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMenu : MonoBehaviour
{
    

    public void ExitGame()
    {
        Debug.Log("Exit Game");
        SceneManager.LoadScene(0);
    }

    public void TogglePauseGame()
    {
        PauseSystem.TogglePauseGame();
    }
}