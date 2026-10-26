using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Utility;
using Reflex.Attributes;
namespace CosmicShore.UI
{
    public class SparrowHUDController : VesselHUDController
    {
        [Header("View binding")]
        [SerializeField] private SparrowHUDView view;

        [Header("Executors")]
        [SerializeField] private FireGunActionExecutor fireGunExecutor;

        // The palette, for the Time card's hull part (the pilot's domain colour). Injected rather
        // than serialized so the slot reads the same ColorSet the rest of the HUD does; vessels DO
        // get GameObjectInjector.InjectRecursive at spawn (the Dolphin's HUD controller is the pattern).
        [Inject] GameDataSO _gameData;

        [Tooltip("The guns' heat/accuracy state. Drives the guns (Space) card's heat gauge and " +
                 "its phase-transition marks. Resolved off the vessel if left empty.")]
        [SerializeField] private GunSprayAccuracy gunSprayAccuracy;

        [Header("Events")]
        [SerializeField] private ScriptableEventBool stationaryModeChanged;
        [SerializeField] private ScriptableEventInputEventBlock onInputEventBlocked;

        Coroutine _initialAmmoRoutine;
        IVesselStatus _vesselStatus;

        bool _pollGunHeat;
        bool _pollAfterburner;
        GunSpreadProfile _paintedProfile;
        float _paintedFullSpread = -1f;
        readonly List<float> _phaseJoins = new();

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _vesselStatus = vesselStatus;

            if (!view)
                view = View as SparrowHUDView;

            if (!view) return;

            Subscribe();
        }

        void Subscribe()
        {
            // Detach-first, BEFORE the pilot gate: a vessel swap re-runs Initialize on live
            // components, and if the re-init hands this HUD to an AI or a remote player the early
            // return below must not leave the previous pilot's subscriptions attached.
            Unsubscribe();

            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser) return;

            if (stationaryModeChanged)
            {
                stationaryModeChanged.OnRaised += HandleStationaryModeChanged;
                HandleStationaryModeChanged(_vesselStatus.IsTranslationRestricted);
            }

            if (onInputEventBlocked)
                onInputEventBlocked.OnRaised += HandleInputEventBlocked;

            // The Time card's hull part: polled per frame (PushAfterburner) - the boost flag is
            // written on the status by the boost action, and the domain can change mid-match.
            _pollAfterburner = true;
            PushAfterburner();

            if (!gunSprayAccuracy && _vesselStatus is Component vesselComponent)
                gunSprayAccuracy = vesselComponent.GetComponentInChildren<GunSprayAccuracy>(true);
            _pollGunHeat = gunSprayAccuracy;

            if (fireGunExecutor == null) return;
            fireGunExecutor.OnAmmoChanged += HandleAmmoChanged;
            _initialAmmoRoutine = StartCoroutine(InitialAmmoPaintRoutine());
        }

        void OnDisable()
        {
            // Unconditional and idempotent — gating teardown on the pilot flags would strand
            // subscriptions on a vessel that was handed to an AI after it subscribed.
            Unsubscribe();

            if (_initialAmmoRoutine != null)
            {
                StopCoroutine(_initialAmmoRoutine);
                _initialAmmoRoutine = null;
            }
        }

        void Unsubscribe()
        {
            _pollGunHeat = false;
            _pollAfterburner = false;
            _paintedProfile = null;
            _paintedFullSpread = -1f;

            if (stationaryModeChanged)
                stationaryModeChanged.OnRaised -= HandleStationaryModeChanged;

            if (onInputEventBlocked)
                onInputEventBlocked.OnRaised -= HandleInputEventBlocked;

            if (fireGunExecutor != null)
                fireGunExecutor.OnAmmoChanged -= HandleAmmoChanged;
        }

        /// <summary>
        /// Heat is continuous (it integrates every frame while firing and while cooling) and has
        /// no event, so it is POLLED - one float compare per frame on the local pilot's HUD only.
        /// The phase marks are re-laid only when the profile the heat is measured against changes.
        /// </summary>
        void Update()
        {
            if (_pollAfterburner) PushAfterburner();
            if (!_pollGunHeat || !view || !gunSprayAccuracy) return;

            var profile = gunSprayAccuracy.Profile;
            float full = profile?.SecondsToFullSpread ?? 0f;
            if (profile != _paintedProfile || !Mathf.Approximately(full, _paintedFullSpread))
            {
                _paintedProfile = profile;
                _paintedFullSpread = full;
                gunSprayAccuracy.CollectPhaseJoins01(_phaseJoins);
                view.SetGunHeatPhaseJoins(_phaseJoins);
            }

            view.SetGunHeat(gunSprayAccuracy.Heat01);
        }

        private IEnumerator InitialAmmoPaintRoutine()
        {
            yield return null;
            view?.InitializeMissileIcon();

            // Seed the charge gauge from the LIVE tank, not from a resting value: the pilot may
            // arrive mid-match (a vessel swap, a replay) with a part-charged bay, and a gauge
            // that starts at zero and then jumps on the next destroyed prism reads as a bug.
            if (fireGunExecutor != null)
                view?.SetMissileCharge(fireGunExecutor.ChargeToNextShot01);

            _initialAmmoRoutine = null;
        }

        private void HandleInputEventBlocked(InputEventBlockPayload payload)
        {
            if (!view) return;
            view.HandleInputEventBlocked(payload);
        }

        private void HandleStationaryModeChanged(bool isStationary)
        {
            if (!view) return;
            view.SetWeaponMode(isStationary);   // the Space card: bullets <-> prism rounds
            view.SetStance(isStationary);       // the Mass card: the ghost rounds turn solid
        }

        /// <summary>
        /// The Time card's hull part: the Sparrow folds while the boost is held (IsBoosting, written
        /// by the boost action on the status) and wears the pilot's domain colour. The card shows
        /// nothing else - the boost is indefinite, and the strafing roll's armed state is not worth
        /// a readout (Garrett 2026-10-26: "its cooldown is so low").
        /// </summary>
        void PushAfterburner()
        {
            if (!view || _vesselStatus == null) return;
            var domain = ResolveDomainSignalColor();
            view.SetAfterburnerHullColor(domain);
            view.SetStanceColors(domain, ResolveShieldedColor());
            view.SetBoosting(_vesselStatus.IsBoosting);
        }

        Color ResolveDomainSignalColor()
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            if (colorSet == null || _vesselStatus == null) return Color.white;
            return colorSet.GetDomainSignalColor(_vesselStatus.Domain);
        }

        // The domain's SHIELDED colour for the Mass card's level-5 rounds; alpha 0 until the palette
        // resolves (the view keeps the domain colour), never a plausible stand-in (Docs/PALETTE.md 2.8).
        Color ResolveShieldedColor()
        {
            var theme = _gameData?.ThemeManagerData;
            if (theme == null || _vesselStatus == null || _vesselStatus.Domain == Domains.Blue) return new Color(0f, 0f, 0f, 0f);
            return theme.GetShieldedSignalColor(_vesselStatus.Domain);
        }

        /// <summary>
        /// One ammo change, two readouts: the icon ladder says how many rockets the bay HOLDS,
        /// the Charge card's gauge says how close the next one is. They are driven from the same
        /// event so they can never disagree, and the gauge is asked of the executor rather than
        /// derived here — the shot's cost is the weapon's business, and re-deriving it in the HUD
        /// is how a UI number drifts from the one the gun spends.
        /// </summary>
        private void HandleAmmoChanged(float ammo01)
        {
            if (!view) return;

            // The COST comes from the executor, never from the HUD: the icon ladder counts
            // rockets, and a count re-derived here would drift from the number the gun spends
            // the moment the weapon's price changes (it just did - a base rocket is half what
            // it was, so the bay holds four).
            view.SetMissilesFromAmmo01(ammo01, fireGunExecutor != null ? fireGunExecutor.ShotCost01 : 0f);

            if (fireGunExecutor != null)
                view.SetMissileCharge(fireGunExecutor.ChargeToNextShot01);
        }
    }
}
