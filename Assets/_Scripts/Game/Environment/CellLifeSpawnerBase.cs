using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class CellLifeSpawnerBase : ICellLifeSpawner
    {
        readonly List<Coroutine> _running = new();

        public void Start(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            Stop(host);

            if (!Validate(host, config, runtime, gameData))
                return;

            OnStart(host, config, runtime, gameData);
        }

        public void Stop(Cell host)
        {
            if (!host) return;

            for (int i = 0; i < _running.Count; i++)
            {
                var c = _running[i];
                if (c != null) host.StopCoroutine(c);
            }
            _running.Clear();

            OnStop(host);
        }

        protected abstract void OnStart(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData);
        protected virtual void OnStop(Cell host) { }

        protected bool Validate(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            if (!host) { Debug.LogError("[CellLifeSpawner] Host is null."); return false; }
            if (!config) { Debug.LogError($"[CellLifeSpawner] Config is null for host '{host.name}'."); return false; }
            if (!runtime) { Debug.LogError($"[CellLifeSpawner] Runtime is null for host '{host.name}'."); return false; }
            if (!gameData) { Debug.LogError($"[CellLifeSpawner] GameData is null for host '{host.name}'."); return false; }
            return true;
        }

        protected Coroutine Track(Cell host, IEnumerator routine)
        {
            if (!host || routine == null) return null;
            var c = host.StartCoroutine(routine);
            if (c != null) _running.Add(c);
            return c;
        }

        protected void RegisterSpawned(Cell host, GameObject go)
        {
            if (!host || !go) return;
            host.RegisterSpawnedObject(go);
        }

        protected Domains GetLocalDomainOr(GameDataSO gameData, Domains fallback) =>
            gameData?.LocalRoundStats?.Domain ?? fallback;

        protected Domains? GetExcludedDomain(bool excludeLocal, GameDataSO gameData, Domains fallbackLocal)
        {
            if (!excludeLocal) return null;
            return GetLocalDomainOr(gameData, fallbackLocal);
        }

        protected Domains PickRandomDomain(Domains? excluded)
        {
            var candidates = new List<Domains>(4) { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };
            if (excluded.HasValue) candidates.Remove(excluded.Value);

            return candidates.Count == 0
                ? Domains.Jade
                : candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        protected T PickWeighted<T>(IReadOnlyList<T> items, Func<T, float> weightSelector)
        {
            if (items == null || items.Count == 0) return default;

            float total = 0f;
            for (int i = 0; i < items.Count; i++)
                total += Mathf.Max(0f, weightSelector(items[i]));

            if (total <= 0f) return items[0];

            float roll = UnityEngine.Random.value * total;
            float cumulative = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                cumulative += Mathf.Max(0f, weightSelector(item));
                if (roll <= cumulative) return item;
            }
            return items[^1];
        }

        /// <summary>
        /// Correct probability roll. p = 0..1.
        /// </summary>
        protected bool AllowSpawn(float spawnProbability) =>
            UnityEngine.Random.value < Mathf.Clamp01(spawnProbability);

        protected bool TryGetCrystalGoal(CellRuntimeDataSO runtime, out Vector3 goal)
        {
            goal = default;
            if (!runtime) return false;

            var t = runtime.CrystalTransform;
            if (!t) return false;

            goal = t.position;
            return true;
        }

        protected Flora SpawnFlora(Cell host, Flora floraPrefab, Domains? excludedDomain)
        {
            if (!host || !floraPrefab) return null;

            var flora = UnityEngine.Object.Instantiate(floraPrefab, host.transform.position, Quaternion.identity);
            flora.domain = PickRandomDomain(excludedDomain);
            flora.Initialize(host);

            RegisterSpawned(host, flora.gameObject);
            return flora;
        }

        protected Fauna SpawnFauna(Cell host, Fauna faunaPrefab, Vector3 goal, Domains? excludedDomain)
        {
            if (!host || !faunaPrefab) return null;

            var pop = UnityEngine.Object.Instantiate(faunaPrefab, host.transform.position, Quaternion.identity);
            pop.domain = PickRandomDomain(excludedDomain);
            pop.Goal = goal;

            RegisterSpawned(host, pop.gameObject);
            return pop;
        }

        protected float GetControllingVolume(GameDataSO gameData) =>
            gameData.GetControllingTeamStatsBasedOnVolumeRemaining().Item2;

        protected IEnumerator RunSpawnLoop(Func<bool> shouldSpawn, Action spawnOnce, Func<float> getWaitSeconds)
        {
            while (true)
            {
                if (shouldSpawn())
                    spawnOnce?.Invoke();

                var wait = Mathf.Max(0f, getWaitSeconds?.Invoke() ?? 0f);
                if (wait <= 0f) yield return null;
                else yield return new WaitForSeconds(wait);
            }
        }

        protected IEnumerator RunThresholdLoop(Func<bool> condition, Action spawnOnce, Func<float> trueWait, Func<float> falseWait)
        {
            while (true)
            {
                if (condition())
                {
                    spawnOnce?.Invoke();
                    var w = Mathf.Max(0f, trueWait?.Invoke() ?? 0f);
                    if (w <= 0f) yield return null;
                    else yield return new WaitForSeconds(w);
                }
                else
                {
                    var w = Mathf.Max(0f, falseWait?.Invoke() ?? 0f);
                    if (w <= 0f) yield return null;
                    else yield return new WaitForSeconds(w);
                }
            }
        }
    }
}