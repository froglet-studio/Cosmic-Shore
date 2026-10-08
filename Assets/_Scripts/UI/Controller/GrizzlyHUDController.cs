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

        /// <summary>Name of the pool the trigger bombs spend. Bound BY NAME so a reordered
        /// resource list cannot point the bar at the cannon's Energy.</summary>
        const string AmmoResourceName = "Ammo";

        void HandleResourceChanged(int index, float current, float max)
        {
            if (!view) return;
            float fill = max > 0f ? current / max : 0f;
            if (index == 0) view.SetEnergy(fill);              // the charged cannon's Energy
            else if (index == AmmoIndex()) view.SetAmmo(fill); // the trigger bombs' Ammo
        }

        void PaintEnergy()
        {
            var resources = _vesselStatus.ResourceSystem.Resources;
            if (!view) return;
            if (resources.Count > 0)
            {
                var r = resources[0];
                view.SetEnergy(r.MaxAmount > 0f ? r.CurrentAmount / r.MaxAmount : 0f);
            }
            int ammo = AmmoIndex();
            if (ammo >= 0)
            {
                var r = resources[ammo];
                view.SetAmmo(r.MaxAmount > 0f ? r.CurrentAmount / r.MaxAmount : 0f);
            }
        }

        int AmmoIndex()
        {
            var system = _vesselStatus != null ? _vesselStatus.ResourceSystem : null;
            var resources = system ? system.Resources : null;
            if (resources == null) return -1;
            for (int i = 0; i < resources.Count; i++)
                if (resources[i] != null && resources[i].Name == AmmoResourceName) return i;
            return -1;
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
