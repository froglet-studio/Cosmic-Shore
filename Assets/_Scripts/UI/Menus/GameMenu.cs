using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMenu : MonoBehaviour
{
    [SerializeField]
    GameObject intensityMeterPanel;
    [SerializeField]
    GameObject pauseMenuPanel;
    [SerializeField]
    GameObject finalScorePanel;
    [SerializeField]
    GameObject pauseButton;
    

    public void ExitGame()
    {
        Debug.Log("Exit Game");
        SceneManager.LoadScene(0);
    }

    public void PauseGame()
    {
        intensityMeterPanel.SetActive(false);
        finalScorePanel.SetActive(false);
        pauseButton.SetActive(false);
        pauseMenuPanel.SetActive(true);
        PauseSystem.TogglePauseGame();
    }

    public void UnpauseGame()
    {

        intensityMeterPanel.SetActive(true);
        finalScorePanel.SetActive(false);
        pauseButton.SetActive(true);
        pauseMenuPanel.SetActive(false);
        PauseSystem.TogglePauseGame();
    }

    public void OnFinalScoreScene()
    {
        intensityMeterPanel.SetActive(false);
        finalScorePanel.SetActive(true);
        pauseButton.SetActive(false);
        pauseMenuPanel.SetActive(false);
    }
}