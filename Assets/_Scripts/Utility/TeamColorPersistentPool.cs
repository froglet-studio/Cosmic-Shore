using UnityEngine;
using CosmicShore.Core;
using System.Collections;
using CosmicShore.Utilities;
using System;


namespace CosmicShore.Game
{
    public class TeamColorPersistentPool : PoolManagerBase
    {

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
                Teams team = Teams.Jade; // Default

                // Determine team from tag
                if (prefab.tag.Contains("Ruby")) team = Teams.Ruby;
                else if (prefab.tag.Contains("Gold")) team = Teams.Gold;
                else if (prefab.tag.Contains("Blue")) team = Teams.Blue;

                // Create and set the material
                Material teamMaterial = new Material(_themeManagerData.GetTeamExplodingBlockMaterial(team));
                renderer.material = teamMaterial;
            }

            return obj;
        }

        protected override void InitializePoolDictionary()
        {
            // Initialize default pool
            AddConfigData(fossilBlockPrefab, poolSizePerTeam);

            // Temporarily modify the tag for each team
            string originalTag = fossilBlockPrefab.tag;

            fossilBlockPrefab.tag = "FossilPrism_Ruby";
            AddConfigData(fossilBlockPrefab, poolSizePerTeam);

            fossilBlockPrefab.tag = "FossilPrism_Gold";
            AddConfigData(fossilBlockPrefab, poolSizePerTeam);

            fossilBlockPrefab.tag = "FossilPrism_Blue";
            AddConfigData(fossilBlockPrefab, poolSizePerTeam);

            // Restore original tag
            fossilBlockPrefab.tag = originalTag;
        }

        public GameObject SpawnFromTeamPool(Teams team, Vector3 position, Quaternion rotation)
        {
            string tag = "FossilPrism";

            // Get the appropriate pool based on team
            switch (team)
            {
                case Teams.Ruby:
                    tag = "FossilPrism_Ruby";
                    break;
                case Teams.Gold:
                    tag = "FossilPrism_Gold";
                    break;
                case Teams.Blue:
                    tag = "FossilPrism_Blue";
                    break;
            }

            return SpawnFromPool(tag, position, rotation);
        }
    }
}