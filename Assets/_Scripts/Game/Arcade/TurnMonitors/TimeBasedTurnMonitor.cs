using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CosmicShore.Game.Arcade;

public class TimeBasedTurnMonitor : TurnMonitor
{
    [SerializeField] float duration;
    [SerializeField] float warningThreshold = 10f; // When to start pulsing
    [SerializeField] float pulseSpeed = 2f; // How fast the pulsing happens
    [SerializeField] Color pulseColor = new Color(1f, 0f, 0f, 0.5f); // Semi-transparent red

    [SerializeField] Image[] uiImages; // UI elements to pulse (e.g., icons, backgrounds)
    [SerializeField] TMP_Text[] uiTexts; // UI text elements to pulse

    float elapsedTime;
    Color[] originalImageColors;
    Color[] originalTextColors;

    void Start()
    {
        // Store original colors to revert back when the timer resets
        originalImageColors = new Color[uiImages.Length];
        for (int i = 0; i < uiImages.Length; i++)
            if (uiImages[i] != null) originalImageColors[i] = uiImages[i].color;

        originalTextColors = new Color[uiTexts.Length];
        for (int i = 0; i < uiTexts.Length; i++)
            if (uiTexts[i] != null) originalTextColors[i] = uiTexts[i].color;
    }

    public override bool CheckForEndOfTurn()
    {
        if (paused) return false;
        return elapsedTime > duration;
    }

    public override void NewTurn(string playerName)
    {
        elapsedTime = 0;
        ResetColors();
    }

    void Update()
    {
        if (paused) return;

        elapsedTime += Time.deltaTime;
        float remainingTime = duration - elapsedTime;

        // Update the display text
        if (Display != null)
            Display.text = Mathf.Max(0, (int)remainingTime).ToString();

        // Apply pulsing effect when time is below threshold
        if (remainingTime < warningThreshold)
        {
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f; // Smooth oscillation between 0 and 1
            ApplyPulseEffect(pulse);
        }
        else
        {
            ResetColors();
        }
    }

    void ApplyPulseEffect(float pulse)
    {
        Color pulsingImageColor = Color.Lerp(originalImageColors[0], pulseColor, pulse);
        Color pulsingTextColor = Color.Lerp(originalTextColors[0], pulseColor, pulse);

        for (int i = 0; i < uiImages.Length; i++)
            if (uiImages[i] != null) uiImages[i].color = pulsingImageColor;

        for (int i = 0; i < uiTexts.Length; i++)
            if (uiTexts[i] != null) uiTexts[i].color = pulsingTextColor;
    }

    void ResetColors()
    {
        for (int i = 0; i < uiImages.Length; i++)
            if (uiImages[i] != null) uiImages[i].color = originalImageColors[i];

        for (int i = 0; i < uiTexts.Length; i++)
            if (uiTexts[i] != null) uiTexts[i].color = originalTextColors[i];
    }
}
