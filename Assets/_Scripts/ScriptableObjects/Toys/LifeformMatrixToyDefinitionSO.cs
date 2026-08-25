using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>Lifeform Matrix</b> toy - the bench for everything you can RELEASE into the cell.
    ///
    /// Fly the toy and three KINGDOM switches bloom out ahead: <b>Fauna</b>, <b>Flora</b> and
    /// <b>Vessels</b>. Fly Fauna or Flora and that kingdom's SPECIES matrix blooms a layer
    /// further out; fly a species and its VARIANT matrix blooms further still - 4 elements x
    /// levels {1, 3, 5} (the extremes and the middle of the 4x5 contract) - and flying a variant
    /// spawns that exact lifeform live into the containing cell. Fly Vessels and a matrix of mini
    /// hulls blooms instead; flying one releases an <b>AI-piloted vessel of that class in your own
    /// domain</b> through the menu's ordinary networked spawn pipeline.
    ///
    /// Toy-faithful: no score, no end condition, no timers. Everything released is an ordinary
    /// citizen - lifeforms feed, starve, reproduce and drop crystals; a companion vessel flies the
    /// lava lamp like any other pilot and lays conserved trail mass the food web can graze.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_LifeformMatrix", menuName = "ScriptableObjects/Toys/Lifeform Matrix Toy")]
    public class LifeformMatrixToyDefinitionSO : ToyDefinitionSO
    {
        [Serializable]
        public class FaunaSpecies
        {
            [Tooltip("Species name shown on its station label.")]
            public string Name = "Tadpole";
            [Tooltip("The per-element configs of this species (one per element it can express). " +
                     "The variant matrix reads each config's Element; levels are applied on a " +
                     "runtime clone, so the assets are never mutated.")]
            public FaunaConfigurationSO[] ElementConfigs;
        }

        [Serializable]
        public class FloraSpecies
        {
            [Tooltip("Species name shown on its station label.")]
            public string Name = "Gyroid";
            [Tooltip("The per-element configs of this species (one per element it can express).")]
            public FloraConfigurationSO[] ElementConfigs;
        }

        [Header("Menagerie")]
        [SerializeField] FaunaSpecies[] faunaSpecies;
        [SerializeField] FloraSpecies[] floraSpecies;

        [Header("Hangar")]
        [SerializeField, Tooltip("Vessel classes offered by the VESSELS branch, each a mini hull. " +
                                 "Flying one releases an AI companion of that class in your own " +
                                 "domain. Leave empty for the shared curated default roster " +
                                 "(ToyVesselRoster.Default) - the same list the vessel changer uses.")]
        VesselClassType[] vesselRoster;

        [Header("Layout")]
        [SerializeField, Min(10f), Tooltip("Spacing between stations in a matrix row/column.")]
        float stationSpacing = 90f;
        [SerializeField, Min(1f), Tooltip("Body radius of a station (a species station shows a mini " +
                                          "MODEL of its creature; variant stations show the element's " +
                                          "crystal and scale with level so level 5 reads biggest).")]
        float stationRadius = 12f;

        // NOTE: elements have SHAPE signatures, not colour signatures (colour belongs to
        // DOMAINS). Stations identify their element with the element's crystal MODEL - the
        // canonical in-world shape signature - never with a per-element tint.

        public FaunaSpecies[] Fauna => faunaSpecies;
        public FloraSpecies[] Flora => floraSpecies;
        public VesselClassType[] VesselRoster => vesselRoster;
        public float StationSpacing => stationSpacing;
        public float StationRadius => stationRadius;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor, DisplayName);
            var toy = go.AddComponent<LifeformMatrixToy>();
            toy.Configure(this);
            toy.Initialize(this, context, placement);
        }
    }
}
