using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.UI
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

        [Header("Colors")]
        [SerializeField] private DomainColorPaletteSO domainColors;

        [Header("Flash Durations")]
        [SerializeField] private float joustFlashDuration = 1f;
        [SerializeField] private float shieldFlashDuration = 1f;

        private IVesselStatus _vesselStatus;
        private Domains _lastSourceDomain = Domains.None;
        private Tween _joustFlashTween;
        private Tween _shieldFlashTween;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _vesselStatus = vesselStatus;

            if (!view)
                view = View as SquirrelVesselHUDView;

            if (!view) return;

            if (vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser)
            {
                view.Hide();
                return;
            }

            Color playerColor = domainColors != null
                ? domainColors.Get(vesselStatus.Domain)
                : Color.white;

            view.Initialize();
            view.SetPlayerDomainColor(playerColor);
            Subscribe();
            PaintFromStatusFallback();
        }

        private void Subscribe()
        {
            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser)
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
            _joustFlashTween?.Kill();
            _shieldFlashTween?.Kill();

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
            if (!view) return;

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

            // Persist source domain across decay frames so the stolen color holds
            Domains effectiveDomain = payload.SourceDomain;
            if (effectiveDomain != Domains.None && effectiveDomain != Domains.Unassigned)
            {
                _lastSourceDomain = effectiveDomain;
            }
            else if (isBoosted)
            {
                effectiveDomain = _lastSourceDomain;
            }
            else
            {
                _lastSourceDomain = Domains.None;
            }

            Color sourceColor = Color.white;
            bool hasSourceDomain = effectiveDomain != Domains.None
                                   && effectiveDomain != Domains.Unassigned;
            if (hasSourceDomain && domainColors != null)
                sourceColor = domainColors.Get(effectiveDomain);

            view.SetBoostState(Mathf.Clamp01(boost01), isBoosted, isFull,
                sourceColor, hasSourceDomain);
        }

        private void HandleJoustCollision(string playerName)
        {
            if (!view) return;

            _joustFlashTween?.Kill();
            view.UpdateDangerIcon(true);
            _joustFlashTween = DOVirtual.DelayedCall(joustFlashDuration, () =>
            {
                if (view) view.UpdateDangerIcon(false);
            });
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

            view.SetBoostState(Mathf.Clamp01(boost01), isBoosted, isFull,
                Color.white, false);
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

            view.FlashCrystalSurge();

            _shieldFlashTween?.Kill();
            view.UpdateShieldColor(true);
            _shieldFlashTween = DOVirtual.DelayedCall(shieldFlashDuration, () =>
            {
                if (view) view.UpdateShieldColor(false);
            });
        }
    }
}
