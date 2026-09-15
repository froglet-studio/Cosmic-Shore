using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A float RGBA image with no Unity texture behind it, so the painter is pure and runs in
    /// tests and offline. <see cref="CharacterBustBuilder"/> uploads it to a Texture2D.
    /// </summary>
    public sealed class TextureCanvas
    {
        public readonly int Width, Height;
        public readonly float[] R, G, B, A;

        public TextureCanvas(int width, int height, float fillR = 0f, float fillG = 0f, float fillB = 0f, float fillA = 1f)
        {
            Width = Mathf.Max(1, width); Height = Mathf.Max(1, height);
            int n = Width * Height;
            R = new float[n]; G = new float[n]; B = new float[n]; A = new float[n];
            for (int i = 0; i < n; i++) { R[i] = fillR; G[i] = fillG; B[i] = fillB; A[i] = fillA; }
        }

        public int Index(int x, int y) => ((y % Height + Height) % Height) * Width + ((x % Width + Width) % Width);

        public Color Get(int x, int y)
        {
            int i = Index(x, y);
            return new Color(R[i], G[i], B[i], A[i]);
        }

        /// <summary>Canvases are LDR: every write is clamped to [0, 1].</summary>
        public void Set(int x, int y, Color c)
        {
            int i = Index(x, y);
            R[i] = Mathf.Clamp01(c.r); G[i] = Mathf.Clamp01(c.g); B[i] = Mathf.Clamp01(c.b); A[i] = Mathf.Clamp01(c.a);
        }

        public void SetRgb(int x, int y, Color c)
        {
            int i = Index(x, y);
            R[i] = c.r; G[i] = c.g; B[i] = c.b;
        }

        /// <summary>Bilinear sample with u wrapping, v clamped; (u,v) in [0,1].</summary>
        public Color Sample(float u, float v)
        {
            float fx = u * Width - 0.5f, fy = Mathf.Clamp01(v) * (Height - 1);
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            int y1 = Mathf.Min(y0 + 1, Height - 1);
            Color a = Get(x0, y0), b = Get(x0 + 1, y0), c = Get(x0, y1), d = Get(x0 + 1, y1);
            return Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, d, tx), ty);
        }

        public Color32[] ToColor32()
        {
            var px = new Color32[Width * Height];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(To8(R[i]), To8(G[i]), To8(B[i]), To8(A[i]));
            return px;
        }

        static byte To8(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

        /// <summary>
        /// Tangent-space normal map from a height canvas (height in R), u wrapping. Encoded
        /// (x*0.5+0.5, y*0.5+0.5, z) with alpha 1, which URP's UnpackNormal reads on every platform.
        /// </summary>
        public static TextureCanvas NormalFromHeight(TextureCanvas height, float strength)
        {
            var n = new TextureCanvas(height.Width, height.Height, 0.5f, 0.5f, 1f, 1f);
            if (strength <= 0f) return n;
            for (int y = 0; y < height.Height; y++)
            {
                for (int x = 0; x < height.Width; x++)
                {
                    float l = height.R[height.Index(x - 1, y)], r = height.R[height.Index(x + 1, y)];
                    float d = height.R[height.Index(x, Mathf.Max(0, y - 1))], u = height.R[height.Index(x, Mathf.Min(height.Height - 1, y + 1))];
                    Vector3 nv = new Vector3(-(r - l) * strength, -(u - d) * strength, 1f).normalized;
                    n.Set(x, y, new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z, 1f));
                }
            }
            return n;
        }
    }

    /// <summary>The painted output for one character: one canvas per material slot, plus the skin normal map.</summary>
    public sealed class CharacterTextures
    {
        public TextureCanvas Skin;        // rgb albedo, alpha = smoothness
        public TextureCanvas SkinNormal;
        public TextureCanvas Eye;
        public TextureCanvas Keratin;
        public TextureCanvas Hair;
        public TextureCanvas Gear;        // rgb albedo, alpha = smoothness
        public float EyeSmoothness = 0.92f;
        public float KeratinSmoothness = 0.55f;
        public float HairSmoothness = 0.30f;
    }
}
