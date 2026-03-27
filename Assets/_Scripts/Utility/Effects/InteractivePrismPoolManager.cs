using CosmicShore.Gameplay;
using UnityEngine;
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
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (OnResetForReplay != null)
                OnResetForReplay.OnRaised -= HandleReset;
            if (OnSceneTransition != null)
                OnSceneTransition.OnRaised -= HandleSceneTransition;
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

        public override Prism Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            var instance = Get_(position, rotation, parent, worldPositionStays);
            if (instance != null)
            {
                instance.OnReturnToPool += Release;
            }
            return instance;
        }

        public override void Release(Prism instance)
        {
            instance.OnReturnToPool -= Release;
            Release_(instance);
        }
    }
}