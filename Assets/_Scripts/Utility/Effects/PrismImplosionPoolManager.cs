using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Pool manager for PrismImplosion. Prefab is the batched suction CONFIG
    /// source AND the live Grow pool (Sparrow ReverseSuction). Death implosions
    /// never Get() this pool (D4). Prewarm is sized for Grow bursts, not swarm-eat.
    /// </summary>
    public class PrismImplosionPoolManager : GenericPoolManager<PrismImplosion>
    {
        private const int MinPrewarm = 12;

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
            // Match PrismExplosionPoolManager / InteractivePrismPoolManager: Get_ can
            // return null when the pool yields a dead instance, and callers already
            // null-check the result — guard the subscribe so we fail soft instead of
            // throwing an NRE per implosion.
            if (implosion != null)
                // -= first: exactly one handler per life, whatever a previous life left behind.
                implosion.OnReturnToPool -= Release;
                implosion.OnReturnToPool += Release; // auto return when done
            return implosion;
        }

        // Detach EVERYTHING on the way back, not just our own Release: PrismFactory's Grow path
        // hangs a one-shot growCallback on the same delegate, and a bulk release (scene change)
        // used to leave it attached - so the NEXT life's return fired the PREVIOUS Grow's
        // completion. Runs on every path back into the pool (GenericPoolManager.OnReturnedToPool).
        protected override void OnReturnedToPool(PrismImplosion instance)
        {
            if (instance) instance.OnReturnToPool = null;
        }

        public override void Release(PrismImplosion instance)
        {
            instance.OnReturnToPool -= Release;
            Release_(instance);
        }
    }
}