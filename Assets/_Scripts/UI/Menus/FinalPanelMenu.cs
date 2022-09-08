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
        GameManager.onGameOver += OnGameOver;
    }

    private void OnDisable()
    {
        GameManager.onGameOver -= OnGameOver;
    }

    private void Start()
    {
        screenshotButton.onClick.AddListener(() => snsShare.Share());
        bedazzledScreenshotButton.onClick.AddListener(() => snsShare.Share());
    }

    private void OnGameOver()
    {
        replayButton.gameObject.SetActive(true);
        bedazzledScreenshotButton.gameObject.SetActive(ScoringManager.IsShareBedazzleWorthy);
        screenshotButton.gameObject.SetActive(!ScoringManager.IsShareBedazzleWorthy);
    }

    public void OnClickReplayButton()
    {
        GameManager.Instance.RestartGame();
    }
}
