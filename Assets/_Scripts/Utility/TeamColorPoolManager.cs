using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Utilities;


namespace CosmicShore.Game
{
    public class TeamColorPoolManager : PoolManagerBase
    {
        private const string FossilTag = "FossilPrism";


        [Header("Event Channels")]
        [SerializeField] PrismEventChannelWithReturnSO _onFlockSpawnedEventChannel;

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

        private PrismReturnEventData OnFlockSpawnedEventRaised(PrismEventData data)
        {
            var spawnedObject = SpawnFromTeamPool(data.OwnTeam, data.Position, data.Rotation);
            return new PrismReturnEventData
            {
                SpawnedObject = spawnedObject
            };
        }

        protected override GameObject CreatePoolObject(GameObject prefab)
        {
            GameObject obj = base.CreatePoolObject(prefab);

            // Set the material based on the pool's tag
            var r = obj.GetComponent<Renderer>();
            if (!r) 
                return obj;
            
            Teams team = GetTeamFromTag(prefab.tag);
            Material teamMaterial = new Material(_themeManagerData.GetTeamExplodingBlockMaterial(team));
            r.material = teamMaterial;

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
                _ => $"{FossilTag}_Gold",                   // This should never happen
            };
        }
    }
}