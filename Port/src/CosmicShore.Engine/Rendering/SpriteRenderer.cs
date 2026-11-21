namespace CosmicShore.Engine
{
    public enum SpriteDrawMode { Simple = 0, Sliced = 1, Tiled = 2 }
    public enum SpriteMaskInteraction { None = 0, VisibleInsideMask = 1, VisibleOutsideMask = 2 }

    /// <summary>World-space sprite quad (UnityEngine.SpriteRenderer).</summary>
    public class SpriteRenderer : Renderer
    {
        public Sprite sprite { get; set; }
        public Color color { get; set; } = Color.white;
        public bool flipX { get; set; }
        public bool flipY { get; set; }
        public SpriteDrawMode drawMode { get; set; }
        public Vector2 size { get; set; } = Vector2.one;
        public SpriteMaskInteraction maskInteraction { get; set; }
        public int sortingOrder { get; set; }
        public int sortingLayerID { get; set; }
        public string sortingLayerName { get; set; } = "Default";
    }
}
