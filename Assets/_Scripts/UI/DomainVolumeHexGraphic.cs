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
        [Range(0.3f, 1f)] [SerializeField] float bandOuterFrac = 0.78f;
        [Tooltip("Radius of the centre 'dominant' hexagon. Bands reach this at fill = 1.")]
        [Range(0.05f, 0.4f)] [SerializeField] float centerHexFrac = 0.18f;
        [Tooltip("Minimum band thickness (fraction) so an empty domain still shows a sliver at full width.")]
        [Range(0f, 0.2f)] [SerializeField] float minBandThicknessFrac = 0.06f;
        [Tooltip("Faint boundary hexagon marking the frenzy extent. 0 thickness = off.")]
        [Range(0f, 0.12f)] [SerializeField] float boundaryThicknessFrac = 0.04f;

        [Header("Spawn-cycle ring (outside the hex)")]
        [Tooltip("Inner radius of the spawn-cycle ring (fraction). Sits OUTSIDE the band/boundary so it doesn't obscure the volume gauge.")]
        [Range(0.6f, 1f)] [SerializeField] float ringInnerFrac = 0.86f;
        [Tooltip("Outer radius of the spawn-cycle ring (fraction).")]
        [Range(0.6f, 1f)] [SerializeField] float ringOuterFrac = 0.98f;
        [Tooltip("Faint background track behind the ring's progress arc. 0 alpha = off.")]
        [SerializeField] Color ringTrackColor = new(1f, 1f, 1f, 0.10f);
        [Tooltip("Number of arc segments — higher = smoother ring at the cost of more triangles.")]
        [Range(12, 128)] [SerializeField] int ringSegments = 64;

        [Header("Concentric phase rings (Skim Race mode)")]
        [Tooltip("Thickness of each phase-boundary ring, as a fraction of the half-min-extent.")]
        [Range(0.005f, 0.1f)] [SerializeField] float phaseRingThicknessFrac = 0.03f;
        [Tooltip("Alpha of the translucent live-mass fill hexagon that grows from the centre toward the current mass radius.")]
        [Range(0f, 1f)] [SerializeField] float massFillAlpha = 0.22f;
        [Tooltip("Alpha of a phase ring once its threshold has been crossed (drawn in the dominant domain's color).")]
        [Range(0f, 1f)] [SerializeField] float crossedRingAlpha = 0.95f;

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
        float _spawnCycle;                               // 0..1 progress toward next fauna spawn

        // Concentric-phase-rings mode (Skim Race). When enabled the per-domain radial
        // bands are replaced by a stack of concentric hexagon rings — one per phase
        // boundary — that light up in the dominant domain's color as the cell's summed
        // mass crosses each threshold.
        bool _concentricMode;
        float _massFrac;                                 // 0..1 summed mass / RabidEnter
        float[] _phaseFracs = System.Array.Empty<float>(); // per-phase enter threshold / RabidEnter
        Color _dominantColor = Color.white;              // dominant domain's identity hue

        /// <summary>
        /// Push the live gauge state. Rebuilds the mesh only when something changed
        /// past a small epsilon, so per-frame lerps from the controller don't force a
        /// Canvas batch rebuild every frame once the values settle.
        /// </summary>
        public void SetState(float fillJade, float fillRuby, float fillGold,
                             Color cJade, Color cRuby, Color cGold, int dominant,
                             float spawnCycle)
        {
            bool changed =
                !Approximately(_fill[0], fillJade) ||
                !Approximately(_fill[1], fillRuby) ||
                !Approximately(_fill[2], fillGold) ||
                _dominant != dominant ||
                !Approximately(_spawnCycle, spawnCycle) ||
                _domainColor[0] != cJade || _domainColor[1] != cRuby || _domainColor[2] != cGold;

            if (!changed) return;

            _fill[0] = fillJade; _fill[1] = fillRuby; _fill[2] = fillGold;
            _domainColor[0] = cJade; _domainColor[1] = cRuby; _domainColor[2] = cGold;
            _dominant = dominant;
            _spawnCycle = spawnCycle;
            SetVerticesDirty();
        }

        /// <summary>
        /// Push concentric-phase-ring state (Skim Race mode). Draws one hexagon ring per
        /// phase boundary (evenly spaced for legibility) plus a translucent live-mass fill
        /// mapped through the thresholds so its edge reaches ring i when mass crosses phase
        /// i. Rings the summed mass has crossed glow in the dominant domain's color;
        /// uncrossed rings stay faint — so you can read, at a glance, which phase the cell
        /// is in and which domain pushed it there. <paramref name="phaseEnterFracs"/> are
        /// the per-phase enter thresholds as a fraction of RabidEnter, ascending. Rebuilds
        /// only on a meaningful delta.
        /// </summary>
        public void SetPhaseState(float massFrac, float[] phaseEnterFracs, Color dominantColor,
                                  int dominant, float spawnCycle)
        {
            bool changed =
                !_concentricMode ||
                !Approximately(_massFrac, massFrac) ||
                _dominant != dominant ||
                !Approximately(_spawnCycle, spawnCycle) ||
                _dominantColor != dominantColor ||
                !FracsApproximately(_phaseFracs, phaseEnterFracs);

            _concentricMode = true;
            if (!changed) return;

            _massFrac = massFrac;
            _dominant = dominant;
            _dominantColor = dominantColor;
            _spawnCycle = spawnCycle;

            // Copy in so external mutation of the caller's array can't desync the mesh.
            if (phaseEnterFracs == null) _phaseFracs = System.Array.Empty<float>();
            else
            {
                if (_phaseFracs.Length != phaseEnterFracs.Length)
                    _phaseFracs = new float[phaseEnterFracs.Length];
                System.Array.Copy(phaseEnterFracs, _phaseFracs, phaseEnterFracs.Length);
            }

            SetVerticesDirty();
        }

        static bool FracsApproximately(float[] a, float[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
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

            if (_concentricMode)
            {
                DrawPhaseRings(vh, c, R);
                return;
            }

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

            // 4) Spawn-cycle ring around the outside. Empty background track + a
            //    progress arc starting at the top and sweeping clockwise. The arc
            //    is tinted by the DOMINANT domain because every periodic fauna
            //    spawn produces the leader's color — when the arc completes a
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

        /// <summary>
        /// Concentric-hexagon phase gauge (Skim Race). Draws, from the outside in:
        /// the boundary hex (frenzy extent), one hex ring per phase boundary, a
        /// translucent live-mass fill hexagon, the centre dominant hex, and the
        /// spawn-cycle ring.
        ///
        /// Rings are spaced EVENLY by phase (not by raw threshold value) so all five
        /// read clearly even when the thresholds are bunched at the low end (Skim Race:
        /// Quiet 100 … Rabid 2000). The live-mass fill is mapped PIECEWISE through the
        /// uneven thresholds onto the even ring radii, so the fill edge reaches ring i
        /// exactly when summed mass crosses phase i's threshold — the disc sweeping past
        /// a ring IS that phase tripping. Crossed rings glow in the dominant domain's
        /// color; the rest stay faint.
        /// </summary>
        void DrawPhaseRings(VertexHelper vh, Vector2 c, float R)
        {
            float ringThick = R * phaseRingThicknessFrac;
            float centerR = R * centerHexFrac;
            float massFrac = Mathf.Clamp01(_massFrac);
            int n = _phaseFracs.Length;

            Color domColor = (_dominant >= 0 && _dominant < 3) ? _dominantColor : neutralCenterColor;

            // 1) Boundary hex (the frenzy / Rabid extent marker).
            if (boundaryThicknessFrac > 0f)
                AddHexRing(vh, c, R, R - R * boundaryThicknessFrac, boundaryColor);

            // 2) Live-mass fill — translucent disc whose edge tracks the current summed
            //    mass, mapped piecewise so it aligns with the evenly-spaced rings.
            float fillR = MassFillRadius(massFrac, n, centerR, R);
            if (massFillAlpha > 0f && fillR > centerR + 0.5f)
            {
                Color fillC = domColor;
                fillC.a = massFillAlpha;
                AddHexFill(vh, c, fillR, fillC);
            }

            // 3) Evenly-spaced phase rings. Ring i lights up once mass passes its
            //    threshold; the highest crossed ring tells you the current phase and the
            //    dominant color tells you who drove it there.
            for (int i = 0; i < n; i++)
            {
                float ringR = EvenRingRadius(i, n, centerR, R);
                if (ringR <= ringThick) continue;

                bool crossed = massFrac >= _phaseFracs[i] - 0.0005f;
                Color ringColor = crossed ? domColor : boundaryColor;
                if (crossed) ringColor.a = crossedRingAlpha;
                AddHexRing(vh, c, ringR, ringR - ringThick, ringColor);
            }

            // 4) Centre hex — dominant domain tint (who holds the most mass).
            AddHexFill(vh, c, centerR, domColor);

            // 5) Spawn-cycle ring around the outside (same as the radial mode).
            float ringInner = R * Mathf.Min(ringInnerFrac, ringOuterFrac);
            float ringOuter = R * Mathf.Max(ringInnerFrac, ringOuterFrac);
            int segs = Mathf.Max(12, ringSegments);
            if (ringTrackColor.a > 0f)
                AddRingArc(vh, c, ringOuter, ringInner, 0f, 1f, segs, ringTrackColor);
            float cycle = Mathf.Clamp01(_spawnCycle);
            if (cycle > 0.001f)
                AddRingArc(vh, c, ringOuter, ringInner, 0f, cycle, segs, domColor);
        }

        /// <summary>Radius of phase ring <paramref name="i"/> — evenly spaced from just
        /// outside the centre hex (i = -1 → centre) to the rim (i = n-1 → R).</summary>
        static float EvenRingRadius(int i, int n, float centerR, float R) =>
            n > 0 ? Mathf.Lerp(centerR, R, (i + 1) / (float)n) : R;

        /// <summary>
        /// Maps the linear summed-mass fraction onto the evenly-spaced ring radii via the
        /// (uneven) phase thresholds, so the fill edge crosses ring i precisely when mass
        /// reaches phaseFracs[i]. Below the first threshold it lerps centre → ring 0;
        /// at/above the last threshold it pins to the rim.
        /// </summary>
        float MassFillRadius(float massFrac, int n, float centerR, float R)
        {
            if (n <= 0) return Mathf.Lerp(centerR, R, massFrac);

            if (massFrac < _phaseFracs[0])
            {
                float denom = Mathf.Max(1e-4f, _phaseFracs[0]);
                return Mathf.Lerp(centerR, EvenRingRadius(0, n, centerR, R), massFrac / denom);
            }

            if (massFrac >= _phaseFracs[n - 1])
                return R;

            int band = n - 1;
            for (int i = 0; i < n - 1; i++)
            {
                if (massFrac < _phaseFracs[i + 1]) { band = i; break; }
            }
            float t = Mathf.InverseLerp(_phaseFracs[band], _phaseFracs[band + 1], massFrac);
            return Mathf.Lerp(
                EvenRingRadius(band, n, centerR, R),
                EvenRingRadius(band + 1, n, centerR, R),
                t);
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
