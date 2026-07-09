namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Texture-drawing graphic (original contract) — the unatlased sibling of
    /// <see cref="Image"/>, used for render-texture previews and raw photos. Faithful
    /// detail: RawImage does NOT implement ILayoutElement in the original — it reports
    /// no size opinion to layout groups; sizing is manual (<see cref="SetNativeSize"/> or
    /// direct RectTransform writes, e.g. the hangar preview's sizeDelta assignment).
    /// </summary>
    public class RawImage : MaskableGraphic
    {
        [SerializeField] Texture m_Texture;
        [SerializeField] Rect m_UVRect = new(0f, 0f, 1f, 1f);

        public Texture texture
        {
            get => m_Texture;
            set
            {
                if (ReferenceEquals(m_Texture, value)) return;
                m_Texture = value;
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        public Texture mainTexture => m_Texture;

        /// <summary>Normalized sub-rect of the texture to draw (default: the whole texture).</summary>
        public Rect uvRect
        {
            get => m_UVRect;
            set
            {
                if (m_UVRect == value) return;
                m_UVRect = value;
                SetVerticesDirty();
            }
        }

        /// <summary>Resizes to the texture's pixel size scaled by the uvRect span (original rule).</summary>
        public virtual void SetNativeSize()
        {
            if (m_Texture == null) return;
            rectTransform.anchorMax = rectTransform.anchorMin;
            rectTransform.sizeDelta = new Vector2(
                m_Texture.width * m_UVRect.width,
                m_Texture.height * m_UVRect.height);
            SetAllDirty();
        }
    }
}
