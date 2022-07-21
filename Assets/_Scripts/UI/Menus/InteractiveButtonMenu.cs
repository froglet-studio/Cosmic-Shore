using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StarWriter.Core;
using System;

public class InteractiveButtonMenu : MonoBehaviour
{
    public Button screenshotButton;
    public Button watchAdButton;
    public Button declineAdButton;
    public Button bedazzledWatchAdButton;
    public Button dullDeclineAdButton;

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

    public void ResetButtons()
    {
        screenshotButton.gameObject.SetActive(false);
        watchAdButton.gameObject.SetActive(false);
        declineAdButton.gameObject.SetActive(false);
        bedazzledWatchAdButton.gameObject.SetActive(false);
        dullDeclineAdButton.gameObject.SetActive(false);
    }

    public void OnClickWatchAdButton()  // called by all ad buttons
    {
        //TODO call Ad to watch
        
        GameManager.Instance.ExtendGame();
        ResetButtons();
    }

    public void OnClickDeclineAdButton()
    {
        ResetButtons();
        GameManager.Instance.ReturnToLobby();
    }

    private void OnGameOver(bool bedazzled, bool advertisement)
    {
        if (advertisement && !bedazzled)
        {
            watchAdButton.gameObject.SetActive(true);
            watchAdButton.onClick.AddListener(() => OnClickWatchAdButton());
            declineAdButton.gameObject.SetActive(true);
            declineAdButton.onClick.AddListener(() => OnClickDeclineAdButton());
            
        }else if (advertisement && bedazzled)
        {
            bedazzledWatchAdButton.gameObject.SetActive(true);
            watchAdButton.onClick.AddListener(() => OnClickWatchAdButton());
            dullDeclineAdButton.gameObject.SetActive(true);
            declineAdButton.onClick.AddListener(() => OnClickDeclineAdButton());
        }
        else
        {
            screenshotButton.gameObject.SetActive(true);
            screenshotButton.onClick.AddListener(() => gameObject.GetComponent<SnsShare>().Share());
        }
    }

}
