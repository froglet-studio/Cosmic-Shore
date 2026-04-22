using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Gameplay;
namespace CosmicShore.Utility
{
    /// <summary>
    /// Object pool manager for prism explosion effects.
    /// </summary>
    public class PrismExplosionPoolManager : GenericPoolManager<PrismExplosion>
    {
        // Ensure the pool has enough objects for a burst frame (matches PrismFactory cap).
        // Inspector-serialized values may be lower than this minimum.
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

        public override PrismExplosion Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            var explosion = Get_(position, rotation, parent, worldPositionStays);
            explosion.OnReturnToPool += Release;
            return explosion;
        }
        
        public override void Release(PrismExplosion instance)
        {
            instance.OnReturnToPool -= Release;
            Release_(instance);
        }
    }
}