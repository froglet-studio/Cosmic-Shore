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
}
