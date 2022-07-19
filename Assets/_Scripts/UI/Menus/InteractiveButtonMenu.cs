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
        ScoringManager.onAdQualify += ShowAdButtons;
        ScoringManager.onAdDisqualify += ShowScreenshotButton;
    }

    private void OnDisable()
    {
        ScoringManager.onAdQualify -= ShowAdButtons;
        ScoringManager.onAdDisqualify -= ShowScreenshotButton;
    }

    // Start is called before the first frame update
    void Start()
    {
        screenshotButton.gameObject.SetActive(false);
        watchAdButton.gameObject.SetActive(false);
        declineAdButton.gameObject.SetActive(false);

        watchAdButton.onClick.AddListener(() => GameManager.Instance.ExtraLifeGiftedByAd()); //TODO Remove once ads 
    }

    private void ShowAdButtons(bool hotness)
    {
        watchAdButton.gameObject.SetActive(true); //ON
        declineAdButton.gameObject.SetActive(true);  //ON
        screenshotButton.gameObject.SetActive(false);
        if (hotness)
        {
            //TODO bump up watchAdButton flare and mute declineAdButton
        }
    }

    private void ShowScreenshotButton()
    {
        screenshotButton.gameObject.SetActive(true); //ON
        watchAdButton.gameObject.SetActive(false);
        declineAdButton.gameObject.SetActive(false);
    }

}
