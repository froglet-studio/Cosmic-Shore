using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public sealed class SquirrelVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private SquirrelVesselHUDView view;

        [Header("Events")]
        [SerializeField] private ScriptableEventBoostChanged boostChanged;

        [Header("Shared Config")]
        [SerializeField] private ScriptableVariable<float> boostBaseMultiplier;
        [SerializeField] private ScriptableVariable<float> boostMaxMultiplier;

        private IVesselStatus _vesselStatus;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _vesselStatus = vesselStatus;

            if (!view)
                view = View as SquirrelVesselHUDView;

            if (!view) return;

            Subscribe();
            PaintFromStatusFallback();
        }

        private void Subscribe()
        {
            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser)
                return;

            if (boostChanged != null)
                boostChanged.OnRaised += HandleBoostChanged;
        }

        private void OnDisable()
        {
            if (boostChanged != null)
                boostChanged.OnRaised -= HandleBoostChanged;
        }

        private void HandleBoostChanged(BoostChangedPayload payload)
        {
            if (!view) return;

            float baseMult = boostBaseMultiplier ? boostBaseMultiplier.Value : 1f;
            float maxMult  = payload.MaxMultiplier;
            if (maxMult <= 0f)
                maxMult = boostMaxMultiplier ? boostMaxMultiplier.Value : baseMult;

            baseMult = Mathf.Max(0.0001f, baseMult);
            maxMult  = Mathf.Max(baseMult, maxMult);

            float mult = Mathf.Max(0f, payload.BoostMultiplier);

            float boost01  = Mathf.InverseLerp(baseMult, maxMult, mult);
            bool  isBoosted = mult > baseMult + 0.0001f;       
            bool  isFull    = mult >= maxMult - 0.0001f;  

            view.SetBoostState(Mathf.Clamp01(boost01), isBoosted, isFull);
        }

        private void PaintFromStatusFallback()
        {
            if (!view || _vesselStatus == null) return;

            float baseMult = boostBaseMultiplier != null ? boostBaseMultiplier.Value : 1f;
            float maxMult  = boostMaxMultiplier  != null ? boostMaxMultiplier.Value  : 5f;

            baseMult = Mathf.Max(0.0001f, baseMult);
            maxMult  = Mathf.Max(baseMult, maxMult);

            float mult = Mathf.Max(0f, _vesselStatus.BoostMultiplier);

            float boost01  = Mathf.InverseLerp(baseMult, maxMult, mult);
            bool  isBoosted = mult > baseMult + 0.0001f;
            bool  isFull    = mult >= maxMult - 0.0001f;

            view.SetBoostState(Mathf.Clamp01(boost01), isBoosted, isFull);
        }
    }
}
