using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMenu : MonoBehaviour
{
    [SerializeField]
    GameObject fuelMeterPanel;
    [SerializeField]
    GameObject pauseMenuPanel;
    [SerializeField]
    GameObject finalScorePanel;
    [SerializeField]
    GameObject pauseButton;

    private void OnEnable()
    {
        FuelSystem.zeroFuel += GameOver;
    }

    private void OnDisable()
    {
        FuelSystem.zeroFuel -= GameOver;
    }

    /// <summary>
    /// Pauses the game and enables the Pause Menu
    /// </summary>
    public void OnClickPauseGame()
    {
        fuelMeterPanel.SetActive(false);
        finalScorePanel.SetActive(false);
        pauseButton.SetActive(false);
        pauseMenuPanel.SetActive(true);
        PauseSystem.TogglePauseGame();
    }
    /// <summary>
    /// PauseMenu calls this method to enable panels
    /// </summary>
    public void OnClickUnpauseGame()
    {

        fuelMeterPanel.SetActive(true);
        finalScorePanel.SetActive(false);
        pauseButton.SetActive(true);
        pauseMenuPanel.SetActive(false);
        //WARNING INFO: PauseSystem is called on the GameManager and not here
    }
    /// <summary>
    /// Calls the Final and High Score Panel
    /// </summary>
    public void DisplayFinalScorePanel()
    {
        fuelMeterPanel.SetActive(false);
        finalScorePanel.SetActive(true);
        pauseButton.SetActive(false);
        pauseMenuPanel.SetActive(false);
    }
    /// <summary>
    /// Called on Game Over Event
    /// </summary>
    private void GameOver()
    {
        DisplayFinalScorePanel();
    }
}