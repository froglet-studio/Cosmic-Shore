using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Gameplay;
namespace CosmicShore.Utility
{
    /// <summary>
    /// Holds the authored explosion CONFIG prefab PrismDebris reads (mesh /
    /// material / layer / clamp / duration). Gameplay never Get()s this pool
    /// (D4); Get remains for editor/debug. Do not prewarm — nothing consumes it.
    /// </summary>
    public class PrismExplosionPoolManager : GenericPoolManager<PrismExplosion>
    {
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
            // Get_ returns null by contract when the pool yields a dead instance
            // (GenericPoolManager.Get_). Factory death spawn no longer calls Get
            // (D4); editor/debug callers still treat null as skip.
            if (explosion != null)
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