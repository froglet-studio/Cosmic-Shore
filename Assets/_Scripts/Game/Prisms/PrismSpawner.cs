using Unity.Entities;
using Unity.Transforms;
using UnityEngine;


namespace CosmicShore
{
    public class PrismSpawner : MonoBehaviour
    {
        [Tooltip("Drag in your GameObject Prism prefab here")]
        public GameObject PrismGameObject;

        [Tooltip("Seconds between spawns")]
        public float SpawnInterval = 0.5f;

        float _timer;
        Entity _prismEntityPrefab;
        EntityManager _em;

        void Awake()
        {
            // 1) Grab the running World�s EntityManager
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;

            /*// 2) Convert the GameObject prefab into an Entity prefab
            var settings = GameObjectConversionSettings.FromWorld(
                World.DefaultGameObjectInjectionWorld,
                GameObjectConversionUtility.GlobalSettings);
            _prismEntityPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(
                PrismGameObject, settings);*/

            // Start the spawn timer
            _timer = SpawnInterval;
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = SpawnInterval;

            // 3) Instantiate the prism entity
            // var prismEnt = _em.Instantiate(_prismEntityPrefab);

            // 4) Copy this vessel�s position into the prism�s LocalTransform
            var pos = transform.position;
            // _em.SetComponentData(prismEnt, LocalTransform.FromPosition(pos));
        }
    }
}
