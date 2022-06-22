using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FuelBar : MonoBehaviour
{
    [SerializeField]
    List<Sprite> fuelLevelImages;

    public Image backgroundImage;
    public Image fuelLevelImage;

    public float maxFuelLevel = 1f;
    public float fuelLevel; 


    private void OnEnable()
    {
        FuelSystem.onFuelChange += ChangeFuelLevel;
    }

    private void OnDisable()
    {
        FuelSystem.onFuelChange -= ChangeFuelLevel;
    }

    // Start is called before the first frame update
    void Start()
    {
        backgroundImage.sprite = fuelLevelImages[0];
        fuelLevelImage.sprite = fuelLevelImages[1]; //starts at full health
        fuelLevel = maxFuelLevel;
    }


    public void ChangeFuelLevel(string uuid, float amount)
    {
        fuelLevel += fuelLevel + amount;
        int fuelLevelInt = ConvertFloatFuelLevelToInteger(fuelLevel);

        fuelLevelInt = Mathf.Clamp(fuelLevelInt, 1, 15);
        UpdateFuelBarDisplay(fuelLevelInt);
    }

    public void UpdateFuelBarDisplay(int displayFuelLevel)
    {
        fuelLevelImage.enabled = true;
        switch (displayFuelLevel)
        {
            case 1: 
                fuelLevelImage.sprite = fuelLevelImages[15];
                break;
            case 2:
                fuelLevelImage.sprite = fuelLevelImages[14];
                break;
            case 3:
                fuelLevelImage.sprite = fuelLevelImages[13];
                break;
            case 4:
                fuelLevelImage.sprite = fuelLevelImages[12];
                break;
            case 5:
                fuelLevelImage.sprite = fuelLevelImages[11];
                break;
            case 6:
                fuelLevelImage.sprite = fuelLevelImages[10];
                break;
            case 7:
                fuelLevelImage.sprite = fuelLevelImages[9];
                break;
            case 8:
                fuelLevelImage.sprite = fuelLevelImages[8];
                break;
            case 9:
                fuelLevelImage.sprite = fuelLevelImages[7];
                break;
            case 10:
                fuelLevelImage.sprite = fuelLevelImages[6];
                break;
            case 11:
                fuelLevelImage.sprite = fuelLevelImages[5];
                break;
            case 12:
                fuelLevelImage.sprite = fuelLevelImages[4];
                break;
            case 13:
                fuelLevelImage.sprite = fuelLevelImages[3];
                break;
            case 14:
                fuelLevelImage.sprite = fuelLevelImages[2];
                break;
            case 15:
                fuelLevelImage.sprite = fuelLevelImages[1];
                break;
        }
    }

    int ConvertFloatFuelLevelToInteger(float amount)
    {
        int returnFuelValue = -1;
        float fuelBarLevels = 14f;
        float multiplier = 1/fuelBarLevels;
        float amountMax = 1f;

        amount = Mathf.Clamp(amount, 0, amountMax);

        switch (amount)
        {
            case float n when (n == amountMax):
                returnFuelValue = 1;
                break;
            case float n when (n < amountMax && n > (amountMax - multiplier)):
                returnFuelValue = 2;
                break;
            case float n when (n < (amountMax - multiplier) && n > (amountMax - 2 * multiplier)):
                returnFuelValue = 3;
                break;
            case float n when (n < (amountMax - 2 * multiplier) && n > (amountMax - 3 * multiplier)):
                returnFuelValue = 4;
                break;
            case float n when (n < (amountMax - 3 * multiplier) && n > (amountMax - 4 * multiplier)):
                returnFuelValue = 5;
                break;
            case float n when (n < (amountMax - 4 * multiplier) && n > (amountMax - 5 * multiplier)):
                returnFuelValue = 6;
                break;
            case float n when (n < (amountMax - 5 * multiplier) && n > (amountMax - 6 * multiplier)):
                returnFuelValue = 7;
                break;
            case float n when (n < (amountMax - 6 * multiplier) && n > (amountMax - 7 * multiplier)):
                returnFuelValue = 8;
                break;
            case float n when (n < (amountMax - 7 * multiplier) && n > (amountMax - 8 * multiplier)):
                returnFuelValue = 9;
                break;
            case float n when (n < (amountMax - 8 * multiplier) && n > (amountMax - 9 * multiplier)):
                returnFuelValue = 10;
                break;
            case float n when (n < (amountMax - 9 * multiplier) && n > (amountMax - 10 * multiplier)):
                returnFuelValue = 11;
                break;
            case float n when (n < (amountMax - 10 * multiplier) && n > (amountMax - 11 * multiplier)):
                returnFuelValue = 12;
                break;
            case float n when (n < (amountMax - 11 * multiplier) && n > (amountMax - 12 * multiplier)):
                returnFuelValue = 13;
                break;
            case float n when (n < (amountMax - 12 * multiplier) && n > (amountMax - 13 * multiplier)):
                returnFuelValue = 14;
                break;
            case float n when (n == 0):
                returnFuelValue = 15;
                break;
        }
        if(returnFuelValue == -1) // for Testing Only 
        {
            Debug.Log("FuelSyste switch condition malue not changed properly");
        }
        return returnFuelValue;
    }
}
