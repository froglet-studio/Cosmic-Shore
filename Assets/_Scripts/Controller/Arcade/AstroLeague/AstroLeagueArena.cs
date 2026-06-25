using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// HyperSea stadium for Astro League — only the GAMEPLAY-bearing structure, constructed at
    /// runtime on every peer (purely deterministic local visuals + static physics, nothing here
    /// needs networking):
    /// - A play-boundary court that IS the Cell's nucleus: the arena builds an <c>AstroLeagueBoundary</c>
    ///   at the per-intensity shape + scaled dimensions, the server's ball bounces elastically off its
    ///   walls (no collider; see <c>AstroLeagueBall.SetBoundary</c>) — flat polytope faces BANK the ball,
    ///   the legacy sphere focuses it — and the nucleus visual is morphed to match (a convex-hull mesh
    ///   via <c>Cell.SetNucleusMesh</c> for polytope shapes, or a radius via <c>Cell.SetNucleusWorldRadius</c>
    ///   for the Sphere baseline). This replaced six invisible BoxCollider walls (−6 colliders).
    /// - Portal-style goal rings at each end, with anticipation glow as the ball approaches
    /// - Center ring marking the soccer midfield / kickoff line
    ///
    /// Everything ATMOSPHERIC or TERRITORIAL is owned by the standard Cell ecosystem, NOT by this
    /// arena (CLAUDE.md ▸ "Universality — one HyperSea, one rule set"): the playfield boundary read
    /// is the Cell's <c>MembranePrefab</c>, the drifting motes are the Cell's <c>CytoplasmPrefab</c>,
    /// and the boundary/core is the Cell's <c>NucleusPrefab</c> (the arena morphs it to the court shape
    /// via <c>Cell.SetNucleusMesh</c>/<c>SetNucleusWorldRadius</c> — it does not own a duplicate). A
    /// previous bespoke wireframe edge cage and a bespoke plankton particle system were removed because
    /// they duplicated the membrane and cytoplasm — do not reintroduce arena-local versions of cell-owned visuals.
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

        [Header("Base Dimensions (intensity 1 — scaled up by Build)")]
        [Tooltip("Used only to place the end goals (Z = ±arenaLength/2) and size the midfield ring; " +
                 "the play boundary itself is the spherical nucleus (settings.boundaryRadius).")]
        [SerializeField] float arenaLength = 300f;
        [SerializeField] float arenaWidth = 200f;
        [SerializeField] float arenaHeight = 100f;
        [SerializeField] float goalRingRadius = 26f;

        [Header("References")]
        [SerializeField] AstroLeagueBall ball;

        public Vector3 Center => transform.position;
        public float GoalRingRadius => goalRingRadius * _scale;

        /// <summary>World-space radius of the SPHERE boundary at the current intensity scale (sphere shape only).</summary>
        public float BoundaryRadius => _boundaryRadius;

        /// <summary>The court boundary the ball bounces off at the current intensity (shape + scaled dims).</summary>
        public AstroLeagueBoundary Boundary => _boundary;

        // Scale-resolved dimensions (set in Build).
        float _scale = 1f;
        float _L, _W, _H, _goalR, _boundaryRadius;
        AstroLeagueBoundary _boundary;
        bool _built;

        readonly List<GameObject> _generated = new();
        Material jadeRingMaterial;
        Material rubyRingMaterial;

        Color JadeColor => settings != null ? settings.jadeGoalColor : new Color(0.15f, 1f, 0.55f, 0.5f);
        Color RubyColor => settings != null ? settings.rubyGoalColor : new Color(1f, 0.22f, 0.35f, 0.5f);

        /// <summary>
        /// Build (or rebuild) the stadium at the given intensity scale + court shape. Called by the
        /// controller on every peer once the intensity is known. Idempotent — clears prior geometry
        /// first. The boundary is exposed via <see cref="Boundary"/> so the controller can morph the
        /// cell nucleus to match (mesh for polytopes, radius for the sphere).
        /// </summary>
        public void Build(float scale, AstroLeagueBoundaryShape shape)
        {
            _scale = Mathf.Max(0.01f, scale);
            _L = arenaLength * _scale;
            _W = arenaWidth * _scale;
            _H = arenaHeight * _scale;
            _goalR = goalRingRadius * _scale;
            _boundaryRadius = (settings != null ? settings.boundaryRadius : 190f) * _scale;

            // Clear anything from a prior Build (defensive — normally built once per scene).
            for (int i = _generated.Count - 1; i >= 0; i--)
                if (_generated[i] != null) Destroy(_generated[i]);
            _generated.Clear();

            // The nucleus IS the wall: build the court boundary (flat polytope faces BANK the ball; a
            // sphere focuses it) and hand it to the ball — a reflect off its walls, no collider. The
            // half-extents are (width/2, height/2, length/2); the goal axis is Z (length), so the flat
            // goal caps sit on the goal lines (±length/2) and "backboard" missed shots. The nucleus
            // visual is morphed to match by the controller (Cell.SetNucleusMesh / SetNucleusWorldRadius).
            Vector3 halfExtents = new Vector3(_W / 2f, _H / 2f, _L / 2f);
            float octFrac = settings != null ? settings.octagonBevelFraction : 0.5f;
            float bevFrac = settings != null ? settings.beveledBoxBevelFraction : 0.45f;
            _boundary = new AstroLeagueBoundary(shape, Center, halfExtents, _boundaryRadius, octFrac, bevFrac);
            if (ball != null) ball.SetBoundary(_boundary);

            BuildGoalPortal("GoalPortal_Jade", Center + Vector3.back * (_L / 2f), Vector3.forward, JadeColor, out jadeRingMaterial);
            BuildGoalPortal("GoalPortal_Ruby", Center + Vector3.forward * (_L / 2f), Vector3.back, RubyColor, out rubyRingMaterial);
            BuildCenterRing();
            _built = true;
        }

        // ── Goal portals + midfield ──────────────────────────────────────────

        /// <summary>
        /// Three concentric rings receding into the goal mouth — reads as a glowing
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

            // Goal anticipation: portals flare as the ball closes in
            if (ball != null && !ball.IsHidden)
            {
                FlareRing(jadeRingMaterial, JadeColor, Center + Vector3.back * (_L / 2f));
                FlareRing(rubyRingMaterial, RubyColor, Center + Vector3.forward * (_L / 2f));
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
            Gizmos.DrawWireCube(Center, new Vector3(arenaWidth, arenaHeight, arenaLength) * s);
            Gizmos.color = new Color(0.15f, 1f, 0.55f, 0.4f);
            Gizmos.DrawWireSphere(Center + Vector3.back * (arenaLength * s / 2f), goalRingRadius * s);
            Gizmos.color = new Color(1f, 0.22f, 0.35f, 0.4f);
            Gizmos.DrawWireSphere(Center + Vector3.forward * (arenaLength * s / 2f), goalRingRadius * s);
        }
    }
}
