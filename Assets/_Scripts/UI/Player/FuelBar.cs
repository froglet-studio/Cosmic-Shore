using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FuelBar : MonoBehaviour
{
    [SerializeField] TMP_Text fuelLevelText;
    [SerializeField] List<Sprite> fuelLevelImages;
    [SerializeField] Sprite backgroundSprite;
    [SerializeField] bool verboseLogging;

    public Image backgroundImage;
    public Image fuelLevelImage;

    public float maxFuelLevel = 1f;
    public float currentFuelLevel;

    private void OnEnable()
    {
        FuelSystem.OnFuelChange += UpdateFuelLevel;
    }

    private void OnDisable()
    {
        FuelSystem.OnFuelChange -= UpdateFuelLevel;
    }

    void Start()
    {
        backgroundImage.sprite = backgroundSprite;
        fuelLevelImage.sprite = fuelLevelImages[0];
        currentFuelLevel = maxFuelLevel;
    }

    public void UpdateFuelLevel(float amount)
    {
        currentFuelLevel = Mathf.Clamp(amount, 0, maxFuelLevel);

        UpdateFuelBarDisplay();

        if (verboseLogging)
            Debug.Log($"FuelBar.UpdateFuelBarDisplay - currentFuelLevel:{currentFuelLevel}");
    }

    public void UpdateFuelBarDisplay()
    {
        // bucket the percent of full and use it as an index into the sprite list
        int maxIndex = fuelLevelImages.Count-1;
        float percentOfFull = (currentFuelLevel / maxFuelLevel);
        int index = maxIndex - (int)Mathf.Floor(percentOfFull * maxIndex);
        
        if (verboseLogging)
            Debug.Log($"FuelBar.UpdateFuelBarDisplay - percentOfFull:{percentOfFull}, maxIndex:{maxIndex}, index:{index}");

        fuelLevelImage.sprite = fuelLevelImages[index];

        fuelLevelText.text = (currentFuelLevel * 100).ToString("F0");
    }
}