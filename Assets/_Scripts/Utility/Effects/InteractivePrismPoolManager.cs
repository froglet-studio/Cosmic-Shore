using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using CosmicShore.Soap;
using CosmicShore.Utility;
namespace CosmicShore.Utility
{
    public class InteractivePrismPoolManager : GenericPoolManager<Prism>
    {
        [Header("Cleanup Events")]
        [SerializeField] private ScriptableEventNoParam OnResetForReplay;
        [SerializeField] private ScriptableEventBool OnSceneTransition;

        /// <summary>Milliseconds a frame one pool spends handing its trail back at a scene exit. A field, not a
        /// constant, so a measurement can set it (0 hands back 32 a frame, near the old fixed 100).</summary>
        public static float SceneExitReleaseMsPerFrame = 12f;

        static readonly System.Collections.Generic.List<InteractivePrismPoolManager> s_live = new();

        /// <summary>Trail prisms the live pools are still handing back after a scene exit began.</summary>
        public static int TotalPendingRelease
        {
            get
            {
                int total = 0;
                for (int i = 0; i < s_live.Count; i++)
                    if (s_live[i]) total += s_live[i].PendingReleaseCount;
                return total;
            }
        }

        /// <summary>
        /// Every live trail pool starts handing its prisms back, a time slice a frame. For a guest, when the
        /// host's scene load arrives: the SOAP scene-transition event below is local to the host, so a guest's
        /// pools used to release everything in one go inside the load's frame (1.15 s for 57,000 prisms,
        /// Docs/PartySystem/BUGS.md B45).
        /// </summary>
        public static void BeginReleaseAllForSceneExit()
        {
            for (int i = 0; i < s_live.Count; i++)
                if (s_live[i]) s_live[i].ReleaseAllActiveBudgetedAsync(SceneExitReleaseMsPerFrame).Forget();
        }

        private void OnEnable()
        {
            if (!s_live.Contains(this)) s_live.Add(this);
            if (OnResetForReplay != null)
                OnResetForReplay.OnRaised += HandleReset;
            if (OnSceneTransition != null)
                OnSceneTransition.OnRaised += HandleSceneTransition;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            s_live.Remove(this);
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
                ReleaseAllActiveBudgetedAsync(SceneExitReleaseMsPerFrame).Forget();
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