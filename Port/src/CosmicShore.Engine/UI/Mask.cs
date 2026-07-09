namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Stencil mask (original contract): clips child MaskableGraphics to this node's
    /// graphic shape. Headless data surface — the clip itself is stencil work that
    /// arrives with the Arc-C renderer; until then the component carries the authored
    /// flag so scene transcription round-trips (e.g. the GameEventFeed viewport).
    /// </summary>
    public class Mask : MonoBehaviour
    {
        [SerializeField] bool m_ShowMaskGraphic = true;

        public bool showMaskGraphic { get => m_ShowMaskGraphic; set => m_ShowMaskGraphic = value; }
    }

    /// <summary>
    /// Rect-based clipper (original contract): clips child MaskableGraphics to this
    /// node's rect without stencil cost — the standard scroll-viewport clipper (the
    /// toast container authors one). Headless data surface until Arc C clips for real.
    /// </summary>
    public class RectMask2D : MonoBehaviour
    {
        [SerializeField] Vector4 m_Padding;
        [SerializeField] Vector2Int m_Softness;

        /// <summary>Clip-rect inset in pixels: (left, bottom, right, top).</summary>
        public Vector4 padding { get => m_Padding; set => m_Padding = value; }

        /// <summary>Soft-edge falloff in pixels per axis.</summary>
        public Vector2Int softness
        {
            get => m_Softness;
            set => m_Softness = new Vector2Int(Mathf.Max(0, value.x), Mathf.Max(0, value.y));
        }
    }
}
