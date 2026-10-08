using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>Spawn Matrix</b> toy - the bench for everything you can RELEASE into the cell.
    ///
    /// Fly the toy and three KINGDOM switches bloom out ahead: <b>Fauna</b>, <b>Flora</b> and
    /// <b>Vessels</b>. Fly Fauna or Flora and that kingdom's SPECIES matrix blooms a layer
    /// further out; fly a species and its VARIANT row blooms further still - its four ELEMENTS,
    /// which is the whole of what a lifeform varies by (levels are retired: Docs/ECOSYSTEM.md
    /// 40) - and flying a variant spawns that exact lifeform live into the containing cell. Fly Vessels and a matrix of mini
    /// hulls blooms instead; flying one releases an <b>AI-piloted vessel of that class in your own
    /// domain</b> through the menu's ordinary networked spawn pipeline.
    ///
    /// Toy-faithful: no score, no end condition, no timers. Everything released is an ordinary
    /// citizen - lifeforms feed, starve, reproduce and drop crystals; a companion vessel flies the
    /// lava lamp like any other pilot and lays conserved trail mass the food web can graze.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_SpawnMatrix", menuName = "ScriptableObjects/Toys/Spawn Matrix Toy")]
    public class SpawnMatrixToyDefinitionSO : ToyDefinitionSO
    {
        [Serializable]
        public class FaunaSpecies
        {
            [Tooltip("Species name, shown in the Toy Box menu.")]
            public string Name = "Tadpole";
            [Tooltip("The per-element configs of this species (one per element it can express). " +
                     "The variant row reads each config's Element; a release runs off a runtime " +
                     "clone, so the assets are never mutated.")]
            public FaunaConfigurationSO[] ElementConfigs;
        }

        [Serializable]
        public class FloraSpecies
        {
            [Tooltip("Species name, shown in the Toy Box menu.")]
            public string Name = "Gyroid";
            [Tooltip("The per-element configs of this species (one per element it can express).")]
            public FloraConfigurationSO[] ElementConfigs;
        }

        [Serializable]
        public class SpeciesDescription
        {
            [Tooltip("The species row this describes - matches FaunaSpecies.Name / FloraSpecies.Name. " +
                     "Not itself called Name: other generators find their rows in the asset by " +
                     "'- Name: <row>' across the whole file, and must never match a description.")]
            public string Species;
            [TextArea(2, 5), Tooltip("What distinguishes this species: how it looks or grows, where " +
                                     "it lives, what it eats or does to a pilot, what it pays.")]
            public string Description;
        }

        [Header("Menagerie")]
        [SerializeField] FaunaSpecies[] faunaSpecies;
        [SerializeField] FloraSpecies[] floraSpecies;
        [SerializeField, Tooltip("One description per species row, shown in the Toy Box while the " +
                                 "species (or one of its elements) is picked. Kept beside the rows " +
                                 "rather than in them because several generators own rows; " +
                                 "Tools/Build/author_spawn_matrix_roster.py owns this list and its " +
                                 "--check fails on a row with no description.")]
        SpeciesDescription[] speciesDescriptions;

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
                                          "MODEL of its creature; variant stations show the " +
                                          "element's crystal at that element's own authored heart " +
                                          "size, so the row shows the real size difference between " +
                                          "the four before you release any of them).")]
        float stationRadius = 12f;
        [SerializeField, Min(1), Tooltip("Species stations per row before a kingdom's species " +
                                         "row wraps into a grid. Every flora and fauna is " +
                                         "registered here, so a single row would be far wider " +
                                         "than a pass can take in.")]
        int speciesPerRow = 6;

        // NOTE: elements have SHAPE signatures, not colour signatures (colour belongs to
        // DOMAINS). Stations identify their element with the element's crystal MODEL - the
        // canonical in-world shape signature - never with a per-element tint.

        /// <summary>The authored description for species row <paramref name="name"/>, or "".</summary>
        public string DescriptionOf(string name)
        {
            if (speciesDescriptions == null || string.IsNullOrEmpty(name)) return "";
            foreach (var d in speciesDescriptions)
                if (d != null && d.Species == name) return d.Description ?? "";
            return "";
        }

        public FaunaSpecies[] Fauna => faunaSpecies;
        public FloraSpecies[] Flora => floraSpecies;
        public VesselClassType[] VesselRoster => vesselRoster;
        public float StationSpacing => stationSpacing;
        public float StationRadius => stationRadius;
        public int SpeciesPerRow => speciesPerRow;

        /// <summary>It leaves POPULATIONS behind - flora, fauna and AI-piloted vessels, every one an
        /// ordinary citizen that feeds, starves, breeds and drops crystals.</summary>
        public override ToyCategory Category => ToyCategory.Creation;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor);
            var toy = go.AddComponent<SpawnMatrixToy>();
            toy.Configure(this);
            toy.Initialize(this, context, placement);
        }
    }
}
