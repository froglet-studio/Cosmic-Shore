using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StarWriter.Core;


public class InteractiveButtonMenu : MonoBehaviour
{
    public Button screenshotButton;
    public Button watchAdButton;

    public bool getsAnotherLife;


    // Start is called before the first frame update
    void Start()
    {
        screenshotButton.gameObject.SetActive(true);
        watchAdButton.gameObject.SetActive(false);

        getsAnotherLife = (PlayerPrefs.GetInt(GameSetting.PlayerPrefKeys.getsExtraLife.ToString()) == 1) ? true : false; // 0 false and 1 true


        if (getsAnotherLife)
        {
            watchAdButton.gameObject.SetActive(true);
            screenshotButton.gameObject.SetActive(false);
        }
    }

    public void ExtraLifeGiftedByAd()
    {
        //FuelSystem.ResetFuel();
        PlayerPrefs.SetInt("Gets Free Life", 0);
        GameManager.Instance.RestartGame();
    }
}
