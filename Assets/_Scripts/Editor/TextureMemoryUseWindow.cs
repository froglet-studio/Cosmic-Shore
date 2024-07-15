using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TextureMemoryUsageWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private List<KeyValuePair<Texture2D, long>> sortedTextures;
    private int topN = 10;

    [MenuItem("Tools/Texture Memory Usage")]
    public static void ShowWindow()
    {
        GetWindow<TextureMemoryUsageWindow>("Texture Memory Usage");
    }

    void OnGUI()
    {
        if (GUILayout.Button("Find Largest Memory Consuming Textures"))
        {
            FindLargestMemoryTextures();
        }

        GUILayout.Label("Top N Textures to Display:");
        topN = EditorGUILayout.IntField(topN);

        if (sortedTextures != null)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            foreach (var kvp in sortedTextures.Take(topN))
            {
                GUILayout.Label($"Texture: {kvp.Key.name}, Memory Usage: {kvp.Value / 1024.0 / 1024.0:F2} MB");
            }
            GUILayout.EndScrollView();
        }
    }

    void FindLargestMemoryTextures()
    {
        // Find all textures in the scene
        Texture2D[] textures = Resources.FindObjectsOfTypeAll<Texture2D>();

        // Dictionary to hold texture and its memory usage
        Dictionary<Texture2D, long> textureMemoryUsage = new Dictionary<Texture2D, long>();

        foreach (var texture in textures)
        {
            // Calculate memory usage for each texture
            long memoryUsage = CalculateTextureMemoryUsage(texture);
            textureMemoryUsage[texture] = memoryUsage;
        }

        // Sort textures by memory usage
        sortedTextures = textureMemoryUsage.OrderByDescending(kvp => kvp.Value).ToList();

        Debug.Log("Scene textures counted and sorted by memory usage.");
    }

    long CalculateTextureMemoryUsage(Texture2D texture)
    {
        // Calculate memory usage based on texture format and dimensions
        int width = texture.width;
        int height = texture.height;
        int bitsPerPixel = GetBitsPerPixel(texture.format);
        long memoryUsage = width * height * bitsPerPixel / 8;

        return memoryUsage;
    }

    int GetBitsPerPixel(TextureFormat format)
    {
        // Return the bits per pixel for the given texture format
        switch (format)
        {
            case TextureFormat.Alpha8: return 8;
            case TextureFormat.ARGB4444: return 16;
            case TextureFormat.RGBA32: return 32;
            case TextureFormat.ARGB32: return 32;
            case TextureFormat.RGB24: return 24;
            case TextureFormat.RGBAHalf: return 64;
            case TextureFormat.RGBAFloat: return 128;
            // Add cases for other formats as needed
            default: return 32; // Default to 32 bits per pixel for unknown formats
        }
    }
}
