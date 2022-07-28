using UnityEngine;
using UnityEngine.UI;
using StarWriter.Core;

public class FinalPanelMenu : MonoBehaviour
{
    public Button bedazzledScreenshotButton;
    public Button screenshotButton;
    public Button replayButton;
    [SerializeField] SnsShare snsShare;

    private void OnEnable()
    {
        GameManager.onPlayGame += ResetButtons;
        GameManager.onGameOver += OnGameOver;
    }

    private void OnDisable()
    {
        GameManager.onPlayGame -= ResetButtons;
        GameManager.onGameOver -= OnGameOver;
    }

    private void OnGameOver()
    {
        replayButton.gameObject.SetActive(true);
        bedazzledScreenshotButton.gameObject.SetActive(ScoringManager.IsScoreBedazzleWorthy);
        bedazzledScreenshotButton.onClick.AddListener(() => snsShare.Share());
        screenshotButton.gameObject.SetActive(!ScoringManager.IsScoreBedazzleWorthy);
        screenshotButton.onClick.AddListener(() => snsShare.Share());
    }

    public void OnClickReplayButton()
    {
        GameManager.Instance.RestartGame();
    }

    public void ResetButtons()
    {
        screenshotButton.gameObject.SetActive(false);
        bedazzledScreenshotButton.gameObject.SetActive(false);
        replayButton.gameObject.SetActive(false);
    }
}
