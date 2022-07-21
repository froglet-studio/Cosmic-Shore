using UnityEngine;
using StarWriter.Core;
using System;

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
    [SerializeField]
    GameObject adsPanel;

    private void OnEnable()
    {
        ScoringManager.onGameOverPre += OnGameOverPre;
        ScoringManager.onGameOver += OnGameOver;
        GameManager.onPlayGame += ResetPanels;
        GameManager.onDeath += OnDeath;
    }

    private void OnDisable()
    {
        ScoringManager.onGameOverPre -= OnGameOverPre;
        ScoringManager.onGameOver -= OnGameOver;
        GameManager.onPlayGame -= ResetPanels;
        GameManager.onDeath -= OnDeath;
    }

    
    /// <summary>
    /// Pauses the game and enables the Pause Menu
    /// </summary>
    public void OnClickPauseGame()
    {
        fuelMeterPanel.SetActive(false);
        finalScorePanel.SetActive(false);
        adsPanel.SetActive(false);
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
        adsPanel.SetActive(false);
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
        adsPanel.SetActive(false);
        pauseButton.SetActive(false);
        pauseMenuPanel.SetActive(false);
    }
    private void OnDeath()
    {
        adsPanel.SetActive(true);
    }

    private void OnGameOverPre()
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Called on Game Over Event
    /// </summary>
    private void OnGameOver(bool bedazzled, bool advertisement)
    {
        DisplayFinalScorePanel();
    }

    private void ResetPanels()
    {
        fuelMeterPanel.SetActive(true);
        finalScorePanel.SetActive(false);
        adsPanel.SetActive(false);
        pauseButton.SetActive(true);
        pauseMenuPanel.SetActive(false);
    }
}