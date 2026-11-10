using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Shatterplane" - the Brittle Star's Space arena (<c>GameModes.Shatterplane</c>): wide, thin round PLATES of
    /// neutral prisms hanging in the cell. Square up to a plate and the thrown ring's flat blast erases it whole; the
    /// guns chew what the ring misses. See <c>Assets/_Scripts/Controller/Arcade/SHATTERPLANE.md</c>.
    ///
    /// <para>All tiles are painted neutral (<see cref="Domains.Blue"/>, as Flatline's panes and the Boneyard's crust
    /// are), so every team breaks them alike and every one scores (Rampage's rule: environment mass is hostile to all).
    /// Ribs, danger rims and screens are the ladder's (I4). The geometry is <see cref="ShatterplanePlates"/> (pure,
    /// tested, twinned in <c>Tools/Build/shatterplane_budget.py</c>). <b>No intensity read</b>: the layout depends only
    /// on the serialized fields below, one prefab per intensity, so each one bakes
    /// (<c>Tools/Build/bake_prism_layouts.py</c>). Authored by <c>Tools/Build/author_shatterplane_assets.py</c>.</para>
    /// </summary>
    public class SpawnableShatterplane : CellEnvironmentSpawnableBase
    {
        [Header("Field")]
        [Tooltip("Structures are spread evenly by volume between this radius (u) and the outer one.")]
        [SerializeField, Min(0f)] float innerRadius = 200f;
        [Tooltip("The field's outer radius (u) for a structure's centre.")]
        [SerializeField, Min(1f)] float outerRadius = 380f;

        [Header("Plates")]
        [Tooltip("How many structures (a plate, or a stack of plates) the field holds.")]
        [SerializeField, Min(0)] int structureCount = 10;
        [Tooltip("A plate's radius in tiles (a tile is 8 u).")]
        [SerializeField, Min(1)] int plateRadius = 13;
        [Tooltip("Plates in a stack, sharing one axis (1 = a lone plate).")]
        [SerializeField, Min(1)] int platesPerStack = 1;
        [Tooltip("Space between a stack's plates (u).")]
        [SerializeField, Min(0f)] float shelfGap = 0f;
        [Tooltip("1 = every plate faces straight out from the centre, square to a pilot coming in from the spawn ring; " +
                 "0 = each axis is drawn at random (and turned to face away from the centre).")]
        [SerializeField, Range(0, 1)] int faceOut = 1;

        [Header("Wear and danger")]
        [Tooltip("Every Nth interior grid line of a plate is a super-shielded rib. 0 = none.")]
        [SerializeField, Min(0)] int ribEvery = 0;
        [Tooltip("1 = a plate's outer ring of tiles is danger.")]
        [SerializeField, Range(0, 1)] int dangerRim = 0;

        [Header("Screens (a super-shielded grille in front of a plate, offset to one side)")]
        [Tooltip("Every Nth structure has a screen. 0 = none.")]
        [SerializeField, Min(0)] int screenEvery = 0;
        [Tooltip("A screen's radius in tiles.")]
        [SerializeField, Min(0)] int screenRadius = 0;
        [Tooltip("How far in front of the front plate the screen stands (u).")]
        [SerializeField, Min(0f)] float screenOffset = 0f;
        [Tooltip("The grille's pitch in tiles: every Nth grid line is laid.")]
        [SerializeField, Min(1)] int screenRibEvery = 1;

        [Header("Crystal sites (read by ShatterplaneController)")]
        [Tooltip("Crystal sites, on the plates' axes in front of them: one per structure, then a second round farther out.")]
        [SerializeField, Min(0)] int crystalCount = 10;

        protected override int DefaultSeed => 8888;
        protected override int LayCapacity => 16384;

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(
                System.HashCode.Combine(innerRadius, outerRadius, structureCount, plateRadius, platesPerStack, shelfGap,
                                        faceOut),
                System.HashCode.Combine(ribEvery, dangerRim, screenEvery, screenRadius, screenOffset, screenRibEvery, 1));

        readonly List<ShatterplanePlates.Structure> _structures = new();
        readonly List<ShatterplanePlates.TileLay> _tiles = new();

        ShatterplanePlates.Settings Settings() => new()
        {
            Seed = seed != 0 ? seed : DefaultSeed,
            InnerRadius = innerRadius,
            OuterRadius = outerRadius,
            StructureCount = structureCount,
            PlateRadius = plateRadius,
            PlatesPerStack = platesPerStack,
            ShelfGap = shelfGap,
            FaceOut = faceOut != 0,
            RibEvery = ribEvery,
            DangerRim = dangerRim != 0,
            ScreenEvery = screenEvery,
            ScreenRadius = screenRadius,
            ScreenOffset = screenOffset,
            ScreenRibEvery = screenRibEvery,
        };

        protected override void BuildEnvironment()
        {
            var s = Settings();
            ShatterplanePlates.Layout(s, _structures);
            ShatterplanePlates.Tiles(_structures, s, _tiles);
            var scale = ShatterplanePlates.TileScale;
            for (int i = 0; i < _tiles.Count; i++)
            {
                var t = _tiles[i];
                Emit(t.Position, t.Rotation, scale, Domains.Blue, t.Kind);
            }
            _tiles.Clear();
        }

        /// <summary>
        /// The placed structures in this arena's local space (the arena centre is its origin). Recomputed from the
        /// fields rather than from a build, because a baked layout never runs <see cref="BuildEnvironment"/>. Read by
        /// <c>ShatterplaneController</c>'s AI to find a plate's axis.
        /// </summary>
        public void CollectStructures(List<ShatterplanePlates.Structure> into) =>
            ShatterplanePlates.Layout(Settings(), into);

        /// <summary>Half a stack's depth (u): from a structure's centre to its front plate.</summary>
        public float FrontOffset => ShatterplanePlates.HalfDepth(Settings());

        /// <summary>Plates in each stack (1 = a lone plate).</summary>
        public int PlatesPerStack => Mathf.Max(1, platesPerStack);

        /// <summary>Space between a stack's plates (u).</summary>
        public float ShelfGap => shelfGap;

        /// <summary>A plate's radius (u), to the outer edge of its rim tiles.</summary>
        public float PlateWorldRadius => (plateRadius + 0.5f) * ShatterplanePlates.Tile;

        /// <summary>
        /// The crystal sites in this arena's local space: on each plate's axis, in front of its front plate. Read by
        /// <c>ShatterplaneController</c> to lay the crystal field.
        /// </summary>
        public void CollectCrystalSites(List<Vector3> into) => CollectCrystalSites(into, null);

        /// <summary>The crystal sites, and (when <paramref name="axes"/> is given) the axis of the plate each one floats
        /// in front of, index for index - so a mode can stand several crystals at one site side by side across it.</summary>
        public void CollectCrystalSites(List<Vector3> into, List<Vector3> axes)
        {
            var s = Settings();
            var structures = new List<ShatterplanePlates.Structure>(32);
            ShatterplanePlates.Layout(s, structures);
            ShatterplanePlates.CrystalSites(structures, s, crystalCount, into);
            if (axes == null) return;
            axes.Clear();
            for (int k = 0; k < into.Count && structures.Count > 0; k++)
                axes.Add(structures[k % structures.Count].N);   // CrystalSites walks the structures in this same order
        }
    }
}
