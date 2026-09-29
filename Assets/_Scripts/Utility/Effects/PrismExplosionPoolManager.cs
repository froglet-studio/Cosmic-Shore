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
                // -= first: exactly one handler per life, whatever a previous life left behind.
                explosion.OnReturnToPool -= Release;
                explosion.OnReturnToPool += Release;
            return explosion;
        }
        
        // Runs on EVERY path back into the pool, including the bulk scene-change releases that
        // bypass Release() - which is where the per-Get handler used to survive and double up
        // (GenericPoolManager.Release_ has the full story).
        protected override void OnReturnedToPool(PrismExplosion instance)
        {
            if (instance) instance.OnReturnToPool -= Release;
        }

        public override void Release(PrismExplosion instance)
        {
            instance.OnReturnToPool -= Release;
            Release_(instance);
        }
    }
}