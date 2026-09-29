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
        [SerializeField] protected Material m_Material;
        [SerializeField] protected Color m_Color = Color.white;
        [SerializeField] bool m_RaycastTarget = true;

        /// <summary>Custom UI material (null = the default UI material). Arc E: filled from scene data.</summary>
        public virtual Material material
        {
            get => m_Material;
            set { if (ReferenceEquals(m_Material, value)) return; m_Material = value; SetMaterialDirty(); }
        }

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

        // ── Mesh generation (the uGUI OnPopulateMesh contract) ──────────────────

        static Texture2D s_WhiteTexture;

        /// <summary>The texture this graphic samples (default: the material's main texture, else white).</summary>
        public virtual Texture mainTexture => m_Material != null && m_Material.mainTexture != null
            ? m_Material.mainTexture
            : (s_WhiteTexture ??= Texture2D.whiteTexture);

        /// <summary>The material actually used to draw (original: material after modifiers).</summary>
        public virtual Material materialForRendering => material;

        /// <summary>Default UI material stand-in (null: the renderer's own UI pipeline).</summary>
        public virtual Material defaultMaterial => null;

        /// <summary>Hierarchy draw depth (-1 when not under a canvas).</summary>
        public int depth => canvas != null ? 0 : -1;

        CanvasRenderer m_CanvasRenderer;
        public CanvasRenderer canvasRenderer => m_CanvasRenderer ??= gameObject.GetComponent<CanvasRenderer>() ?? gameObject.AddComponent<CanvasRenderer>();

        /// <summary>
        /// Fills <paramref name="vh"/> with this graphic's geometry in RectTransform local space.
        /// Default (Unity's base Graphic): one quad over the pixel-adjusted rect in <see cref="color"/>.
        /// </summary>
        protected virtual void OnPopulateMesh(VertexHelper vh)
        {
            var r = GetPixelAdjustedRect();
            var v = new Vector4(r.x, r.y, r.x + r.width, r.y + r.height);
            Color32 c = color;
            vh.Clear();
            vh.AddVert(new Vector3(v.x, v.y), c, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(v.x, v.w), c, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(v.z, v.w), c, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(v.z, v.y), c, new Vector2(1f, 0f));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        /// <summary>Renderer entry point: builds this graphic's mesh, applying <see cref="IMeshModifier"/>s like the canvas update.</summary>
        public void PopulateMeshForRendering(VertexHelper vh)
        {
            vh.Clear();
            OnPopulateMesh(vh);
            foreach (var c in gameObject.GetComponents<Component>())
                if (c is IMeshModifier m && (c is not Behaviour b || b.isActiveAndEnabled)) m.ModifyMesh(vh);
        }

        /// <summary>The rect in local space (the port does not snap to pixels; pixel-perfect is a render nicety).</summary>
        public Rect GetPixelAdjustedRect() => rectTransform.rect;

        public Vector2 PixelAdjustPoint(Vector2 point) => point;

        public virtual void Rebuild(CanvasUpdate update) { }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }
        public virtual void SetNativeSize() { }

        public virtual bool Raycast(Vector2 sp, Camera eventCamera) => isActiveAndEnabled && raycastTarget;

        /// <summary>Tweens the CanvasRenderer tint (applied instantly in the port; the renderer multiplies it).</summary>
        public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha)
            => CrossFadeColor(targetColor, duration, ignoreTimeScale, useAlpha, true);
        public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha, bool useRGB)
        {
            var cur = canvasRenderer.GetColor();
            canvasRenderer.SetColor(new Color(useRGB ? targetColor.r : cur.r, useRGB ? targetColor.g : cur.g, useRGB ? targetColor.b : cur.b, useAlpha ? targetColor.a : cur.a));
        }
        public virtual void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale) => canvasRenderer.SetAlpha(alpha);

        public virtual void SetRaycastDirty() { }
        public virtual void SetMaterialDirtyInternal() { }

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
