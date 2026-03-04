using CosmicShore.Core;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Obvious.Soap;

namespace CosmicShore.Game
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
            ReleaseAllActiveAsync(100).Forget();
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
