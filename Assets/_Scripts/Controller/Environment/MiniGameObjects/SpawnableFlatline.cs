using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Flatline" - the Brittle Star's charge arena (<c>GameModes.Flatline</c>): thin PANES of prisms crossing the cell.
    /// See <c>Assets/_Scripts/Controller/Arcade/FLATLINE.md</c>.
    ///
    /// <para>Every pane has a job for the Brittle Star's own kit, which the mode never changes: a round stops at the
    /// first prism, so a pane is cover from the guns; the cords slice what they sweep, so they reach through a pane and
    /// strike the pilot hiding behind it; and the thrown ring bursts in a flat disk across its line of flight, so a ring
    /// thrown face-on into a pane bursts flat along it and catches whoever hugs it. The seams where panes cross carry
    /// the crystals (the spare rings), the most exposed places in the cell.</para>
    ///
    /// <para><b>Ribs.</b> Every <see cref="ribEvery"/>th grid line of a pane is super-shielded: nothing breaks it, so a
    /// pane the cords and rings have worn down stays a readable plane, a lattice instead of nothing. The plain tiles
    /// between the ribs are what the match wears away.</para>
    ///
    /// <para>All tiles are painted neutral (<see cref="Domains.Blue"/>, as the Boneyard's crust is), so every team cuts
    /// them alike. The geometry is <see cref="FlatlinePanes"/> (pure, tested, twinned in
    /// <c>Tools/Build/flatline_budget.py</c>). <b>No intensity read</b>: the layout depends only on the serialized fields
    /// below, one prefab per intensity, so each one bakes (<c>Tools/Build/bake_prism_layouts.py</c>). Authored by
    /// <c>Tools/Build/author_flatline_assets.py</c>.</para>
    /// </summary>
    public class SpawnableFlatline : CellEnvironmentSpawnableBase
    {
        [Header("Panes")]
        [Tooltip("0 = three planes (the axes), 1 = five pages round one spine, 2 = the six icosahedral axes, " +
                 "3 = a geode of ten panes standing off the centre.")]
        [SerializeField, Range(0, 3)] int arrangement = 0;
        [Tooltip("Each pane's radius in tiles (a tile is 10 u).")]
        [SerializeField, Min(1)] int paneRadiusTiles = 23;
        [Tooltip("How far (u) each pane stands from the centre along its own normal. 0 = every pane through the centre.")]
        [SerializeField, Min(0f)] float paneOffset = 0f;

        [Header("Wear and danger")]
        [Tooltip("Every Nth grid line is a super-shielded rib (the pane's skeleton). 0 = none.")]
        [SerializeField, Min(0)] int ribEvery = 6;
        [Tooltip("Outer rings of tiles that are danger prisms. 0 = none.")]
        [SerializeField, Min(0)] int dangerRimTiles = 0;
        [Tooltip("Share of the cells between the ribs left open as holes (0..1).")]
        [SerializeField, Range(0f, 1f)] float holeShare = 0f;
        [Tooltip("A hole cell's edge, in tiles.")]
        [SerializeField, Min(1)] int holeCellTiles = 4;

        [Header("Crystal sites (read by FlatlineController)")]
        [Tooltip("Crystal sites on the seams where panes cross.")]
        [SerializeField, Min(0)] int crystalCount = 6;
        [Tooltip("Step (u) along a seam between its sites; 0 puts each site at its seam's nearest point to the centre.")]
        [SerializeField, Min(0f)] float crystalSpacing = 110f;

        protected override int DefaultSeed => 8484;
        protected override int LayCapacity => 16384;

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(
                System.HashCode.Combine(arrangement, paneRadiusTiles, paneOffset),
                System.HashCode.Combine(ribEvery, dangerRimTiles, holeShare, holeCellTiles, 1));

        readonly List<FlatlinePanes.Pane> _panes = new();
        readonly List<FlatlinePanes.TileLay> _tiles = new();

        protected override void BuildEnvironment()
        {
            FlatlinePanes.Panes(arrangement, paneRadiusTiles, paneOffset, _panes);
            FlatlinePanes.Tiles(_panes, dangerRimTiles, ribEvery, holeShare, holeCellTiles, _tiles);
            var scale = FlatlinePanes.TileScale;
            for (int i = 0; i < _tiles.Count; i++)
            {
                var t = _tiles[i];
                Emit(t.Position, t.Rotation, scale, Domains.Blue, t.Kind);
            }
            _tiles.Clear();
        }

        /// <summary>
        /// The crystal sites in this structure's local space (the arena centre is its origin), on the seams. Read by
        /// <c>FlatlineController</c> to lay the crystal field and by its AI to forage. Recomputed from the fields
        /// rather than from a build, because a baked layout never runs <see cref="BuildEnvironment"/>.
        /// </summary>
        public void CollectCrystalSites(List<Vector3> into)
        {
            var panes = new List<FlatlinePanes.Pane>(10);
            FlatlinePanes.Panes(arrangement, paneRadiusTiles, paneOffset, panes);
            FlatlinePanes.CrystalSites(panes, crystalCount, crystalSpacing, into);
        }
    }
}
