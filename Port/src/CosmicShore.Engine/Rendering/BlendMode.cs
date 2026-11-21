namespace CosmicShore.Engine.Rendering
{
    /// <summary>
    /// Original-engine blend factors (UnityEngine.Rendering.BlendMode). Numeric values
    /// frozen to the original — transparency setup code writes them into material ints
    /// (`_SrcBlend`/`_DstBlend`); data-only until a render backend reads them.
    /// </summary>
    public enum BlendMode
    {
        Zero = 0,
        One = 1,
        DstColor = 2,
        SrcColor = 3,
        OneMinusDstColor = 4,
        SrcAlpha = 5,
        OneMinusSrcColor = 6,
        DstAlpha = 7,
        OneMinusDstAlpha = 8,
        SrcAlphaSaturate = 9,
        OneMinusSrcAlpha = 10,
    }

    /// <summary>Original: UnityEngine.Rendering.CullMode - written into materials' _Cull (values frozen).</summary>
    public enum CullMode { Off = 0, Front = 1, Back = 2 }

    /// <summary>Original: UnityEngine.Rendering.CompareFunction - written into materials' _ZTest / stencil ints (values frozen).</summary>
    public enum CompareFunction { Disabled = 0, Never = 1, Less = 2, Equal = 3, LessEqual = 4, Greater = 5, NotEqual = 6, GreaterEqual = 7, Always = 8 }

    /// <summary>Original: UnityEngine.Rendering.RenderQueue - the standard queue values (frozen).</summary>
    public enum RenderQueue { Background = 1000, Geometry = 2000, AlphaTest = 2450, GeometryLast = 2500, Transparent = 3000, Overlay = 4000 }
}
