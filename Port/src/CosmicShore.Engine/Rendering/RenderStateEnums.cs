using System;

namespace CosmicShore.Engine.Rendering
{
    /// <summary>
    /// Original-engine face culling (UnityEngine.Rendering.CullMode). Values frozen to the original:
    /// material setup code writes them into the <c>_Cull</c> float.
    /// </summary>
    public enum CullMode
    {
        Off = 0,
        Front = 1,
        Back = 2,
    }

    /// <summary>Original-engine render queue anchors (UnityEngine.Rendering.RenderQueue); values frozen.</summary>
    public enum RenderQueue
    {
        Background = 1000,
        Geometry = 2000,
        AlphaTest = 2450,
        GeometryLast = 2500,
        Transparent = 3000,
        Overlay = 4000,
    }

    /// <summary>Original-engine texture copy capabilities (UnityEngine.Rendering.CopyTextureSupport); values frozen.</summary>
    [Flags]
    public enum CopyTextureSupport
    {
        None = 0,
        Basic = 1,
        Copy3D = 2,
        DifferentTypes = 4,
        TextureToRT = 8,
        RTToTexture = 16,
    }
}

namespace CosmicShore.Engine
{
    public static partial class SystemInfo
    {
        /// <summary>
        /// Original contract: which <see cref="Graphics.CopyTexture(Texture, int, int, Texture, int, int)"/>
        /// forms the GPU supports. This engine implements no GPU texture copy, so it reports
        /// <see cref="Rendering.CopyTextureSupport.None"/> and callers take their fallback path.
        /// </summary>
        public static Rendering.CopyTextureSupport copyTextureSupport => Rendering.CopyTextureSupport.None;
    }
}
