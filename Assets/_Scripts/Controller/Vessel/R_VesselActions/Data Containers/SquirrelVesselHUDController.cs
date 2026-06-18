using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using DG.Tweening;
using Obvious.Soap;
using Reflex.Attributes;
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

        [Inject] private GameDataSO gameData;

        [Header("Flash Durations")]
        [SerializeField] private float joustFlashDuration = 1f;
        [SerializeField] private float shieldFlashDuration = 1f;

        private IVesselStatus _vesselStatus;
        private Domains _lastSourceDomain = Domains.Blue;
        private Tween _joustFlashTween;
        private Tween _shieldFlashTween;

        // Single source of truth — the same ColorSet the vessels and prisms use (R5).
        private Color ResolveDomainColor(Domains domain) =>
            gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.GetDomainUIColor(domain)
                : Color.white;

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

            Color playerColor = ResolveDomainColor(vesselStatus.Domain);

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

            // Multiplayer: boostChanged is a shared global SOAP channel raised by EVERY
            // vessel (notably the remote owner's per-frame DecayBoost). Ignore raises that
            // didn't originate from our own vessel, else a remote vessel pins this HUD and
            // the local owner's energy bar goes unresponsive.
            if (payload.VesselStatus != null && payload.VesselStatus != _vesselStatus) return;

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
            if (effectiveDomain != Domains.Blue)
            {
                _lastSourceDomain = effectiveDomain;
            }
            else if (isBoosted)
            {
                effectiveDomain = _lastSourceDomain;
            }
            else
            {
                _lastSourceDomain = Domains.Blue;
            }

            bool hasSourceDomain = effectiveDomain != Domains.Blue;
            Color sourceColor = hasSourceDomain ? ResolveDomainColor(effectiveDomain) : Color.white;

            view.SetBoostState(Mathf.Clamp01(boost01), isBoosted, isFull,
                sourceColor, hasSourceDomain);
        }

        private void HandleJoustCollision(string playerName)
        {
            if (!view) return;
            // Shared global event — only react to our own vessel's joust collisions.
            if (playerName != _vesselStatus.PlayerName) return;

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
