using System;
using CosmicShore.Engine;
using Silk.NET.OpenGL;
using AmbientMode = CosmicShore.Engine.Rendering.AmbientMode;
using NVector3 = System.Numerics.Vector3;
using NMatrix = System.Numerics.Matrix4x4;

namespace CosmicShore.Render
{
    /// <summary>
    /// The scene's environment lighting as URP lights a Lit surface with it, from
    /// <see cref="RenderSettings"/>:
    /// <list type="bullet">
    /// <item>the ambient probe, an L2 spherical harmonic of the irradiance: the skybox's (ambient
    /// mode Skybox, scaled by ambientIntensity), the sky colour (Flat), or the sky / equator /
    /// ground gradient (Trilight);</item>
    /// <item>the default reflection: the skybox in a mipmapped cubemap at the default reflection
    /// resolution (128), sampled at URP's roughness-to-mip level and scaled by reflectionIntensity.
    /// Its mips are box-filtered, where Unity's are convolved with the GGX lobe.</item>
    /// </list>
    /// The skybox is captured when its material changes (at most every two seconds), not every frame.
    /// </summary>
    public sealed class EnvironmentLighting : IDisposable
    {
        /// <summary>The texture unit the reflection cubemap is bound to for both scene programs.</summary>
        public const int CubeUnit = 15;
        const int Size = 128, ReadMip = 3; // SH projected from the 16 x 16 mip
        /// <summary>URP's UNITY_SPECCUBE_LOD_STEPS: perceptual roughness 1 samples this mip.</summary>
        public const float LodSteps = 6f;

        readonly GL _gl;
        uint _cube, _fbo;
        bool _failed;
        Material _capturedSky;
        int _capturedRevision = -1;
        float _capturedAt = float.NegativeInfinity;

        /// <summary>The probe's nine coefficients against <see cref="Basis"/>, irradiance / pi (what a white Lambert surface reflects).</summary>
        public readonly NVector3[] SH = new NVector3[9];
        readonly NVector3[] _skySH = new NVector3[9];
        bool _hasSkySH;

        /// <summary>A reflection cubemap is bound on <see cref="CubeUnit"/>; false = the probe stands in for it.</summary>
        public bool HasReflection { get; private set; }
        public float ReflectionIntensity { get; private set; } = 1f;

        public EnvironmentLighting(GL gl) { _gl = gl; }

        /// <summary>Re-captures the skybox when it changed and rebuilds the probe for this frame's settings.</summary>
        public void Update(SkyboxPass sky)
        {
            var mat = RenderSettings.skybox;
            bool drawable = SkyboxPass.CanDraw(mat) && sky != null && !_failed;
            if (drawable && (!ReferenceEquals(mat, _capturedSky) || mat.Revision != _capturedRevision) && Time.time - _capturedAt >= 2f)
                Capture(sky, mat);
            if (!drawable) { HasReflection = false; _hasSkySH = false; }
            ReflectionIntensity = RenderSettings.reflectionIntensity;

            Array.Clear(SH);
            switch (RenderSettings.ambientMode)
            {
                case AmbientMode.Skybox when _hasSkySH:
                    for (int i = 0; i < 9; i++) SH[i] = _skySH[i] * RenderSettings.ambientIntensity;
                    break;
                case AmbientMode.Trilight:
                    Trilight(Linear(RenderSettings.ambientSkyColor), Linear(RenderSettings.ambientEquatorColor), Linear(RenderSettings.ambientGroundColor));
                    break;
                default:
                    // Flat (and Custom, and a Skybox mode with no skybox): one colour from every direction.
                    SH[0] = Linear(RenderSettings.ambientSkyColor);
                    break;
            }
        }

        /// <summary>Binds the reflection cubemap (when there is one) on <see cref="CubeUnit"/>.</summary>
        public void Bind()
        {
            _gl.ActiveTexture(TextureUnit.Texture0 + CubeUnit);
            _gl.BindTexture(TextureTarget.TextureCubeMap, HasReflection ? _cube : 0);
            _gl.ActiveTexture(TextureUnit.Texture0);
        }

        static NVector3 Linear(Color c) => new(ColorSpace.ToLinear(c.r), ColorSpace.ToLinear(c.g), ColorSpace.ToLinear(c.b));

        // ── Capture ──

        /// <summary>
        /// The world direction a cubemap face's pixel shows, as a matrix on NDC (x, y, 1, 1): GL's
        /// face order (+X, -X, +Y, -Y, +Z, -Z), a framebuffer row 0 being the face's t = 0.
        /// </summary>
        static NMatrix FaceMatrix(int face)
        {
            // Rows of the math matrix M with M * (x, y, 1, 1) = (direction, 1).
            float[,] m = face switch
            {
                0 => new float[,] { { 0, 0, 1, 0 }, { 0, -1, 0, 0 }, { -1, 0, 0, 0 }, { 0, 0, 0, 1 } },  // +X: ( 1, -y, -x)
                1 => new float[,] { { 0, 0, -1, 0 }, { 0, -1, 0, 0 }, { 1, 0, 0, 0 }, { 0, 0, 0, 1 } },  // -X: (-1, -y,  x)
                2 => new float[,] { { 1, 0, 0, 0 }, { 0, 0, 1, 0 }, { 0, 1, 0, 0 }, { 0, 0, 0, 1 } },   // +Y: ( x,  1,  y)
                3 => new float[,] { { 1, 0, 0, 0 }, { 0, 0, -1, 0 }, { 0, -1, 0, 0 }, { 0, 0, 0, 1 } }, // -Y: ( x, -1, -y)
                4 => new float[,] { { 1, 0, 0, 0 }, { 0, -1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } },  // +Z: ( x, -y,  1)
                _ => new float[,] { { -1, 0, 0, 0 }, { 0, -1, 0, 0 }, { 0, 0, -1, 0 }, { 0, 0, 0, 1 } }, // -Z: (-x, -y, -1)
            };
            // GL reads the uploaded floats column-major: the numerics matrix's row i is M's column i.
            return new NMatrix(m[0, 0], m[1, 0], m[2, 0], m[3, 0],
                               m[0, 1], m[1, 1], m[2, 1], m[3, 1],
                               m[0, 2], m[1, 2], m[2, 2], m[3, 2],
                               m[0, 3], m[1, 3], m[2, 3], m[3, 3]);
        }

        /// <summary>The direction of a face's texel centre (u, v in -1..1), matching <see cref="FaceMatrix"/>.</summary>
        static NVector3 FaceDirection(int face, float u, float v) => face switch
        {
            0 => new NVector3(1, -v, -u),
            1 => new NVector3(-1, -v, u),
            2 => new NVector3(u, 1, v),
            3 => new NVector3(u, -1, -v),
            4 => new NVector3(u, -v, 1),
            _ => new NVector3(-u, -v, -1),
        };

        unsafe void Capture(SkyboxPass sky, Material mat)
        {
            _capturedSky = mat; _capturedRevision = mat.Revision; _capturedAt = Time.time;
            _gl.GetInteger(GetPName.DrawFramebufferBinding, out int prevFbo);
            Span<int> viewport = stackalloc int[4];
            _gl.GetInteger(GetPName.Viewport, viewport);
            try
            {
                if (_cube == 0)
                {
                    _cube = _gl.GenTexture();
                    _gl.BindTexture(TextureTarget.TextureCubeMap, _cube);
                    for (int f = 0; f < 6; f++)
                        _gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + f, 0, InternalFormat.Rgba16f, (uint)Size, (uint)Size, 0, PixelFormat.Rgba, PixelType.HalfFloat, null);
                    _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                    _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
                    _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
                    _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)GLEnum.ClampToEdge);
                    _fbo = _gl.GenFramebuffer();
                    // Mip lookups across face edges (always so on GL ES 3.0).
                    if (!GlCaps.IsEs) _gl.Enable(EnableCap.TextureCubeMapSeamless);
                }
                _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
                _gl.Viewport(0, 0, Size, Size);
                for (int f = 0; f < 6; f++)
                {
                    _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + f, _cube, 0);
                    if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
                    {
                        _failed = true; HasReflection = false; _hasSkySH = false;
                        Console.WriteLine("[environment] the reflection cubemap is not renderable here (RGBA16F); ambient falls back to the sky colour");
                        return;
                    }
                    sky.DrawSky(mat, FaceMatrix(f));
                }
                _gl.BindTexture(TextureTarget.TextureCubeMap, _cube);
                _gl.GenerateMipmap(TextureTarget.TextureCubeMap);
                HasReflection = true;
                _hasSkySH = ProjectSH();
            }
            finally
            {
                _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)prevFbo);
                _gl.Viewport(viewport[0], viewport[1], (uint)viewport[2], (uint)viewport[3]);
            }
        }

        /// <summary>
        /// The skybox's radiance projected onto the nine SH basis functions (each texel weighted by
        /// its solid angle), convolved with the cosine lobe and divided by pi.
        /// </summary>
        unsafe bool ProjectSH()
        {
            int n = Size >> ReadMip;
            var px = new float[n * n * 4];
            Span<double> acc = stackalloc double[27];
            for (int f = 0; f < 6; f++)
            {
                _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + f, _cube, ReadMip);
                if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete) return false;
                while (_gl.GetError() != GLEnum.NoError) { }
                fixed (float* p = px) _gl.ReadPixels(0, 0, (uint)n, (uint)n, PixelFormat.Rgba, PixelType.Float, p);
                if (_gl.GetError() != GLEnum.NoError) return false;
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float t = 1f + u * u + v * v;
                    double dw = 4.0 / (n * n) / (t * MathF.Sqrt(t));
                    var d = NVector3.Normalize(FaceDirection(f, u, v));
                    int o = (y * n + x) * 4;
                    var basis = Basis(d);
                    for (int i = 0; i < 9; i++)
                    {
                        double w = basis[i] * dw;
                        acc[i * 3] += px[o] * w; acc[i * 3 + 1] += px[o + 1] * w; acc[i * 3 + 2] += px[o + 2] * w;
                    }
                }
            }
            for (int i = 0; i < 9; i++)
            {
                // Lambert convolution per band (pi, 2pi/3, pi/4), over pi; then the evaluated
                // basis' own constant, so the shader sums plain polynomials (see Basis).
                float band = i == 0 ? 1f : i < 4 ? 2f / 3f : 0.25f;
                float k = band * BasisConstant(i);
                _skySH[i] = new NVector3((float)acc[i * 3], (float)acc[i * 3 + 1], (float)acc[i * 3 + 2]) * k;
            }
            return true;
        }

        // Real SH, l <= 2: Y = constant * polynomial. The shader evaluates the polynomials
        // (1, y, z, x, xy, yz, 3z^2 - 1, xz, x^2 - y^2) against coefficients already carrying
        // the constant, so the projection weights by the full Y and the stored value is
        // band * constant * projection.
        static float BasisConstant(int i) => i switch
        {
            0 => 0.282095f,
            1 or 2 or 3 => 0.488603f,
            4 or 5 or 7 => 1.092548f,
            6 => 0.315392f,
            _ => 0.546274f,
        };

        static float[] Basis(NVector3 d)
        {
            var b = s_basis ??= new float[9];
            b[0] = BasisConstant(0);
            b[1] = BasisConstant(1) * d.Y; b[2] = BasisConstant(2) * d.Z; b[3] = BasisConstant(3) * d.X;
            b[4] = BasisConstant(4) * d.X * d.Y; b[5] = BasisConstant(5) * d.Y * d.Z;
            b[6] = BasisConstant(6) * (3f * d.Z * d.Z - 1f);
            b[7] = BasisConstant(7) * d.X * d.Z; b[8] = BasisConstant(8) * (d.X * d.X - d.Y * d.Y);
            return b;
        }
        [ThreadStatic] static float[] s_basis;

        /// <summary>Trilight: sky above, equator at the horizon, ground below, projected like the skybox.</summary>
        void Trilight(NVector3 sky, NVector3 equator, NVector3 ground)
        {
            const int Steps = 32;
            var acc = new NVector3[9];
            for (int f = 0; f < 6; f++)
            for (int y = 0; y < Steps; y++)
            for (int x = 0; x < Steps; x++)
            {
                float u = (x + 0.5f) / Steps * 2f - 1f, v = (y + 0.5f) / Steps * 2f - 1f;
                float t = 1f + u * u + v * v;
                float dw = 4f / (Steps * Steps) / (t * MathF.Sqrt(t));
                var d = NVector3.Normalize(FaceDirection(f, u, v));
                var c = d.Y >= 0f ? NVector3.Lerp(equator, sky, d.Y) : NVector3.Lerp(equator, ground, -d.Y);
                var basis = Basis(d);
                for (int i = 0; i < 9; i++) acc[i] += c * (basis[i] * dw);
            }
            for (int i = 0; i < 9; i++)
            {
                float band = i == 0 ? 1f : i < 4 ? 2f / 3f : 0.25f;
                SH[i] = acc[i] * (band * BasisConstant(i));
            }
        }

        public void Dispose()
        {
            if (_cube != 0) _gl.DeleteTexture(_cube);
            if (_fbo != 0) _gl.DeleteFramebuffer(_fbo);
        }
    }
}
