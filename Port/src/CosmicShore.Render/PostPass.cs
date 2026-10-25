using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using Silk.NET.OpenGL;
using GlWrap = Silk.NET.OpenGL.TextureWrapMode;

namespace CosmicShore.Render
{
    /// <summary>Post-processing settings the composite applies (the active volume profile's values, as URP 17.3 reads them).</summary>
    public struct PostSettings
    {
        public bool Bloom;
        /// <summary>The bloom threshold in LINEAR space and its soft knee (half of it). The volume authors the threshold in gamma space.</summary>
        public float BloomThreshold, BloomKnee;
        public float BloomIntensity;
        /// <summary>The low/high mip blend on the way up: lerp(0.05, 0.95, scatter).</summary>
        public float BloomScatter;
        public float BloomClamp;
        /// <summary>The tint in linear space, normalised to unit luminance (white is (1, 1, 1)).</summary>
        public System.Numerics.Vector3 BloomTint;
        public int BloomMaxIterations;
        public bool BloomQuarterRes, BloomHighQuality;
        public bool Panini;
        public float PaniniDistance, PaniniCropToFit;
        public float TanHalfFovX, TanHalfFovY;

        /// <summary>
        /// The post state one camera renders with: nothing unless the camera's URP data turns
        /// post-processing on (the original's per-camera switch — the connecting panel's arena
        /// preview renders without it), otherwise the blended volume stack's Bloom and Panini.
        /// </summary>
        public static PostSettings For(CosmicShore.Engine.Camera camera, CosmicShore.Engine.Rendering.VolumeStack stack)
        {
            var s = new PostSettings();
            if (camera == null) return s;
            var data = camera.GetComponent<CosmicShore.Engine.Rendering.UniversalAdditionalCameraData>();
            if (data == null || !data.renderPostProcessing) return s;
            // A ViaScripting camera renders with its own stack, as its scripts last refreshed it (URP); every other camera
            // with the pipeline's shared stack, refreshed here each frame.
            if (!data.requiresVolumeFrameworkUpdate && data.volumeStack != null) stack = data.volumeStack;
            else stack.Update(camera.transform.position, data.volumeLayerMask.value);

            var bloom = stack.GetComponent<CosmicShore.Engine.Rendering.Bloom>();
            if (bloom != null && bloom.intensity.value > 0f) s.SetBloom(bloom);
            var panini = stack.GetComponent<CosmicShore.Engine.Rendering.PaniniProjection>();
            if (panini != null && panini.distance.value > 0f)
            {
                s.Panini = true;
                s.PaniniDistance = panini.distance.value;
                s.PaniniCropToFit = panini.cropToFit.value;
            }
            return s;
        }

        /// <summary>
        /// Bloom's shader parameters from the component, as URP's documentation defines them: the
        /// threshold is a gamma-space brightness (converted to linear here, the knee is half of it),
        /// scatter maps to a 0.05-0.95 blend, and the tint is applied in linear space at unit
        /// luminance so it changes the hue of the glow, not its strength.
        /// </summary>
        public void SetBloom(CosmicShore.Engine.Rendering.Bloom bloom)
        {
            Bloom = true;
            BloomThreshold = Mathf.GammaToLinearSpace(bloom.threshold.value);
            BloomKnee = BloomThreshold * 0.5f;
            BloomIntensity = bloom.intensity.value;
            BloomScatter = 0.05f + 0.9f * Math.Clamp(bloom.scatter.value, 0f, 1f);
            BloomClamp = bloom.clamp.value;
            var t = bloom.tint.value.linear;
            float luma = Luminance(t.r, t.g, t.b);
            BloomTint = luma > 0f ? new System.Numerics.Vector3(t.r / luma, t.g / luma, t.b / luma) : System.Numerics.Vector3.One;
            BloomMaxIterations = bloom.maxIterations.value;
            BloomQuarterRes = bloom.downscale.value == CosmicShore.Engine.Rendering.BloomDownscaleMode.Quarter;
            BloomHighQuality = bloom.highQualityFiltering.value;
        }

        /// <summary>Rec. 709 relative luminance of a linear colour.</summary>
        public static float Luminance(float r, float g, float b) => r * 0.2126729f + g * 0.7151522f + b * 0.0721750f;

        /// <summary>
        /// The bloom pyramid's depth for a source of <paramref name="width"/> x <paramref name="height"/>:
        /// it starts at half (or quarter) resolution and halves until the larger side would drop
        /// below 4 texels, capped at <paramref name="maxIterations"/>.
        /// </summary>
        public static int BloomMipCount(int width, int height, int maxIterations, bool quarter)
        {
            int shift = quarter ? 2 : 1;
            int tw = Math.Max(1, width >> shift), th = Math.Max(1, height >> shift);
            int iterations = (int)MathF.Floor(MathF.Log2(Math.Max(tw, th)) - 1f);
            return Math.Clamp(iterations, 1, Math.Max(1, maxIterations));
        }
    }

    /// <summary>
    /// The 3D frame's post stack, from the HDR scene target into the UI frame target, in URP's
    /// order: the Panini projection (its own pass), then bloom built from the projected image, then
    /// the composite (scene + bloom x intensity x tint). Bloom is URP's Gaussian pyramid: a
    /// prefilter at half resolution (clamp, then the soft-knee threshold), each lower level made by
    /// a 2x-downsampling horizontal 9-tap gaussian and a vertical 9-tap gaussian (5 bilinear
    /// taps), and the way back up blending each level with the one below by the scatter. No
    /// tonemapper — the project's default volume authors Tonemapping None, so the present pass's
    /// clamp is the display transform, as in the Unity build.
    /// </summary>
    public sealed class PostPass : IDisposable
    {
        const string Vert = @"#version 330 core
out vec2 vUv;
void main(){
  vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
  vUv = p;
  gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
}";

        // B-spline bicubic filtering in 4 bilinear fetches (Sigg & Hadwiger, GPU Gems 2 ch. 20).
        const string Bicubic = @"
vec3 sampleBicubic(sampler2D tex, vec2 uv, vec2 size){
  vec2 st = uv * size - 0.5;
  vec2 i = floor(st);
  vec2 f = st - i;
  vec2 f2 = f * f, f3 = f2 * f;
  vec2 w0 = (1.0 / 6.0) * (-f3 + 3.0 * f2 - 3.0 * f + 1.0);
  vec2 w1 = (1.0 / 6.0) * (3.0 * f3 - 6.0 * f2 + 4.0);
  vec2 w2 = (1.0 / 6.0) * (-3.0 * f3 + 3.0 * f2 + 3.0 * f + 1.0);
  vec2 w3 = (1.0 / 6.0) * f3;
  vec2 g0 = w0 + w1, g1 = w2 + w3;
  vec2 h0 = (w1 / g0) - 1.0, h1 = (w3 / g1) + 1.0;
  vec2 inv = 1.0 / size;
  vec2 p0 = (i + 0.5 + h0) * inv, p1 = (i + 0.5 + h1) * inv;
  return g0.y * (g0.x * texture(tex, vec2(p0.x, p0.y)).rgb + g1.x * texture(tex, vec2(p1.x, p0.y)).rgb)
       + g1.y * (g0.x * texture(tex, vec2(p0.x, p1.y)).rgb + g1.x * texture(tex, vec2(p1.x, p1.y)).rgb);
}";

        const string Prefilter = @"#version 330 core
in vec2 vUv;
uniform sampler2D uSrc;
uniform vec2 uTexel;     // the SOURCE texel
uniform int uHq;
uniform float uThreshold;
uniform float uKnee;
uniform float uClamp;
out vec4 frag;
vec3 tap(vec2 o){ return texture(uSrc, vUv + uTexel * o).rgb; }
void main(){
  vec3 c;
  if (uHq == 1) {
    // 13 taps: the inner 2x2 box at half weight, the four overlapping outer boxes at an eighth.
    vec3 A = tap(vec2(-1.0, -1.0)), B = tap(vec2(0.0, -1.0)), C = tap(vec2(1.0, -1.0));
    vec3 D = tap(vec2(-0.5, -0.5)), E = tap(vec2(0.5, -0.5));
    vec3 F = tap(vec2(-1.0, 0.0)), G = tap(vec2(0.0, 0.0)), H = tap(vec2(1.0, 0.0));
    vec3 I = tap(vec2(-0.5, 0.5)), J = tap(vec2(0.5, 0.5));
    vec3 K = tap(vec2(-1.0, 1.0)), L = tap(vec2(0.0, 1.0)), M = tap(vec2(1.0, 1.0));
    c = (D + E + I + J) * 0.125
      + ((A + B + G + F) + (B + C + H + G) + (F + G + L + K) + (G + H + M + L)) * 0.03125;
  } else {
    // One bilinear fetch between four source texels: their average.
    c = tap(vec2(0.0));
  }
  c = min(c, vec3(uClamp));
  float br = max(c.r, max(c.g, c.b));
  float soft = clamp(br - uThreshold + uKnee, 0.0, 2.0 * uKnee);
  soft = soft * soft / (4.0 * uKnee + 1e-4);
  c *= max(br - uThreshold, soft) / max(br, 1e-4);
  frag = vec4(max(c, 0.0), 1.0);
}";

        const string BlurH = @"#version 330 core
in vec2 vUv;
uniform sampler2D uSrc;
uniform vec2 uTexel;     // the SOURCE texel; the taps step one DESTINATION texel (two source texels)
out vec4 frag;
void main(){
  float s = uTexel.x * 2.0;
  vec3 c = texture(uSrc, vUv).rgb * 0.22702703;
  c += (texture(uSrc, vUv - vec2(s, 0.0)).rgb + texture(uSrc, vUv + vec2(s, 0.0)).rgb) * 0.19459459;
  c += (texture(uSrc, vUv - vec2(2.0 * s, 0.0)).rgb + texture(uSrc, vUv + vec2(2.0 * s, 0.0)).rgb) * 0.12162162;
  c += (texture(uSrc, vUv - vec2(3.0 * s, 0.0)).rgb + texture(uSrc, vUv + vec2(3.0 * s, 0.0)).rgb) * 0.05405405;
  c += (texture(uSrc, vUv - vec2(4.0 * s, 0.0)).rgb + texture(uSrc, vUv + vec2(4.0 * s, 0.0)).rgb) * 0.01621622;
  frag = vec4(c, 1.0);
}";

        const string BlurV = @"#version 330 core
in vec2 vUv;
uniform sampler2D uSrc;
uniform vec2 uTexel;
out vec4 frag;
void main(){
  // The same 9-tap gaussian as 5 bilinear fetches on a same-sized source.
  float a = uTexel.y * 1.38461538, b = uTexel.y * 3.23076923;
  vec3 c = texture(uSrc, vUv).rgb * 0.22702703;
  c += (texture(uSrc, vUv - vec2(0.0, a)).rgb + texture(uSrc, vUv + vec2(0.0, a)).rgb) * 0.31621622;
  c += (texture(uSrc, vUv - vec2(0.0, b)).rgb + texture(uSrc, vUv + vec2(0.0, b)).rgb) * 0.07027027;
  frag = vec4(c, 1.0);
}";

        const string Upsample = @"#version 330 core
in vec2 vUv;
uniform sampler2D uHigh;
uniform sampler2D uLow;
uniform vec2 uLowSize;
uniform int uHq;
uniform float uScatter;
out vec4 frag;
" + Bicubic + @"
void main(){
  vec3 lo = uHq == 1 ? sampleBicubic(uLow, vUv, uLowSize) : texture(uLow, vUv).rgb;
  frag = vec4(mix(texture(uHigh, vUv).rgb, lo, uScatter), 1.0);
}";

        const string Panini = @"#version 330 core
in vec2 vUv;
uniform sampler2D uSrc;
uniform vec4 uParams;    // view extents (tan of the half angles), distance d, crop scale
out vec4 frag;
void main(){
  // An output point on the image plane, seen from P (d behind the unit cylinder's axis O, 1 + d
  // from the plane): where that ray meets the cylinder is the scene point; re-projecting it from
  // O onto the rectilinear plane gives the source coordinate.
  vec2 v = (vUv * 2.0 - 1.0) * uParams.xy * uParams.w;
  float d = uParams.z, L = 1.0 + d;
  float h2 = v.x * v.x + L * L;
  float t = (d * L + sqrt(max(h2 - v.x * v.x * d * d, 0.0))) / h2;
  vec2 src = v * t / (t * L - d);
  frag = vec4(texture(uSrc, src / uParams.xy * 0.5 + 0.5).rgb, 1.0);
}";

        const string Composite = @"#version 330 core
in vec2 vUv;
uniform sampler2D uScene;
uniform sampler2D uBloom;
uniform vec2 uBloomSize;
uniform int uHq;
uniform float uBloomIntensity;
uniform vec3 uBloomTint;
out vec4 frag;
" + Bicubic + @"
void main(){
  vec3 c = texture(uScene, vUv).rgb;
  if (uBloomIntensity > 0.0) {
    vec3 b = uHq == 1 ? sampleBicubic(uBloom, vUv, uBloomSize) : texture(uBloom, vUv).rgb;
    c += b * uBloomIntensity * uBloomTint;
  }
  frag = vec4(c, 1.0);
}";

        sealed class Rt { public uint Fbo, Tex; public int W, H; }

        readonly GL _gl;
        readonly GlProgram _prefilter, _blurH, _blurV, _upsample, _panini, _composite;
        readonly uint _vao;
        readonly List<Rt> _down = new(), _up = new();
        Rt _paniniRt;
        int _srcW, _srcH, _mips;
        bool _quarter;

        public PostPass(GL gl)
        {
            _gl = gl;
            _prefilter = new GlProgram(gl, Vert, Prefilter, "bloom-prefilter");
            _blurH = new GlProgram(gl, Vert, BlurH, "bloom-blur-h");
            _blurV = new GlProgram(gl, Vert, BlurV, "bloom-blur-v");
            _upsample = new GlProgram(gl, Vert, Upsample, "bloom-upsample");
            _panini = new GlProgram(gl, Vert, Panini, "panini");
            _composite = new GlProgram(gl, Vert, Composite, "post-composite");
            _vao = gl.GenVertexArray();
        }

        /// <summary>Composites <paramref name="sceneTex"/> (the window's size) into <paramref name="dstFbo"/>.</summary>
        public void Draw(uint sceneTex, int w, int h, uint dstFbo, in PostSettings s) => Draw(sceneTex, w, h, w, h, dstFbo, s);

        /// <summary>
        /// Composites <paramref name="sceneTex"/> (<paramref name="srcW"/> x <paramref name="srcH"/>, the
        /// camera target at render scale) into <paramref name="dstFbo"/> (<paramref name="w"/> x <paramref name="h"/>).
        /// </summary>
        public void Draw(uint sceneTex, int srcW, int srcH, int w, int h, uint dstFbo, in PostSettings s)
        {
            _gl.Disable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.Blend);
            _gl.Disable(EnableCap.CullFace);
            _gl.BindVertexArray(_vao);

            uint source = sceneTex;
            if (s.Panini && s.PaniniDistance > 0f && s.TanHalfFovX > 0f && s.TanHalfFovY > 0f)
            {
                if (_paniniRt == null || _paniniRt.W != srcW || _paniniRt.H != srcH)
                {
                    if (_paniniRt != null) Free(_paniniRt);
                    _paniniRt = Make(srcW, srcH, InternalFormat.Rgba16f);
                }
                Bind(_paniniRt);
                _panini.Use();
                _panini.Set("uSrc", 0);
                _panini.Set("uParams", s.TanHalfFovX, s.TanHalfFovY, s.PaniniDistance, PaniniCropScale(s));
                Tex(0, sceneTex);
                Fullscreen();
                source = _paniniRt.Tex;
            }

            uint bloomTex = 0;
            int bloomW = 1, bloomH = 1;
            if (s.Bloom && s.BloomIntensity > 0f)
            {
                Ensure(srcW, srcH, s.BloomMaxIterations, s.BloomQuarterRes);
                _prefilter.Use();
                _prefilter.Set("uSrc", 0);
                _prefilter.Set("uTexel", 1f / srcW, 1f / srcH);
                _prefilter.Set("uHq", s.BloomHighQuality ? 1 : 0);
                _prefilter.Set("uThreshold", s.BloomThreshold);
                _prefilter.Set("uKnee", s.BloomKnee);
                _prefilter.Set("uClamp", s.BloomClamp);
                Bind(_down[0]);
                Tex(0, source);
                Fullscreen();

                // Down: horizontal gaussian while halving into up[i] (scratch), vertical into down[i].
                for (int i = 1; i < _mips; i++)
                {
                    _blurH.Use();
                    _blurH.Set("uSrc", 0);
                    _blurH.Set("uTexel", 1f / _down[i - 1].W, 1f / _down[i - 1].H);
                    Bind(_up[i]);
                    Tex(0, _down[i - 1].Tex);
                    Fullscreen();
                    _blurV.Use();
                    _blurV.Set("uSrc", 0);
                    _blurV.Set("uTexel", 1f / _up[i].W, 1f / _up[i].H);
                    Bind(_down[i]);
                    Tex(0, _up[i].Tex);
                    Fullscreen();
                }

                // Up: up[i] = lerp(down[i], the level below, scatter); the deepest level is its own down.
                _upsample.Use();
                _upsample.Set("uHigh", 0);
                _upsample.Set("uLow", 1);
                _upsample.Set("uHq", s.BloomHighQuality ? 1 : 0);
                _upsample.Set("uScatter", s.BloomScatter);
                for (int i = _mips - 2; i >= 0; i--)
                {
                    var low = i == _mips - 2 ? _down[i + 1] : _up[i + 1];
                    _upsample.Set("uLowSize", low.W, low.H);
                    Bind(_up[i]);
                    Tex(0, _down[i].Tex);
                    Tex(1, low.Tex);
                    Fullscreen();
                }
                var top = _mips == 1 ? _down[0] : _up[0];
                bloomTex = top.Tex;
                bloomW = top.W;
                bloomH = top.H;
            }

            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, dstFbo);
            _gl.Viewport(0, 0, (uint)w, (uint)h);
            _composite.Use();
            _composite.Set("uScene", 0);
            _composite.Set("uBloom", 1);
            _composite.Set("uBloomSize", bloomW, bloomH);
            _composite.Set("uHq", s.BloomHighQuality ? 1 : 0);
            _composite.Set("uBloomIntensity", bloomTex != 0 ? s.BloomIntensity : 0f);
            _gl.Uniform3(_composite.Loc("uBloomTint"), s.BloomTint.X, s.BloomTint.Y, s.BloomTint.Z);
            Tex(0, source);
            Tex(1, bloomTex != 0 ? bloomTex : source);
            Fullscreen();
            _gl.ActiveTexture(TextureUnit.Texture0);
        }

        /// <summary>
        /// Crop-to-fit (URP's rule): how much of the projected image to keep so the frame edge lands
        /// on the source's edge. The source's corner direction is traced through the cylinder back to
        /// the projected plane; the smaller of the two axis ratios is the full crop, and cropToFit
        /// blends from no crop (1) to it.
        /// </summary>
        public static float PaniniCropScale(in PostSettings s)
        {
            float d = s.PaniniDistance, L = 1f + d;
            float px = s.TanHalfFovX, py = s.TanHalfFovY;
            float hyp = MathF.Sqrt(px * px + 1f);
            float cylMinusD = 1f / hyp;           // the corner ray's point on the unit cylinder, relative to its axis
            float cyl = cylMinusD + d;            // ... and its distance from the projection centre
            float cropX = px * cylMinusD * (L / cyl), cropY = py * cylMinusD * (L / cyl);
            float scale = MathF.Min(cropX / px, cropY / py);
            return 1f + (Math.Clamp(scale, 0f, 1f) - 1f) * Math.Clamp(s.PaniniCropToFit, 0f, 1f);
        }

        void Fullscreen() => _gl.DrawArrays(Silk.NET.OpenGL.PrimitiveType.Triangles, 0, 3);

        void Tex(int unit, uint tex)
        {
            _gl.ActiveTexture(TextureUnit.Texture0 + unit);
            _gl.BindTexture(TextureTarget.Texture2D, tex);
        }

        void Bind(Rt rt)
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, rt.Fbo);
            _gl.Viewport(0, 0, (uint)rt.W, (uint)rt.H);
        }

        /// <summary>The pyramid for a <paramref name="srcW"/> x <paramref name="srcH"/> source: level 0 at half (or quarter) size, each next level halved.</summary>
        void Ensure(int srcW, int srcH, int maxIterations, bool quarter)
        {
            int mips = PostSettings.BloomMipCount(srcW, srcH, maxIterations, quarter);
            if (srcW == _srcW && srcH == _srcH && mips == _mips && quarter == _quarter) return;
            Release();
            _srcW = srcW; _srcH = srcH; _mips = mips; _quarter = quarter;
            int shift = quarter ? 2 : 1;
            int tw = Math.Max(1, srcW >> shift), th = Math.Max(1, srcH >> shift);
            for (int i = 0; i < mips; i++)
            {
                int lw = Math.Max(1, tw >> i), lh = Math.Max(1, th >> i);
                _down.Add(Make(lw, lh, InternalFormat.R11fG11fB10f));
                _up.Add(Make(lw, lh, InternalFormat.R11fG11fB10f));
            }
        }

        unsafe Rt Make(int w, int h, InternalFormat format)
        {
            var rt = new Rt { W = w, H = h, Fbo = _gl.GenFramebuffer(), Tex = _gl.GenTexture() };
            _gl.BindTexture(TextureTarget.Texture2D, rt.Tex);
            var (fmt, layout, type) = GlCaps.HdrTarget(format);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, fmt, (uint)w, (uint)h, 0, layout, type, null);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GlWrap.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GlWrap.ClampToEdge);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, rt.Fbo);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, rt.Tex, 0);
            return rt;
        }

        void Free(Rt rt)
        {
            _gl.DeleteFramebuffer(rt.Fbo);
            _gl.DeleteTexture(rt.Tex);
        }

        void Release()
        {
            foreach (var list in new[] { _down, _up })
            {
                foreach (var rt in list) Free(rt);
                list.Clear();
            }
            _mips = 0;
        }

        public void Dispose()
        {
            Release();
            if (_paniniRt != null) Free(_paniniRt);
            _prefilter.Dispose(); _blurH.Dispose(); _blurV.Dispose(); _upsample.Dispose(); _panini.Dispose(); _composite.Dispose();
            _gl.DeleteVertexArray(_vao);
        }
    }
}
