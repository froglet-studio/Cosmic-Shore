// File: Assets/Scripts/Game/TeamColorPoolManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Utilities; // your existing event/SO types

namespace CosmicShore.Game
{
    public class TeamColorPoolManager : PoolManagerBase
    {
        [Header("Pool Configurations")]
        [SerializeField] private PoolConfigurationSO[] poolConfigurations;

        [Header("Event Channels")]
        [SerializeField] private PrismEventChannelWithReturnSO _onFlockSpawnedEventChannel;

        [Header("Data Containers")]
        [SerializeField] private ThemeManagerDataContainerSO _themeManagerData;

        // Maps poolName (key) -> config
        private Dictionary<string, PoolConfigurationSO> configurationMap;
        // Maps instance -> config (set at creation)
        private Dictionary<GameObject, PoolConfigurationSO> objectConfigurationMap;
        // Cache for component type lookup
        private Dictionary<string, Type> componentTypeCache;

        #region Lifecycle

        private void Awake()
        {
            InitializeCaches();
            InitializeConfigurationMap();

            // create base dictionaries
            InitializePoolDictionary();

            // register pools by poolName (key)
            foreach (var cfg in poolConfigurations)
            {
                if (cfg == null || cfg.basePrefab == null || string.IsNullOrEmpty(cfg.poolName))
                    continue;

                CreateNewPool(cfg.poolName, cfg.basePrefab, Mathf.Max(1, cfg.poolSize));
            }
        }

        public override void Start()
        {
            // Awake handled setup
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            configurationMap?.Clear();
            objectConfigurationMap?.Clear();
            componentTypeCache?.Clear();
        }

        private void OnEnable()
        {
            if (_onFlockSpawnedEventChannel != null)
                _onFlockSpawnedEventChannel.OnEventReturn += OnFlockSpawnedEventRaised;
        }

        private void OnDisable()
        {
            if (_onFlockSpawnedEventChannel != null)
                _onFlockSpawnedEventChannel.OnEventReturn -= OnFlockSpawnedEventRaised;
        }

        #endregion

        #region Init Helpers

        private void InitializeCaches()
        {
            objectConfigurationMap = new Dictionary<GameObject, PoolConfigurationSO>();
            componentTypeCache = new Dictionary<string, Type>();
        }

        private void InitializeConfigurationMap()
        {
            configurationMap = new Dictionary<string, PoolConfigurationSO>();

            foreach (var config in poolConfigurations)
            {
                if (config == null || string.IsNullOrEmpty(config.poolName)) continue;

                if (configurationMap.ContainsKey(config.poolName))
                {
                    Debug.LogWarning($"[TeamColorPoolManager] Duplicate poolName '{config.poolName}'. Skipping duplicate.");
                    continue;
                }

                configurationMap[config.poolName] = config;

                // pre-cache required component types
                if (config.requiredComponents != null)
                {
                    foreach (var r in config.requiredComponents)
                        if (r != null && !string.IsNullOrEmpty(r.componentTypeName))
                            CacheComponentType(r.componentTypeName);
                }
            }
        }

        #endregion

        #region Pool Overrides

        protected override GameObject CreatePoolObject(string key, GameObject prefab, Transform parent)
        {
            var obj = base.CreatePoolObject(key, prefab, parent);
            if (obj == null) return null;

            if (configurationMap != null && configurationMap.TryGetValue(key, out var cfg))
                objectConfigurationMap[obj] = cfg;

            return obj;
        }

        #endregion

        #region Events

        private PrismReturnEventData OnFlockSpawnedEventRaised(PrismEventData data)
        {
            if (data == null)
            {
                Debug.LogError("[TeamColorPoolManager] Received null PrismEventData");
            }

            var spawnedObject = SpawnFromPool(
                data?.PoolName,
                data != null ? data.OwnTeam : default,
                data?.Position ?? Vector3.zero,
                data?.Rotation ?? Quaternion.identity
            );

            return new PrismReturnEventData { SpawnedObject = spawnedObject };
        }

        #endregion

        #region Public API (game-facing)

        public GameObject SpawnFromPool(string poolName, Teams team, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrEmpty(poolName))
            {
                Debug.LogError("[TeamColorPoolManager] poolName cannot be null or empty.");
                return null;
            }

            if (!configurationMap.TryGetValue(poolName, out var cfg))
            {
                Debug.LogWarning($"[TeamColorPoolManager] No PoolConfiguration found for '{poolName}'.");
                return null;
            }

            var obj = base.SpawnFromPool(poolName, position, rotation);
            if (obj != null) ConfigureForTeam(obj, team);
            return obj;
        }

        public void ReturnToPool(GameObject obj) => base.ReturnToPool(obj);

        // Convenience effects
        public GameObject SpawnExplosion(Teams team, Vector3 position, Quaternion rotation)
            => SpawnFromPool("Explosion", team, position, rotation);

        public GameObject SpawnImplosion(Teams team, Vector3 position, Quaternion rotation)
            => SpawnFromPool("Implosion", team, position, rotation);

        public GameObject SpawnShockwave(Teams team, Vector3 position, Quaternion rotation)
            => SpawnFromPool("Shockwave", team, position, rotation);

        public GameObject SpawnDisintegration(Teams team, Vector3 position, Quaternion rotation)
            => SpawnFromPool("Disintegration", team, position, rotation);

        public GameObject SpawnEffect(string effectName, Teams team, Vector3 position, Quaternion rotation)
            => SpawnFromPool(effectName, team, position, rotation);

        #endregion

        #region Configure Spawned Instance

        private void ConfigureForTeam(GameObject obj, Teams team)
        {
            if (obj == null) return;

            if (!objectConfigurationMap.TryGetValue(obj, out var config))
            {
                Debug.LogError($"[TeamColorPoolManager] No cached configuration for instance '{obj.name}'.");
                return;
            }

            // Get the team's material set
            SO_MaterialSet materialSet = null;
            if (_themeManagerData?.TeamMaterialSets != null)
            {
                if (!_themeManagerData.TeamMaterialSets.TryGetValue(team, out materialSet))
                    Debug.LogWarning($"[TeamColorPoolManager] No material set for team '{team}'.");
            }
            else
            {
                Debug.LogWarning("[TeamColorPoolManager] ThemeManagerData or TeamMaterialSets is null.");
            }

            // Ensure component requirements
            if (config.requiredComponents != null && config.requiredComponents.Length > 0)
            {
                foreach (var requirement in config.requiredComponents)
                    if (requirement != null) HandleComponentRequirement(obj, requirement);
            }

            // Apply visual/material configuration
            if (materialSet != null)
                config.ConfigurePoolObject(obj, materialSet);
        }

        private void HandleComponentRequirement(GameObject obj, ComponentRequirement requirement)
        {
            var componentType = GetCachedComponentType(requirement.componentTypeName);
            if (componentType == null)
            {
                Debug.LogError($"[TeamColorPoolManager] Component type '{requirement.componentTypeName}' not found.");
                return;
            }

            var existing = obj.GetComponent(componentType);

            if (requirement.ShouldRemoveExisting() && existing != null)
            {
                Destroy(existing);
                existing = null;
            }

            Component target = existing;
            if (target == null)
            {
                try { target = obj.AddComponent(componentType); }
                catch (Exception e)
                {
                    Debug.LogError($"[TeamColorPoolManager] Failed to add component '{componentType.Name}': {e.Message}");
                    return;
                }
            }

            var cfg = requirement.GetConfiguration();
            if (target != null && cfg != null)
            {
                try { cfg.ConfigureComponent(target); }
                catch (Exception e)
                {
                    Debug.LogError($"[TeamColorPoolManager] Failed to configure component '{componentType.Name}': {e.Message}");
                }
            }
        }

        #endregion

        #region Type Cache

        private Type GetCachedComponentType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            if (componentTypeCache.TryGetValue(typeName, out var t)) return t;
            return CacheComponentType(typeName);
        }

        private Type CacheComponentType(string typeName)
        {
            // Fast path
            var t = Type.GetType(typeName);
            if (t != null) { componentTypeCache[typeName] = t; return t; }

            // Scan loaded assemblies (Unity-friendly)
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in asms)
            {
                t = asm.GetType(typeName);
                if (t != null) { componentTypeCache[typeName] = t; return t; }
            }
            return null;
        }

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (poolConfigurations == null) return;

            foreach (var cfg in poolConfigurations)
            {
                if (cfg == null || cfg.requiredComponents == null) continue;

                foreach (var req in cfg.requiredComponents)
                {
                    if (req != null && !req.IsValid())
                        Debug.LogWarning($"[TeamColorPoolManager] Invalid component type '{req.componentTypeName}' in config '{cfg.poolName}'.");
                }
            }
        }
#endif
    }
}
