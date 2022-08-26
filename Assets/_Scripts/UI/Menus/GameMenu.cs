using UnityEngine;
using StarWriter.Core;
using System;

public class GameMenu : MonoBehaviour
{
    [SerializeField] GameObject fuelMeterPanel;
    [SerializeField] GameObject pauseMenuPanel;
    [SerializeField] GameObject finalScorePanel;
    [SerializeField] GameObject pauseButton;
    [SerializeField] GameObject adsPanel;
    [SerializeField] GameObject Controls;

    private void OnEnable()
    {
        GameManager.onPlayGame += ResetMenuPanels;
        GameManager.onDeath += DisplayAdsPanel;
        GameManager.onGameOver += DisplayFinalScorePanel;
        GameManager.onExtendGamePlay += ResetMenuPanels;
    }

    private void OnDisable()
    {
        GameManager.onPlayGame -= ResetMenuPanels;
        GameManager.onDeath -= DisplayAdsPanel;
        GameManager.onGameOver -= DisplayFinalScorePanel;
        GameManager.onExtendGamePlay -= ResetMenuPanels;
    }

    private bool extendGamePlayNeeded = false;

    private void Update()
    {
        // TODO: not sure if this whole thing is necessary... unity seems to complain, but it also seems to work?
        if (extendGamePlayNeeded)
        {
            fuelMeterPanel.SetActive(true);
            finalScorePanel.SetActive(false);
            adsPanel.SetActive(false);
            pauseButton.SetActive(false);
            pauseMenuPanel.SetActive(false);
            Controls.SetActive(false);
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
        Controls.SetActive(false);
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
        Controls.SetActive(true);
        //WARNING INFO: PauseSystem is called on the GameManager and not here
    }
    /// <summary>
    /// Calls the Final and High Score Panel
    /// </summary>
    private void DisplayFinalScorePanel()
    {
        fuelMeterPanel.SetActive(false);
        finalScorePanel.SetActive(true);
        adsPanel.SetActive(false);
        pauseButton.SetActive(false);
        pauseMenuPanel.SetActive(false);
        Controls.SetActive(false);
    }

    private void DisplayAdsPanel()
    {
        adsPanel.SetActive(true);
        Controls.SetActive(false);
    }

    public void OnExtendGamePlay()
    {
        // This looks wacky, but "SetActive" can only be called in the main thread, not through a delegate
        // extendGamePlayNeeded = true;

        ResetMenuPanels();
    }

    private void ResetMenuPanels()
    {
        fuelMeterPanel.SetActive(true);
        finalScorePanel.SetActive(false);
        adsPanel.SetActive(false);
        pauseButton.SetActive(true);
        pauseMenuPanel.SetActive(false);
        Controls.SetActive(true);

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