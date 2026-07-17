using System;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>Lifeform Matrix</b> toy - the ecology tuning bench. Fly through the station and a
    /// matrix of every registered lifeform SPECIES blooms in beside it. Fly through a species
    /// and a second matrix blooms: that species' variants, 4 elements x levels {1, 3, 5}
    /// (the extremes and the middle of the 4x5 contract). Fly through a variant and that exact
    /// lifeform spawns live into the containing cell - the fastest way to see the whole range
    /// of each element and tune it into a good place.
    ///
    /// Toy-faithful: no score, no end condition, no timers. Spawned lifeforms are ordinary
    /// citizens of the cell's food web (they feed, starve, reproduce, drop crystals).
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

        [Header("Layout")]
        [SerializeField, Min(10f), Tooltip("Spacing between stations in a matrix row/column.")]
        float stationSpacing = 45f;
        [SerializeField, Min(1f), Tooltip("Body radius of a station sphere (variant stations also " +
                                          "scale with level so level 5 reads biggest).")]
        float stationRadius = 6f;

        // NOTE: elements have SHAPE signatures, not colour signatures (colour belongs to
        // DOMAINS). Stations identify their element with the element's crystal MODEL - the
        // canonical in-world shape signature - never with a per-element tint.

        public FaunaSpecies[] Fauna => faunaSpecies;
        public FloraSpecies[] Flora => floraSpecies;
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
