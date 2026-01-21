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

        [Flags]
        public enum DomainMask
        {
            None = 0,
            Jade = 1 << 0,
            Ruby = 1 << 1,
            Gold = 1 << 2,
            Blue = 1 << 3,

            All = Jade | Ruby | Gold | Blue
        }
        
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
            [Header("Domain Rules")]
            [Tooltip("Which domains are allowed for FLORA spawned by this cell type.")]
            public DomainMask AllowedFloraDomains = DomainMask.All;

            [Tooltip("Which domains are allowed for FAUNA populations spawned by this cell type.")]
            public DomainMask AllowedFaunaDomains = DomainMask.All;

            [Tooltip("If ON, spawned fauna populations will never use the local player's domain.")]
            public bool FaunaExcludeLocalDomain = true;
            [Header("Domain")]
            [Tooltip("If ON, Jade can be chosen for spawned flora domains. If OFF, Jade is excluded.")]
            public bool SpawnJade = true;
        }


        [Tooltip("Used ONLY when CellTypeChoiceOptions = IntensityWise.")]
        public LifeFormConfiguration IntensityLifeFormConfiguration = new LifeFormConfiguration();
    }
}
