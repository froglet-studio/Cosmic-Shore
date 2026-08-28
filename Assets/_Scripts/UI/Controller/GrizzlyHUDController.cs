using Obvious.Soap;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.UI
{
    /// <summary>
    /// Grizzly HUD controller: binds the executors' events to GrizzlyHUDView.
    /// Follows the Sparrow pattern — detach-first resubscribe, pilot gating
    /// (AI/remote vessels never drive the local HUD), unconditional teardown.
    /// </summary>
    public class GrizzlyHUDController : VesselHUDController
    {
        [Header("View binding")]
        [SerializeField] GrizzlyHUDView view;

        [Header("Executors")]
        [SerializeField] GrizzlyChargedShotActionExecutor chargedShotExecutor;
        [SerializeField] GrizzlyRushActionExecutor rushExecutor;
        [SerializeField] GrizzlyDigInActionExecutor digInExecutor;
        [SerializeField] GrizzlyWeaponModeExecutor weaponModeExecutor;
        [SerializeField] GrizzlySniperShotActionExecutor sniperExecutor;

        IVesselStatus _vesselStatus;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _vesselStatus = vesselStatus;

            if (!view)
                view = View as GrizzlyHUDView;
            if (!view) return;

            // The scope reticle colours friend/foe off this, so it must land before
            // the first Update - a stale domain would paint allies as enemies.
            view.SetOwnDomain(vesselStatus.Domain);

            Subscribe();
        }

        void Subscribe()
        {
            Unsubscribe();

            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser) return;

            if (chargedShotExecutor)
                chargedShotExecutor.OnChargeChanged += HandleChargeChanged;

            if (rushExecutor)
                rushExecutor.OnChargesChanged += HandleRushCharges;

            if (digInExecutor)
            {
                digInExecutor.OnDugInChanged += HandleDugIn;
                view.SetDugIn(digInExecutor.IsDugIn);
            }

            if (weaponModeExecutor)
            {
                weaponModeExecutor.OnModeChanged += HandleWeaponMode;
                HandleWeaponMode(weaponModeExecutor.CurrentMode);
            }

            if (sniperExecutor)
                sniperExecutor.OnScopeChanged += HandleScope;
                sniperExecutor.OnRoundInFlight += HandleRoundInFlight;
                sniperExecutor.OnRoundEnded += HandleRoundEnded;

            if (_vesselStatus.ResourceSystem != null)
            {
                _vesselStatus.ResourceSystem.OnResourceChanged += HandleResourceChanged;
                PaintEnergy();
            }
        }

        void OnDisable() => Unsubscribe();

        void Unsubscribe()
        {
            if (chargedShotExecutor)
                chargedShotExecutor.OnChargeChanged -= HandleChargeChanged;
            if (rushExecutor)
                rushExecutor.OnChargesChanged -= HandleRushCharges;
            if (digInExecutor)
                digInExecutor.OnDugInChanged -= HandleDugIn;
            if (weaponModeExecutor)
                weaponModeExecutor.OnModeChanged -= HandleWeaponMode;
            if (sniperExecutor)
                sniperExecutor.OnScopeChanged -= HandleScope;
                sniperExecutor.OnRoundInFlight -= HandleRoundInFlight;
                sniperExecutor.OnRoundEnded -= HandleRoundEnded;
            if (_vesselStatus?.ResourceSystem != null)
                _vesselStatus.ResourceSystem.OnResourceChanged -= HandleResourceChanged;
        }

        void HandleResourceChanged(int index, float current, float max)
        {
            if (index != 0) return; // single Energy pool
            if (view) view.SetEnergy(max > 0f ? current / max : 0f);
        }

        void PaintEnergy()
        {
            var resources = _vesselStatus.ResourceSystem.Resources;
            if (resources.Count > 0 && view)
            {
                var r = resources[0];
                view.SetEnergy(r.MaxAmount > 0f ? r.CurrentAmount / r.MaxAmount : 0f);
            }
        }

        void HandleChargeChanged(float charge01) { if (view) view.SetCharge(charge01); }
        void HandleRushCharges(int current, int max) { if (view) view.SetRushCharges(current, max); }
        void HandleDugIn(bool dugIn) { if (view) view.SetDugIn(dugIn); }
        void HandleScope(bool scoped) { if (view) view.SetScope(scoped); }
        void HandleRoundInFlight(Transform round) { if (view) view.FollowRound(round); }
        void HandleRoundEnded() { if (view) view.ReleaseRound(); }

        void HandleWeaponMode(GrizzlyWeaponModeExecutor.WeaponMode mode)
        {
            if (!view) return;
            view.SetWeaponMode(mode switch
            {
                GrizzlyWeaponModeExecutor.WeaponMode.Sniper => "SNIPER",
                GrizzlyWeaponModeExecutor.WeaponMode.Flamethrower => "PLASMA CLAW",
                _ => "EXPLOSIVES",
            });
        }
    }
}
