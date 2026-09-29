using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using CosmicShore.Utility;
namespace CosmicShore.Utility
{
    public class InteractivePrismPoolManager : GenericPoolManager<Prism>
    {
        [Header("Cleanup Events")]
        [SerializeField] private ScriptableEventNoParam OnResetForReplay;
        [SerializeField] private ScriptableEventBool OnSceneTransition;

        private void OnEnable()
        {
            if (OnResetForReplay != null)
                OnResetForReplay.OnRaised += HandleReset;
            if (OnSceneTransition != null)
                OnSceneTransition.OnRaised += HandleSceneTransition;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (OnResetForReplay != null)
                OnResetForReplay.OnRaised -= HandleReset;
            if (OnSceneTransition != null)
                OnSceneTransition.OnRaised -= HandleSceneTransition;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void HandleReset()
        {
            // [Optimization] Batch size 100 cleans fast but keeps Main Thread responsive for Network Heartbeats
            ReleaseAllActiveAsync(100).Forget();
        }

        private void HandleSceneTransition(bool isLoading)
        {
            // Release all active prisms back to pool before scene unload
            // to prevent visual artifacts from leaking across scene transitions.
            if (!isLoading)
                ReleaseAllActiveAsync(100).Forget();
        }

        private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            // Synchronously release all active prisms on scene change.
            // Works on both host and client - critical for clients where the
            // SOAP OnSceneTransition event never fires (Netcode drives their scene load).
            ReleaseAllActive();
        }

        public override Prism Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            var instance = Get_(position, rotation, parent, worldPositionStays);
            if (instance != null)
            {
                // -= first: exactly one handler per life, whatever a previous life left behind.
                instance.OnReturnToPool -= Release;
                instance.OnReturnToPool += Release;
            }
            return instance;
        }

        // Runs on EVERY path back into the pool, including the bulk scene-change releases that
        // bypass Release() - which is where the per-Get handler used to survive and double up
        // (GenericPoolManager.Release_ has the full story).
        protected override void OnReturnedToPool(Prism instance)
        {
            if (instance) instance.OnReturnToPool -= Release;
        }

        public override void Release(Prism instance)
        {
            instance.OnReturnToPool -= Release;
            Release_(instance);
        }
    }
}