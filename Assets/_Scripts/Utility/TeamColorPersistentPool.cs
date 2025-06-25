using UnityEngine;
using CosmicShore.Core;
using System.Collections;
using CosmicShore.Utilities;
using System;


namespace CosmicShore.Game
{
    public class TeamColorPersistentPool : PoolManagerBase
    {

        private const string FossilTag = "FossilPrism";

        [SerializeField] private GameObject fossilBlockPrefab;
        [SerializeField] private int poolSizePerTeam = 750; // 750 per team = 3000 total

        [Header("Event Channels")]
        [SerializeField] TrailBlockEventChannelWithReturnSO _onFlockSpawnedEventChannel;

        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;

        private void OnEnable()
        {
            _onFlockSpawnedEventChannel.OnEventReturn += OnFlockSpawnedEventRaised;
        }

        private void OnDisable()
        {
            _onFlockSpawnedEventChannel.OnEventReturn -= OnFlockSpawnedEventRaised;
        }

        private TrailBlockReturnEventData OnFlockSpawnedEventRaised(TrailBlockEventData data)
        {
            var spawnedObject = SpawnFromTeamPool(data.Team, data.Position, data.Rotation);
            return new TrailBlockReturnEventData
            {
                SpawnedObject = spawnedObject
            };
        }

        protected override GameObject CreatePoolObject(GameObject prefab)
        {
            GameObject obj = base.CreatePoolObject(prefab);

            // Set the material based on the pool's tag
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Teams team = GetTeamFromTag(prefab.tag);
                Material teamMaterial = new Material(_themeManagerData.GetTeamExplodingBlockMaterial(team));
                renderer.material = teamMaterial;
            }

            return obj;
        }

        private static Teams GetTeamFromTag(string tag)
        {
            if (tag.Contains("Ruby")) return Teams.Ruby;
            if (tag.Contains("Gold")) return Teams.Gold;
            if (tag.Contains("Blue")) return Teams.Blue;
            if (tag.Contains("Jade")) return Teams.Jade;

            return Teams.Jade;
        }

        protected override void InitializePoolDictionary()
        {
            // Initialize default pool
            // AddConfigData(fossilBlockPrefab, poolSizePerTeam);

            string originalTag = fossilBlockPrefab.tag;

            // Create team specific pools
            fossilBlockPrefab.tag = GetTagForTeam(Teams.Jade);
            AddConfigData(fossilBlockPrefab, poolSizePerTeam);

            fossilBlockPrefab.tag = GetTagForTeam(Teams.Ruby);
            AddConfigData(fossilBlockPrefab, poolSizePerTeam);

            fossilBlockPrefab.tag = GetTagForTeam(Teams.Gold);
            AddConfigData(fossilBlockPrefab, poolSizePerTeam);

            fossilBlockPrefab.tag = GetTagForTeam(Teams.Blue);
            AddConfigData(fossilBlockPrefab, poolSizePerTeam);

            // Restore original tag
            fossilBlockPrefab.tag = originalTag;
        }

        public GameObject SpawnFromTeamPool(Teams team, Vector3 position, Quaternion rotation)
        {
            string tag = GetTagForTeam(team);
            return SpawnFromPool(tag, position, rotation);
        }

        private static string GetTagForTeam(Teams team)
        {
            return team switch
            {
                Teams.Ruby => $"{FossilTag}_Ruby",
                Teams.Gold => $"{FossilTag}_Gold",
                Teams.Blue => $"{FossilTag}_Blue",
                Teams.Jade => $"{FossilTag}_Jade",
                _ => FossilTag,
            };
        }
    }
}