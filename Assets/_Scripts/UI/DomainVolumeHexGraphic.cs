using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Procedural hexagonal domain-volume gauge - the universal in-game pause-button
    /// face. Trained as a single recognizable element across the main menu and every
    /// gameplay scene (attached to the "Volume / Pause Button" by MenuMiniGameHUD in
    /// the menu and by MiniGameHUD in gameplay).
    ///
    /// A pointy-top hexagon split into three fixed 120° sectors - one per playable
    /// domain - each spanning two of the six edges:
    ///   Jade  → top      (corners 150°, 90°, 30°)
    ///   Ruby  → lower-left  (corners 150°, 210°, 270°)
    ///   Gold  → lower-right (corners 270°, 330°, 30°)
    /// Seams sit at the top-left (150°), bottom (270°) and top-right (30°) vertices.
    ///
    /// Each sector ALWAYS spans its full angular width; volume is shown by RADIAL
    /// DEPTH - the colored band grows from the outer hexagon edge inward toward the
    /// centre as the domain's mass approaches the frenzy threshold. The END of the
    /// wedge (the centre hexagon) is the final Frenzy state.
    ///
    /// Overlaid on top are CONCENTRIC THRESHOLD RINGS - one per intermediate cell phase
    /// boundary (Restless; Frenzy itself is the centre / boundary hexagon) - placed at the
    /// radius a wedge reaches when its mass equals that threshold. As a domain's wedge
    /// fills inward it PASSES
    /// THROUGH each ring; a ring DISAPPEARS once a wedge has filled past it, so only the
    /// upcoming thresholds are shown and the colored wedges aren't visually cluttered.
    /// "Which domain" reads off the colored wedge extending past where the ring was.
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
        [Range(0.3f, 1f)] [SerializeField] float bandOuterFrac = 0.78f;
        [Tooltip("Radius of the centre 'dominant' hexagon. Bands reach this at fill = 1 (frenzy).")]
        [Range(0.05f, 0.4f)] [SerializeField] float centerHexFrac = 0.18f;
        [Tooltip("Minimum band thickness (fraction) so an empty domain still shows a sliver at full width.")]
        [Range(0f, 0.2f)] [SerializeField] float minBandThicknessFrac = 0.06f;
        [Tooltip("Faint boundary hexagon marking the frenzy extent. 0 thickness = off.")]
        [Range(0f, 0.12f)] [SerializeField] float boundaryThicknessFrac = 0.04f;

        [Header("Phase threshold rings (wedges pass through these)")]
        [Tooltip("Thickness of each concentric phase-threshold ring, as a fraction of the half-min-extent.")]
        [Range(0.005f, 0.08f)] [SerializeField] float phaseRingThicknessFrac = 0.022f;
        [Tooltip("Color of a phase-threshold ring no wedge has reached yet. The ring disappears once a wedge fills past it.")]
        [SerializeField] Color thresholdRingColor = new(1f, 1f, 1f, 0.28f);

        [Header("Spawn-cycle ring (outside the hex)")]
        [Tooltip("Inner radius of the spawn-cycle ring (fraction). Sits OUTSIDE the band/boundary so it doesn't obscure the volume gauge.")]
        [Range(0.6f, 1f)] [SerializeField] float ringInnerFrac = 0.86f;
        [Tooltip("Outer radius of the spawn-cycle ring (fraction).")]
        [Range(0.6f, 1f)] [SerializeField] float ringOuterFrac = 0.98f;
        [Tooltip("Faint background track behind the ring's progress arc. 0 alpha = off.")]
        [SerializeField] Color ringTrackColor = new(1f, 1f, 1f, 0.10f);
        [Tooltip("Number of arc segments - higher = smoother ring at the cost of more triangles.")]
        [Range(12, 128)] [SerializeField] int ringSegments = 64;

        [Header("Colors")]
        [SerializeField] Color boundaryColor = new(1f, 1f, 1f, 0.18f);
        [Tooltip("Centre hexagon tint when no domain leads (empty cell / tie at zero).")]
        [SerializeField] Color neutralCenterColor = new(1f, 1f, 1f, 0.25f);

        // Pointy-top hexagon: corner k sits at 90° + 60°·k.
        static readonly float[] CornerAngles = { 90f, 150f, 210f, 270f, 330f, 30f };

        // Each domain's three corner angles, in perimeter order (2 edges per domain).
        static readonly float[][] DomainCornerAngles =
        {
            new[] { 150f,  90f,  30f }, // 0: Jade  - top
            new[] { 150f, 210f, 270f }, // 1: Ruby  - lower-left
            new[] { 270f, 330f,  30f }, // 2: Gold  - lower-right
        };

        // State pushed by the controller.
        readonly float[] _fill = new float[3];           // 0..1 radial depth per domain
        readonly Color[] _domainColor = { Color.green, Color.red, Color.yellow };
        int _dominant = -1;                              // 0/1/2 or -1 (none)
        float _spawnCycle;                               // 0..1 progress toward next fauna spawn
        float[] _thresholdFracs = System.Array.Empty<float>(); // phase enter thresholds / RabidEnter (ascending)

        /// <summary>
        /// Push the live gauge state. Rebuilds the mesh only when something changed
        /// past a small epsilon, so per-frame lerps from the controller don't force a
        /// Canvas batch rebuild every frame once the values settle.
        ///
        /// <paramref name="thresholdFracs"/> are the cell's phase enter thresholds as a
        /// fraction of RabidEnter (ascending) - where to draw the concentric rings the
        /// wedges pass through. Pass an empty/null array to draw no rings.
        /// </summary>
        public void SetState(float fillJade, float fillRuby, float fillGold,
                             Color cJade, Color cRuby, Color cGold, int dominant,
                             float spawnCycle, float[] thresholdFracs)
        {
            bool changed =
                !Approximately(_fill[0], fillJade) ||
                !Approximately(_fill[1], fillRuby) ||
                !Approximately(_fill[2], fillGold) ||
                _dominant != dominant ||
                !Approximately(_spawnCycle, spawnCycle) ||
                _domainColor[0] != cJade || _domainColor[1] != cRuby || _domainColor[2] != cGold ||
                !FracsApproximately(_thresholdFracs, thresholdFracs);

            if (!changed) return;

            _fill[0] = fillJade; _fill[1] = fillRuby; _fill[2] = fillGold;
            _domainColor[0] = cJade; _domainColor[1] = cRuby; _domainColor[2] = cGold;
            _dominant = dominant;
            _spawnCycle = spawnCycle;
            CopyThresholds(thresholdFracs);
            SetVerticesDirty();
        }

        void CopyThresholds(float[] src)
        {
            if (src == null || src.Length == 0) { _thresholdFracs = System.Array.Empty<float>(); return; }
            if (_thresholdFracs.Length != src.Length) _thresholdFracs = new float[src.Length];
            System.Array.Copy(src, _thresholdFracs, src.Length);
        }

        static bool FracsApproximately(float[] a, float[] b)
        {
            int la = a?.Length ?? 0, lb = b?.Length ?? 0;
            if (la != lb) return false;
            for (int i = 0; i < la; i++)
                if (!Approximately(a[i], b[i])) return false;
            return true;
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

            // 1) Faint boundary hexagon ring - the frenzy extent marker.
            if (boundaryThicknessFrac > 0f)
                AddHexRing(vh, c, R, R - R * boundaryThicknessFrac, boundaryColor);

            // 2) Three domain bands, each filling its 120° sector radially inward.
            float maxFill = 0f;
            for (int d = 0; d < 3; d++)
            {
                float fill = Mathf.Clamp01(_fill[d]);
                if (fill > maxFill) maxFill = fill;
                // fill = 0 → a thin sliver at the outer edge (full angular width, per spec).
                // fill = 1 → inner edge reaches the centre hexagon (frenzy).
                float innerR = Mathf.Lerp(bandOuterR - minThick, centerR, fill);
                innerR = Mathf.Min(innerR, bandOuterR - 0.5f); // guard against degenerate/inverted band
                AddSectorBand(vh, c, bandOuterR, innerR, DomainCornerAngles[d], _domainColor[d]);
            }

            // 3) Concentric phase-threshold rings the wedges pass through. Each ring sits
            //    at the radius a wedge reaches when its mass equals that threshold. A ring
            //    DISAPPEARS once the leading wedge has filled past it - so only the
            //    upcoming thresholds are drawn, reducing visual competition with the
            //    colored wedges. "Which domain" still reads off the wedge extending past
            //    where the ring used to be.
            float ringThick = R * phaseRingThicknessFrac;
            for (int i = 0; i < _thresholdFracs.Length; i++)
            {
                float f = Mathf.Clamp01(_thresholdFracs[i]);
                if (maxFill >= f - 0.0005f) continue; // crossed → ring gone
                float ringR = Mathf.Lerp(bandOuterR - minThick, centerR, f);
                if (ringR <= ringThick) continue;
                AddHexRing(vh, c, ringR, ringR - ringThick, thresholdRingColor);
            }

            // 4) Centre hexagon - tinted by the dominant domain (who leads on volume).
            Color centerColor = (_dominant >= 0 && _dominant < 3) ? _domainColor[_dominant] : neutralCenterColor;
            AddHexFill(vh, c, centerR, centerColor);

            // 5) Spawn-cycle ring around the outside. Empty background track + a
            //    progress arc starting at the top and sweeping clockwise. The arc
            //    is tinted by the DOMINANT domain because every periodic fauna
            //    spawn produces the leader's color - when the arc completes a
            //    full revolution, a new pack of that color spawns.
            float ringInner = R * Mathf.Min(ringInnerFrac, ringOuterFrac);
            float ringOuter = R * Mathf.Max(ringInnerFrac, ringOuterFrac);
            int segs = Mathf.Max(12, ringSegments);
            if (ringTrackColor.a > 0f)
                AddRingArc(vh, c, ringOuter, ringInner, 0f, 1f, segs, ringTrackColor);
            float cycle = Mathf.Clamp01(_spawnCycle);
            if (cycle > 0.001f)
            {
                Color arcColor = (_dominant >= 0 && _dominant < 3) ? _domainColor[_dominant] : neutralCenterColor;
                AddRingArc(vh, c, ringOuter, ringInner, 0f, cycle, segs, arcColor);
            }
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

        /// <summary>
        /// Circular annulus arc from <paramref name="startFrac"/> to
        /// <paramref name="endFrac"/> of a full circle, sweeping CLOCKWISE from the
        /// top (12 o'clock). Used for the spawn-cycle ring.
        /// </summary>
        static void AddRingArc(VertexHelper vh, Vector2 c, float rOuter, float rInner,
                               float startFrac, float endFrac, int totalSegments, Color color)
        {
            startFrac = Mathf.Clamp01(startFrac);
            endFrac = Mathf.Clamp01(endFrac);
            if (endFrac <= startFrac) return;

            int segsThisArc = Mathf.Max(1, Mathf.CeilToInt((endFrac - startFrac) * totalSegments));
            int b = vh.currentVertCount;
            for (int i = 0; i <= segsThisArc; i++)
            {
                float t = Mathf.Lerp(startFrac, endFrac, i / (float)segsThisArc);
                // Clockwise from top: angle = 90° - t·360°
                float angle = (90f - t * 360f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(MakeVert(c + dir * rOuter, color)); // 2i
                vh.AddVert(MakeVert(c + dir * rInner, color)); // 2i+1
            }
            for (int seg = 0; seg < segsThisArc; seg++)
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
