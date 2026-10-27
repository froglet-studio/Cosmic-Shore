namespace CosmicShore.Engine.Experimental.Rendering
{
    /// <summary>
    /// Texture formats by layout (original contract: UnityEngine.Experimental.Rendering.GraphicsFormat), the type a
    /// render graph's <see cref="CosmicShore.Engine.Rendering.TextureDesc.colorFormat"/> takes. The members game code
    /// names, at Unity's values.
    /// </summary>
    public enum GraphicsFormat
    {
        None = 0,
        R8_SRGB = 1,
        R8G8B8A8_SRGB = 4,
        R8_UNorm = 5,
        R8G8B8A8_UNorm = 8,
        R16_SFloat = 45,
        R16G16_SFloat = 46,
        R16G16B16A16_SFloat = 48,
        R32_SFloat = 49,
        R32G32_SFloat = 50,
        R32G32B32A32_SFloat = 52,
    }
}

namespace CosmicShore.Engine.Rendering
{
    /// <summary>A render-graph texture's depth buffer size (original contract: UnityEngine.Rendering.DepthBits).</summary>
    public enum DepthBits
    {
        None = 0,
        Depth8 = 8,
        Depth16 = 16,
        Depth24 = 24,
        Depth32 = 32,
    }
}
