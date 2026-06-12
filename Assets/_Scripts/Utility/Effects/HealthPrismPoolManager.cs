using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using Obvious.Soap;

namespace CosmicShore.Utility
{
    public class HealthPrismPoolManager : GenericPoolManager<HealthPrism>
    {
        static HealthPrismPoolManager s_instance;
        public static HealthPrismPoolManager Instance => s_instance;

        protected override void Awake()
        {
            base.Awake();
            s_instance = this;
        }

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
            ReleaseAllActive();
        }

        protected override HealthPrism CreateFunc()
        {
            Prism.BeginPoolCreation();
            try { return base.CreateFunc(); }
            finally { Prism.EndPoolCreation(); }
        }

        public override HealthPrism Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            var instance = Get_(position, rotation, parent, worldPositionStays);
            if (instance != null)
                instance.OnReturnToPool += HandleReturnToPool;
            return instance;
        }

        public override void Release(HealthPrism instance)
        {
            instance.OnReturnToPool -= HandleReturnToPool;
            Release_(instance);
        }

        private void HandleReturnToPool(Prism prism)
        {
            if (prism is HealthPrism hp)
                Release(hp);
        }
    }
}
