using UnityEngine;

public class IntensityBar : MonoBehaviour
{
    [Tooltip("Handle onto the Mask element for occluding the intensity fill gradient to show partial fill levels")]
    [SerializeField]
    RectTransform IntensityMask;

    [Tooltip("Current intensity level from 0-1")]
    [SerializeField]
    [Range(0, 1)]
    float Intensity = 1f;
    
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
        MaxMaskWidth = GetComponent<RectTransform>().rect.width; // GetRect
    }

    // Handles updating intensity bar UI
    void LateUpdate()
    {
        IntensityMask.sizeDelta = new Vector2((1 - Intensity) * MaxMaskWidth, IntensityMask.rect.height);
    }

    /// <summary>
    /// Updates intensity on changes in Intensity System
    /// </summary>
    /// <param name="amount">Amount to increase intensity expressed from 0-1</param>
    private void ChangeIntensity(string uuid, float currentIntensity)
    {
        Intensity = currentIntensity;
    }
}
