namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Sprite-drawing graphic (original contract). Headless-first split: the sprite/
    /// type/fill state is data until Arc C rasterizes it, but the ILayoutElement face is
    /// REAL — a sprite-bearing Image reports its pixel size as preferred size (scaled by
    /// the sprite ppu ÷ canvas reference ppu), which is how menu panels, fitters, and
    /// layout groups size themselves around icons. Layout priority 0, so an explicit
    /// LayoutElement (priority 1) on the same node overrides it — original resolution
    /// rule, already encoded in LayoutUtility.
    /// </summary>
    public class Image : MaskableGraphic, ILayoutElement
    {
        public enum Type { Simple = 0, Sliced = 1, Tiled = 2, Filled = 3 }
        public enum FillMethod { Horizontal = 0, Vertical = 1, Radial90 = 2, Radial180 = 3, Radial360 = 4 }

        [SerializeField] Sprite m_Sprite;
        [SerializeField] Type m_Type = Type.Simple;
        [SerializeField] bool m_PreserveAspect;
        [SerializeField] bool m_FillCenter = true;
        [SerializeField] FillMethod m_FillMethod = FillMethod.Radial360;
        [SerializeField] float m_FillAmount = 1f;
        [SerializeField] int m_FillOrigin;
        [SerializeField] float m_PixelsPerUnitMultiplier = 1f;

        Sprite m_OverrideSprite;

        /// <summary>The authored sprite. Changing it re-solves layout (size opinion changed).</summary>
        public Sprite sprite
        {
            get => m_Sprite;
            set
            {
                if (ReferenceEquals(m_Sprite, value)) return;
                m_Sprite = value;
                SetAllDirty();
            }
        }

        /// <summary>Runtime override (e.g. pressed-state swap); null falls back to <see cref="sprite"/>.</summary>
        public Sprite overrideSprite
        {
            get => activeSprite;
            set
            {
                if (ReferenceEquals(m_OverrideSprite, value)) return;
                m_OverrideSprite = value;
                SetAllDirty();
            }
        }

        Sprite activeSprite => m_OverrideSprite != null ? m_OverrideSprite : m_Sprite;

        public Type type { get => m_Type; set { if (m_Type == value) return; m_Type = value; SetVerticesDirty(); } }
        public bool preserveAspect { get => m_PreserveAspect; set { if (m_PreserveAspect == value) return; m_PreserveAspect = value; SetVerticesDirty(); } }
        public bool fillCenter { get => m_FillCenter; set { if (m_FillCenter == value) return; m_FillCenter = value; SetVerticesDirty(); } }
        public FillMethod fillMethod { get => m_FillMethod; set { if (m_FillMethod == value) return; m_FillMethod = value; m_FillOrigin = 0; SetVerticesDirty(); } }
        public int fillOrigin { get => m_FillOrigin; set { if (m_FillOrigin == value) return; m_FillOrigin = value; SetVerticesDirty(); } }

        /// <summary>Filled-type progress, clamped [0,1] (boost bars, cooldown rings).</summary>
        public float fillAmount
        {
            get => m_FillAmount;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (m_FillAmount == clamped) return;
                m_FillAmount = clamped;
                SetVerticesDirty();
            }
        }

        public float pixelsPerUnitMultiplier
        {
            get => m_PixelsPerUnitMultiplier;
            set { m_PixelsPerUnitMultiplier = Mathf.Max(0.01f, value); SetVerticesDirty(); }
        }

        /// <summary>
        /// Sprite pixels per canvas unit: sprite ppu ÷ the canvas's reference ppu
        /// (both default 100, so the common case is 1 — sprite pixels ARE canvas units).
        /// </summary>
        public float pixelsPerUnit
        {
            get
            {
                float spritePixelsPerUnit = activeSprite != null ? activeSprite.pixelsPerUnit : 100f;
                float referencePixelsPerUnit = canvas != null ? canvas.referencePixelsPerUnit : 100f;
                return spritePixelsPerUnit / referencePixelsPerUnit;
            }
        }

        protected float multipliedPixelsPerUnit => pixelsPerUnit * m_PixelsPerUnitMultiplier;

        /// <summary>Resizes the RectTransform to the sprite's native size in canvas units.</summary>
        public virtual void SetNativeSize()
        {
            if (activeSprite == null) return;
            var size = activeSprite.rect.size / multipliedPixelsPerUnit;
            rectTransform.anchorMax = rectTransform.anchorMin;
            rectTransform.sizeDelta = size;
            SetAllDirty();
        }

        // ── ILayoutElement (REAL — layout consumes this) ─────────────

        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }

        public virtual float minWidth => 0f;
        public virtual float minHeight => 0f;
        public virtual float flexibleWidth => -1f;
        public virtual float flexibleHeight => -1f;
        public virtual int layoutPriority => 0;

        public virtual float preferredWidth
        {
            get
            {
                if (activeSprite == null) return 0f;
                if (type is Type.Sliced or Type.Tiled)
                    return SlicedMinSize().x / multipliedPixelsPerUnit;
                return activeSprite.rect.size.x / multipliedPixelsPerUnit;
            }
        }

        public virtual float preferredHeight
        {
            get
            {
                if (activeSprite == null) return 0f;
                if (type is Type.Sliced or Type.Tiled)
                    return SlicedMinSize().y / multipliedPixelsPerUnit;
                return activeSprite.rect.size.y / multipliedPixelsPerUnit;
            }
        }

        // Original rule for 9-sliced sprites: the smallest size that keeps the border
        // corners unsquashed — the border sums (left+right, bottom+top), in pixels.
        Vector2 SlicedMinSize()
        {
            var b = activeSprite.border;
            return new Vector2(b.x + b.z, b.y + b.w);
        }
    }
}
