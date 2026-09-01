using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>Arkway</b> toy - the cellular Wanderway, and the first vehicle of the <b>Ark</b>
    /// fundamental. Fly through it and a VOYAGE begins: a corridor of whole CELLS (real
    /// satellite <see cref="CosmicShore.Gameplay.Cell"/>s drawn from the cell selector's own
    /// rotation, built thinned) opens ahead, and an <see cref="CosmicShore.Gameplay.Ark"/> - a
    /// prism-bodied mothership in your domain - sails it at its own unhurried pace. Three cells
    /// stand at once (previous / current / next); as the Ark crosses into the next, a fresh cell
    /// blooms beyond it and the one two-behind retires once it is out of sight.
    ///
    /// The whole mechanic is the shipped ecology, composed rather than re-invented: each
    /// traversal cell declares no nucleus control zone, so control is whole-cell VOLUME and the
    /// herbivore diet is the legacy opposing-domain rule - take over a cell's volume and its
    /// fauna waves spawn in YOUR colour (they cannot eat the Ark, and they graze the mass that
    /// can); lose it and the waves hunt the Ark's hull, which is ordinary grazeable domain mass.
    /// When the food web eats the Ark's last hull prism the voyage resets. Stay within a few
    /// cell radii of the Ark or it recalls you to its side. The trail you lay is recycled with
    /// the corridor - each struck cell takes the ribbon laid up to it - so a voyage can run
    /// indefinitely.
    ///
    /// Toy-faithful: no score, no end condition. The one clock in it is the Ark itself - the
    /// pace of a voyage the player opts into, sustains (the leash), and can end at will.
    /// This is a stepping stone toward faction missions: venturing into the hypersea with, and
    /// for, a mothership.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_Arkway", menuName = "ScriptableObjects/Toys/Arkway Toy")]
    public class ArkwayToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Arkway - the Ark")]
        [SerializeField, Tooltip("Prism prefab the Ark's hull is laid from (a plain environment prism, " +
                                 "e.g. SpawnablePrism). The hull is ordinary conserved mass in the local " +
                                 "player's domain: fauna of another domain graze it, your own never do.")]
        Prism prismPrefab;

        [SerializeField, Min(4f), Tooltip("Ark speed under a cell's core, world units per second - the " +
                                          "pace of the slow pass THROUGH a world.")]
        float arkSpeed = 18f;

        [SerializeField, Min(1f), Tooltip("Multiple of the speed above that the Ark makes in the open " +
                                          "water BETWEEN cells. It eases back down to the base speed " +
                                          "across the destination cell's own membrane radius, so the " +
                                          "deceleration IS entering the cell.")]
        float arkCruiseSpeedFactor = 4f;

        [SerializeField, Min(30f), Tooltip("Hull length, world units. Prism count scales with it (the " +
                                           "default hull is ~150 prisms at 110).")]
        float arkHullLength = 110f;

        [Header("Arkway - the corridor of cells")]
        [SerializeField, Tooltip("Explicit traversal-cell rotation. Leave EMPTY to read the host Cell's own " +
                                 "AvailableConfigs (the cell selector's list) minus its environment-free " +
                                 "entries - the Cell stays the single source of truth for what a cell here " +
                                 "can be.")]
        List<CellConfigDataSO> cells = new();

        [SerializeField, Tooltip("Crystal seated at each traversal cell's CORE (inside the nucleus - the " +
                                 "canonical omni volume). Leave EMPTY to use the omni crystal on " +
                                 "Resources/ModePreviewLibrary. A satellite cell has no CrystalManager " +
                                 "feeding it, so this is the one thing the corridor hands its cells.")]
        Crystal crystalPrefab;

        [SerializeField, Min(2000f), Tooltip("Centre-to-centre spacing between consecutive traversal " +
                                             "cells. Must exceed two membrane radii (freestyle membranes " +
                                             "are 1200) plus a gap of open water.")]
        float cellSpacing = 3200f;

        [SerializeField, Range(0f, 60f), Tooltip("Maximum course deviation per cell, degrees. The corridor " +
                                                 "wanders rather than running straight; 0 = a straight line.")]
        float maxTurnDegrees = 25f;

        [SerializeField, Range(1, 8), Tooltip("Prism-lay stride for each traversal cell's environment: at N " +
                                              "every dense trail lays every Nth prism (the mode preview's " +
                                              "thinning). 4 keeps three standing freestyle worlds inside " +
                                              "the Wanderway-stock envelope (~30k prisms).")]
        int prismStride = 4;

        [SerializeField, Range(0.1f, 1f), Tooltip("Runtime population scale for each traversal cell's " +
                                                  "flora/fauna (Cell.RuntimePopulationScale): seed floors " +
                                                  "and caps together, production gating only - nothing is " +
                                                  "ever culled to meet it.")]
        float populationScale = 0.5f;

        [SerializeField, Tooltip("Deterministic seed for the config shuffle-bag and corridor headings. " +
                                 "0 = a fresh voyage every session.")]
        int seed;

        [Header("Arkway - the leash")]
        [SerializeField, Min(1f), Tooltip("Leash radius as a multiple of the current traversal cell's " +
                                          "membrane radius. Beyond it the recall countdown starts. " +
                                          "3 gives room to range out and explore a cell rather than " +
                                          "flying formation with the hull.")]
        float leashRadiusFactor = 3f;

        [SerializeField, Min(300f), Tooltip("Leash radius fallback, world units, for the moments no " +
                                            "traversal cell has a measurable membrane yet.")]
        float leashRadiusFallback = 1300f;

        [SerializeField, Min(1f), Tooltip("Seconds of grace beyond the leash before the Ark recalls you " +
                                          "to its side. The countdown is telegraphed on screen.")]
        float leashGraceSeconds = 5f;

        [Header("Arkway - the run")]
        [SerializeField, Tooltip("Starting a voyage hands the host cell its BARE CANVAS config through " +
                                 "Cell.RequestCellSwap (the Wanderway's own opening move): the corridor " +
                                 "is the world you look at, and three standing cells beside a heavy home " +
                                 "world is a collider budget nobody authored.")]
        bool revertCellOnStart = true;

        [SerializeField, Min(4f), Tooltip("Body radius of the DISEMBARK station, which stands at the " +
                                          "entrance you sailed from and stays there - thread it to head " +
                                          "home.")]
        float returnStationRadius = 16f;

        [SerializeField, Tooltip("Accent for the disembark station - distinct from the toy's accent so the " +
                                 "way home never reads as another voyage station.")]
        Color returnStationColor = new(1f, 0.78f, 0.25f, 1f);

        /// <summary>Where you are, by way of an escort: it opens a corridor of whole cells and an
        /// Ark that sails them, with you sworn to its side.</summary>
        public override ToyCategory Category => ToyCategory.World;

        /// <summary>Authored traversal rotation (empty = read the host cell's own configs).</summary>
        public IReadOnlyList<CellConfigDataSO> Cells => cells;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor, DisplayName);
            var toy = go.AddComponent<ArkwayToy>();
            toy.Configure(BuildConfig());
            toy.Initialize(this, context, placement);
        }

        ArkwayConfig BuildConfig() => new()
        {
            DisplayName = DisplayName,
            PrismPrefab = prismPrefab,
            ArkSpeed = arkSpeed,
            ArkCruiseSpeedFactor = arkCruiseSpeedFactor,
            ArkHullLength = arkHullLength,
            Cells = cells,
            CrystalPrefab = crystalPrefab,
            CellSpacing = cellSpacing,
            MaxTurnDegrees = maxTurnDegrees,
            PrismStride = prismStride,
            PopulationScale = populationScale,
            Seed = seed,
            LeashRadiusFactor = leashRadiusFactor,
            LeashRadiusFallback = leashRadiusFallback,
            LeashGraceSeconds = leashGraceSeconds,
            RevertCellOnStart = revertCellOnStart,
            ReturnStationRadius = returnStationRadius,
            ReturnStationColor = returnStationColor,
        };

        /// <summary>Wires a prism prefab on a runtime-synthesised definition (the zero-config default toybox).</summary>
        internal void SetRuntimePrismPrefab(Prism prefab) => prismPrefab = prefab;
    }
}
