using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class RuntimeTextureMemoryUsageWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private List<KeyValuePair<Texture2D, long>> sortedTextures;
    private int topN = 10;

    [MenuItem("Tools/Runtime Texture Memory Usage")]
    public static void ShowWindow()
    {
        GetWindow<RuntimeTextureMemoryUsageWindow>("Runtime Texture Memory Usage");
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
        // Dictionary to hold texture and its memory usage
        Dictionary<Texture2D, long> textureMemoryUsage = new Dictionary<Texture2D, long>();

        // Find all active renderers in the scene
        Renderer[] renderers = FindObjectsOfType<Renderer>();
        foreach (var renderer in renderers)
        {
            // Iterate through all materials in the renderer
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;

                // Get all textures in the material
                foreach (var textureName in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(textureName);

                    if (texture is Texture2D texture2D)
                    {
                        if (!textureMemoryUsage.ContainsKey(texture2D))
                        {
                            // Calculate memory usage for each texture
                            long memoryUsage = CalculateTextureMemoryUsage(texture2D);
                            textureMemoryUsage[texture2D] = memoryUsage;
                        }
                    }
                }
            }
        }

        // Find all UI Images
        Image[] uiImages = FindObjectsOfType<Image>();
        foreach (var uiImage in uiImages)
        {
            if (uiImage.sprite != null && uiImage.sprite.texture != null)
            {
                Texture2D texture2D = uiImage.sprite.texture;
                if (!textureMemoryUsage.ContainsKey(texture2D))
                {
                    long memoryUsage = CalculateTextureMemoryUsage(texture2D);
                    textureMemoryUsage[texture2D] = memoryUsage;
                }
            }
        }

        // Find all UI RawImages
        RawImage[] rawImages = FindObjectsOfType<RawImage>();
        foreach (var rawImage in rawImages)
        {
            if (rawImage.texture is Texture2D texture2D)
            {
                if (!textureMemoryUsage.ContainsKey(texture2D))
                {
                    long memoryUsage = CalculateTextureMemoryUsage(texture2D);
                    textureMemoryUsage[texture2D] = memoryUsage;
                }
            }
        }

        // Find all Terrain components
        Terrain[] terrains = FindObjectsOfType<Terrain>();
        foreach (var terrain in terrains)
        {
            foreach (var texture in terrain.terrainData.terrainLayers.Select(layer => layer.diffuseTexture).OfType<Texture2D>())
            {
                if (!textureMemoryUsage.ContainsKey(texture))
                {
                    long memoryUsage = CalculateTextureMemoryUsage(texture);
                    textureMemoryUsage[texture] = memoryUsage;
                }
            }
        }

        // Find all Lightmaps
        LightmapData[] lightmaps = LightmapSettings.lightmaps;
        foreach (var lightmap in lightmaps)
        {
            if (lightmap.lightmapColor is Texture2D texture2D)
            {
                if (!textureMemoryUsage.ContainsKey(texture2D))
                {
                    long memoryUsage = CalculateTextureMemoryUsage(texture2D);
                    textureMemoryUsage[texture2D] = memoryUsage;
                }
            }
        }

        // Find all Skybox textures
        if (RenderSettings.skybox != null)
        {
            foreach (var textureName in RenderSettings.skybox.GetTexturePropertyNames())
            {
                Texture texture = RenderSettings.skybox.GetTexture(textureName);

                if (texture is Texture2D texture2D)
                {
                    if (!textureMemoryUsage.ContainsKey(texture2D))
                    {
                        long memoryUsage = CalculateTextureMemoryUsage(texture2D);
                        textureMemoryUsage[texture2D] = memoryUsage;
                    }
                }
            }
        }

        // Sort textures by memory usage
        sortedTextures = textureMemoryUsage.OrderByDescending(kvp => kvp.Value).ToList();

        Debug.Log($"Runtime textures counted and sorted by memory usage: {sortedTextures.Count} found");
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
