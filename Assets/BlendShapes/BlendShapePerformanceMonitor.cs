using UnityEngine;
using UnityEngine.Profiling;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
            
            string message = $"[Performance] FPS: {fps:F1} | Memory: {memoryUsage}MB";
#if UNITY_EDITOR
            message += $" | Draw Calls: {UnityStats.drawCalls} | Vertices: {UnityStats.vertices}";
#endif
            Debug.Log(message);
            
            timer = 0f;
            frameCount = 0;
        }
    }
}
