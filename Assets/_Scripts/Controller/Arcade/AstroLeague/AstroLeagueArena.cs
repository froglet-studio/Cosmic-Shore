using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// HyperSea stadium for Astro League, constructed at runtime:
    /// - Invisible high-restitution boundary walls (pure physics)
    /// - Pulsing wireframe edge cage so pilots always read the playfield bounds
    /// - Portal-style goal rings at each end (visible from inside and outside),
    ///   with anticipation glow as the ball approaches
    /// - Center ring marking midfield
    /// - Drifting plankton motes for hypersea atmosphere and speed perception
    /// </summary>
    public class AstroLeagueArena : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] AstroLeagueSettingsSO settings;

        [Header("Dimensions")]
        [SerializeField] float arenaLength = 300f;
        [SerializeField] float arenaWidth = 200f;
        [SerializeField] float arenaHeight = 100f;
        [SerializeField] float wallThickness = 4f;
        [SerializeField] float goalRingRadius = 26f;

        [Header("References")]
        [SerializeField] AstroLeagueBall ball;

        public Vector3 Center => transform.position;
        public float GoalRingRadius => goalRingRadius;

        LineRenderer[] edgeLines;
        LineRenderer[] jadeRing;
        LineRenderer[] rubyRing;
        Material edgeMaterial;
        Material jadeRingMaterial;
        Material rubyRingMaterial;

        Color EdgeColor => settings != null ? settings.edgeColor : new Color(0.25f, 0.85f, 1f, 0.55f);
        Color JadeColor => settings != null ? settings.jadeGoalColor : new Color(0.15f, 1f, 0.55f, 0.5f);
        Color RubyColor => settings != null ? settings.rubyGoalColor : new Color(1f, 0.22f, 0.35f, 0.5f);

        void Awake()
        {
            BuildWalls();
            BuildEdgeCage();
            jadeRing = BuildGoalPortal("GoalPortal_Jade", Center + Vector3.back * (arenaLength / 2f), Vector3.forward, JadeColor, out jadeRingMaterial);
            rubyRing = BuildGoalPortal("GoalPortal_Ruby", Center + Vector3.forward * (arenaLength / 2f), Vector3.back, RubyColor, out rubyRingMaterial);
            BuildCenterRing();
            BuildPlankton();
        }

        // ── Physics shell ────────────────────────────────────────────────────

        void BuildWalls()
        {
            float hw = arenaWidth / 2f, hh = arenaHeight / 2f, hl = arenaLength / 2f;

            CreateWall("Wall_Top",    Center + Vector3.up * (hh + wallThickness / 2f),      new Vector3(arenaWidth, wallThickness, arenaLength));
            CreateWall("Wall_Bottom", Center + Vector3.down * (hh + wallThickness / 2f),    new Vector3(arenaWidth, wallThickness, arenaLength));
            CreateWall("Wall_Left",   Center + Vector3.left * (hw + wallThickness / 2f),    new Vector3(wallThickness, arenaHeight, arenaLength));
            CreateWall("Wall_Right",  Center + Vector3.right * (hw + wallThickness / 2f),   new Vector3(wallThickness, arenaHeight, arenaLength));
            CreateWall("Wall_Back",   Center + Vector3.back * (hl + wallThickness / 2f),    new Vector3(arenaWidth, arenaHeight, wallThickness));
            CreateWall("Wall_Front",  Center + Vector3.forward * (hl + wallThickness / 2f), new Vector3(arenaWidth, arenaHeight, wallThickness));
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
        }

        // ── Visual cage ──────────────────────────────────────────────────────

        void BuildEdgeCage()
        {
            float hw = arenaWidth / 2f, hh = arenaHeight / 2f, hl = arenaLength / 2f;

            Vector3[][] edges =
            {
                // Bottom rectangle
                new[] { V(-hw, -hh, -hl), V(hw, -hh, -hl) }, new[] { V(hw, -hh, -hl), V(hw, -hh, hl) },
                new[] { V(hw, -hh, hl), V(-hw, -hh, hl) },   new[] { V(-hw, -hh, hl), V(-hw, -hh, -hl) },
                // Top rectangle
                new[] { V(-hw, hh, -hl), V(hw, hh, -hl) },   new[] { V(hw, hh, -hl), V(hw, hh, hl) },
                new[] { V(hw, hh, hl), V(-hw, hh, hl) },     new[] { V(-hw, hh, hl), V(-hw, hh, -hl) },
                // Verticals
                new[] { V(-hw, -hh, -hl), V(-hw, hh, -hl) }, new[] { V(hw, -hh, -hl), V(hw, hh, -hl) },
                new[] { V(hw, -hh, hl), V(hw, hh, hl) },     new[] { V(-hw, -hh, hl), V(-hw, hh, hl) },
            };

            edgeMaterial = CreateLineMaterial(EdgeColor);
            edgeLines = new LineRenderer[edges.Length];
            for (int i = 0; i < edges.Length; i++)
                edgeLines[i] = CreateLine($"Edge_{i}", edges[i], edgeMaterial, 0.8f);

            Vector3 V(float x, float y, float z) => Center + new Vector3(x, y, z);
        }

        /// <summary>
        /// Three concentric rings receding into the goal mouth — reads as a glowing
        /// portal from anywhere in the arena (LineRenderers are inherently double-sided).
        /// </summary>
        LineRenderer[] BuildGoalPortal(string name, Vector3 mouthCenter, Vector3 inward, Color color, out Material ringMaterial)
        {
            ringMaterial = CreateLineMaterial(color);
            var rings = new LineRenderer[3];
            for (int i = 0; i < rings.Length; i++)
            {
                float depth = i * 6f;
                float radius = goalRingRadius * (1f - i * 0.18f);
                rings[i] = CreateCircle($"{name}_Ring{i}",
                    mouthCenter - inward * depth, inward, radius, ringMaterial,
                    width: 1.6f - i * 0.4f);
            }
            return rings;
        }

        void BuildCenterRing()
        {
            var mat = CreateLineMaterial(new Color(1f, 1f, 1f, 0.18f));
            CreateCircle("CenterRing", Center, Vector3.forward, arenaWidth * 0.35f, mat, 0.6f);
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

        // ── HyperSea atmosphere ──────────────────────────────────────────────

        void BuildPlankton()
        {
            var go = new GameObject("HyperSeaPlankton");
            go.transform.SetParent(transform, false);
            go.transform.position = Center;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 30f;
            main.startSpeed = 0.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.6f);
            int count = settings != null ? settings.planktonCount : 400;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            main.loop = true;
            Color motes = settings != null ? settings.planktonColor : new Color(0.55f, 0.8f, 1f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(motes, motes * 0.5f);
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = count / 30f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(arenaWidth, arenaHeight, arenaLength);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.08f;
            noise.scrollSpeed = 0.05f;

            var psRenderer = go.GetComponent<ParticleSystemRenderer>();
            var psMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            psMat.SetFloat("_Surface", 1);
            psMat.SetInt("_Blend", 1); // Additive — motes glow softly against the sea
            psMat.SetColor(Shader.PropertyToID("_BaseColor"), Color.white);
            psMat.renderQueue = 3050;
            psRenderer.sharedMaterial = psMat;
            psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ── Living arena ─────────────────────────────────────────────────────

        void Update()
        {
            // Breathing edge cage
            if (edgeMaterial != null)
            {
                float pulse = 0.45f + 0.2f * Mathf.Sin(Time.time * 1.4f);
                var c = EdgeColor;
                edgeMaterial.color = new Color(c.r, c.g, c.b, c.a * pulse / 0.55f);
            }

            // Goal anticipation: portals flare as the ball closes in
            if (ball != null)
            {
                FlareRing(jadeRingMaterial, JadeColor, Center + Vector3.back * (arenaLength / 2f));
                FlareRing(rubyRingMaterial, RubyColor, Center + Vector3.forward * (arenaLength / 2f));
            }
        }

        void FlareRing(Material ringMat, Color baseColor, Vector3 mouthCenter)
        {
            if (ringMat == null) return;

            float distance = Vector3.Distance(ball.transform.position, mouthCenter);
            float anticipation = Mathf.Clamp01(1f - distance / (arenaLength * 0.45f));
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
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireCube(Center, new Vector3(arenaWidth, arenaHeight, arenaLength));
            Gizmos.color = new Color(0.15f, 1f, 0.55f, 0.4f);
            Gizmos.DrawWireSphere(Center + Vector3.back * (arenaLength / 2f), goalRingRadius);
            Gizmos.color = new Color(1f, 0.22f, 0.35f, 0.4f);
            Gizmos.DrawWireSphere(Center + Vector3.forward * (arenaLength / 2f), goalRingRadius);
        }
    }
}
