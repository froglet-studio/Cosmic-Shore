using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CosmicShore.Game.Arcade;

public class TimeBasedTurnMonitor : TurnMonitor
{
    [SerializeField] protected float duration;
    [SerializeField] protected float warningThreshold = 10f; // When to start pulsing
    [SerializeField] protected float pulseSpeed = 2f; // How fast the pulsing happens
    [SerializeField] protected Color pulseColor = new Color(1f, 0f, 0f, 0.5f); // Semi-transparent red

    [SerializeField] protected Image[] uiImages;
    [SerializeField] protected TMP_Text[] uiTexts;

    protected float elapsedTime;
    protected bool timerActive = false;  // Added so child classes can control the timer

    Color[] originalImageColors;
    Color[] originalTextColors;

    protected virtual void Start()
    {
        originalImageColors = new Color[uiImages.Length];
        for (int i = 0; i < uiImages.Length; i++)
            if (uiImages[i] != null) originalImageColors[i] = uiImages[i].color;

        originalTextColors = new Color[uiTexts.Length];
        for (int i = 0; i < uiTexts.Length; i++)
            if (uiTexts[i] != null) originalTextColors[i] = uiTexts[i].color;

        if (Display != null)
            Display.text = Mathf.Max(0, (int)duration).ToString();
    }

    public override bool CheckForEndOfTurn()
    {
        if (paused || !timerActive) return false;
        return elapsedTime > duration;
    }

    public override void NewTurn(string playerName)
    {
        ResetTimer();
        timerActive = true;
    }

    protected virtual void Update()
    {
        if (paused || !timerActive) return;

        elapsedTime += Time.deltaTime;
        float remainingTime = duration - elapsedTime;

        if (Display != null)
            Display.text = Mathf.Max(0, (int)remainingTime).ToString();

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

    protected void StartTimer()
    {
        timerActive = true;
        elapsedTime = 0;
    }

    protected void StopTimer()
    {
        timerActive = false;
        ResetTimer();
    }

    protected void ResetTimer()
    {
        elapsedTime = 0;
        ResetColors();
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