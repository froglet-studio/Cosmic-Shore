using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class ShipHUDPanel : MonoBehaviour
{
    public TMP_Text displayMessage;

    //TODO
    //accept ship status messages such as changes in fuel, low fuel warnings, health, general info, countdowns or effects applied
    //display messages
    //apply fade or float away effects to the TMPro text

    // Start is called before the first frame update
    void Start()
    {
        displayMessage.text = "Testing";
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
