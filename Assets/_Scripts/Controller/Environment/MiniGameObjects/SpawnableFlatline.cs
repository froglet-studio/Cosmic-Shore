using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Flatline" - the Brittle Star's charge arena (<c>GameModes.Flatline</c>): an OPEN field of thin-paned structures
    /// scattered through the cell - loose panes, tunnels you fly through and windowed cages you fly into. See
    /// <c>Assets/_Scripts/Controller/Arcade/FLATLINE.md</c>.
    ///
    /// <para>Every pane has a job for the Brittle Star's own kit, which the mode never changes: a round stops at the
    /// first prism, so a pane is cover from the guns; the cords slice what they sweep, so they reach through a pane and
    /// strike the pilot hiding behind it; and the thrown ring bursts in a flat disk across its line of flight, so a ring
    /// thrown face-on into a pane bursts flat along it and catches whoever hugs it. The crystals (spare rings) float
    /// inside the rooms first, so re-arming means going in.</para>
    ///
    /// <para><b>Ribs.</b> Every <see cref="ribEvery"/>th grid line of a wall is super-shielded: nothing breaks it, so a
    /// wall the cords and rings have worn down stays a readable lattice. The plain tiles between the ribs are what the
    /// match wears away.</para>
    ///
    /// <para>All tiles are painted neutral (<see cref="Domains.Blue"/>, as the Boneyard's crust is), so every team cuts
    /// them alike. The geometry is <see cref="FlatlinePanes"/> (pure, tested, twinned in
    /// <c>Tools/Build/flatline_budget.py</c>). <b>No intensity read</b>: the layout depends only on the serialized fields
    /// below, one prefab per intensity, so each one bakes (<c>Tools/Build/bake_prism_layouts.py</c>). Authored by
    /// <c>Tools/Build/author_flatline_assets.py</c>.</para>
    /// </summary>
    public class SpawnableFlatline : CellEnvironmentSpawnableBase
    {
        [Header("Field")]
        [Tooltip("Structures are spread evenly by volume between this radius (u) and the outer one.")]
        [SerializeField, Min(0f)] float innerRadius = 80f;
        [Tooltip("The field's outer radius (u).")]
        [SerializeField, Min(1f)] float outerRadius = 430f;

        [Header("Structures")]
        [Tooltip("Loose panes: square plates of (2h+1)^2 tiles (a tile is 10 u).")]
        [SerializeField, Min(0)] int paneCount = 16;
        [Tooltip("The smallest loose pane's half-size in tiles.")]
        [SerializeField, Min(1)] int paneHalfMin = 3;
        [Tooltip("The largest loose pane's half-size in tiles (sizes cycle from min to max).")]
        [SerializeField, Min(1)] int paneHalfMax = 3;
        [Tooltip("Tunnels: four walls of a hollow cube, open at both ends, to fly through.")]
        [SerializeField, Min(0)] int tunnelCount = 0;
        [Tooltip("Cages: six walls, each with a window in the middle, to fly into from any side.")]
        [SerializeField, Min(0)] int cageCount = 0;
        [Tooltip("A room's half-size in tiles (walls stand half a tile beyond it).")]
        [SerializeField, Min(2)] int roomHalf = 4;

        [Header("Wear and danger")]
        [Tooltip("Every Nth grid line of a wall is a super-shielded rib. 0 = none.")]
        [SerializeField, Min(0)] int ribEvery = 3;
        [Tooltip("Every Nth loose pane wears a danger rim. 0 = none.")]
        [SerializeField, Min(0)] int dangerPaneEvery = 0;
        [Tooltip("Every Nth room wears danger rims on its walls. 0 = none.")]
        [SerializeField, Min(0)] int dangerRoomEvery = 0;
        [Tooltip("Share of a loose pane's cells left open as holes (0..1).")]
        [SerializeField, Range(0f, 1f)] float holeShare = 0f;
        [Tooltip("A hole cell's edge, in tiles.")]
        [SerializeField, Min(1)] int holeCellTiles = 2;

        [Header("Crystal sites (read by FlatlineController)")]
        [Tooltip("Crystal sites: inside the rooms first, then in front of loose panes.")]
        [SerializeField, Min(0)] int crystalCount = 6;

        protected override int DefaultSeed => 8484;
        protected override int LayCapacity => 16384;

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(
                System.HashCode.Combine(innerRadius, outerRadius, paneCount, paneHalfMin, paneHalfMax, tunnelCount,
                                        cageCount, roomHalf),
                System.HashCode.Combine(ribEvery, dangerPaneEvery, dangerRoomEvery, holeShare, holeCellTiles, 2));

        readonly List<FlatlinePanes.Structure> _structures = new();
        readonly List<FlatlinePanes.TileLay> _tiles = new();

        FlatlinePanes.Settings Settings() => new()
        {
            Seed = seed != 0 ? seed : DefaultSeed,
            InnerRadius = innerRadius,
            OuterRadius = outerRadius,
            PaneCount = paneCount,
            PaneHalfMin = paneHalfMin,
            PaneHalfMax = Mathf.Max(paneHalfMin, paneHalfMax),
            TunnelCount = tunnelCount,
            CageCount = cageCount,
            RoomHalf = roomHalf,
            RibEvery = ribEvery,
            DangerPaneEvery = dangerPaneEvery,
            DangerRoomEvery = dangerRoomEvery,
            HoleShare = holeShare,
            HoleCellTiles = holeCellTiles,
        };

        protected override void BuildEnvironment()
        {
            var s = Settings();
            FlatlinePanes.Layout(s, _structures);
            FlatlinePanes.Tiles(_structures, s, _tiles);
            var scale = FlatlinePanes.TileScale;
            for (int i = 0; i < _tiles.Count; i++)
            {
                var t = _tiles[i];
                Emit(t.Position, t.Rotation, scale, Domains.Blue, t.Kind);
            }
            _tiles.Clear();
        }

        /// <summary>
        /// The crystal sites in this structure's local space (the arena centre is its origin): inside the rooms, then in
        /// front of loose panes. Read by <c>FlatlineController</c> to lay the crystal field and by its AI to forage.
        /// Recomputed from the fields rather than from a build, because a baked layout never runs
        /// <see cref="BuildEnvironment"/>.
        /// </summary>
        public void CollectCrystalSites(List<Vector3> into)
        {
            var structures = new List<FlatlinePanes.Structure>(64);
            FlatlinePanes.Layout(Settings(), structures);
            FlatlinePanes.CrystalSites(structures, crystalCount, into);
        }
    }
}
