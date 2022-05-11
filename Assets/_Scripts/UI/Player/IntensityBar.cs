using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IntensityBar : MonoBehaviour
{
    [Tooltip("Initial intensity level from 0-1")]
    [SerializeField]
    [Range(0,1)]
    float InitialIntensity = 1;

    [Tooltip("Handle onto the Mask element for occluding the intensity fill gradient to show partial fill levels")]
    [SerializeField]
    RectTransform IntensityMask;

    [Tooltip("Percentage Per Second from 0-1")]
    [SerializeField]
    [Range(0, 1)]
    float IntensityDecayRate = .03f;

    float Intensity;
    float IntensityDecay;
    float MaxMaskWidth;

    private void OnEnable()
    {
        IntensitySystem.onIntensityChange += ChangeIntensity;
    }                                       

    private void OnDisable()
    {
        IntensitySystem.onIntensityChange -= ChangeIntensity;
    }

    private void Start()
    {
        Intensity = InitialIntensity;
        IntensityDecay = IntensityDecayRate;
        MaxMaskWidth = GetComponent<RectTransform>().rect.width; // GetRect
    }

    // Handle decay and updating intensity bar UI
    void Update()
    {
        Intensity = Mathf.Clamp(Intensity - (IntensityDecay * Time.deltaTime), 0, 1);
        IntensityMask.sizeDelta = new Vector2((1 - Intensity) * MaxMaskWidth, IntensityMask.rect.height);
    }

    /// <summary>
    /// Set intensity level
    /// </summary>
    /// <param name="intensity">intensity percentage expressed from 0-1</param>
    public void SetIntensity(string uuid, float intensity)
    {
        Intensity = Mathf.Clamp(intensity, 0, 1);
    }

    /// <summary>
    /// Increase intensity by amount
    /// </summary>
    /// <param name="amount">Amount to increase intensity expressed from 0-1</param>
    public void ChangeIntensity(string uuid, float amount)
    {
        Intensity += amount;
        Intensity = Mathf.Clamp(Intensity, 0, 1);
    }
}
