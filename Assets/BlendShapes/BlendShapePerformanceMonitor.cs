using UnityEngine;
using UnityEngine.Profiling;

public class BlendShapePerformanceMonitor : MonoBehaviour
{
    [Header("Monitoring")]
    public bool enableMonitoring = true;
    public float updateInterval = 1f;
    
    private float timer = 0f;
    private int frameCount = 0;
    private float fps = 0f;
    private long memoryUsage = 0;
    
    void Update()
    {
        if (!enableMonitoring) return;
        
        timer += Time.deltaTime;
        frameCount++;
        
        if (timer >= updateInterval)
        {
            fps = frameCount / timer;
            memoryUsage = Profiler.GetTotalAllocatedMemoryLong() / 1048576; // Convert to MB
            
            Debug.Log($"[Performance] FPS: {fps:F1} | Memory: {memoryUsage}MB | " +
                     $"Draw Calls: {UnityStats.drawCalls} | Vertices: {UnityStats.vertices}");
            
            timer = 0f;
            frameCount = 0;
        }
    }
}
