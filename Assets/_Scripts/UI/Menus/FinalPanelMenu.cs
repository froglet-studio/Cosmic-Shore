using UnityEngine;
using UnityEngine.UI;
using StarWriter.Core;

public class FinalPanelMenu : MonoBehaviour
{
    GameManager gameManager;
    [SerializeField] SnsShare snsShare;
    [SerializeField] public Button screenshotButton;
    [SerializeField] public Button replayButton;
    [SerializeField] public GameObject toggleObject;

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
        screenshotButton?.onClick.AddListener(() => snsShare.Share());
    }

    void OnGameOver()
    {
        replayButton.gameObject.SetActive(true);
        screenshotButton?.gameObject.SetActive(true);
    }

    public void OnClickReplayButton()
    {
        GameManager.Instance.RestartGame();
    }

    public void OnClickMainMenu()
    {
        gameManager.ReturnToLobby();
    }

    
    public void ToggleGameObject() //So I have a gut feeling that this ISNT where we want this script to live... But it does work.
    {
        if (toggleObject.activeInHierarchy == true)
            toggleObject.SetActive(false);
        else
            toggleObject.SetActive(true);
    }
}