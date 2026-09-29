using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using GlWrap = Silk.NET.OpenGL.TextureWrapMode;

namespace CosmicShore.Render
{
    /// <summary>Post-processing settings the composite applies (the active volume profile's values).</summary>
    public struct PostSettings
    {
        public bool Bloom;
        public float BloomThreshold, BloomIntensity, BloomScatter, BloomClamp;
        public System.Numerics.Vector3 BloomTint;
        public int BloomMaxIterations;
        public bool Panini;
        public float PaniniDistance, PaniniCropToFit;
        public float TanHalfFovX, TanHalfFovY;

        /// <summary>The gameplay profile (GamePlay PostProcessing Profile.asset) the game ships.</summary>
        public static PostSettings Gameplay => new()
        {
            Bloom = true, BloomThreshold = 0.2f, BloomIntensity = 2.5f, BloomScatter = 0.7f, BloomClamp = 0.5f,
            BloomTint = System.Numerics.Vector3.One, BloomMaxIterations = 6,
            Panini = true, PaniniDistance = 0.7f, PaniniCropToFit = 1f,
        };
    }

    /// <summary>
    /// The 3D frame's post stack, from the HDR scene target into the UI frame target:
    /// a bloom mip chain (threshold with a soft knee, clamp, separable blur down, scatter-
    /// weighted blend up) and a Panini projection remap, composited additively. No
    /// tonemapper — the project's default volume authors Tonemapping None, so the present
    /// pass's clamp is the display transform, as in the Unity build.
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

        const string Prefilter = @"#version 330 core
in vec2 vUv;
uniform sampler2D uSrc;
uniform vec2 uTexel;
uniform float uThreshold;
uniform float uClamp;
out vec4 frag;
void main(){
  // 4-tap box downsample, then clamp + soft-knee threshold (knee = threshold / 2).
  vec3 c = texture(uSrc, vUv + uTexel * vec2(-0.5,-0.5)).rgb + texture(uSrc, vUv + uTexel * vec2(0.5,-0.5)).rgb
         + texture(uSrc, vUv + uTexel * vec2(-0.5, 0.5)).rgb + texture(uSrc, vUv + uTexel * vec2(0.5, 0.5)).rgb;
  c = min(c * 0.25, vec3(uClamp));
  float knee = uThreshold * 0.5;
  float br = max(c.r, max(c.g, c.b));
  float soft = clamp(br - uThreshold + knee, 0.0, 2.0 * knee);
  soft = soft * soft / (4.0 * knee + 1e-4);
  float mult = max(br - uThreshold, soft) / max(br, 1e-4);
  frag = vec4(c * mult, 1.0);
}";

        const string Blur = @"#version 330 core
in vec2 vUv;
uniform sampler2D uSrc;
uniform vec2 uStep;
out vec4 frag;
void main(){
  // 9-tap gaussian as 5 bilinear fetches.
  vec3 c = texture(uSrc, vUv).rgb * 0.2270270270;
  c += texture(uSrc, vUv + uStep * 1.3846153846).rgb * 0.3162162162;
  c += texture(uSrc, vUv - uStep * 1.3846153846).rgb * 0.3162162162;
  c += texture(uSrc, vUv + uStep * 3.2307692308).rgb * 0.0702702703;
  c += texture(uSrc, vUv - uStep * 3.2307692308).rgb * 0.0702702703;
  frag = vec4(c, 1.0);
}";

        const string Upsample = @"#version 330 core
in vec2 vUv;
uniform sampler2D uHigh;
uniform sampler2D uLow;
uniform float uScatter;
out vec4 frag;
void main(){
  frag = vec4(mix(texture(uHigh, vUv).rgb, texture(uLow, vUv).rgb, uScatter), 1.0);
}";

        const string Composite = @"#version 330 core
in vec2 vUv;
uniform sampler2D uScene;
uniform sampler2D uBloom;
uniform float uBloomIntensity;
uniform vec3 uBloomTint;
uniform int uPanini;
uniform float uPaniniD;
uniform vec2 uTanHalf;   // source view-plane half extents
uniform float uPaniniScale;
out vec4 frag;
vec2 panini(vec2 uv){
  // Output plane point -> Panini inverse -> rectilinear source point.
  vec2 o = (uv * 2.0 - 1.0) * uTanHalf * uPaniniScale;
  float d = uPaniniD;
  float k = o.x / (d + 1.0);
  float phi = atan(k) + asin(clamp(k * d / sqrt(1.0 + k * k), -1.0, 1.0));
  float S = (d + 1.0) / (d + cos(phi));
  float h = o.y / S;
  vec2 rect = vec2(tan(phi), h / cos(phi));
  return rect / uTanHalf * 0.5 + 0.5;
}
void main(){
  vec2 uv = uPanini == 1 ? panini(vUv) : vUv;
  vec3 c = texture(uScene, uv).rgb + texture(uBloom, uv).rgb * uBloomIntensity * uBloomTint;
  frag = vec4(c, 1.0);
}";

        sealed class Rt { public uint Fbo, Tex; public int W, H; }

        readonly GL _gl;
        readonly GlProgram _prefilter, _blur, _upsample, _composite;
        readonly uint _vao;
        readonly List<Rt> _down = new(), _tmp = new(), _up = new();
        int _w, _h, _levels;

        public PostPass(GL gl)
        {
            _gl = gl;
            _prefilter = new GlProgram(gl, Vert, Prefilter, "bloom-prefilter");
            _blur = new GlProgram(gl, Vert, Blur, "bloom-blur");
            _upsample = new GlProgram(gl, Vert, Upsample, "bloom-upsample");
            _composite = new GlProgram(gl, Vert, Composite, "post-composite");
            _vao = gl.GenVertexArray();
        }

        /// <summary>Composites <paramref name="sceneTex"/> into the currently bound framebuffer (<paramref name="dstFbo"/>).</summary>
        public void Draw(uint sceneTex, int w, int h, uint dstFbo, in PostSettings s)
        {
            _gl.Disable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.Blend);
            _gl.Disable(EnableCap.CullFace);
            _gl.BindVertexArray(_vao);

            uint bloomTex = 0;
            if (s.Bloom && s.BloomIntensity > 0f)
            {
                Ensure(w, h, s.BloomMaxIterations);
                // Prefilter into level 0 (half res).
                Bind(_down[0]);
                _prefilter.Use();
                _prefilter.Set("uSrc", 0);
                _prefilter.Set("uTexel", 1f / w, 1f / h);
                _prefilter.Set("uThreshold", s.BloomThreshold);
                _prefilter.Set("uClamp", s.BloomClamp <= 0f ? 65472f : s.BloomClamp);
                Tex(0, sceneTex);
                Fullscreen();
                // Blur each level, downsampling as we go.
                _blur.Use();
                _blur.Set("uSrc", 0);
                for (int i = 0; i < _levels; i++)
                {
                    if (i > 0)
                    {
                        // downsample previous blurred level into this one
                        Bind(_down[i]);
                        _blur.Set("uStep", 0f, 0f);
                        Tex(0, _down[i - 1].Tex);
                        Fullscreen();
                    }
                    Bind(_tmp[i]);
                    _blur.Set("uStep", 1f / _down[i].W, 0f);
                    Tex(0, _down[i].Tex);
                    Fullscreen();
                    Bind(_down[i]);
                    _blur.Set("uStep", 0f, 1f / _down[i].H);
                    Tex(0, _tmp[i].Tex);
                    Fullscreen();
                }
                // Upsample: up[i] = mix(down[i], up[i+1], scatter).
                float scatter = 0.05f + 0.9f * Math.Clamp(s.BloomScatter, 0f, 1f);
                _upsample.Use();
                _upsample.Set("uHigh", 0);
                _upsample.Set("uLow", 1);
                _upsample.Set("uScatter", scatter);
                uint low = _down[_levels - 1].Tex;
                for (int i = _levels - 2; i >= 0; i--)
                {
                    Bind(_up[i]);
                    Tex(0, _down[i].Tex);
                    Tex(1, low);
                    Fullscreen();
                    low = _up[i].Tex;
                }
                bloomTex = low;
            }

            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, dstFbo);
            _gl.Viewport(0, 0, (uint)w, (uint)h);
            _composite.Use();
            _composite.Set("uScene", 0);
            _composite.Set("uBloom", 1);
            _composite.Set("uBloomIntensity", bloomTex != 0 ? s.BloomIntensity : 0f);
            _gl.Uniform3(_composite.Loc("uBloomTint"), s.BloomTint.X, s.BloomTint.Y, s.BloomTint.Z);
            bool panini = s.Panini && s.PaniniDistance > 0f && s.TanHalfFovX > 0f;
            _composite.Set("uPanini", panini ? 1 : 0);
            _composite.Set("uPaniniD", s.PaniniDistance);
            _composite.Set("uTanHalf", s.TanHalfFovX, s.TanHalfFovY);
            _composite.Set("uPaniniScale", panini ? PaniniScale(s) : 1f);
            Tex(0, sceneTex);
            Tex(1, bloomTex != 0 ? bloomTex : sceneTex);
            Fullscreen();
            _gl.ActiveTexture(TextureUnit.Texture0);
        }

        /// <summary>
        /// Crop-to-fit: scale the Panini output so its horizontal edge lands on the source's
        /// (1 = fully cropped, no black border; 0 = the full distorted view).
        /// </summary>
        static float PaniniScale(in PostSettings s)
        {
            float d = s.PaniniDistance;
            float phiEdge = MathF.Atan(s.TanHalfFovX);
            float S = (d + 1f) / (d + MathF.Cos(phiEdge));
            float xEdge = S * MathF.Sin(phiEdge);
            float fit = xEdge / s.TanHalfFovX; // output units per source unit at the edge
            return 1f + (fit - 1f) * Math.Clamp(s.PaniniCropToFit, 0f, 1f);
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

        void Ensure(int w, int h, int maxIterations)
        {
            int levels = Math.Clamp((int)MathF.Floor(MathF.Log2(Math.Max(w, h) / 2f)) - 1, 1, Math.Max(1, maxIterations));
            if (w == _w && h == _h && levels == _levels) return;
            Release();
            _w = w; _h = h; _levels = levels;
            int lw = Math.Max(1, w / 2), lh = Math.Max(1, h / 2);
            for (int i = 0; i < levels; i++)
            {
                _down.Add(Make(lw, lh));
                _tmp.Add(Make(lw, lh));
                _up.Add(Make(lw, lh));
                lw = Math.Max(1, lw / 2);
                lh = Math.Max(1, lh / 2);
            }
        }

        unsafe Rt Make(int w, int h)
        {
            var rt = new Rt { W = w, H = h, Fbo = _gl.GenFramebuffer(), Tex = _gl.GenTexture() };
            _gl.BindTexture(TextureTarget.Texture2D, rt.Tex);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R11fG11fB10f, (uint)w, (uint)h, 0, PixelFormat.Rgb, PixelType.Float, null);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GlWrap.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GlWrap.ClampToEdge);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, rt.Fbo);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, rt.Tex, 0);
            return rt;
        }

        void Release()
        {
            foreach (var list in new[] { _down, _tmp, _up })
            {
                foreach (var rt in list) { _gl.DeleteFramebuffer(rt.Fbo); _gl.DeleteTexture(rt.Tex); }
                list.Clear();
            }
        }

        public void Dispose()
        {
            Release();
            _prefilter.Dispose(); _blur.Dispose(); _upsample.Dispose(); _composite.Dispose();
            _gl.DeleteVertexArray(_vao);
        }
    }
}
