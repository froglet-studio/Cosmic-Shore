using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// HyperSea stadium for Astro League — only the GAMEPLAY-bearing structure, constructed at
    /// runtime on every peer (purely deterministic local visuals + static physics, nothing here
    /// needs networking):
    /// - Invisible high-restitution boundary walls (pure physics; the server's ball
    ///   simulation bounces off them, clients only render the replicated ball)
    /// - Portal-style goal rings at each end, with anticipation glow as the ball approaches
    /// - Center ring marking the soccer midfield / kickoff line
    ///
    /// Everything ATMOSPHERIC or TERRITORIAL is owned by the standard Cell ecosystem, NOT by this
    /// arena (CLAUDE.md ▸ "Universality — one HyperSea, one rule set"): the playfield boundary read
    /// is the Cell's <c>MembranePrefab</c>, the drifting motes are the Cell's <c>CytoplasmPrefab</c>,
    /// and the core marker is the Cell's <c>NucleusPrefab</c>. A previous bespoke wireframe edge cage
    /// and a bespoke plankton particle system were removed because they duplicated the membrane and
    /// cytoplasm — do not reintroduce arena-local versions of cell-owned visuals.
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
        [SerializeField] float arenaLength = 300f;
        [SerializeField] float arenaWidth = 200f;
        [SerializeField] float arenaHeight = 100f;
        [SerializeField] float wallThickness = 4f;
        [SerializeField] float goalRingRadius = 26f;

        [Header("References")]
        [SerializeField] AstroLeagueBall ball;

        public Vector3 Center => transform.position;
        public float GoalRingRadius => goalRingRadius * _scale;

        // Scale-resolved dimensions (set in Build).
        float _scale = 1f;
        float _L, _W, _H, _wall, _goalR;
        bool _built;

        readonly List<GameObject> _generated = new();
        Material jadeRingMaterial;
        Material rubyRingMaterial;

        Color JadeColor => settings != null ? settings.jadeGoalColor : new Color(0.15f, 1f, 0.55f, 0.5f);
        Color RubyColor => settings != null ? settings.rubyGoalColor : new Color(1f, 0.22f, 0.35f, 0.5f);

        /// <summary>
        /// Build (or rebuild) the stadium at the given intensity scale. Called by the controller on
        /// every peer once the intensity is known. Idempotent — clears prior geometry first.
        /// </summary>
        public void Build(float scale)
        {
            _scale = Mathf.Max(0.01f, scale);
            _L = arenaLength * _scale;
            _W = arenaWidth * _scale;
            _H = arenaHeight * _scale;
            _wall = wallThickness * _scale;
            _goalR = goalRingRadius * _scale;

            // Clear anything from a prior Build (defensive — normally built once per scene).
            for (int i = _generated.Count - 1; i >= 0; i--)
                if (_generated[i] != null) Destroy(_generated[i]);
            _generated.Clear();

            BuildWalls();
            BuildGoalPortal("GoalPortal_Jade", Center + Vector3.back * (_L / 2f), Vector3.forward, JadeColor, out jadeRingMaterial);
            BuildGoalPortal("GoalPortal_Ruby", Center + Vector3.forward * (_L / 2f), Vector3.back, RubyColor, out rubyRingMaterial);
            BuildCenterRing();
            _built = true;
        }

        // ── Physics shell ────────────────────────────────────────────────────

        void BuildWalls()
        {
            float hw = _W / 2f, hh = _H / 2f, hl = _L / 2f;

            CreateWall("Wall_Top",    Center + Vector3.up * (hh + _wall / 2f),      new Vector3(_W, _wall, _L));
            CreateWall("Wall_Bottom", Center + Vector3.down * (hh + _wall / 2f),    new Vector3(_W, _wall, _L));
            CreateWall("Wall_Left",   Center + Vector3.left * (hw + _wall / 2f),    new Vector3(_wall, _H, _L));
            CreateWall("Wall_Right",  Center + Vector3.right * (hw + _wall / 2f),   new Vector3(_wall, _H, _L));
            CreateWall("Wall_Back",   Center + Vector3.back * (hl + _wall / 2f),    new Vector3(_W, _H, _wall));
            CreateWall("Wall_Front",  Center + Vector3.forward * (hl + _wall / 2f), new Vector3(_W, _H, _wall));
        }

        void CreateWall(string wallName, Vector3 position, Vector3 size)
        {
            var wall = new GameObject(wallName);
            wall.transform.SetParent(transform);
            wall.transform.position = position;
            wall.layer = gameObject.layer;

            var col = wall.AddComponent<BoxCollider>();
            col.size = size;
            col.material = new PhysicsMaterial("ArenaWall")
            {
                bounciness = 1f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                dynamicFriction = 0f,
                staticFriction = 0f
            };
            _generated.Add(wall);
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
