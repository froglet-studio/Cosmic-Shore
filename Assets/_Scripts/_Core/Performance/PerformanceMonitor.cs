using System.Collections.Generic;
using UnityEngine;

public class PerformanceMonitor : MonoBehaviour
{
    [SerializeField] int NumberOfFramesToAverage = 120;

    Queue<float> SampledFrames = new Queue<float>();

    const float SixtyFPSFrameDuration = .16f;
    const float ThirtyFPSFrameDuration = .33f;
    const float MinimumFPSFrameDuration = 1f;

    float runningAvgFrameDuration;

    void Start()
    {
        
    }

    void Update()
    {
        
    }
}
