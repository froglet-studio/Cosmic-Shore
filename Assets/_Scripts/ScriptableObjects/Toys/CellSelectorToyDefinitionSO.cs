using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>Cell Selector</b> toy - the freestyle world picker, and the freestyle reset.
    ///
    /// Fly the station and a matrix of MINI-CELLS blooms beside it, one per config the
    /// containing <see cref="Cell"/> can be - each holding a genuine SCALE MODEL of the world
    /// that config creates, sampled by <see cref="CellMiniatureBuilder"/> from the generator's
    /// own output (no prisms spawned). Fly a mini-cell and the cell becomes it: the old world
    /// suctions away and the chosen one grows back behind the standard environment veil.
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
        float stationSpacing = 110f;

        [SerializeField, Min(1f), Tooltip("Radius of one mini-cell (its membrane rings). The scale " +
                                          "model inside is fitted to just under this.")]
        float stationRadius = 18f;

        [SerializeField, Min(0.5f), Tooltip("How far out the matrix blooms, in multiples of Station " +
                                            "Spacing, measured from the toy along the outward radial " +
                                            "(away from the cell centre). You fly AT the toy and keep " +
                                            "going, so this is the gap you cross before the choices.")]
        float matrixDistanceFactor = 3f;

        [Header("Scale models")]
        [SerializeField, Range(200, 4000), Tooltip("Samples taken from an environment's real generated " +
                                                   "output to build its mini-cell scale model (24 mesh " +
                                                   "vertices each). The silhouette is what reads at " +
                                                   "thumbnail size, so ~1k carries it; higher just costs " +
                                                   "vertices. No prisms are ever spawned for a model.")]
        int modelPointBudget = 2400;

        [SerializeField, Range(0.2f, 1f), Tooltip("How much of an environment's mass the model keeps, " +
                                                  "densest structures first. Below 1 the diffuse scatter " +
                                                  "is dropped and only the world's SIGNATURE structures " +
                                                  "survive - a whole cell at thumbnail size is a uniform " +
                                                  "dust cloud that reads the same for every world. 1 = the " +
                                                  "entire world, haze included.")]
        float signatureCoverage = 0.7f;

        public IReadOnlyList<CellConfigDataSO> Cells => cells;
        public bool ClearLooseTrailMass => clearLooseTrailMass;
        public float StationSpacing => stationSpacing;
        public float StationRadius => stationRadius;
        public float MatrixDistanceFactor => matrixDistanceFactor;
        public int ModelPointBudget => modelPointBudget;
        public float SignatureCoverage => signatureCoverage;

        /// <summary>The world itself. It changes CELL - the old one suctions away and the chosen one grows
        /// back - which is the heaviest thing any toy does.</summary>
        public override ToyCategory Category => ToyCategory.World;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor, DisplayName);
            var toy = go.AddComponent<CellSelectorToy>();
            toy.Configure(this);
            toy.Initialize(this, context, placement);
        }
    }
}
