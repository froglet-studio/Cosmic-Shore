using Obvious.Soap;
using UnityEngine;
using System.Collections;

namespace CosmicShore.Game
{
    public sealed class SquirrelVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private SquirrelVesselHUDView view;

        [Header("Events")]
        [SerializeField] private ScriptableEventBoostChanged boostChanged;
        [SerializeField] private ScriptableEventString joustCollisionEvent;
        [SerializeField] private ScriptableEventVesselImpactor squirrelCrystalExplosionEvent;
        [SerializeField] private ScriptableEventNoParam isDrifting;
        [SerializeField] private ScriptableEventNoParam isDoubleDrifting;
        [SerializeField] private ScriptableEventNoParam driftEnded;

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

            view.Initialize();
            Subscribe();
            PaintFromStatusFallback();
        }

        private void Subscribe()
        {
            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsNetworkOwner)
                return;

            if (boostChanged != null)
                boostChanged.OnRaised += HandleBoostChanged;
            if (isDrifting != null)
                isDrifting.OnRaised += UpdateDrift;
            if (isDoubleDrifting != null)
                isDoubleDrifting.OnRaised += UpdateDoubleDrift;
            if (joustCollisionEvent != null)
                joustCollisionEvent.OnRaised += HandleJoustCollision;
            if (squirrelCrystalExplosionEvent != null)
                squirrelCrystalExplosionEvent.OnRaised += HandleSquirrelCrystalExplosion;
            if (driftEnded != null)
                driftEnded.OnRaised += OnDriftEnded;
        }

        private void OnDisable()
        {
            if (boostChanged != null)
                boostChanged.OnRaised -= HandleBoostChanged;
            if (isDrifting != null)
                isDrifting.OnRaised -= UpdateDrift;
            if (isDoubleDrifting != null)
                isDoubleDrifting.OnRaised -= UpdateDoubleDrift;
            if (joustCollisionEvent != null)
                joustCollisionEvent.OnRaised -= HandleJoustCollision;
            if (squirrelCrystalExplosionEvent != null)
                squirrelCrystalExplosionEvent.OnRaised -= HandleSquirrelCrystalExplosion;
            if (driftEnded != null)
                driftEnded.OnRaised -= OnDriftEnded;
        }


        private void HandleBoostChanged(BoostChangedPayload payload)
        {
            if (!view)
                return;

            float baseMult = boostBaseMultiplier ? boostBaseMultiplier.Value : 1f;
            float maxMult = payload.MaxMultiplier;
            if (maxMult <= 0f)
                maxMult = boostMaxMultiplier ? boostMaxMultiplier.Value : baseMult;

            baseMult = Mathf.Max(0.0001f, baseMult);
            maxMult = Mathf.Max(baseMult, maxMult);

            float mult = Mathf.Max(0f, payload.BoostMultiplier);

            float boost01 = Mathf.InverseLerp(baseMult, maxMult, mult);
            bool isBoosted = mult > baseMult + 0.0001f;
            bool isFull = mult >= maxMult - 0.0001f;

            view.SetBoostState(Mathf.Clamp01(boost01), isBoosted, isFull);
        }

        private void HandleJoustCollision(string playerName)
        {
            if (!view) return;
            StartCoroutine(JoustFlash());
        }
        private IEnumerator JoustFlash()
        {
            view.UpdateDangerIcon(true);
            yield return new WaitForSeconds(1f);
            view.UpdateDangerIcon(false);
        }
        private void PaintFromStatusFallback()
        {
            if (!view || _vesselStatus == null) return;

            float baseMult = boostBaseMultiplier != null ? boostBaseMultiplier.Value : 1f;
            float maxMult = boostMaxMultiplier != null ? boostMaxMultiplier.Value : 5f;

            baseMult = Mathf.Max(0.0001f, baseMult);
            maxMult = Mathf.Max(baseMult, maxMult);

            float mult = Mathf.Max(0f, _vesselStatus.BoostMultiplier);

            float boost01 = Mathf.InverseLerp(baseMult, maxMult, mult);
            bool isBoosted = mult > baseMult + 0.0001f;
            bool isFull = mult >= maxMult - 0.0001f;

            view.SetBoostState(Mathf.Clamp01(boost01), isBoosted, isFull);
        }

        private void UpdateDrift()
        {
            if (!view) return;
            view.UpdateDriftIcon(true, false);
        }
        private void UpdateDoubleDrift()
        {
            if (!view || _vesselStatus == null) return;

            view.UpdateDriftIcon(true, true);
        }
        private void OnDriftEnded()
        {
            if (!view) return;
            view.UpdateDriftIcon(false, false);
        }

        private void HandleSquirrelCrystalExplosion(VesselImpactor vesselImpactor)
        {
            if (!view || vesselImpactor.Vessel.VesselStatus.PlayerName != _vesselStatus.PlayerName)
                return;

            StartCoroutine(ShieldFlash());
        }
        private IEnumerator ShieldFlash()
        {
            view.UpdateShieldColor(true);
            yield return new WaitForSeconds(1f);
            view.UpdateShieldColor(false);
        }
    }
}