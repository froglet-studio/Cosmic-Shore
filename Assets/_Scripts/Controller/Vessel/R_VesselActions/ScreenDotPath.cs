using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A world-space path drawn as round DOTS on a screen-space overlay — the Stoat pathfinder's look
    /// (<c>StoatPathfinderDots</c>, branch <c>claude/peaceful-rubin-hhw49n</c>), so every predicted
    /// path in the fleet reads the same: dots evenly spaced along the line AS IT IS SEEN, each
    /// <c>dotPixels</c> across at 1080p with <c>dotGap</c> diameters of space between two, each with a
    /// dark rim so a dot never reads as a star, fading along the path. Used by the Thresher's launch
    /// line (<c>THRESHER.md</c> §5a).
    ///
    /// <para><b>Why a copy and not the Stoat's own class:</b> the Stoat has not landed on this branch's
    /// base. The spacing rule (<see cref="LayDots"/>) is a verbatim port of
    /// <c>StoatDipoleMath.LayDots</c> and the constants match <c>StoatPathfinderDots</c>; when the Stoat
    /// lands, its pathfinder should draw through this class (it is the generic half) and keep only its
    /// warp/loop alphas — recorded as a follow-up in THRESHER.md.</para>
    ///
    /// <para>An overlay, not a world-space line, for the Stoat's reason: the black-hole lens copies the
    /// camera's colour after the transparents, so anything drawn in the world near a hole is bent and
    /// doubled. One quad pair per dot, one draw call, under every HUD canvas.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScreenDotPath : MaskableGraphic
    {
        /// <summary>Below every HUD canvas: the line is the world's, the HUD is drawn over it.</summary>
        const int SortingOrder = -100;
        const int MaxPoints = 256;
        const int MaxDots = 512;
        const float ReferenceHeight = 1080f;
        const float RimScale = 1.45f;

        static Texture2D s_disc;

        readonly Vector2[] _screen = new Vector2[MaxPoints];
        readonly bool[] _visible = new bool[MaxPoints];
        readonly Vector2[] _dotsAt = new Vector2[MaxDots];
        readonly int[] _dotSource = new int[MaxDots];
        int _dotCount, _pointCount;
        float _diameter;
        Color _tint;

        public override Texture mainTexture => Disc();

        /// <summary>A fresh overlay canvas carrying one dot path (created on first use, hidden).</summary>
        public static ScreenDotPath Create(string owner)
        {
            var root = new GameObject($"[DotPath {owner}]");
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
            var dots = go.AddComponent<ScreenDotPath>();
            dots.raycastTarget = false;
            dots.Hide();
            return dots;
        }

        /// <summary>Project the straight segment <paramref name="from"/> → <paramref name="to"/>
        /// (sampled, so a segment that passes behind the lens breaks cleanly) and lay this frame's dots.</summary>
        public void ShowSegment(Camera cam, Vector3 from, Vector3 to, int samples, Color tint, float dotPixels, float dotGap)
        {
            if (!cam) { Hide(); return; }
            int count = Mathf.Clamp(samples, 2, MaxPoints);
            float near = cam.nearClipPlane + 0.5f;
            for (int i = 0; i < count; i++)
            {
                var v = cam.WorldToScreenPoint(Vector3.Lerp(from, to, i / (float)(count - 1)));
                _visible[i] = v.z > near;
                _screen[i] = new Vector2(v.x, v.y);
            }
            if (!transform.root.gameObject.activeSelf) transform.root.gameObject.SetActive(true);
            // Pixels on screen: the overlay canvas is unscaled, so its units ARE pixels.
            _diameter = Mathf.Max(0.5f, dotPixels) * Screen.height / ReferenceHeight;
            float spacing = _diameter * (1f + Mathf.Max(0f, dotGap));
            _dotCount = LayDots(_screen, _visible, null, count, spacing, _dotsAt, _dotSource);
            _pointCount = count;
            _tint = tint;
            SetVerticesDirty();
        }

        public void Hide()
        {
            _dotCount = 0;
            var root = transform.root.gameObject;
            if (root != gameObject && root.activeSelf) root.SetActive(false);
        }

        /// <summary>Destroy the overlay canvas this path lives on.</summary>
        public void DestroyOverlay()
        {
            var root = transform.root.gameObject;
            Destroy(root != gameObject ? root : gameObject);
        }

        /// <summary>
        /// Lays dots along a polyline AS IT IS SEEN (screen space): one dot at the first visible point,
        /// then one every <paramref name="spacing"/> pixels of on-screen length. A point marked not
        /// <paramref name="visible"/> (behind the camera) breaks the line, and so does a
        /// <paramref name="breakBefore"/> point; the next visible point restarts it with a dot. Returns
        /// the dots written to <paramref name="dots"/> (its length caps them);
        /// <paramref name="sourceIndex"/> receives which segment each came from, for its fade.
        /// A verbatim port of <c>StoatDipoleMath.LayDots</c>.
        /// </summary>
        public static int LayDots(Vector2[] screen, bool[] visible, bool[] breakBefore, int count, float spacing,
            Vector2[] dots, int[] sourceIndex)
        {
            if (screen == null || dots == null || count <= 0) return 0;
            spacing = Mathf.Max(0.5f, spacing);
            int n = 0;
            bool have = false;
            float carry = 0f;
            Vector2 prev = default;
            for (int i = 0; i < count && n < dots.Length; i++)
            {
                if (visible != null && !visible[i]) { have = false; continue; }
                var p = screen[i];
                if (!have || (breakBefore != null && breakBefore[i]))
                {
                    dots[n] = p;
                    if (sourceIndex != null) sourceIndex[n] = i;
                    n++;
                    have = true;
                    carry = spacing;
                    prev = p;
                    continue;
                }
                var d = p - prev;
                float len = d.magnitude;
                float t = carry;
                for (; t <= len && n < dots.Length; t += spacing)
                {
                    dots[n] = prev + d * (t / Mathf.Max(len, 1e-6f));
                    if (sourceIndex != null) sourceIndex[n] = i;
                    n++;
                }
                carry = t - len;
                prev = p;
            }
            return n;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_dotCount <= 0) return;
            float r = 0.5f * _diameter;
            var rim = new Color(4f / 255f, 6f / 255f, 20f / 255f, 1f);
            for (int i = 0; i < _dotCount; i++)
            {
                // The Stoat's open-path fade: bright at the start, fading with distance along the path.
                float a = Mathf.Max(0.45f, 0.95f - 0.5f * _dotSource[i] / Mathf.Max(1, _pointCount));
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
                name = "ScreenDotPathDisc", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
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
