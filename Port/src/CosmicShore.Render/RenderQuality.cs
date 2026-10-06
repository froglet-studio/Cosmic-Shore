using System;
using Silk.NET.OpenGL;

namespace CosmicShore.Render
{
    /// <summary>
    /// The player's render-quality settings, mirroring what the Unity build ships with:
    /// URP_Asset.asset authors MSAA 4x and render scale 1, and every desktop quality level
    /// enables anisotropic texture filtering. Set once at startup (player arguments or
    /// COSMIC_SHORE_MSAA / _RENDER_SCALE / _ANISO); the renderer reads them every frame.
    /// </summary>
    public static class RenderQuality
    {
        /// <summary>Multisample count for the 3D scene target: 0/1 = off, 2, 4 (Unity's), 8.</summary>
        public static int Msaa = 4;

        /// <summary>3D resolution multiplier: below 1 renders fewer pixels, above 1 supersamples.</summary>
        public static float RenderScale = 1f;

        /// <summary>Anisotropic filtering for mipmapped textures (1 = off, up to 16).</summary>
        public static float Anisotropy = 8f;

        /// <summary>Wait for the display's refresh (Unity: QualitySettings.vSyncCount 1).</summary>
        public static bool VSync = true;

        /// <summary>Frame cap when vsync is off; 0 = unlimited.</summary>
        public static int TargetFps;

        public static void FromEnvironment()
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("COSMIC_SHORE_MSAA"), out int m)) Msaa = m;
            if (float.TryParse(Environment.GetEnvironmentVariable("COSMIC_SHORE_RENDER_SCALE"),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s)) RenderScale = s;
            if (float.TryParse(Environment.GetEnvironmentVariable("COSMIC_SHORE_ANISO"),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float a)) Anisotropy = a;
            Clamp();
        }

        public static void Clamp()
        {
            Msaa = Msaa >= 8 ? 8 : Msaa >= 4 ? 4 : Msaa >= 2 ? 2 : 0;
            RenderScale = Math.Clamp(RenderScale, 0.5f, 2f);
            Anisotropy = Math.Clamp(Anisotropy, 1f, 16f);
        }

        const GLEnum MaxTextureMaxAnisotropy = (GLEnum)0x84FF;
        const TextureParameterName TextureMaxAnisotropy = (TextureParameterName)0x84FE;
        static float s_maxAniso = -1f;

        /// <summary>Applies anisotropic filtering to the bound 2D texture where the context supports it.</summary>
        public static void ApplyAnisotropy(GL gl)
        {
            if (Anisotropy <= 1f) return;
            if (s_maxAniso < 0f)
            {
                s_maxAniso = 0f;
                bool supported = GlCaps.Has("GL_EXT_texture_filter_anisotropic") || GlCaps.Has("GL_ARB_texture_filter_anisotropic")
                    || (!GlCaps.IsEs && (GlCaps.Major > 4 || (GlCaps.Major == 4 && GlCaps.Minor >= 6)));
                if (supported) { gl.GetFloat(MaxTextureMaxAnisotropy, out float max); s_maxAniso = max; }
            }
            if (s_maxAniso <= 1f) return;
            gl.TexParameter(TextureTarget.Texture2D, TextureMaxAnisotropy, Math.Min(Anisotropy, s_maxAniso));
        }
    }
}
