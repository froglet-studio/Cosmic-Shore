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
        GameManager.onPlayGame += ResetPanels;
        GameManager.onDeath += OnDeath;
        GameManager.onGameOver += OnGameOver;
        GameManager.onExtendGamePlay += OnExtendGamePlay;
    }

    private void OnDisable()
    {
        GameManager.onPlayGame -= ResetPanels;
        GameManager.onDeath -= OnDeath;
        GameManager.onGameOver -= OnGameOver;
        GameManager.onExtendGamePlay -= OnExtendGamePlay;
    }

    private bool extendGamePlayNeeded = false;

    private void Update()
    {
        if (extendGamePlayNeeded)
        {
            fuelMeterPanel.SetActive(true);
            finalScorePanel.SetActive(false);
            adsPanel.SetActive(false);
            pauseButton.SetActive(false);
            pauseMenuPanel.SetActive(false);

            extendGamePlayNeeded = false;
        }
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
        DisplayFinalScorePanel();
        adsPanel.SetActive(true);
    }

    /// <summary>
    /// Called on Game Over Event
    /// </summary>
    private void OnGameOver()
    {
        adsPanel.SetActive(false);
        DisplayFinalScorePanel();
    }

    public void OnExtendGamePlay()
    {
        // This looks wacky, but "SetActive" can only be called in the main thread, not through a delegate
        extendGamePlayNeeded = true;
    }

    private void ResetPanels()
    {
        fuelMeterPanel.SetActive(true);
        finalScorePanel.SetActive(false);
        adsPanel.SetActive(false);
        pauseButton.SetActive(true);
        pauseMenuPanel.SetActive(false);
    }

    public void OnClickDeclineAdsButton()
    {
        adsPanel.gameObject.SetActive(false);
        GameManager.EndGame();
    }

    public void OnClickShowAdsButton()
    {
        adsPanel.gameObject.SetActive(false);

        // TODO: this is questionable - probably want to link this up in the AdsManager events
        GameManager.EndGame();
    }
}