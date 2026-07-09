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
