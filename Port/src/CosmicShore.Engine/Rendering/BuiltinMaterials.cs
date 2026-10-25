namespace CosmicShore.Engine
{
    /// <summary>
    /// Unity's built-in extra materials, addressed by their fileID under the builtin-extra
    /// guid. Only the ones the project references in renderers are modelled; each is one
    /// shared instance, as the built-in asset is.
    /// </summary>
    public static class BuiltinMaterials
    {
        public const long DefaultLineId = 10306;
        public const long DefaultParticleId = 10301;

        static Material s_line, s_particle;

        /// <summary>The shader of both: vertex colour x texture x vertex alpha, Blend One OneMinusSrcAlpha.</summary>
        public const string ShaderName = "Legacy Shaders/Particles/Alpha Blended Premultiply";

        /// <summary>Default-Line: unlit, premultiplied, tinted by the vertex colour (every Line/TrailRenderer's default).</summary>
        public static Material DefaultLine => s_line ??= new Material(Shader.Find(ShaderName)) { name = "Default-Line", renderQueue = 3000 };

        /// <summary>Default-Particle: the particle system's default material (same shader).</summary>
        public static Material DefaultParticle => s_particle ??= new Material(Shader.Find(ShaderName)) { name = "Default-Particle", renderQueue = 3000 };

        public static Material ForFileId(long fileId) => fileId switch
        {
            DefaultLineId => DefaultLine,
            DefaultParticleId => DefaultParticle,
            _ => null,
        };
    }
}
