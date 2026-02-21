using UnityEngine;

namespace CosmicShore.Game.Arcade.AstroLeague
{
    /// <summary>
    /// Defines the Astro League arena boundaries and spawns walls at runtime.
    /// The arena is a rectangular box in 3D space (no gravity).
    /// Goals are placed at the +Z and -Z ends.
    /// </summary>
    public class AstroLeagueArena : MonoBehaviour
    {
        [Header("Arena Dimensions")]
        [SerializeField] float arenaLength = 300f;
        [SerializeField] float arenaWidth = 200f;
        [SerializeField] float arenaHeight = 100f;
        [SerializeField] float wallThickness = 5f;

        [Header("Visuals")]
        [SerializeField] Material wallMaterial;

        public float ArenaLength => arenaLength;
        public float ArenaWidth => arenaWidth;
        public float ArenaHeight => arenaHeight;

        public Vector3 Center => transform.position;

        /// <summary>
        /// Spawn position for the jade team (negative Z side).
        /// </summary>
        public Vector3 JadeSpawnPosition => Center + Vector3.back * (arenaLength * 0.3f);

        /// <summary>
        /// Spawn position for the ruby team (positive Z side).
        /// </summary>
        public Vector3 RubySpawnPosition => Center + Vector3.forward * (arenaLength * 0.3f);

        void Awake()
        {
            CreateWalls();
        }

        void CreateWalls()
        {
            // Top wall
            CreateWall("Wall_Top",
                Center + Vector3.up * arenaHeight / 2f,
                new Vector3(arenaWidth, wallThickness, arenaLength));

            // Bottom wall
            CreateWall("Wall_Bottom",
                Center + Vector3.down * arenaHeight / 2f,
                new Vector3(arenaWidth, wallThickness, arenaLength));

            // Left wall
            CreateWall("Wall_Left",
                Center + Vector3.left * arenaWidth / 2f,
                new Vector3(wallThickness, arenaHeight, arenaLength));

            // Right wall
            CreateWall("Wall_Right",
                Center + Vector3.right * arenaWidth / 2f,
                new Vector3(wallThickness, arenaHeight, arenaLength));

            // Back wall (behind jade goal - ball passes through goal trigger first)
            CreateWall("Wall_Back",
                Center + Vector3.back * (arenaLength / 2f + wallThickness),
                new Vector3(arenaWidth, arenaHeight, wallThickness));

            // Front wall (behind ruby goal - ball passes through goal trigger first)
            CreateWall("Wall_Front",
                Center + Vector3.forward * (arenaLength / 2f + wallThickness),
                new Vector3(arenaWidth, arenaHeight, wallThickness));
        }

        void CreateWall(string wallName, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = wallName;
            wall.transform.SetParent(transform);
            wall.transform.position = position;
            wall.transform.localScale = scale;

            if (wallMaterial != null)
                wall.GetComponent<Renderer>().material = wallMaterial;
            else
                wall.GetComponent<Renderer>().enabled = false;

            // Walls don't need rigidbodies - they're static colliders
            var col = wall.GetComponent<BoxCollider>();
            col.isTrigger = false;

            // Add a physics material for bounciness
            var physicsMat = new PhysicsMaterial("ArenaBounce")
            {
                bounciness = 0.9f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                dynamicFriction = 0.1f,
                staticFriction = 0.1f
            };
            col.material = physicsMat;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireCube(Center, new Vector3(arenaWidth, arenaHeight, arenaLength));

            // Draw goal zones
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireCube(Center + Vector3.back * arenaLength / 2f,
                new Vector3(arenaWidth * 0.4f, arenaHeight * 0.4f, wallThickness));

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
            Gizmos.DrawWireCube(Center + Vector3.forward * arenaLength / 2f,
                new Vector3(arenaWidth * 0.4f, arenaHeight * 0.4f, wallThickness));
        }
    }
}
