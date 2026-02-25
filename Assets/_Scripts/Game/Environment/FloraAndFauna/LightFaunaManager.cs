using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Game;
using CosmicShore.Utility;

namespace CosmicShore
{
    /// <summary>
    /// Manages a group of <see cref="LightFauna"/> creatures.
    /// Handles spawning, formation layout, and population maintenance.
    /// Extends Fauna for domain/goal propagation from the spawning system (LSP-compliant:
    /// lifecycle methods use base defaults instead of throwing NotImplementedException).
    /// </summary>
    public class LightFaunaManager : Fauna
    {
        [Header("Prefab")]
        [SerializeField] LightFauna lightFaunaPrefab;

        [Header("Data")]
        [SerializeField] LightFaunaManagerDataSO managerData;

        private readonly List<LightFauna> activeFauna = new();

        protected override void Start()
        {
            base.Start();
            SpawnGroup();
        }

        void SpawnGroup()
        {
            if (!managerData)
            {
                CSDebug.LogError($"{nameof(LightFaunaManager)} on {name} is missing {nameof(LightFaunaManagerDataSO)}.");
                return;
            }

            if (!lightFaunaPrefab)
            {
                CSDebug.LogError($"{nameof(LightFaunaManager)} on {name} is missing LightFauna prefab reference.");
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

            ApplyFormation();
        }

        void ApplyFormation()
        {
            if (activeFauna.Count == 0) return;

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

        public void RemoveFauna(LightFauna fauna)
        {
            if (activeFauna.Contains(fauna))
            {
                activeFauna.Remove(fauna);
                Destroy(fauna.gameObject);
            }

            if (managerData && activeFauna.Count < Mathf.Max(0, managerData.spawnCount / 2))
                SpawnGroup();
        }
    }
}
