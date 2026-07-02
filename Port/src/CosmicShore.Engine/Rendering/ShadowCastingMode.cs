namespace CosmicShore.Engine.Rendering
{
    /// <summary>
    /// Original-engine shadow casting modes (UnityEngine.Rendering.ShadowCastingMode).
    /// Data-only in the headless engine — a render backend reads the value later.
    /// Numeric values frozen to the original.
    /// </summary>
    public enum ShadowCastingMode
    {
        Off = 0,
        On = 1,
        TwoSided = 2,
        ShadowsOnly = 3,
    }

    /// <summary>
    /// Original-engine primitive shapes (UnityEngine.PrimitiveType) for
    /// <see cref="GameObject.CreatePrimitive"/>. Numeric values frozen to the original.
    /// Lives under Engine.Rendering (not the engine root the original used) so a render
    /// backend importing both CosmicShore.Engine and a GL binding (Silk.NET.OpenGL has
    /// its own PrimitiveType) doesn't hit CS0104 — call sites add
    /// `using CosmicShore.Engine.Rendering;`.
    /// </summary>
    public enum PrimitiveType
    {
        Sphere = 0,
        Capsule = 1,
        Cylinder = 2,
        Cube = 3,
        Plane = 4,
        Quad = 5,
    }
}
