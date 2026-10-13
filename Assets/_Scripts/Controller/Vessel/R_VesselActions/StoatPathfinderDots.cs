using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat pathfinder's line, drawn as round DOTS on a screen-space overlay
    /// (<c>R_VesselActions/STOAT_DIPOLE.md</c>): evenly spaced along the line AS IT IS SEEN, each
    /// <c>dotPixels</c> across with <c>dotGap</c> diameters of space between two
    /// (<see cref="StoatDipoleMath.LayDots(Vector2[], bool[], bool[], float[], int, float, Vector2[], int[])"/>),
    /// each with a dark rim so a dot never reads as a star.
    ///
    /// <para><b>Depth.</b> An overlay has no perspective of its own, so it is given one: each dot is sized by
    /// <see cref="StoatDipoleMath.DepthScale"/> from its camera depth (the nearest dot full size, the far end
    /// down to <c>dotDepthMinScale</c>), its gap is measured in its own diameters, and it fades by
    /// <c>dotDepthFade</c> toward the far end — so a path climbing away or diving toward the camera reads as
    /// one.</para>
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
        /// <summary>No dot is drawn smaller than this radius, pixels, however far: a sub-pixel dot only flickers.</summary>
        const float MinRadiusPixels = 0.75f;

        static Texture2D s_disc;

        readonly Vector2[] _screen = new Vector2[1024];
        readonly bool[] _visible = new bool[1024];
        readonly bool[] _breaks = new bool[1024];
        readonly float[] _depth = new float[1024];
        readonly float[] _scale = new float[1024];
        readonly Vector2[] _dotsAt = new Vector2[MaxDots];
        readonly int[] _dotSource = new int[MaxDots];
        int _dotCount, _pointCount, _loopIndex;
        float _diameter, _minScale, _depthFade;
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
            float dotPixels, float dotGap, bool warped, float depthMinScale = 1f, float depthFade = 0f)
        {
            if (!cam || points == null) { Hide(); return; }
            if (!transform.root.gameObject.activeSelf) transform.root.gameObject.SetActive(true);
            count = Mathf.Min(count, _screen.Length);
            float near = cam.nearClipPlane + 0.5f;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var v = cam.WorldToScreenPoint(points[i]);
                _visible[i] = v.z > near;
                _screen[i] = new Vector2(v.x, v.y);
                _depth[i] = v.z;
                _breaks[i] = false;
                if (_visible[i] && v.z < nearest) nearest = v.z;
            }
            _minScale = Mathf.Clamp01(depthMinScale);
            _depthFade = Mathf.Clamp01(depthFade);
            for (int i = 0; i < count; i++)
                _scale[i] = _visible[i] ? StoatDipoleMath.DepthScale(_depth[i], nearest, _minScale) : 1f;
            for (int j = 0; j < jumpCount && jumps != null && j < jumps.Length; j++)
                if (jumps[j] >= 0 && jumps[j] < count) _breaks[jumps[j]] = true;

            // Pixels on screen: the overlay canvas is unscaled, so its units ARE pixels.
            _diameter = Mathf.Max(0.5f, dotPixels) * Screen.height / ReferenceHeight;
            float spacing = _diameter * (1f + Mathf.Max(0f, dotGap));
            _dotCount = StoatDipoleMath.LayDots(_screen, _visible, _breaks, _scale, count, spacing, _dotsAt, _dotSource);
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
                // Depth: smaller and fainter toward the far end (1 at the nearest dot).
                float scale = _scale[src];
                float far = _minScale < 1f ? 1f - Mathf.InverseLerp(_minScale, 1f, scale) : 0f;
                a *= 1f - _depthFade * far;
                float rr = Mathf.Max(MinRadiusPixels, r * scale);
                var c = _tint;
                c.a *= a;
                var rc = rim;
                rc.a = 0.75f * a;
                Quad(vh, _dotsAt[i], rr * RimScale, rc);
                Quad(vh, _dotsAt[i], rr, c);
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
