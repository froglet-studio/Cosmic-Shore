using UnityEngine;
using StarWriter.Core;

public class GameMenu : MonoBehaviour
{
    [SerializeField] GameObject fuelMeterPanel;
    [SerializeField] GameObject scorePanel;
    [SerializeField] GameObject pauseMenuPanel;
    [SerializeField] GameObject finalScorePanel;
    [SerializeField] GameObject pauseButton;
    [SerializeField] GameObject adsPanel;
    [SerializeField] GameObject Controls;

    void OnEnable()
    {
        GameManager.onPlayGame += ResetMenuPanels;
        GameManager.onDeath += DisplayAdsPanel;
        GameManager.onGameOver += DisplayFinalScorePanel;
    }

    void OnDisable()
    {
        GameManager.onPlayGame -= ResetMenuPanels;
        GameManager.onDeath -= DisplayAdsPanel;
        GameManager.onGameOver -= DisplayFinalScorePanel;
    }

    bool extendGamePlayNeeded = false;

    void Update()
    {
        if (extendGamePlayNeeded)
        {
            // This looks wacky, but "SetActive" can only be called in the main thread, not through a delegate
            ResetMenuPanels();
            extendGamePlayNeeded = false;
        }
    }

    /// <summary>
    /// Pauses the game and enables the Pause Menu
    /// </summary>
    public void OnClickPauseGame()
    {
        fuelMeterPanel.SetActive(false);
        scorePanel.SetActive(false);
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
        scorePanel.SetActive(true);
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
    void DisplayFinalScorePanel()
    {
        fuelMeterPanel.SetActive(false);
        scorePanel.SetActive(false);
        finalScorePanel.SetActive(true);
        adsPanel.SetActive(false);
        pauseButton.SetActive(false);
        pauseMenuPanel.SetActive(false);
        Controls.SetActive(false);
    }

    void DisplayAdsPanel()
    {
        adsPanel.SetActive(true);
        Controls.SetActive(false);
    }

    public void OnExtendGamePlay()
    {
        // This looks wacky, but "SetActive" can only be called in the main thread, not through a delegate
        extendGamePlayNeeded = true;
    }

    void ResetMenuPanels()
    {
        fuelMeterPanel.SetActive(true);
        scorePanel.SetActive(true);
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
    }
}