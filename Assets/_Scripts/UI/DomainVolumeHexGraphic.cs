using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Procedural hexagonal domain-volume gauge (the pause-button face).
    ///
    /// A pointy-top hexagon split into three fixed 120° sectors — one per playable
    /// domain — each spanning two of the six edges:
    ///   Jade  → top      (corners 150°, 90°, 30°)
    ///   Ruby  → lower-left  (corners 150°, 210°, 270°)
    ///   Gold  → lower-right (corners 270°, 330°, 30°)
    /// Seams sit at the top-left (150°), bottom (270°) and top-right (30°) vertices.
    ///
    /// Each sector ALWAYS spans its full angular width; volume is shown by RADIAL
    /// DEPTH — the colored band grows from the outer hexagon edge inward toward the
    /// centre as the domain's mass approaches the frenzy threshold (fill = 1 reaches
    /// the centre hexagon). A small centre hexagon is tinted by the dominant domain,
    /// denoting who currently holds the most volume.
    ///
    /// Follows the ObjectiveArrowGraphic idiom: one MaskableGraphic, no sprite/font
    /// dependency, geometry built in OnPopulateMesh. The owning DomainVolumeIndicator
    /// pushes state via SetState; the mesh rebuilds only when values actually change.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Cosmic Shore/Domain Volume Hex")]
    public class DomainVolumeHexGraphic : MaskableGraphic
    {
        [Header("Geometry (fractions of the rect's half-min-extent)")]
        [Tooltip("Outer radius of the colored bands. <1 leaves a gap to the boundary ring.")]
        [Range(0.3f, 1f)] [SerializeField] float bandOuterFrac = 0.92f;
        [Tooltip("Radius of the centre 'dominant' hexagon. Bands reach this at fill = 1.")]
        [Range(0.05f, 0.4f)] [SerializeField] float centerHexFrac = 0.18f;
        [Tooltip("Minimum band thickness (fraction) so an empty domain still shows a sliver at full width.")]
        [Range(0f, 0.2f)] [SerializeField] float minBandThicknessFrac = 0.06f;
        [Tooltip("Faint boundary hexagon marking the frenzy extent. 0 thickness = off.")]
        [Range(0f, 0.12f)] [SerializeField] float boundaryThicknessFrac = 0.04f;

        [Header("Colors")]
        [SerializeField] Color boundaryColor = new(1f, 1f, 1f, 0.18f);
        [Tooltip("Centre hexagon tint when no domain leads (empty cell / tie at zero).")]
        [SerializeField] Color neutralCenterColor = new(1f, 1f, 1f, 0.25f);

        // Pointy-top hexagon: corner k sits at 90° + 60°·k.
        static readonly float[] CornerAngles = { 90f, 150f, 210f, 270f, 330f, 30f };

        // Each domain's three corner angles, in perimeter order (2 edges per domain).
        static readonly float[][] DomainCornerAngles =
        {
            new[] { 150f,  90f,  30f }, // 0: Jade  — top
            new[] { 150f, 210f, 270f }, // 1: Ruby  — lower-left
            new[] { 270f, 330f,  30f }, // 2: Gold  — lower-right
        };

        // State pushed by the controller.
        readonly float[] _fill = new float[3];           // 0..1 radial depth per domain
        readonly Color[] _domainColor = { Color.green, Color.red, Color.yellow };
        int _dominant = -1;                              // 0/1/2 or -1 (none)

        /// <summary>
        /// Push the live gauge state. Rebuilds the mesh only when something changed
        /// past a small epsilon, so per-frame lerps from the controller don't force a
        /// Canvas batch rebuild every frame once the values settle.
        /// </summary>
        public void SetState(float fillJade, float fillRuby, float fillGold,
                             Color cJade, Color cRuby, Color cGold, int dominant)
        {
            bool changed =
                !Approximately(_fill[0], fillJade) ||
                !Approximately(_fill[1], fillRuby) ||
                !Approximately(_fill[2], fillGold) ||
                _dominant != dominant ||
                _domainColor[0] != cJade || _domainColor[1] != cRuby || _domainColor[2] != cGold;

            if (!changed) return;

            _fill[0] = fillJade; _fill[1] = fillRuby; _fill[2] = fillGold;
            _domainColor[0] = cJade; _domainColor[1] = cRuby; _domainColor[2] = cGold;
            _dominant = dominant;
            SetVerticesDirty();
        }

        static bool Approximately(float a, float b) => Mathf.Abs(a - b) < 0.002f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = rectTransform.rect;
            Vector2 c = r.center;
            float R = Mathf.Min(r.width, r.height) * 0.5f;
            if (R <= 0f) return;

            float bandOuterR = R * bandOuterFrac;
            float centerR = R * centerHexFrac;
            float minThick = R * minBandThicknessFrac;

            // 1) Faint boundary hexagon ring — the frenzy extent marker.
            if (boundaryThicknessFrac > 0f)
                AddHexRing(vh, c, R, R - R * boundaryThicknessFrac, boundaryColor);

            // 2) Three domain bands, each filling its 120° sector radially inward.
            for (int d = 0; d < 3; d++)
            {
                float fill = Mathf.Clamp01(_fill[d]);
                // fill = 0 → a thin sliver at the outer edge (full angular width, per spec).
                // fill = 1 → inner edge reaches the centre hexagon.
                float innerR = Mathf.Lerp(bandOuterR - minThick, centerR, fill);
                innerR = Mathf.Min(innerR, bandOuterR - 0.5f); // guard against degenerate/inverted band
                AddSectorBand(vh, c, bandOuterR, innerR, DomainCornerAngles[d], _domainColor[d]);
            }

            // 3) Centre hexagon — tinted by the dominant domain (who leads on volume).
            Color centerColor = (_dominant >= 0 && _dominant < 3) ? _domainColor[_dominant] : neutralCenterColor;
            AddHexFill(vh, c, centerR, centerColor);
        }

        // ------------------------------------------------------------------
        //  Geometry helpers
        // ------------------------------------------------------------------

        /// <summary>Filled regular hexagon (triangle fan from centre).</summary>
        static void AddHexFill(VertexHelper vh, Vector2 c, float radius, Color color)
        {
            int center = vh.currentVertCount;
            vh.AddVert(MakeVert(c, color));
            for (int k = 0; k < 6; k++)
                vh.AddVert(MakeVert(c + Dir(CornerAngles[k]) * radius, color));
            for (int k = 0; k < 6; k++)
                vh.AddTriangle(center, center + 1 + k, center + 1 + ((k + 1) % 6));
        }

        /// <summary>Closed hexagonal ring (annulus) between rOuter and rInner.</summary>
        static void AddHexRing(VertexHelper vh, Vector2 c, float rOuter, float rInner, Color color)
        {
            int b = vh.currentVertCount;
            for (int k = 0; k < 6; k++)
            {
                Vector2 dir = Dir(CornerAngles[k]);
                vh.AddVert(MakeVert(c + dir * rOuter, color)); // 2k   outer
                vh.AddVert(MakeVert(c + dir * rInner, color)); // 2k+1 inner
            }
            for (int k = 0; k < 6; k++)
            {
                int o0 = b + 2 * k,       i0 = b + 2 * k + 1;
                int o1 = b + 2 * ((k + 1) % 6), i1 = b + 2 * ((k + 1) % 6) + 1;
                vh.AddTriangle(o0, o1, i1);
                vh.AddTriangle(o0, i1, i0);
            }
        }

        /// <summary>
        /// A hexagonal-annulus SECTOR following the given corner angles (3 corners =
        /// 2 edges), between rOuter (perimeter) and rInner (toward centre).
        /// </summary>
        static void AddSectorBand(VertexHelper vh, Vector2 c, float rOuter, float rInner, float[] angles, Color color)
        {
            int b = vh.currentVertCount;
            for (int i = 0; i < angles.Length; i++)
            {
                Vector2 dir = Dir(angles[i]);
                vh.AddVert(MakeVert(c + dir * rOuter, color)); // 2i   outer
                vh.AddVert(MakeVert(c + dir * rInner, color)); // 2i+1 inner
            }
            for (int seg = 0; seg < angles.Length - 1; seg++)
            {
                int o0 = b + 2 * seg,       i0 = b + 2 * seg + 1;
                int o1 = b + 2 * (seg + 1), i1 = b + 2 * (seg + 1) + 1;
                vh.AddTriangle(o0, o1, i1);
                vh.AddTriangle(o0, i1, i0);
            }
        }

        static Vector2 Dir(float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        static UIVertex MakeVert(Vector2 pos, Color c) =>
            new() { position = pos, color = c, uv0 = Vector2.zero };
    }
}
