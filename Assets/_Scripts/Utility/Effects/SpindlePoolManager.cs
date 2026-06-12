using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using Obvious.Soap;

namespace CosmicShore.Utility
{
    public class SpindlePoolManager : GenericPoolManager<Spindle>
    {
        static SpindlePoolManager s_instance;
        public static SpindlePoolManager Instance => s_instance;

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
            // Release all active spindles back to pool before scene unload
            // to prevent visual artifacts from leaking across scene transitions.
            if (!isLoading)
                ReleaseAllActiveAsync(100).Forget();
        }

        private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            // Synchronously release all active spindles on scene change.
            ReleaseAllActive();
        }

        public override Spindle Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
        {
            var instance = Get_(position, rotation, parent, worldPositionStays);
            if (instance != null)
                instance.InitializeFromPool();
            return instance;
        }

        public override void Release(Spindle instance)
        {
            if (!instance) return;
            instance.ResetForPool();
            Release_(instance);
        }
    }
}
