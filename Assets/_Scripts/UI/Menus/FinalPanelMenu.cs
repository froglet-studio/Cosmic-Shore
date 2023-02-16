using UnityEngine;
using UnityEngine.UI;
using StarWriter.Core;

public class FinalPanelMenu : MonoBehaviour
{
    GameManager gameManager;
    [SerializeField] SnsShare snsShare;
    [SerializeField] public Button screenshotButton;
    [SerializeField] public Button replayButton;

    void OnEnable()
    {
        GameManager.onGameOver += OnGameOver;
    }

    void OnDisable()
    {
        GameManager.onGameOver -= OnGameOver;
    }

    void Start()
    {
        gameManager = GameManager.Instance;
        screenshotButton.onClick.AddListener(() => snsShare.Share());
    }

    void OnGameOver()
    {
        replayButton.gameObject.SetActive(true);
        screenshotButton.gameObject.SetActive(true);
    }

    public void OnClickReplayButton()
    {
        GameManager.Instance.RestartGame();
    }

    public void OnClickMainMenu()
    {
        gameManager.ReturnToLobby();
    }
}