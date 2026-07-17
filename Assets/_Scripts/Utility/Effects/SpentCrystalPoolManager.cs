using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Object pool for spent-crystal husks — the Impact-driven "dandruff" that
    /// drifts off a collected crystal. Five spent prefabs exist (the omni dummy
    /// plus one dandruff per element), so one pool instance lives per prefab on
    /// PrismManagers and self-registers into a prefab-keyed registry. Crystals
    /// resolve their own serialized SpentCrystalPrefab through
    /// <see cref="GetPooledOrInstantiate"/> — no per-crystal wiring, and prefabs
    /// without a registered pool fall back to Instantiate (Impact then destroys
    /// the husk itself when its drift completes).
    /// </summary>
    public class SpentCrystalPoolManager : GenericPoolManager<Impact>
    {
        // Keyed by the spent prefab's root GameObject (what Crystal/Mine serialize).
        static readonly Dictionary<GameObject, SpentCrystalPoolManager> s_poolsByPrefab = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => s_poolsByPrefab.Clear();

        protected override void Awake()
        {
            base.Awake();
            if (Prefab)
                s_poolsByPrefab[Prefab.gameObject] = this;
        }

        void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Prefab && s_poolsByPrefab.TryGetValue(Prefab.gameObject, out var registered) && registered == this)
                s_poolsByPrefab.Remove(Prefab.gameObject);
        }

        void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            // Husks are sub-second transients, but a scene change mid-drift would
            // strand checked-out instances: deactivation kills their coroutine
            // before it can raise OnReturnToPool.
            ReleaseAllActive();
        }

        /// <summary>
        /// Checkout from the pool registered for this prefab, or Instantiate when
        /// none is. Returns null only when the prefab carries no Impact.
        /// </summary>
        public static Impact GetPooledOrInstantiate(GameObject spentPrefab, Vector3 position, Quaternion rotation)
        {
            if (!spentPrefab) return null;

            if (s_poolsByPrefab.TryGetValue(spentPrefab, out var pool))
            {
                var pooled = pool.Get(position, rotation, pool.transform);
                if (pooled) return pooled;
            }

            var spawned = Instantiate(spentPrefab, position, rotation);
            if (spawned.TryGetComponent(out Impact impact))
                return impact;

            // No Impact means nothing will ever animate or clean this up.
            Destroy(spawned);
            return null;
        }

        public override Impact Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            var impact = Get_(position, rotation, parent, worldPositionStays);
            if (impact != null)
                impact.OnReturnToPool += Release;
            return impact;
        }

        public override void Release(Impact instance)
        {
            instance.OnReturnToPool -= Release;
            Release_(instance);
        }
    }
}
