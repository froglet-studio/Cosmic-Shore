using UnityEngine;

namespace CosmicShore.Game.Arcade.AstroLeague
{
    /// <summary>
    /// Defines the Astro League arena boundaries and spawns walls at runtime.
    /// Creates visible, semi-transparent boundary walls with edge wireframes
    /// so players can see the arena extents in space.
    /// </summary>
    public class AstroLeagueArena : MonoBehaviour
    {
        [Header("Arena Dimensions")]
        [SerializeField] float arenaLength = 300f;
        [SerializeField] float arenaWidth = 200f;
        [SerializeField] float arenaHeight = 100f;
        [SerializeField] float wallThickness = 2f;

        [Header("Visuals")]
        [SerializeField] Material wallMaterial;
        [SerializeField] Color wallColor = new(0.15f, 0.4f, 0.8f, 0.08f);
        [SerializeField] Color edgeColor = new(0.3f, 0.6f, 1f, 0.5f);
        [SerializeField] Color jadeGoalColor = new(0.1f, 1f, 0.5f, 0.25f);
        [SerializeField] Color rubyGoalColor = new(1f, 0.2f, 0.3f, 0.25f);

        [Header("Center Line")]
        [SerializeField] Color centerLineColor = new(1f, 1f, 1f, 0.15f);

        public float ArenaLength => arenaLength;
        public float ArenaWidth => arenaWidth;
        public float ArenaHeight => arenaHeight;
        public Vector3 Center => transform.position;
        public Vector3 JadeSpawnPosition => Center + Vector3.back * (arenaLength * 0.3f);
        public Vector3 RubySpawnPosition => Center + Vector3.forward * (arenaLength * 0.3f);

        Material _generatedWallMat;
        Material _jadeGoalMat;
        Material _rubyGoalMat;
        Material _centerLineMat;

        void Awake()
        {
            CreateMaterials();
            CreateWalls();
            CreateGoalMarkers();
            CreateCenterLine();
            CreateEdgeFrame();
        }

        void CreateMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            _generatedWallMat = CreateTransparentMat(shader, wallColor);
            _jadeGoalMat = CreateTransparentMat(shader, jadeGoalColor);
            _rubyGoalMat = CreateTransparentMat(shader, rubyGoalColor);
            _centerLineMat = CreateTransparentMat(shader, centerLineColor);
        }

        static Material CreateTransparentMat(Shader shader, Color color)
        {
            var mat = new Material(shader) { color = color };
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            return mat;
        }

        void CreateWalls()
        {
            var mat = wallMaterial != null ? wallMaterial : _generatedWallMat;
            float hw = arenaWidth / 2f;
            float hh = arenaHeight / 2f;
            float hl = arenaLength / 2f;

            CreateWall("Wall_Top",    Center + Vector3.up * hh,    new Vector3(arenaWidth, wallThickness, arenaLength), mat);
            CreateWall("Wall_Bottom", Center + Vector3.down * hh,  new Vector3(arenaWidth, wallThickness, arenaLength), mat);
            CreateWall("Wall_Left",   Center + Vector3.left * hw,  new Vector3(wallThickness, arenaHeight, arenaLength), mat);
            CreateWall("Wall_Right",  Center + Vector3.right * hw, new Vector3(wallThickness, arenaHeight, arenaLength), mat);
            CreateWall("Wall_Back",   Center + Vector3.back * (hl + wallThickness),    new Vector3(arenaWidth, arenaHeight, wallThickness), mat);
            CreateWall("Wall_Front",  Center + Vector3.forward * (hl + wallThickness), new Vector3(arenaWidth, arenaHeight, wallThickness), mat);
        }

        void CreateWall(string wallName, Vector3 position, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = wallName;
            wall.transform.SetParent(transform);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.layer = gameObject.layer;

            var renderer = wall.GetComponent<Renderer>();
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var col = wall.GetComponent<BoxCollider>();
            col.isTrigger = false;
            col.material = new PhysicsMaterial("ArenaBounce")
            {
                bounciness = 0.9f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                dynamicFriction = 0.05f,
                staticFriction = 0.05f
            };
        }

        void CreateGoalMarkers()
        {
            float hl = arenaLength / 2f;
            CreateGoalPlane("GoalMarker_Jade", Center + Vector3.back * hl, _jadeGoalMat);
            CreateGoalPlane("GoalMarker_Ruby", Center + Vector3.forward * hl, _rubyGoalMat);
        }

        void CreateGoalPlane(string name, Vector3 position, Material mat)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = name;
            marker.transform.SetParent(transform);
            marker.transform.position = position;
            // Quad faces +Z by default; back goal needs to face +Z, front goal faces -Z
            if (position.z < Center.z)
                marker.transform.rotation = Quaternion.identity;
            else
                marker.transform.rotation = Quaternion.Euler(0, 180, 0);
            marker.transform.localScale = new Vector3(arenaWidth * 0.4f, arenaHeight * 0.4f, 1f);

            var renderer = marker.GetComponent<Renderer>();
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Remove collider — goals have their own trigger colliders
            var col = marker.GetComponent<MeshCollider>();
            if (col) Destroy(col);
        }

        void CreateCenterLine()
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Quad);
            line.name = "CenterLine";
            line.transform.SetParent(transform);
            line.transform.position = Center;
            line.transform.rotation = Quaternion.Euler(90, 0, 0);
            line.transform.localScale = new Vector3(arenaWidth, arenaLength * 0.005f, 1f);

            var renderer = line.GetComponent<Renderer>();
            renderer.material = _centerLineMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var col = line.GetComponent<MeshCollider>();
            if (col) Destroy(col);
        }

        void CreateEdgeFrame()
        {
            float hw = arenaWidth / 2f;
            float hh = arenaHeight / 2f;
            float hl = arenaLength / 2f;

            // 12 edges of the arena box
            Vector3[] starts =
            {
                new(-hw, -hh, -hl), new( hw, -hh, -hl), new( hw, -hh,  hl), new(-hw, -hh,  hl),
                new(-hw,  hh, -hl), new( hw,  hh, -hl), new( hw,  hh,  hl), new(-hw,  hh,  hl),
                new(-hw, -hh, -hl), new( hw, -hh, -hl), new( hw, -hh,  hl), new(-hw, -hh,  hl),
            };
            Vector3[] ends =
            {
                new( hw, -hh, -hl), new( hw, -hh,  hl), new(-hw, -hh,  hl), new(-hw, -hh, -hl),
                new( hw,  hh, -hl), new( hw,  hh,  hl), new(-hw,  hh,  hl), new(-hw,  hh, -hl),
                new(-hw,  hh, -hl), new( hw,  hh, -hl), new( hw,  hh,  hl), new(-hw,  hh,  hl),
            };

            for (int i = 0; i < starts.Length; i++)
            {
                CreateEdge($"Edge_{i}", Center + starts[i], Center + ends[i]);
            }
        }

        void CreateEdge(string name, Vector3 from, Vector3 to)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPositions(new[] { from, to });
            lr.startWidth = 0.8f;
            lr.endWidth = 0.8f;
            lr.material = new Material(Shader.Find("Sprites/Default")) { color = edgeColor };
            lr.startColor = edgeColor;
            lr.endColor = edgeColor;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireCube(Center, new Vector3(arenaWidth, arenaHeight, arenaLength));

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireCube(Center + Vector3.back * arenaLength / 2f,
                new Vector3(arenaWidth * 0.4f, arenaHeight * 0.4f, wallThickness));

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
            Gizmos.DrawWireCube(Center + Vector3.forward * arenaLength / 2f,
                new Vector3(arenaWidth * 0.4f, arenaHeight * 0.4f, wallThickness));
        }
    }
}
