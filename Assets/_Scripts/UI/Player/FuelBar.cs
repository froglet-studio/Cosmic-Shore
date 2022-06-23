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

    float fuelBarLevels = 14f;

    public float maxFuelLevel = 1f;
    public float currentFuelLevel; 


    private void OnEnable()
    {
        FuelSystem.onFuelChange += UpdateFuelLevel;
    }

    private void OnDisable()
    {
        FuelSystem.onFuelChange -= UpdateFuelLevel;
    }

    // Start is called before the first frame update
    void Start()
    {
        backgroundImage.sprite = fuelLevelImages[0];
        fuelLevelImage.sprite = fuelLevelImages[1]; //starts at full health
        currentFuelLevel = maxFuelLevel;
    }


    public void UpdateFuelLevel(string uuid, float amount)
    {
        currentFuelLevel = amount;
        currentFuelLevel = Mathf.Clamp(currentFuelLevel, 0, maxFuelLevel);
        Debug.Log("Fuel Level is " + currentFuelLevel);
     
        UpdateFuelBarDisplay(currentFuelLevel);
    }

    public void UpdateFuelBarDisplay(float displayFuelLevel)
    {
        float multiplier = 1 / fuelBarLevels;

        fuelLevelImage.enabled = true;

        switch (displayFuelLevel)
        {
            case float n when (n == maxFuelLevel):
                fuelLevelImage.sprite = fuelLevelImages[1];
                break;
            case float n when (n < maxFuelLevel && n > (maxFuelLevel - multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[2];
                break;
            case float n when (n < (maxFuelLevel - multiplier) && n > (maxFuelLevel - 2 * multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[3];
                break;
            case float n when (n < (maxFuelLevel - 2 * multiplier) && n > (maxFuelLevel - 3 * multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[4];
                break;
            case float n when (n < (maxFuelLevel - 3 * multiplier) && n > (maxFuelLevel - 4 * multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[5];
                break;
            case float n when (n < (maxFuelLevel - 4 * multiplier) && n > (maxFuelLevel - 5 * multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[6];
                break;
            case float n when (n < (maxFuelLevel - 5 * multiplier) && n > (maxFuelLevel - 6 * multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[7];
                break;
            case float n when (n < (maxFuelLevel - 6 * multiplier) && n > (maxFuelLevel - 7 * multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[8];
                break;
            case float n when (n < (maxFuelLevel - 7 * multiplier) && n > (maxFuelLevel - 8 * multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[9];
                break;
            case float n when (n < (maxFuelLevel - 8 * multiplier) && n > (maxFuelLevel - 9 * multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[10];
                break;
            case float n when (n < (maxFuelLevel - 9 * multiplier) && n > (maxFuelLevel - 10 * multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[11];
                break;
            case float n when (n < (maxFuelLevel - 10 * multiplier) && n > (maxFuelLevel - 11 * multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[12];
                break;
            case float n when (n < (maxFuelLevel - 11 * multiplier) && n > (maxFuelLevel - 12 * multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[13];
                break;
            case float n when (n < (maxFuelLevel - 12 * multiplier) && n > (maxFuelLevel - 13 * multiplier)):
                fuelLevelImage.sprite = fuelLevelImages[14]; 
                break;
            case float n when (n == 0):
                fuelLevelImage.sprite = fuelLevelImages[15];
                break;
        }
    }
}
