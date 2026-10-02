using System;
using CosmicShore.Engine;
#if GLES
using Silk.NET.OpenGLES;
using GLNS = Silk.NET.OpenGLES;
#else
using Silk.NET.OpenGL;
using GLNS = Silk.NET.OpenGL;
#endif
using EMatrix = CosmicShore.Engine.Matrix4x4;
using Shader = CosmicShore.Engine.Shader;

namespace CosmicShore.Render
{
    /// <summary>
    /// Draws <see cref="RenderSettings.skybox"/> behind everything for a camera that clears
    /// to Skybox. The project's one skybox, CosmicShore/HyperSeaSkybox, runs its own
    /// procedural shader (translated); any other skybox material falls back to its tint.
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

        // Properties declared as Vector (not Color) in the source shader: never linearised.
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

        readonly GL _gl;
        readonly GlProgram _hyperSea;
        readonly uint _vao;

        public SkyboxPass(GL gl)
        {
            _gl = gl;
            _hyperSea = new GlProgram(gl, Vert, HyperSeaSkyboxGlsl.Fragment, "hypersea-skybox");
            _vao = gl.GenVertexArray();
        }

        /// <summary>True when it drew; false leaves the camera's background colour showing.</summary>
        public bool Draw(Camera camera)
        {
            var sky = RenderSettings.skybox;
            if (sky == null || camera.clearFlags != CameraClearFlags.Skybox) return false;
            if (sky.shader?.name != "CosmicShore/HyperSeaSkybox") return false;

            // Rotation-only view so the sky sits at infinity.
            var view = camera.worldToCameraMatrix;
            view.m03 = view.m13 = view.m23 = 0f;
            var inv = (camera.projectionMatrix * view).inverse;

            _hyperSea.Use();
            _hyperSea.Set("uInvViewProj", SceneRenderer.ToNumerics(inv));
            _hyperSea.Set("uTime", Time.time);
            foreach (var n in ColorProps)
            {
                var c = sky.GetColor(n);
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
            _gl.DrawArrays(GLNS.PrimitiveType.Triangles, 0, 3);
            _gl.DepthMask(true);
            return true;
        }

        public void Dispose()
        {
            _hyperSea.Dispose();
            _gl.DeleteVertexArray(_vao);
        }
    }
}
