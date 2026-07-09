namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Base class for everything the UI draws (original contract: the visual component
    /// that owns color/material state and registers with a Canvas). Headless-first split:
    /// the STATE surface (color, raycastTarget, rectTransform, canvas walk, dirty
    /// notifications) is REAL — layout and, later, the Arc-D event system consume it —
    /// while the vertex/material rebuild hooks are virtual no-ops until the Arc-C
    /// renderer gives them a mesh to fill.
    ///
    /// A Graphic requires a RectTransform in the original (RequireComponent); here
    /// <see cref="rectTransform"/> converts the host's Transform in place on first read
    /// (the Arc-A AddComponent conversion), which is the same end state.
    /// </summary>
    public abstract class Graphic : MonoBehaviour
    {
        [SerializeField] protected Color m_Color = Color.white;
        [SerializeField] bool m_RaycastTarget = true;

        RectTransform m_RectTransform;

        /// <summary>Vertex tint. Setting marks vertices dirty (a no-op headless).</summary>
        public virtual Color color
        {
            get => m_Color;
            set
            {
                if (m_Color == value) return;
                m_Color = value;
                SetVerticesDirty();
            }
        }

        /// <summary>Whether the Arc-D raycaster considers this graphic a hit target.</summary>
        public virtual bool raycastTarget { get => m_RaycastTarget; set => m_RaycastTarget = value; }

        /// <summary>The host RectTransform (converts a plain Transform in place on first read).</summary>
        public RectTransform rectTransform =>
            m_RectTransform ??= transform as RectTransform ?? gameObject.AddComponent<RectTransform>();

        /// <summary>The nearest enabled Canvas at or above this graphic (null when none).</summary>
        public Canvas canvas
        {
            get
            {
                for (var t = transform; t is not null; t = t.parent)
                {
                    var c = t.gameObject.GetComponent<Canvas>();
                    if (c != null && c.isActiveAndEnabled) return c;
                }
                return null;
            }
        }

        public virtual void SetAllDirty()
        {
            SetLayoutDirty();
            SetVerticesDirty();
            SetMaterialDirty();
        }

        /// <summary>Queues the layout root above this graphic for the canvas-slot rebuild.</summary>
        public virtual void SetLayoutDirty() => LayoutRebuilder.MarkLayoutForRebuild(rectTransform);

        /// <summary>Mesh regeneration hook — no-op until the Arc-C renderer consumes it.</summary>
        public virtual void SetVerticesDirty() { }

        /// <summary>Material rebind hook — no-op until the Arc-C renderer consumes it.</summary>
        public virtual void SetMaterialDirty() { }

        protected virtual void OnEnable() => SetAllDirty();

        // Marks even while disabling — the layout above must re-solve WITHOUT this
        // graphic's contribution (same rule as LayoutElement).
        protected virtual void OnDisable() => SetLayoutDirty();
    }

    /// <summary>
    /// A Graphic that can be clipped by <see cref="Mask"/>/<see cref="RectMask2D"/>
    /// ancestors (original contract). Clipping itself is an Arc-C render concern;
    /// headless this carries the maskable flag the menu prefabs author.
    /// </summary>
    public abstract class MaskableGraphic : Graphic
    {
        [SerializeField] bool m_Maskable = true;

        public bool maskable { get => m_Maskable; set => m_Maskable = value; }
    }
}
