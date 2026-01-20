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
        public List<CellModifier> CellModifiers = new List<CellModifier>();

        [Header("Flora and Fauna (Supported Types)")]
        public List<FloraConfiguration> SupportedFlora = new List<FloraConfiguration>();
        public List<PopulationConfiguration> SupportedFauna = new List<PopulationConfiguration>();

        [Serializable]
        public class LifeFormConfiguration
        {
            [Header("Initial Delays")]
            [Tooltip("Wait this many seconds after the crystal spawns before flora begins spawning.")]
            [Min(0f)] public float FloraInitialDelaySeconds = 0f;
            [Tooltip("Wait this many seconds after the crystal spawns before fauna begins spawning.")]
            [Min(0f)] public float FaunaInitialDelaySeconds = 0f;
            [Header("Spawn Timing")]
            [Tooltip("Seconds between each flora spawn (within the initial flora batch). 0 = spawn all instantly.")]
            [Min(0f)] public float FloraSpawnIntervalSeconds = 0f;
            [Tooltip("Seconds between each population spawn (within the initial fauna batch). 0 = spawn all instantly.")]
            [Min(0f)] public float FaunaSpawnIntervalSeconds = 0f;

            [Header("Domain")]
            [Tooltip("If ON, Jade can be chosen for spawned flora domains. If OFF, Jade is excluded.")]
            public bool SpawnJade = true;
        }


        [Tooltip("Used ONLY when CellTypeChoiceOptions = IntensityWise.")]
        public LifeFormConfiguration IntensityLifeFormConfiguration = new LifeFormConfiguration();
    }
}
