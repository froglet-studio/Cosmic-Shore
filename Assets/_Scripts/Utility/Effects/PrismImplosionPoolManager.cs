using CosmicShore.Utility;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Pool manager for PrismImplosion VFX.
    /// </summary>
    public class PrismImplosionPoolManager : GenericPoolManager<PrismImplosion>
    {
        private const int MinPrewarm = 64;

        protected override void Awake()
        {
            base.Awake();
            EnsureBuffer(MinPrewarm);
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            ReleaseAllActive();
        }

        public override PrismImplosion Get(Vector3 spawnPosition, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            var implosion = Get_(spawnPosition, rotation, parent, worldPositionStays);
            implosion.OnReturnToPool += Release; // auto return when done
            return implosion;
        }

        public override void Release(PrismImplosion instance)
        {
            instance.OnReturnToPool -= Release;
            Release_(instance);
        }
    }
}