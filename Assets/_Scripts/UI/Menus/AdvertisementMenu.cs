using UnityEngine;
using UnityEngine.UI;
using StarWriter.Core;

public class AdvertisementMenu : MonoBehaviour
{
    
    public Button watchAdButton;
    public Button declineAdButton;
    public Button bedazzledWatchAdButton;
    

    private void OnEnable()
    {
        GameManager.onPlayGame += ResetButtons;
        ScoringManager.onGameOver += OnGameOver;
    }

    private void OnDisable()
    {
        GameManager.onPlayGame -= ResetButtons;
        ScoringManager.onGameOver -= OnGameOver;
    }

    private void Awake()
    {
        ResetButtons();
    }

    public void ResetButtons()
    {
        watchAdButton.gameObject.SetActive(false);
        declineAdButton.gameObject.SetActive(true);
        bedazzledWatchAdButton.gameObject.SetActive(true);
    }

    public void OnClickWatchAdButton()  // called by all ad buttons
    {
        //TODO call Ad to watch
        Debug.Log("Ad requested");
        ResetButtons();
        GameManager.Instance.ExtendGame(); 
    }

    public void OnClickDeclineAdButton()
    {
        ResetButtons();
        GameManager.Instance.ReturnToLobby();
    }

    private void OnGameOver(bool bedazzled, bool advertisement)
    {
        if (advertisement)
        {
            if (bedazzled)
            {
                bedazzledWatchAdButton.gameObject.SetActive(true);
            }
            else
            {
                watchAdButton.gameObject.SetActive(true);
            }
            declineAdButton.gameObject.SetActive(true);
            watchAdButton.onClick.AddListener(() => OnClickWatchAdButton());
            declineAdButton.onClick.AddListener(() => OnClickDeclineAdButton());
        }
        
    }
}
