namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Original contract: a component that can veto a raycast hit on its subtree. The
    /// raycaster asks every filter on the hit graphic and its ancestors; any false rejects it.
    /// </summary>
    public interface ICanvasRaycastFilter
    {
        bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera);
    }

    /// <summary>
    /// Stencil mask (original contract): clips child MaskableGraphics to this node's
    /// graphic shape. Headless data surface — the clip itself is stencil work that
    /// arrives with the Arc-C renderer; until then the component carries the authored
    /// flag so scene transcription round-trips (e.g. the GameEventFeed viewport).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class Mask : MonoBehaviour, ICanvasRaycastFilter
    {
        [SerializeField] bool m_ShowMaskGraphic = true;

        public bool showMaskGraphic { get => m_ShowMaskGraphic; set => m_ShowMaskGraphic = value; }

        /// <summary>A hit outside the mask's own rect is clipped away (original contract).</summary>
        public virtual bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
            => !isActiveAndEnabled || transform is not RectTransform rt || RectTransformUtility.RectangleContainsScreenPoint(rt, sp, eventCamera);
    }

    /// <summary>
    /// Rect-based clipper (original contract): clips child MaskableGraphics to this
    /// node's rect without stencil cost — the standard scroll-viewport clipper (the
    /// toast container authors one). Headless data surface until Arc C clips for real.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class RectMask2D : MonoBehaviour, ICanvasRaycastFilter
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

        /// <summary>A hit outside the padded clip rect is clipped away (original contract).</summary>
        public virtual bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (!isActiveAndEnabled || transform is not RectTransform rt) return true;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float xMin = corners[0].x + m_Padding.x, yMin = corners[0].y + m_Padding.y;
            float xMax = corners[2].x - m_Padding.z, yMax = corners[2].y - m_Padding.w;
            return sp.x >= xMin && sp.x <= xMax && sp.y >= yMin && sp.y <= yMax;
        }
    }
}
