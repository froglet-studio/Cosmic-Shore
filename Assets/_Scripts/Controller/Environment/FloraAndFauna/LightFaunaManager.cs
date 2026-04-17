using UnityEngine;
using System.Collections;
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

        [Header("Aggression Response")]
        [Tooltip("Spawn-count multiplier indexed by CellAggressionLevel: [Calm, Elevated, Stressed, Critical]. " +
                 "Higher values grow the pack under stress so prisms get consumed faster. Cap prevents blowing up GC / physics.")]
        [SerializeField] float[] spawnCountByAggression = { 1f, 1.35f, 1.8f, 2.25f };
        [Tooltip("Absolute cap on active LightFauna regardless of aggression, so cleanup never exacerbates the load it's relieving.")]
        [SerializeField, Min(1)] int maxActiveFauna = 48;
        [Tooltip("Seconds between aggression-driven reinforcement checks (adds fauna only if we're short of the current target count).")]
        [SerializeField, Min(0.25f)] float reinforcementCheckInterval = 2f;

        private readonly List<LightFauna> activeFauna = new();
        private Coroutine _reinforcementRoutine;

        protected override void Start()
        {
            base.Start();
            SpawnGroup();

            if (cell != null)
                cell.OnAggressionChanged += HandleAggressionChanged;

            _reinforcementRoutine = StartCoroutine(ReinforcementLoop());
        }

        protected virtual void OnDestroy()
        {
            if (cell != null)
                cell.OnAggressionChanged -= HandleAggressionChanged;

            if (_reinforcementRoutine != null)
                StopCoroutine(_reinforcementRoutine);
        }

        void HandleAggressionChanged(CellAggressionLevel level)
        {
            // Immediate reinforcement when stress jumps (don't wait for interval tick).
            Reinforce();
        }

        IEnumerator ReinforcementLoop()
        {
            var wait = new WaitForSeconds(reinforcementCheckInterval);
            while (true)
            {
                yield return wait;
                Reinforce();
            }
        }

        int GetTargetActiveCount()
        {
            if (!managerData) return 0;

            int baseCount = Mathf.Max(0, managerData.spawnCount);
            int idx = 0;
            if (cell != null && spawnCountByAggression != null && spawnCountByAggression.Length > 0)
                idx = Mathf.Clamp((int)cell.AggressionLevel, 0, spawnCountByAggression.Length - 1);

            float mult = (spawnCountByAggression != null && spawnCountByAggression.Length > 0)
                ? Mathf.Max(0f, spawnCountByAggression[idx])
                : 1f;

            int target = Mathf.CeilToInt(baseCount * mult);
            return Mathf.Min(target, Mathf.Max(1, maxActiveFauna));
        }

        void Reinforce()
        {
            // Prune dead refs.
            for (int i = activeFauna.Count - 1; i >= 0; i--)
                if (!activeFauna[i]) activeFauna.RemoveAt(i);

            int target = GetTargetActiveCount();
            int deficit = target - activeFauna.Count;
            if (deficit <= 0) return;

            SpawnAdditional(deficit);
        }

        void SpawnAdditional(int count)
        {
            if (!managerData || !lightFaunaPrefab) return;
            if (count <= 0) return;

            float radius = Mathf.Max(0f, managerData.spawnRadius);
            for (int i = 0; i < count; i++)
            {
                if (activeFauna.Count >= maxActiveFauna) break;

                Vector3 randomOffset = Random.insideUnitSphere * radius;
                randomOffset.y = 0f;

                Vector3 spawnPosition = transform.position + randomOffset;

                LightFauna fauna = Instantiate(lightFaunaPrefab, spawnPosition, Random.rotation, transform);
                fauna.domain = domain;
                fauna.LightFaunaManager = this;
                fauna.Phase = managerData.phaseIncrease * (activeFauna.Count + i);
                fauna.Initialize(cell);

                activeFauna.Add(fauna);
            }
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

            int target = GetTargetActiveCount();
            int deficit = Mathf.Max(0, target - activeFauna.Count);
            SpawnAdditional(deficit);

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

            // Reinforcement against the aggression-scaled target (not just the base spawnCount).
            int target = GetTargetActiveCount();
            if (activeFauna.Count < Mathf.Max(1, target / 2))
                Reinforce();
        }
    }
}
