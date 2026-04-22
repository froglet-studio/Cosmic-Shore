using DG.Tweening;
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

        [Header("Elemental Bars Juice")]
        [Tooltip("Reference to the SilhouetteController to access ElementalBarsView")]
        [SerializeField] private SilhouetteController silhouetteController;

        private IVesselStatus _vesselStatus;
        private Domains _lastSourceDomain = Domains.None;
        private Tween _joustFlashTween;
        private Tween _shieldFlashTween;
        private bool _isDriftingLeft;

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
                isDrifting.OnRaised += HandleDriftStarted;
            if (isDoubleDrifting != null)
                isDoubleDrifting.OnRaised += HandleDoubleDriftStarted;
            if (joustCollisionEvent != null)
                joustCollisionEvent.OnRaised += HandleJoustCollision;
            if (squirrelCrystalExplosionEvent != null)
                squirrelCrystalExplosionEvent.OnRaised += HandleSquirrelCrystalExplosion;
            if (driftEnded != null)
                driftEnded.OnRaised += HandleDriftEnded;
        }

        private void OnDisable()
        {
            _joustFlashTween?.Kill();
            _shieldFlashTween?.Kill();

            if (boostChanged != null)
                boostChanged.OnRaised -= HandleBoostChanged;
            if (isDrifting != null)
                isDrifting.OnRaised -= HandleDriftStarted;
            if (isDoubleDrifting != null)
                isDoubleDrifting.OnRaised -= HandleDoubleDriftStarted;
            if (joustCollisionEvent != null)
                joustCollisionEvent.OnRaised -= HandleJoustCollision;
            if (squirrelCrystalExplosionEvent != null)
                squirrelCrystalExplosionEvent.OnRaised -= HandleSquirrelCrystalExplosion;
            if (driftEnded != null)
                driftEnded.OnRaised -= HandleDriftEnded;
        }

        private void HandleBoostChanged(BoostChangedPayload payload)
        {
            if (!view || _vesselStatus == null) return;

            // Only react when the payload matches the local player's boost state
            if (!Mathf.Approximately(payload.BoostMultiplier, _vesselStatus.BoostMultiplier))
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

        // ---------------------------------------------------------------
        // Joust — juice on both HUD icons and elemental bars
        // ---------------------------------------------------------------
        private void HandleJoustCollision(string playerName)
        {
            if (!view) return;

            // Only react when the local player is the one who got jousted
            if (_vesselStatus == null || playerName != _vesselStatus.PlayerName)
                return;

            // HUD icon juice
            _joustFlashTween?.Kill();
            view.UpdateDangerIcon(true);
            view.JuiceJoust();
            _joustFlashTween = DOVirtual.DelayedCall(joustFlashDuration, () =>
            {
                if (view) view.UpdateDangerIcon(false);
            });

            // Elemental bars juice
            GetElementBars()?.JuiceJoust();
        }

        // ---------------------------------------------------------------
        // Crystal explosion — juice with domain color
        // ---------------------------------------------------------------
        private void HandleSquirrelCrystalExplosion(VesselImpactor vesselImpactor)
        {
            if (!view || vesselImpactor.Vessel.VesselStatus.PlayerName != _vesselStatus.PlayerName)
                return;

            // Determine domain color from the crystal
            Color crystalColor = Color.white;
            if (domainColors != null)
                crystalColor = domainColors.Get(_vesselStatus.Domain);

            // HUD icon juice
            view.FlashCrystalSurge();
            view.JuiceCrystalCollected(crystalColor);

            _shieldFlashTween?.Kill();
            view.UpdateShieldColor(true);
            _shieldFlashTween = DOVirtual.DelayedCall(shieldFlashDuration, () =>
            {
                if (view) view.UpdateShieldColor(false);
            });

            // Elemental bars juice
            GetElementBars()?.JuiceCrystalCollected(crystalColor);
        }

        // ---------------------------------------------------------------
        // Drift — detect direction from InputStatus.XSum, apply juice
        // ---------------------------------------------------------------
        private void HandleDriftStarted()
        {
            if (!view || _vesselStatus == null) return;

            // Only react when the local player's vessel is actually drifting
            if (!_vesselStatus.IsDrifting) return;

            bool isLeft = _vesselStatus.InputStatus != null && _vesselStatus.InputStatus.XSum < 0f;
            _isDriftingLeft = isLeft;

            view.UpdateDriftIcon(true, false);
            view.JuiceDriftStart(isLeft, false);

            GetElementBars()?.JuiceDriftStart(isLeft, false);
        }

        private void HandleDoubleDriftStarted()
        {
            if (!view || _vesselStatus == null) return;

            // Only react when the local player's vessel is actually drifting
            if (!_vesselStatus.IsDrifting) return;

            bool isLeft = _vesselStatus.InputStatus != null && _vesselStatus.InputStatus.XSum < 0f;
            _isDriftingLeft = isLeft;

            view.UpdateDriftIcon(true, true);
            view.JuiceDriftStart(isLeft, true);

            GetElementBars()?.JuiceDriftStart(isLeft, true);
        }

        private void HandleDriftEnded()
        {
            if (!view || _vesselStatus == null) return;

            // Only react when the local player's vessel stopped drifting
            if (_vesselStatus.IsDrifting) return;

            view.UpdateDriftIcon(false, false);
            view.JuiceDriftEnd();

            GetElementBars()?.JuiceDriftEnd();
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

        private ElementalBarsView GetElementBars()
        {
            return silhouetteController ? silhouetteController.ElementBars : null;
        }
    }
}
