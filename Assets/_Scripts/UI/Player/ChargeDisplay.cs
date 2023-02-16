using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChargeDisplay : MonoBehaviour
{
    [SerializeField] TMP_Text fuelLevelText;
    [SerializeField] List<Sprite> fuelLevelImages;
    [SerializeField] Sprite backgroundSprite;
    [SerializeField] bool verboseLogging;
    [SerializeField] Image backgroundImage;
    [SerializeField] Image fuelLevelImage;
    [SerializeField] StarChanger starChanger;
    [SerializeField] float majaNumber = 1;
    
    readonly float maxChargeLevel = 1f;
    float currentChargeLevel;

    public static readonly float OneFuelUnit = 1/7f;

    void Start()
    {
        backgroundImage.sprite = backgroundSprite;
        fuelLevelImage.sprite = fuelLevelImages[0];
        currentChargeLevel = maxChargeLevel;
    }

    public void UpdateDisplay(float newChargeLevel)
    {
        currentChargeLevel = Mathf.Clamp(newChargeLevel, 0, maxChargeLevel);

        // Change the color of the stars
        if (starChanger != null)
            starChanger.UpdateFuelLevel(currentChargeLevel);

        // bucket the percent of full and use it as an index into the sprite list
        int maxIndex = fuelLevelImages.Count - 1;
        float percentOfFull = (currentChargeLevel / maxChargeLevel);
        int index = maxIndex - (int)Mathf.Floor(percentOfFull * maxIndex);

        if (verboseLogging)
            Debug.Log($"FuelBar.UpdateFuelBarDisplay - percentOfFull:{percentOfFull}, maxIndex:{maxIndex}, index:{index}");

        fuelLevelImage.sprite = fuelLevelImages[index];

        fuelLevelText.text = (currentChargeLevel * 100/majaNumber).ToString("F0");
    }
}