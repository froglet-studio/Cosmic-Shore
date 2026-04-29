using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.Gameplay
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

            int count = ComputeBatchSize();
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

            // Replenish when the live count drops below half of what cell load currently
            // calls for. This makes the trigger respond to prism availability — a cell
            // saturated with prisms repopulates fauna sooner than a sparse one.
            if (managerData && activeFauna.Count < ComputeBatchSize() / 2)
                SpawnGroup();
        }

        /// <summary>
        /// Target batch size as a function of live prism load in the host cell.
        /// Implements the Fauna fundamental's response to its food supply: more food → more
        /// fauna → more consumption → equilibrium. Falls back to the static spawnCount
        /// baseline when no cell is wired or extraFaunaPerHundredPrisms is zero.
        /// </summary>
        int ComputeBatchSize()
        {
            if (!managerData) return 0;

            int baseCount = Mathf.Max(0, managerData.spawnCount);
            int extra = 0;

            // Guard cellData explicitly — Fauna.cell property dereferences cellData.Cell
            // and would NRE if the SO link isn't wired in the inspector.
            if (cellData != null && cellData.Cell != null && managerData.extraFaunaPerHundredPrisms > 0)
                extra = (cellData.Cell.LiveBlockCount / 100) * managerData.extraFaunaPerHundredPrisms;

            int total = baseCount + extra;

            int ceiling = Mathf.Max(0, managerData.maxFaunaPerGroup);
            if (ceiling > 0 && total > ceiling) total = ceiling;

            return total;
        }
    }
}
