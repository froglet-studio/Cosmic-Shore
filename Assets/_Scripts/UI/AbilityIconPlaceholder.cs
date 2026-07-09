using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Provides an obvious, code-generated "unassigned ability" placeholder sprite — diagonal
    /// hazard stripes inside a rounded border. No asset authoring required, so any vessel can show
    /// four ability icons the moment its HUD exists, even before real icons are drawn. The sprite is
    /// generated once and cached for the whole app.
    /// </summary>
    public static class AbilityIconPlaceholder
    {
        static Sprite _sprite;

        static readonly Color Background = new(0.09f, 0.09f, 0.11f, 1f);
        static readonly Color Stripe     = new(0.92f, 0.78f, 0.12f, 1f); // hazard yellow
        static readonly Color Border     = new(0.92f, 0.78f, 0.12f, 1f);

        public static Sprite Sprite
        {
            get
            {
                if (_sprite) return _sprite;
                _sprite = Build();
                return _sprite;
            }
        }

        static Sprite Build()
        {
            const int size = 96;
            const int border = 6;
            const int corner = 16;      // rounded-corner radius (transparent outside)
            const int stripeWidth = 14; // diagonal hazard stripe period (half on/half off)

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "AbilityIconPlaceholderTex",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c;
                    if (OutsideRoundedRect(x, y, size, corner))
                        c = clear;
                    else if (x < border || y < border || x >= size - border || y >= size - border)
                        c = Border;
                    else
                        c = (((x + y) / stripeWidth) & 1) == 0 ? Stripe : Background;

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "AbilityIconPlaceholder";
            return sprite;
        }

        // Rounded-rect mask: only the four corners get clipped to transparent.
        static bool OutsideRoundedRect(int x, int y, int size, int corner)
        {
            int nx = -1, ny = -1;
            if (x < corner) nx = corner - 1 - x;
            else if (x >= size - corner) nx = x - (size - corner);

            if (y < corner) ny = corner - 1 - y;
            else if (y >= size - corner) ny = y - (size - corner);

            if (nx < 0 || ny < 0) return false; // not in a corner region
            return nx * nx + ny * ny > corner * corner;
        }
    }
}
