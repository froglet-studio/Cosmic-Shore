using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// HyperSea stadium for Astro League - only the GAMEPLAY-bearing structure, constructed at
    /// runtime on every peer (purely deterministic local visuals + static physics, nothing here
    /// needs networking):
    /// - A play-boundary court that IS the Cell's nucleus: the arena builds an <c>AstroLeagueBoundary</c>
    ///   at the per-intensity shape + scaled dimensions, the server's ball bounces elastically off its
    ///   walls (no collider; see <c>AstroLeagueBall.SetBoundary</c>) - flat polytope faces BANK the ball,
    ///   the legacy sphere focuses it - and the nucleus visual is morphed to match (a convex-hull mesh
    ///   via <c>Cell.SetNucleusMesh</c> for polytope shapes, or a radius via <c>Cell.SetNucleusWorldRadius</c>
    ///   for the Sphere baseline). This replaced six invisible BoxCollider walls (−6 colliders).
    /// - Portal-style goal rings at each end, with anticipation glow as the ball approaches
    /// - Center ring marking the soccer midfield / kickoff line
    ///
    /// Everything ATMOSPHERIC or TERRITORIAL is owned by the standard Cell ecosystem, NOT by this
    /// arena (CLAUDE.md ▸ "Universality - one HyperSea, one rule set"): the playfield boundary read
    /// is the Cell's <c>MembranePrefab</c>, the drifting motes are the Cell's <c>CytoplasmPrefab</c>,
    /// and the boundary/core is the Cell's <c>NucleusPrefab</c> (the arena morphs it to the court shape
    /// via <c>Cell.SetNucleusMesh</c>/<c>SetNucleusWorldRadius</c> - it does not own a duplicate). A
    /// previous bespoke wireframe edge cage and a bespoke plankton particle system were removed because
    /// they duplicated the membrane and cytoplasm - do not reintroduce arena-local versions of cell-owned visuals.
    ///
    /// The whole stadium scales with match intensity: the controller calls <see cref="Build"/>
    /// with the intensity scale factor, and every dimension (and the goal-ring positions the
    /// goal triggers align to) multiplies by it. The serialized dimensions are the BASE
    /// (intensity-1) size.
    /// </summary>
    public class AstroLeagueArena : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] AstroLeagueSettingsSO settings;

        [Header("Base Dimensions (LEGACY fallback only)")]
        [Tooltip("Base (intensity-1) court dimensions. These are a FALLBACK for a scene with no " +
                 "settings asset wired - the shipping source is AstroLeagueSettingsSO " +
                 "(arenaLength/Width/Height/goalMouthRadius), so resizing the playfield is a " +
                 "one-asset edit and can never disagree with the goal detectors, which read the " +
                 "same numbers. Do not tune these.")]
        [SerializeField] float arenaLength = 400f;
        [SerializeField] float arenaWidth = 320f;
        [SerializeField] float arenaHeight = 240f;
        [SerializeField] float goalRingRadius = 62f;

        // Config-first dimension reads (see the header above): the settings asset owns the court.
        float BaseLength => settings != null ? settings.arenaLength : arenaLength;
        float BaseWidth => settings != null ? settings.arenaWidth : arenaWidth;
        float BaseHeight => settings != null ? settings.arenaHeight : arenaHeight;
        float BaseGoalRadius => settings != null ? settings.goalMouthRadius : goalRingRadius;

        [Header("References")]
        [SerializeField] AstroLeagueBall ball;

        [Tooltip("Prism spawn channel (EventOnSpawnPrismAndReturn) - the standard PrismFactory pooled " +
                 "path the super-shielded edge lining is laid through on every peer.")]
        [SerializeField] PrismEventChannelWithReturnSO prismSpawnChannel;

        public Vector3 Center => transform.position;
        public float GoalRingRadius => BaseGoalRadius * _scale;

        /// <summary>Court length along the goal axis at the current intensity - the goal lines sit at ±half of it.</summary>
        public float ArenaLength => _L;

        /// <summary>World-space radius of the SPHERE boundary at the current intensity scale (sphere shape only).</summary>
        public float BoundaryRadius => _boundaryRadius;

        /// <summary>The court boundary the ball bounces off at the current intensity (shape + scaled dims).</summary>
        public AstroLeagueBoundary Boundary => _boundary;

        // Scale-resolved dimensions (set in Build).
        float _scale = 1f;
        float _L, _W, _H, _goalR, _boundaryRadius;
        AstroLeagueBoundary _boundary;
        bool _centralGoal;
        bool _built;

        readonly List<GameObject> _generated = new();
        Material jadeRingMaterial;
        Material rubyRingMaterial;

        // Super-shielded edge lining (laid per peer via the PrismFactory channel; see RebuildEdgeLining).
        readonly List<Prism> _edgePrisms = new();
        static readonly List<Vector3[]> s_edgePaths = new();

        // Goal-portal tints are config-only (AstroLeagueSettingsSO) - no inline palette duplicate.
        // Gray fallback only fires with no settings wired (a broken scene, not a shipping state).
        Color JadeColor => settings != null ? settings.jadeGoalColor : Color.gray;
        Color RubyColor => settings != null ? settings.rubyGoalColor : Color.gray;

        /// <summary>
        /// Build (or rebuild) the stadium at the given intensity scale + court shape. Called by the
        /// controller on every peer once the intensity is known. Idempotent - clears prior geometry
        /// first. The boundary is exposed via <see cref="Boundary"/> so the controller can morph the
        /// cell nucleus to match (mesh for polytopes, radius for the sphere).
        /// </summary>
        public void Build(float scale, AstroLeagueBoundaryShape shape, bool centralGoal = false)
        {
            _scale = Mathf.Max(0.01f, scale);
            _centralGoal = centralGoal;
            _L = BaseLength * _scale;
            _W = BaseWidth * _scale;
            _H = BaseHeight * _scale;
            _goalR = BaseGoalRadius * _scale;
            _boundaryRadius = (settings != null ? settings.boundaryRadius : 190f) * _scale;

            // Clear anything from a prior Build (defensive - normally built once per scene).
            for (int i = _generated.Count - 1; i >= 0; i--)
                if (_generated[i] != null) Destroy(_generated[i]);
            _generated.Clear();

            // The nucleus IS the wall: build the court boundary (flat polytope faces BANK the ball; a
            // sphere focuses it) and hand it to the ball - a reflect off its walls, no collider. The
            // half-extents are (width/2, height/2, length/2); the goal axis is Z (length), so the flat
            // goal caps sit on the goal lines (±length/2) and "backboard" missed shots. The nucleus
            // visual is morphed to match by the controller (Cell.SetNucleusMesh / SetNucleusWorldRadius).
            Vector3 halfExtents = new Vector3(_W / 2f, _H / 2f, _L / 2f);
            float octFrac = settings != null ? settings.octagonBevelFraction : 0.5f;
            float bevFrac = settings != null ? settings.beveledBoxBevelFraction : 0.45f;
            var ringOuter = settings != null ? settings.notchedRingOuterShape : AstroLeagueBoundaryShape.Cylinder;
            float ringMajor = settings != null ? settings.ringMajorRadiusFraction : 0.5f;
            float ringTube = settings != null ? settings.ringTubeRadiusFraction : 0.18f;
            float notchCenter = (settings != null ? settings.notchCenterDegrees : 0f) * Mathf.Deg2Rad;
            float notchHalf = (settings != null ? settings.notchHalfWidthDegrees : 30f) * Mathf.Deg2Rad;
            _boundary = new AstroLeagueBoundary(shape, Center, halfExtents, _boundaryRadius, octFrac, bevFrac,
                ringOuter, ringMajor, ringTube, notchCenter, notchHalf);
            if (ball != null) ball.SetBoundary(_boundary);

            RebuildEdgeLining();

            if (_centralGoal)
            {
                // ONE shared goal at center: two back-to-back portals. Push the ball +Z (toward the Ruby
                // cone) to score for Ruby, -Z (toward the Jade cone) to score for Jade - the rings recede
                // outward in each scoring direction so each side reads as a portal you shoot through.
                BuildGoalPortal("CentralGoal_Ruby", Center, Vector3.back, RubyColor, out rubyRingMaterial);
                BuildGoalPortal("CentralGoal_Jade", Center, Vector3.forward, JadeColor, out jadeRingMaterial);
            }
            else
            {
                BuildGoalPortal("GoalPortal_Jade", Center + Vector3.back * (_L / 2f), Vector3.forward, JadeColor, out jadeRingMaterial);
                BuildGoalPortal("GoalPortal_Ruby", Center + Vector3.forward * (_L / 2f), Vector3.back, RubyColor, out rubyRingMaterial);
                BuildCenterRing();
            }
            _built = true;
        }

        // ── Super-shielded edge lining ───────────────────────────────────────

        /// <summary>
        /// Dress the court's edges with a lining of SUPER-SHIELDED neutral prisms - invulnerable
        /// structure marking the arena rim at every intensity. A FIXED total count is distributed
        /// evenly over the summed edge length (spacing scales with the arena), so the lining's
        /// volume budget (count x prism volume) is deterministic and the Astro League Cell Config's
        /// phase-volume thresholds can be raised by exactly that budget. Laid per peer through the
        /// standard PrismFactory channel (prisms are per-peer local, like trail): spawn blooms in,
        /// removal is the animated Damage path - continuity law, never a raw Destroy. The lining
        /// binds VOLUME-ONLY to the cell (super-shielded structure never sways control or prey
        /// signals - see PrismSpatialIndex.ComputeEnvironmentMass), the ball passes through it
        /// untouched, and fauna never target shielded mass. Idempotent per Build.
        /// </summary>
        void RebuildEdgeLining()
        {
            ClearEdgeLining();
            if (!Application.isPlaying || settings == null || !settings.edgePrismsEnabled || _boundary == null)
                return;
            if (!prismSpawnChannel)
            {
                CSDebug.LogWarning("[AstroLeagueArena] prismSpawnChannel not wired - edge lining skipped.");
                return;
            }

            _boundary.CollectEdgePaths(s_edgePaths);

            float totalLength = 0f;
            foreach (var path in s_edgePaths)
                for (int i = 1; i < path.Length; i++)
                    totalLength += Vector3.Distance(path[i - 1], path[i]);

            int count = Mathf.Max(0, settings.edgePrismCount);
            if (count == 0 || totalLength < 1f) return;

            float spacing = totalLength / count;
            float inset = settings.edgePrismInset * _scale;
            int laid = 0;

            // One continuous arc-length walk across ALL edge paths (the carry-over between paths is
            // what makes the laid total land on edgePrismCount exactly, keeping the volume budget
            // deterministic), phase-offset by spacing/2 so placements sit off the corners.
            float distanceToNext = spacing * 0.5f;
            foreach (var path in s_edgePaths)
            {
                for (int i = 1; i < path.Length; i++)
                {
                    Vector3 a = path[i - 1];
                    Vector3 b = path[i];
                    float segmentLength = Vector3.Distance(a, b);
                    if (segmentLength < 1e-4f) continue;
                    Vector3 tangent = (b - a) / segmentLength;

                    float travelled = 0f;
                    while (travelled + distanceToNext <= segmentLength)
                    {
                        travelled += distanceToNext;
                        distanceToNext = spacing;

                        Vector3 p = a + tangent * travelled; // center-relative
                        // Inward = toward the arena center, projected off the edge tangent so the
                        // prism nests against the wall instead of sliding along it.
                        Vector3 inward = -p - Vector3.Dot(-p, tangent) * tangent;
                        inward = inward.sqrMagnitude > 1e-6f ? inward.normalized : Vector3.zero;

                        Vector3 worldPos = Center + p + inward * inset;
                        Quaternion rotation = Quaternion.LookRotation(
                            tangent, inward == Vector3.zero ? Vector3.up : inward);

                        BoostRingBuilder.LayOne(prismSpawnChannel, worldPos, rotation,
                            settings.edgePrismScale, PrismKind.SuperShielded, Domains.Blue,
                            playerName: null, $"AstroLeagueEdge::{laid++}", collected: _edgePrisms);
                    }
                    distanceToNext -= segmentLength - travelled;
                }
            }

            if (_edgePrisms.Count == 0)
                CSDebug.LogWarning("[AstroLeagueArena] Edge lining laid no prisms - is the PrismFactory " +
                                   "listening on the spawn channel yet?");
        }

        /// <summary>
        /// Actively remove a previously-laid lining (arena rebuild on a late-arriving match config).
        /// Shields drop first so the canonical animated Damage teardown can run - mass is conserved
        /// through the standard explode-out, never a raw Destroy.
        /// </summary>
        void ClearEdgeLining()
        {
            for (int i = 0; i < _edgePrisms.Count; i++)
            {
                var prism = _edgePrisms[i];
                if (prism == null || prism.destroyed) continue;
                prism.DeactivateShields();
                prism.Damage(Vector3.zero, Domains.Blue, string.Empty, devastate: true);
            }
            _edgePrisms.Clear();
        }

        // ── Goal portals + midfield ──────────────────────────────────────────

        /// <summary>
        /// Three concentric rings receding into the goal mouth - reads as a glowing
        /// portal from anywhere in the arena (LineRenderers are inherently double-sided).
        /// </summary>
        void BuildGoalPortal(string name, Vector3 mouthCenter, Vector3 inward, Color color, out Material ringMaterial)
        {
            ringMaterial = CreateLineMaterial(color);
            for (int i = 0; i < 3; i++)
            {
                float depth = i * 6f * _scale;
                float radius = _goalR * (1f - i * 0.18f);
                CreateCircle($"{name}_Ring{i}",
                    mouthCenter - inward * depth, inward, radius, ringMaterial,
                    width: (1.6f - i * 0.4f) * _scale);
            }
        }

        void BuildCenterRing()
        {
            var mat = CreateLineMaterial(new Color(1f, 1f, 1f, 0.18f));
            CreateCircle("CenterRing", Center, Vector3.forward, _W * 0.35f, mat, 0.6f * _scale);
        }

        LineRenderer CreateCircle(string name, Vector3 center, Vector3 normal, float radius, Material mat, float width)
        {
            const int segments = 48;
            var points = new Vector3[segments + 1];
            Quaternion orient = Quaternion.LookRotation(normal);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                points[i] = center + orient * new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }
            return CreateLine(name, points, mat, width);
        }

        LineRenderer CreateLine(string name, Vector3[] points, Material mat, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            _generated.Add(go);
            return lr;
        }

        static Material CreateLineMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader) { color = color };
            mat.SetFloat("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetFloat("_Cull", 0); // Double-sided
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            return mat;
        }

        // ── Living arena ─────────────────────────────────────────────────────

        void Update()
        {
            if (!_built) return;

            // Goal anticipation: portals flare as the ball closes in. The central shared goal flares both
            // cones off the ball's distance to center; the end goals flare off their own mouths.
            if (ball != null && !ball.IsHidden)
            {
                if (_centralGoal)
                {
                    FlareRing(jadeRingMaterial, JadeColor, Center);
                    FlareRing(rubyRingMaterial, RubyColor, Center);
                }
                else
                {
                    FlareRing(jadeRingMaterial, JadeColor, Center + Vector3.back * (_L / 2f));
                    FlareRing(rubyRingMaterial, RubyColor, Center + Vector3.forward * (_L / 2f));
                }
            }
        }

        void FlareRing(Material ringMat, Color baseColor, Vector3 mouthCenter)
        {
            if (ringMat == null) return;

            float distance = Vector3.Distance(ball.transform.position, mouthCenter);
            float anticipation = Mathf.Clamp01(1f - distance / (_L * 0.45f));
            float flicker = 1f + anticipation * 0.6f * Mathf.Sin(Time.time * (6f + anticipation * 10f));
            float alpha = Mathf.Lerp(baseColor.a * 0.8f, 1f, anticipation) * flicker;
            ringMat.color = new Color(
                Mathf.Min(1f, baseColor.r * (1f + anticipation)),
                Mathf.Min(1f, baseColor.g * (1f + anticipation)),
                Mathf.Min(1f, baseColor.b * (1f + anticipation)),
                Mathf.Clamp01(alpha));
        }

        void OnDrawGizmos()
        {
            float s = Application.isPlaying ? _scale : 1f;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireCube(Center, new Vector3(BaseWidth, BaseHeight, BaseLength) * s);
            Gizmos.color = new Color(0.15f, 1f, 0.55f, 0.4f);
            Gizmos.DrawWireSphere(Center + Vector3.back * (BaseLength * s / 2f), BaseGoalRadius * s);
            Gizmos.color = new Color(1f, 0.22f, 0.35f, 0.4f);
            Gizmos.DrawWireSphere(Center + Vector3.forward * (BaseLength * s / 2f), BaseGoalRadius * s);
        }
    }
}
