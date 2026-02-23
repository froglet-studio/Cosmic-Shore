using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Defines how elemental buffs are applied to vessels as a comeback mechanism in a minigame.
    /// Each minigame references one of these profiles. Weights control how much each element
    /// gets buffed per unit of score difference from the leader. Vessel-specific configs allow
    /// different vessels to receive different elemental buffs in the same game.
    /// </summary>
    [CreateAssetMenu(fileName = "New Elemental Comeback Profile", menuName = "CosmicShore/Game/ElementalComebackProfile")]
    public class SO_ElementalComebackProfile : ScriptableObject
    {
        [Serializable]
        public struct VesselComebackConfig
        {
            public VesselClassType VesselClass;

            [Header("Comeback Weights (buff per unit of score difference)")]
            [Tooltip("How much the Mass element increases per unit of score difference from the leader")]
            public float MassWeight;
            [Tooltip("How much the Charge element increases per unit of score difference from the leader")]
            public float ChargeWeight;
            [Tooltip("How much the Space element increases per unit of score difference from the leader")]
            public float SpaceWeight;
            [Tooltip("How much the Time element increases per unit of score difference from the leader")]
            public float TimeWeight;

            [Header("Initial Elemental Values (per-vessel per-minigame balancing)")]
            [Tooltip("Starting Mass level for this vessel in this minigame")]
            [Range(-5, 15)] public float InitialMass;
            [Tooltip("Starting Charge level for this vessel in this minigame")]
            [Range(-5, 15)] public float InitialCharge;
            [Tooltip("Starting Space level for this vessel in this minigame")]
            [Range(-5, 15)] public float InitialSpace;
            [Tooltip("Starting Time level for this vessel in this minigame")]
            [Range(-5, 15)] public float InitialTime;

            public float GetWeight(Element element) => element switch
            {
                Element.Mass => MassWeight,
                Element.Charge => ChargeWeight,
                Element.Space => SpaceWeight,
                Element.Time => TimeWeight,
                _ => 0f
            };

            public float GetInitialLevel(Element element) => element switch
            {
                Element.Mass => InitialMass,
                Element.Charge => InitialCharge,
                Element.Space => InitialSpace,
                Element.Time => InitialTime,
                _ => 0f
            };
        }

        [SerializeField] List<VesselComebackConfig> vesselConfigs = new();

        [Header("Default Config (for vessels without a specific entry)")]
        [SerializeField] VesselComebackConfig defaultConfig;

        public VesselComebackConfig GetConfig(VesselClassType vesselClass)
        {
            for (int i = 0; i < vesselConfigs.Count; i++)
            {
                if (vesselConfigs[i].VesselClass == vesselClass)
                    return vesselConfigs[i];
            }
            return defaultConfig;
        }

        public IReadOnlyList<VesselComebackConfig> VesselConfigs => vesselConfigs;
        public VesselComebackConfig DefaultConfig => defaultConfig;
    }
}
