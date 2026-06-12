namespace CosmicShore.Engine
{
    /// <summary>
    /// E2-style asset data stub (vessel-layer V11): a named 2D image asset reference
    /// used by config SOs (e.g. CellConfigDataSO.Icon). Headless-first — pixel data and
    /// atlas semantics arrive with the presentation phase; until then the reference is
    /// the asset, exactly like the <see cref="Material"/> stub.
    /// </summary>
    public class Sprite : Object
    {
    }
}
