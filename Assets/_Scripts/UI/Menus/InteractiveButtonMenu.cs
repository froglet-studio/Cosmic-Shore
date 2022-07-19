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

    private void OnEnable()
    {
        ScoringManager.onGameOver += OnGameOver;
    }

    private void OnDisable()
    {
        ScoringManager.onGameOver -= OnGameOver;
    }

    // Start is called before the first frame update
    void Start()
    {
        screenshotButton.gameObject.SetActive(false);
        watchAdButton.gameObject.SetActive(false);
        declineAdButton.gameObject.SetActive(false);
    }

    private void OnGameOver(bool bedazzled, bool advertisement)
    {
        if (advertisement)
        {
            if (bedazzled)
            {

            }
        }
        else
        {

        }
    }

    private void ShowAdButtons(bool hotness)
    {
        watchAdButton.gameObject.SetActive(true); //ON
        watchAdButton.onClick.AddListener(() => GameManager.Instance.ExtraLifeGiftedByAd()); //TODO Remove once ads 
        declineAdButton.gameObject.SetActive(true);  //ON
        Debug.Log("Ad button activated");
        screenshotButton.gameObject.SetActive(false);
        if (hotness)
        {
            //TODO bump up watchAdButton flare and mute declineAdButton flashiness
        }
    }

    private void ShowScreenshotButton()
    {
        screenshotButton.gameObject.SetActive(true); //ON
        watchAdButton.gameObject.SetActive(false);
        declineAdButton.gameObject.SetActive(false);
    }

}
