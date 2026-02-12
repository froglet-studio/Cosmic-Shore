using CosmicShore.Core;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Obvious.Soap;

namespace CosmicShore.Game
{
    public class InteractivePrismPoolManager : GenericPoolManager<Prism>
    {
        [Header("Cleanup Events")]
        [SerializeField] private ScriptableEventNoParam OnResetForReplay;

        private void OnEnable()
        {
            if (OnResetForReplay != null)
                OnResetForReplay.OnRaised += HandleReset;
        }

        private void OnDisable()
        {
            if (OnResetForReplay != null)
                OnResetForReplay.OnRaised -= HandleReset;
        }

        private void HandleReset()
        {
            // [Optimization] Batch size 100 cleans fast but keeps Main Thread responsive for Network Heartbeats
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