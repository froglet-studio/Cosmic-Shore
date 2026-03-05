using CosmicShore.Core;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Obvious.Soap;

namespace CosmicShore.Game
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

        private void OnEnable()
        {
            if (OnResetForReplay != null)
                OnResetForReplay.OnRaised += HandleReset;
        }

        private new void OnDisable()
        {
            base.OnDisable();
            if (OnResetForReplay != null)
                OnResetForReplay.OnRaised -= HandleReset;
        }

        private void HandleReset()
        {
            ReleaseAllActiveAsync(100).Forget();
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
