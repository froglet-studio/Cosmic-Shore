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

        const float MaybeSpawnPollInterval = 0.5f;
        float _nextMaybeSpawnAt;

        protected override void Start()
        {
            base.Start();

            // Initial spawn only happens if the cell already holds mass (e.g., a
            // designer placed the manager in a scene that starts populated, or a replay
            // reset began with prisms present). Otherwise the OnPhaseChanged subscription
            // below triggers spawn the moment the cell starts accumulating mass.
            MaybeSpawnGroup();
        }

        void OnEnable()
        {
            // Match the project's SOAP wiring pattern: guard the runtime SO ref but
            // fail loud on a missing event ref (CLAUDE.md SOAP policy).
            if (cellData != null)
                cellData.OnPhaseChanged.OnRaised += HandleCellPhaseChanged;
        }

        void OnDisable()
        {
            if (cellData != null)
                cellData.OnPhaseChanged.OnRaised -= HandleCellPhaseChanged;
        }

        void Update()
        {
            // Poll fallback: covers two scenarios where OnPhaseChanged won't reach us -
            // (1) the SOAP event asset isn't wired into CellRuntimeDataSO so Raise NREs
            // before the listener gets called, and (2) the manager started after the
            // cell already began accumulating mass (subscription missed the transition).
            // Cheap: a FaunaSpawningEnabled check + a Count check after the interval gate.
            if (Time.time < _nextMaybeSpawnAt) return;
            _nextMaybeSpawnAt = Time.time + MaybeSpawnPollInterval;
            MaybeSpawnGroup();
        }

        void HandleCellPhaseChanged(CellPhase newPhase)
        {
            // Any phase change re-evaluates seeding; MaybeSpawnGroup gates on the cell
            // actually holding mass (FaunaSpawningEnabled). A phase falling back never
            // culls - existing fauna stay alive and keep consuming until removed.
            MaybeSpawnGroup();
        }

        void MaybeSpawnGroup()
        {
            if (activeFauna.Count > 0) return;
            if (!cell || !cell.FaunaSpawningEnabled) return;
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

            // Fauna spawn in the cell's dominant domain - the live leader by per-domain
            // prism count. Falls back to the manager's own domain when the cell hasn't
            // accrued enough prisms to pick a leader (DominantDomain returns Blue, the
            // "no team" sentinel, on an empty cell, but we shouldn't be spawning there
            // anyway thanks to the phase gate). Existing fauna keep their assigned domain
            // even as the cell's dominant shifts - only newly-spawned fauna track it.
            Domains spawnDomain = cell ? cell.DominantDomain : Domains.Blue;
            if (spawnDomain == Domains.Blue) spawnDomain = domain;

            for (int i = 0; i < count; i++)
            {
                Vector3 randomOffset = Random.insideUnitSphere * radius;
                randomOffset.y = 0f;

                Vector3 spawnPosition = transform.position + randomOffset;

                LightFauna fauna = Instantiate(lightFaunaPrefab, spawnPosition, Random.rotation, transform);
                fauna.domain = spawnDomain;
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
            // calls for. This makes the trigger respond to prism availability - a cell
            // saturated with prisms repopulates fauna sooner than a sparse one.
            if (managerData && activeFauna.Count < ComputeBatchSize() / 2)
                SpawnGroup();
        }

        /// <summary>
        /// Target batch size as a function of live prism load in the host cell.
        /// Returns 0 in an empty cell (no mass to host fauna), then layers the
        /// per-100-prism scaling on top once the cell holds mass so consumption keeps
        /// pace with growth. Falls back to the static spawnCount baseline when no cell
        /// is wired or extraFaunaPerHundredPrisms is zero.
        /// </summary>
        int ComputeBatchSize()
        {
            if (!managerData) return 0;

            // Spawn floor: no fauna in an empty cell. The manager's OnPhaseChanged
            // subscription re-triggers SpawnGroup once the cell starts accumulating mass.
            if (cellData != null && cellData.Cell != null && !cellData.Cell.FaunaSpawningEnabled)
                return 0;

            int baseCount = Mathf.Max(0, managerData.spawnCount);
            int extra = 0;

            // Guard cellData explicitly - Fauna.cell property dereferences cellData.Cell
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
