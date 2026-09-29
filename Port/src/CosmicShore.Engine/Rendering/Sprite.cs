namespace CosmicShore.Engine
{
    /// <summary>
    /// 2D image asset reference (grown for UI arc B2 from the E2-style data stub —
    /// vessel-layer V11 introduced it for config SOs like CellConfigDataSO.Icon).
    /// Headless-first: geometry (<see cref="rect"/>, <see cref="pixelsPerUnit"/>,
    /// <see cref="border"/>) is REAL because Image's layout inputs consume it; pixel
    /// data and atlas semantics arrive with the presentation phase (Arc C).
    /// </summary>
    public class Sprite : Object
    {
        /// <summary>Backing texture (may be null for headless-authored sprites).</summary>
        public Texture2D texture { get; private set; }

        /// <summary>Sprite's sub-rect on the texture, in pixels.</summary>
        public Rect rect { get; private set; }

        /// <summary>Pivot, normalized [0,1] within <see cref="rect"/>.</summary>
        public Vector2 pivot { get; private set; } = new(0.5f, 0.5f);

        /// <summary>Pixels-per-unit density (original default: 100).</summary>
        public float pixelsPerUnit { get; private set; } = 100f;

        /// <summary>9-slice border sizes in pixels: (left, bottom, right, top) — original layout.</summary>
        public Vector4 border { get; private set; }

        /// <summary>
        /// Original contract: the sprite's rect on its texture after packing. The port never
        /// tight-packs or atlases, so it is always <see cref="rect"/> (and the uGUI padding is zero).
        /// </summary>
        public Rect textureRect => rect;

        /// <summary>Original contract: true when the sprite lives in an atlas. Never, in the port.</summary>
        public bool packed => false;

        /// <summary>
        /// UV rect of the whole sprite on its texture: (xMin, yMin, xMax, yMax), v = 0 at the
        /// bottom. (0,0,1,1) when the sprite has no texture — the original DataUtility.GetOuterUV.
        /// </summary>
        public Vector4 outerUV
        {
            get
            {
                if (texture == null || texture.width <= 0 || texture.height <= 0) return new Vector4(0f, 0f, 1f, 1f);
                float w = texture.width, h = texture.height;
                return new Vector4(rect.xMin / w, rect.yMin / h, rect.xMax / w, rect.yMax / h);
            }
        }

        /// <summary>
        /// UV rect of the 9-slice CENTRE cell (the rect inset by <see cref="border"/>) — the
        /// original DataUtility.GetInnerUV. Without a texture it is expressed within (0,0,1,1).
        /// </summary>
        public Vector4 innerUV
        {
            get
            {
                if (texture == null || texture.width <= 0 || texture.height <= 0)
                {
                    float rw = rect.width > 0f ? rect.width : 1f, rh = rect.height > 0f ? rect.height : 1f;
                    return new Vector4(border.x / rw, border.y / rh, 1f - border.z / rw, 1f - border.w / rh);
                }
                float w = texture.width, h = texture.height;
                return new Vector4((rect.xMin + border.x) / w, (rect.yMin + border.y) / h,
                                   (rect.xMax - border.z) / w, (rect.yMax - border.w) / h);
            }
        }

        /// <summary>
        /// Factory matching the original engine's creation contract (the subset the port
        /// consumes; extrude/mesh-type are presentation concerns deferred to Arc C).
        /// </summary>
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot,
            float pixelsPerUnit = 100f, uint extrude = 0, Vector4 border = default)
        {
            return new Sprite
            {
                texture = texture,
                rect = rect,
                pivot = pivot,
                pixelsPerUnit = pixelsPerUnit <= 0f ? 100f : pixelsPerUnit,
                border = border,
            };
        }
    }
}
