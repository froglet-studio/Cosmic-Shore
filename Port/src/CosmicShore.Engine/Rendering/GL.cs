namespace CosmicShore.Engine
{
    /// <summary>
    /// Immediate-mode drawing (original: UnityEngine.GL). PLACEHOLDER: the port's renderer has no immediate-mode
    /// path, so the calls are accepted and draw nothing (the game uses it only for debug lines, ThirdEyeLines). The
    /// matrix stack is kept so code that reads <see cref="modelview"/> back sees what it wrote.
    /// </summary>
    public static class GL
    {
        public const int TRIANGLES = 4, TRIANGLE_STRIP = 5, QUADS = 7, LINES = 1, LINE_STRIP = 2;

        static readonly System.Collections.Generic.Stack<(Matrix4x4 mv, Matrix4x4 proj)> s_stack = new();
        static Matrix4x4 s_proj = Matrix4x4.identity;

        public static Matrix4x4 modelview { get; set; } = Matrix4x4.identity;
        public static bool wireframe { get; set; }
        public static bool invertCulling { get; set; }
        public static bool sRGBWrite { get; set; }

        public static void PushMatrix() => s_stack.Push((modelview, s_proj));
        public static void PopMatrix() { if (s_stack.Count > 0) (modelview, s_proj) = s_stack.Pop(); }
        public static void LoadIdentity() => modelview = Matrix4x4.identity;
        public static void LoadOrtho() { s_proj = Matrix4x4.identity; modelview = Matrix4x4.identity; }
        public static void LoadPixelMatrix() { s_proj = Matrix4x4.identity; modelview = Matrix4x4.identity; }
        public static void LoadProjectionMatrix(Matrix4x4 mat) => s_proj = mat;
        public static void MultMatrix(Matrix4x4 m) => modelview = modelview * m;
        public static Matrix4x4 GetGPUProjectionMatrix(Matrix4x4 proj, bool renderIntoTexture) => proj;

        public static void Begin(int mode) { }
        public static void End() { }
        public static void Color(Color c) { }
        public static void Vertex(Vector3 v) { }
        public static void Vertex3(float x, float y, float z) { }
        public static void TexCoord(Vector3 v) { }
        public static void TexCoord2(float x, float y) { }
        public static void TexCoord3(float x, float y, float z) { }
        public static void MultiTexCoord(int unit, Vector3 v) { }
        public static void Clear(bool clearDepth, bool clearColor, Color backgroundColor, float depth = 1f) { }
        public static void Viewport(Rect pixelRect) { }
        public static void Flush() { }
        public static void IssuePluginEvent(System.IntPtr callback, int eventID) { }
    }
}
