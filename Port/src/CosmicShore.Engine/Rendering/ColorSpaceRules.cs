namespace CosmicShore.Engine
{
    /// <summary>
    /// How a colour reaches the GPU in a Linear colour-space project (the project's setting:
    /// ProjectSettings m_ActiveColorSpace 1), as Unity documents it:
    /// <list type="bullet">
    /// <item>a plain Color material property is authored and stored in sRGB and converted to linear
    /// on its way to the GPU;</item>
    /// <item>an [HDR] Color property is stored linear (the HDR picker's intensity doubles linear
    /// light per stop) and uploaded as stored; <see cref="Material.SetColor(int, Color)"/> converts
    /// its sRGB argument on the way in;</item>
    /// <item><see cref="MaterialPropertyBlock.SetColor(int, Color)"/> converts, its SetVector does not;</item>
    /// <item><see cref="Shader.SetGlobalColor(int, Color)"/> does not convert (an alias of SetGlobalVector);</item>
    /// <item>vertex colours of particles, trails and lines are converted; a mesh's own vertex
    /// colours are not.</item>
    /// </list>
    /// </summary>
    public static class ColorSpaceRules
    {
        /// <summary>sRGB → linear on rgb (alpha unchanged).</summary>
        public static Vector4 Linear(Vector4 v)
            => new(Mathf.GammaToLinearSpace(v.x), Mathf.GammaToLinearSpace(v.y), Mathf.GammaToLinearSpace(v.z), v.w);

        /// <summary>Linear → sRGB on rgb (alpha unchanged).</summary>
        public static Vector4 Gamma(Vector4 v)
            => new(Mathf.LinearToGammaSpace(v.x), Mathf.LinearToGammaSpace(v.y), Mathf.LinearToGammaSpace(v.z), v.w);

        /// <summary>sRGB → linear on rgb (alpha unchanged).</summary>
        public static Color Linear(Color c)
            => new(Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b), c.a);
    }
}
