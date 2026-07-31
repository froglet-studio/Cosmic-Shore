using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>Cell Selector</b> toy - the freestyle world picker, and the freestyle reset.
    ///
    /// Fly the station and a matrix of MINI-CELLS blooms beside it, one per config the
    /// containing <see cref="Cell"/> can be. Fly a mini-cell and the cell becomes it: the old
    /// world suctions away and the chosen one grows back behind the standard environment veil.
    /// Fly the mini-cell of the world you are already in and you get that same cycle on the
    /// same config - the reset.
    ///
    /// This is what makes entering freestyle cheap. The Cell boots with
    /// <c>CellTypeChoiceOptions.EnvironmentFree</c> (no authored environment, so Menu_Main
    /// opens - and re-opens after an arcade game - without a multi-second prism build), and the
    /// heavy worlds become opt-in: the load is paid only by the player who flies in and asks
    /// for one.
    ///
    /// Toy-faithful: no score, no end condition, no timers. Nothing here is on a clock and no
    /// prism ages out - a cell swap is an explicit, player-initiated world change, the same
    /// class of event as a scene load. See Docs/ECOSYSTEM.md §19.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_CellSelector", menuName = "ScriptableObjects/Toys/Cell Selector Toy")]
    public class CellSelectorToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Cells")]
        [SerializeField, Tooltip("OPTIONAL override of which cells to offer. Leave EMPTY (the default " +
                                 "and the recommendation) and the toy reads the containing Cell's own " +
                                 "CellConfigs list - the Cell owns the environment, so there is one " +
                                 "source of truth for what this scene's cell can be and the toy can " +
                                 "never drift from it.")]
        List<CellConfigDataSO> cells = new();

        [Header("Reset scope")]
        [SerializeField, Tooltip("Also retire the POOLED prisms the cell tracks - the vessels' " +
                                 "accumulated freestyle trail - so a selection is a full scene reset " +
                                 "rather than an environment swap. Prisms owned by a closed toy system " +
                                 "(the Wanderway conveyor transports its own fixed stock) are never " +
                                 "touched either way.")]
        bool clearLooseTrailMass = true;

        [Header("Layout")]
        [SerializeField, Min(10f), Tooltip("Spacing between mini-cells in the matrix.")]
        float stationSpacing = 55f;

        [SerializeField, Min(1f), Tooltip("Radius of one mini-cell (its membrane rings).")]
        float stationRadius = 9f;

        [SerializeField, Range(0, 60), Tooltip("Prism shards drawn inside a mini-cell that HAS an " +
                                               "authored environment. Environment-free cells always " +
                                               "draw empty, so the picture tells you the entry is free " +
                                               "before you read the label.")]
        int shardsPerCell = 24;

        public IReadOnlyList<CellConfigDataSO> Cells => cells;
        public bool ClearLooseTrailMass => clearLooseTrailMass;
        public float StationSpacing => stationSpacing;
        public float StationRadius => stationRadius;
        public int ShardsPerCell => shardsPerCell;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor, DisplayName);
            var toy = go.AddComponent<CellSelectorToy>();
            toy.Configure(this);
            toy.Initialize(this, context, placement);
        }
    }
}
