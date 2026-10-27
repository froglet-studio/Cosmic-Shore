using System;
using CosmicShore.Engine;
using Silk.NET.OpenGL;
using EMatrix = CosmicShore.Engine.Matrix4x4;
using Shader = CosmicShore.Engine.Shader;

namespace CosmicShore.Render
{
    /// <summary>
    /// Draws <see cref="RenderSettings.skybox"/> behind everything for a camera that clears
    /// to Skybox. The project's procedural skybox, CosmicShore/HyperSeaSkybox, runs its own
    /// translated shader; its baked twin, CosmicShore/StaticSkyPanorama (what the quality presets
    /// below High and the mobile tiers swap in: one equirectangular texture sample per pixel),
    /// is sampled with the same mapping Tools/Build/bake_static_skybox.py inverts. Any other skybox
    /// material falls back to the camera's background colour.
    /// </summary>
    public sealed class SkyboxPass : IDisposable
    {
        const string Vert = @"#version 330 core
out vec2 vNdc;
void main(){
  vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2) * 2.0 - 1.0;
  vNdc = p;
  gl_Position = vec4(p, 1.0, 1.0);
}";

        // Colours go up as the GPU receives them (Material.GetShaderColor): the plain ones
        // linearised, the [HDR] ones as stored. Vectors are never converted.
        static readonly string[] VectorProps = { "_GalacticNormal", "_CoreDirection", "_AndromedaDirection" };
        static readonly string[] ColorProps =
        {
            "_DeepColor", "_AmbientColor", "_GalacticColor", "_GalacticEmission", "_NebulaColor1", "_NebulaColor2",
            "_NebulaColor3", "_CoreColor", "_CoreHaloColor", "_AndromedaDiskColor", "_AndromedaNucleusColor",
            "_CellOverlayColor", "_AtmosphereColor",
        };
        static readonly string[] FloatProps =
        {
            "_AmbientStrength", "_GalacticBrightness", "_GalacticWidth", "_GalacticNoiseScale", "_GalacticNoiseStrength",
            "_StarDensity", "_StarBrightness", "_StarBaseProb", "_StarGalacticBoost", "_StarConcentration", "_TwinkleSpeed",
            "_NebulaStrength", "_NebulaScale", "_DustStrength", "_DustScale", "_CoreBrightness", "_CoreSize", "_CoreHaloSize",
            "_AndromedaBrightness", "_AndromedaSize", "_CellOverlayStrength", "_CellOverlayScale", "_CellEdgeSharpness",
            "_AtmosphereStrength", "_AtmosphereHeight", "_AtmosphereFalloff", "_DriftSpeed",
        };

        const string PanoramaFrag = @"#version 330 core
in vec2 vNdc;
uniform mat4 uInvViewProj;
uniform sampler2D uPanorama;
out vec4 frag;
void main(){
  vec4 p = uInvViewProj * vec4(vNdc, 1.0, 1.0);
  vec3 d = normalize(p.xyz / p.w);
  // StaticSkyPanorama.shader's mapping, exactly: the bake inverts it.
  vec2 uv = vec2(0.5 - atan(d.z, d.x) * (0.5 / 3.14159265), 1.0 - acos(clamp(d.y, -1.0, 1.0)) / 3.14159265);
  frag = vec4(textureLod(uPanorama, uv, 0.0).rgb, 1.0);
}";

        public const string HyperSeaShader = "CosmicShore/HyperSeaSkybox";
        public const string PanoramaShader = "CosmicShore/StaticSkyPanorama";

        readonly GL _gl;
        readonly GlProgram _hyperSea;
        readonly GlProgram _panorama;
        readonly TextureCache _textures;
        readonly uint _vao;

        public SkyboxPass(GL gl, TextureCache textures = null)
        {
            _gl = gl;
            _textures = textures;
            _hyperSea = new GlProgram(gl, Vert, HyperSeaSkyboxGlsl.Fragment, "hypersea-skybox");
            _panorama = new GlProgram(gl, Vert, PanoramaFrag, "panorama-skybox");
            _vao = gl.GenVertexArray();
        }

        /// <summary>True when it drew; false leaves the camera's background colour showing.</summary>
        public bool Draw(Camera camera)
        {
            var sky = RenderSettings.skybox;
            if (camera.clearFlags != CameraClearFlags.Skybox || !CanDraw(sky)) return false;

            // Rotation-only view so the sky sits at infinity.
            var view = camera.worldToCameraMatrix;
            view.m03 = view.m13 = view.m23 = 0f;
            var inv = (camera.projectionMatrix * view).inverse;
            DrawSky(sky, SceneRenderer.ToNumerics(inv));
            return true;
        }

        /// <summary>True when <see cref="RenderSettings.skybox"/> is a skybox this pass can draw.</summary>
        public static bool CanDraw(Material sky) => sky != null && (sky.shader?.name == HyperSeaShader || sky.shader?.name == PanoramaShader);

        static bool IsPanorama(Material sky) => sky?.shader?.name == PanoramaShader;

        /// <summary>
        /// Draws the skybox over the bound target with <paramref name="ndcToDirection"/> taking each
        /// pixel's NDC (x, y, 1, 1) to its world direction (a camera's inverse rotation-projection, or
        /// a cubemap face's mapping: EnvironmentLighting).
        /// </summary>
        public void DrawSky(Material sky, System.Numerics.Matrix4x4 ndcToDirection)
        {
            if (IsPanorama(sky)) { DrawPanorama(sky, ndcToDirection); return; }
            _hyperSea.Use();
            _hyperSea.Set("uInvViewProj", ndcToDirection);
            _hyperSea.Set("uTime", Time.time);
            foreach (var n in ColorProps)
            {
                var c = sky.GetShaderColor(Shader.PropertyToID(n));
                _hyperSea.Set(n, c.r, c.g, c.b, c.a);
            }
            foreach (var n in VectorProps)
            {
                var v = sky.GetVector(n);
                _hyperSea.Set(n, v.x, v.y, v.z, v.w);
            }
            foreach (var n in FloatProps)
                _hyperSea.Set(n, sky.GetFloat(n));

            _gl.Disable(EnableCap.DepthTest);
            _gl.DepthMask(false);
            _gl.Disable(EnableCap.Blend);
            _gl.Disable(EnableCap.CullFace);
            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(Silk.NET.OpenGL.PrimitiveType.Triangles, 0, 3);
            _gl.DepthMask(true);
        }

        /// <summary>The baked panorama: one texture sample per pixel (a black sky when the texture or cache is missing).</summary>
        void DrawPanorama(Material sky, System.Numerics.Matrix4x4 ndcToDirection)
        {
            var tex = sky.GetTexture("_MainTex");
            uint handle = tex != null && _textures != null ? _textures.Get(tex) : 0u;
            _panorama.Use();
            _panorama.Set("uInvViewProj", ndcToDirection);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, handle);
            _panorama.Set("uPanorama", 0);
            _gl.Disable(EnableCap.DepthTest);
            _gl.DepthMask(false);
            _gl.Disable(EnableCap.Blend);
            _gl.Disable(EnableCap.CullFace);
            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(Silk.NET.OpenGL.PrimitiveType.Triangles, 0, 3);
            _gl.DepthMask(true);
        }

        public void Dispose()
        {
            _panorama.Dispose();
            _hyperSea.Dispose();
            _gl.DeleteVertexArray(_vao);
        }
    }
}
