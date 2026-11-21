using System;
using Silk.NET.OpenGL;

namespace CosmicShore.Render
{
    /// <summary>
    /// Final pass: the linear frame → the display, sRGB-encoded (Unity's linear color
    /// space output conversion). A fullscreen triangle, no state left behind.
    /// </summary>
    public sealed class PresentPass : IDisposable
    {
        const string Vert = @"#version 330 core
out vec2 vUv;
void main(){
  vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
  vUv = p;
  gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
}";
        const string Frag = @"#version 330 core
in vec2 vUv;
uniform sampler2D uSrc;
out vec4 frag;
vec3 toSrgb(vec3 c){
  c = max(c, 0.0);
  return mix(c * 12.92, 1.055 * pow(c, vec3(1.0/2.4)) - 0.055, step(0.0031308, c));
}
void main(){
  vec4 c = texture(uSrc, vUv);
  frag = vec4(toSrgb(clamp(c.rgb, 0.0, 1.0)), 1.0);
}";

        readonly GL _gl;
        readonly GlProgram _program;
        readonly uint _vao;

        public PresentPass(GL gl)
        {
            _gl = gl;
            _program = new GlProgram(gl, Vert, Frag, "present");
            _vao = gl.GenVertexArray();
        }

        /// <param name="targetFbo">0 = the window; else an 8-bit target (the control port's virtual resolution).</param>
        public void Draw(uint sourceTexture, int width, int height, uint targetFbo = 0)
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, targetFbo);
            _gl.Viewport(0, 0, (uint)width, (uint)height);
            _gl.Disable(EnableCap.Blend);
            _gl.Disable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.StencilTest);
            _program.Use();
            _program.Set("uSrc", 0);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, sourceTexture);
            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        public void Dispose()
        {
            _program.Dispose();
            _gl.DeleteVertexArray(_vao);
        }
    }
}
