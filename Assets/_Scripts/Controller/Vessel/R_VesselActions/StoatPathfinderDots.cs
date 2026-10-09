using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat pathfinder's line, drawn as round DOTS on a screen-space overlay
    /// (<c>R_VesselActions/STOAT_DIPOLE.md</c>): evenly spaced along the line AS IT IS SEEN, each
    /// <c>dotPixels</c> across with <c>dotGap</c> diameters of space between two
    /// (<see cref="StoatDipoleMath.LayDots"/>), each with a dark rim so a dot never reads as a star.
    ///
    /// <para>An overlay, not a world-space line, because the black-hole lens copies the camera's colour
    /// AFTER the transparents (<c>BlackHoleLensPass</c>): anything drawn in the world is bent and doubled by
    /// the hole it points at. The overlay draws after the lens, under the HUD (sorting order
    /// <see cref="SortingOrder"/>). One quad pair per dot, one draw call.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StoatPathfinderDots : MaskableGraphic
    {
        /// <summary>Below every HUD canvas: the line is the world's, the HUD is drawn over it.</summary>
        const int SortingOrder = -100;
        const int MaxDots = 512;
        const float ReferenceHeight = 1080f;
        const float RimScale = 1.45f;

        static Texture2D s_disc;

        readonly Vector2[] _screen = new Vector2[1024];
        readonly bool[] _visible = new bool[1024];
        readonly bool[] _breaks = new bool[1024];
        readonly Vector2[] _dotsAt = new Vector2[MaxDots];
        readonly int[] _dotSource = new int[MaxDots];
        int _dotCount, _pointCount, _loopIndex;
        float _diameter;
        bool _warped;
        Color _tint;

        public override Texture mainTexture => Disc();

        /// <summary>A fresh overlay canvas carrying one dots graphic (the pilot's; created on first use).</summary>
        public static StoatPathfinderDots Create(string owner)
        {
            var root = new GameObject($"[StoatPathfinder {owner}]");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var go = new GameObject("Dots", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var dots = go.AddComponent<StoatPathfinderDots>();
            dots.raycastTarget = false;
            dots.Hide();
            return dots;
        }

        /// <summary>Project <paramref name="points"/> through <paramref name="cam"/> and lay the dots for this frame.</summary>
        public void Show(Camera cam, Vector3[] points, int count, int[] jumps, int jumpCount, int loopIndex, Color tint,
            float dotPixels, float dotGap, bool warped)
        {
            if (!cam || points == null) { Hide(); return; }
            if (!transform.root.gameObject.activeSelf) transform.root.gameObject.SetActive(true);
            count = Mathf.Min(count, _screen.Length);
            float near = cam.nearClipPlane + 0.5f;
            for (int i = 0; i < count; i++)
            {
                var v = cam.WorldToScreenPoint(points[i]);
                _visible[i] = v.z > near;
                _screen[i] = new Vector2(v.x, v.y);
                _breaks[i] = false;
            }
            for (int j = 0; j < jumpCount && jumps != null && j < jumps.Length; j++)
                if (jumps[j] >= 0 && jumps[j] < count) _breaks[jumps[j]] = true;

            // Pixels on screen: the overlay canvas is unscaled, so its units ARE pixels.
            _diameter = Mathf.Max(0.5f, dotPixels) * Screen.height / ReferenceHeight;
            float spacing = _diameter * (1f + Mathf.Max(0f, dotGap));
            _dotCount = StoatDipoleMath.LayDots(_screen, _visible, _breaks, count, spacing, _dotsAt, _dotSource);
            _pointCount = count;
            _loopIndex = loopIndex;
            _tint = tint;
            _warped = warped;
            SetVerticesDirty();
        }

        public void Hide()
        {
            _dotCount = 0;
            var root = transform.root.gameObject;
            if (root != gameObject && root.activeSelf) root.SetActive(false);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_dotCount <= 0) return;
            float r = 0.5f * _diameter;
            var rim = new Color(4f / 255f, 6f / 255f, 20f / 255f, 1f);
            for (int i = 0; i < _dotCount; i++)
            {
                int src = _dotSource[i];
                // Open: fading with distance. Warped: bright, the loop itself brightest (the studio's alphas).
                float a = _warped ? (_loopIndex >= 0 && src > _loopIndex ? 0.95f : 0.8f)
                                  : Mathf.Max(0.45f, 0.95f - 0.5f * src / Mathf.Max(1, _pointCount));
                var c = _tint;
                c.a *= a;
                var rc = rim;
                rc.a = 0.75f * a;
                Quad(vh, _dotsAt[i], r * RimScale, rc);
                Quad(vh, _dotsAt[i], r, c);
            }
        }

        static void Quad(VertexHelper vh, Vector2 centre, float radius, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(centre.x - radius, centre.y - radius), color, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(centre.x - radius, centre.y + radius), color, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(centre.x + radius, centre.y + radius), color, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(centre.x + radius, centre.y - radius), color, new Vector2(1f, 0f));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        /// <summary>A soft-edged white disc, made once (no asset to lose).</summary>
        static Texture2D Disc()
        {
            if (s_disc) return s_disc;
            const int size = 32;
            s_disc = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "StoatPathfinderDisc", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[size * size];
            float c = 0.5f * (size - 1);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (0.5f * size);
                byte alpha = (byte)(255f * Mathf.Clamp01((1f - d) * 8f));
                px[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            s_disc.SetPixels32(px);
            s_disc.Apply(false, true);
            return s_disc;
        }
    }
}
