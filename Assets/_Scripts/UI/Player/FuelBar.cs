using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FuelBar : MonoBehaviour
{
    [SerializeField] List<Sprite> fuelLevelImages;
    [SerializeField] Sprite backgroundSprite;
    [SerializeField] bool verboseLogging;

    public Image backgroundImage;
    public Image fuelLevelImage;

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
        //backgroundImage.sprite = fuelLevelImages[0];
        backgroundImage.sprite = backgroundSprite;
        fuelLevelImage.sprite = fuelLevelImages[0];
        currentFuelLevel = maxFuelLevel;
    }

    public void UpdateFuelLevel(string uuid, float amount)
    {
        currentFuelLevel = amount;
        currentFuelLevel = Mathf.Clamp(currentFuelLevel, 0, maxFuelLevel);
        
        if (verboseLogging)
            Debug.Log("Fuel Level is " + currentFuelLevel);

        UpdateFuelBarDisplay(currentFuelLevel);
    }

    public void UpdateFuelBarDisplay(float displayFuelLevel)
    {
        int maxIndex = fuelLevelImages.Count - 1;
        float percentOfFull = (displayFuelLevel / maxFuelLevel);
        int index = maxIndex - (int)Mathf.Floor(percentOfFull * maxIndex);
        
        if (verboseLogging)
            Debug.Log("pof: " + percentOfFull + "MI: " + maxIndex + ", index: " + index);

        fuelLevelImage.sprite = fuelLevelImages[index];
    }
}
