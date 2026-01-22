using UnityEngine;
using System;
using System.Collections.Generic;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "New Cell Type", menuName = "Cosmic Shore/Cell Type")]
    public class SO_CellType : ScriptableObject
    {
        [Header("AppShell Properties")]
        public string CellName;
        public string Description;
        public Sprite Icon;

        [Header("Cell Properties")]
        public float Difficulty;

        [Header("Visual Properties")]
        public GameObject MembranePrefab;
        public GameObject NucleusPrefab;
        public SnowChanger CytoplasmPrefab;

        [Header("Mechanical Properties")]
        public List<CellModifier> CellModifiers = new();

        [Header("Flora and Fauna (Supported Types)")]
        public List<FloraConfiguration> SupportedFlora = new();
        public List<PopulationConfiguration> SupportedFauna = new();

        [Serializable]
        public class LifeFormConfiguration
        {
            [Header("Initial Delays")]
            [Tooltip("Wait this many seconds after the crystal spawns before flora begins spawning.")]
            [Min(0f)] public float FloraInitialDelaySeconds;
            [Tooltip("Wait this many seconds after the crystal spawns before fauna begins spawning.")]
            [Min(0f)] public float FaunaInitialDelaySeconds;

            [Header("Spawn Timing")]
            [Tooltip("Seconds between each flora spawn (within the initial flora batch). 0 = spawn all instantly.")]
            [Min(0f)] public float FloraSpawnIntervalSeconds;
            [Tooltip("Seconds between each population spawn (within the initial fauna batch). 0 = spawn all instantly.")]
            [Min(0f)] public float FaunaSpawnIntervalSeconds;

            [Header("Domain Rules")]
            [Tooltip("If ON, spawned flora will never use the local player's domain.")]
            public bool FloraExcludeLocalDomain;
            [Tooltip("If ON, spawned fauna populations will never use the local player's domain.")]
            public bool FaunaExcludeLocalDomain;
        }

        [Tooltip("Used ONLY when CellTypeChoiceOptions = IntensityWise.")]
        public LifeFormConfiguration IntensityLifeFormConfiguration;
    }
}
