using UnityEngine;
using UnityEngine.UI;
using StarWriter.Core;

public class FinalPanelMenu : MonoBehaviour
{
    public Button bedazzledScreenshotButton;
    public Button screenshotButton;
    public Button replayButton;

    private void OnEnable()
    {
        ScoringManager.onGameOver += OnGameOver;
        GameManager.onPlayGame += ResetButtons;
    }

    private void OnDisable()
    {
        ScoringManager.onGameOver -= OnGameOver;
        GameManager.onPlayGame -= ResetButtons;
    }

    private void OnGameOver(bool bedazzled, bool advertisement)
    {
        replayButton.gameObject.SetActive(true);
        if (!advertisement)
        {
            if (bedazzled)
            {
                bedazzledScreenshotButton.gameObject.SetActive(true);
            }
            else
            {
                screenshotButton.gameObject.SetActive(true);
            }
            screenshotButton.onClick.AddListener(() => gameObject.GetComponent<SnsShare>().Share());
        }
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
