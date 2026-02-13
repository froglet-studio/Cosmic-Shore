using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Game;

namespace CosmicShore
{
    public class LightFaunaManager : Fauna
    {
        [Header("Prefab (keep here)")]
        [SerializeField] LightFauna lightFaunaPrefab;

        [Header("Data (all tuning lives here)")]
        [SerializeField] LightFaunaManagerDataSO managerData;

        private readonly List<LightFauna> activeFauna = new List<LightFauna>();

        protected override void Start()
        {
            base.Start();
            SpawnGroup();
        }

        public override void Initialize(Cell cell)
        {
            throw new System.NotImplementedException();
        }

        protected override void Spawn()
        {
            throw new System.NotImplementedException();
        }

        protected override void Die(string killername = "")
        {
            throw new System.NotImplementedException();
        }

        void SpawnGroup()
        {
            if (!managerData)
            {
                Debug.LogError($"{nameof(LightFaunaManager)} on {name} is missing {nameof(LightFaunaManagerDataSO)}.");
                return;
            }

            if (!lightFaunaPrefab)
            {
                Debug.LogError($"{nameof(LightFaunaManager)} on {name} is missing LightFauna prefab reference.");
                return;
            }

            int count = Mathf.Max(0, managerData.spawnCount);
            float radius = Mathf.Max(0f, managerData.spawnRadius);

            for (int i = 0; i < count; i++)
            {
                Vector3 randomOffset = Random.insideUnitSphere * radius;
                randomOffset.y = 0f;

                Vector3 spawnPosition = transform.position + randomOffset;

                LightFauna fauna = Instantiate(lightFaunaPrefab, spawnPosition, Random.rotation, transform);
                fauna.domain = domain;
                fauna.LightFaunaManager = this;
                fauna.Phase = managerData.phaseIncrease * i;
                fauna.Initialize(cell);

                activeFauna.Add(fauna);
            }

            // Formation
            if (activeFauna.Count > 0)
            {
                float spread = Mathf.Max(0f, managerData.formationSpread);

                for (int i = 0; i < activeFauna.Count; i++)
                {
                    float angle = (i * 360f / activeFauna.Count) * Mathf.Deg2Rad;
                    Vector3 formationOffset = new Vector3(
                        Mathf.Cos(angle) * spread,
                        0f,
                        Mathf.Sin(angle) * spread
                    );

                    activeFauna[i].transform.position = transform.position + formationOffset;
                }
            }
        }

        public void RemoveFauna(LightFauna fauna)
        {
            if (activeFauna.Contains(fauna))
            {
                activeFauna.Remove(fauna);
                Destroy(fauna.gameObject);
            }

            // Optional: Respawn if count gets too low (kept behavior)
            if (managerData && activeFauna.Count < Mathf.Max(0, managerData.spawnCount / 2))
            {
                SpawnGroup();
            }
        }
    }
}
