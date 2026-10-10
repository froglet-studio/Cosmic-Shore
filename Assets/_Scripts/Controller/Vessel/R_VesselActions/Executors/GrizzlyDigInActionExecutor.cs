using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Dig In: translation-restricted turret stance with boosted Energy regen.
    ///
    /// Every stuck-state from the restoration branch is closed here:
    ///  - un-plant ALWAYS routes through VesselController.SetTranslationRestricted so the
    ///    NetworkVariable stays in sync (never a bare IVesselStatus write),
    ///  - gain-rate restore is idempotent (recomputed from initialResourceGainRate, never
    ///    compounded), and runs on turn end, disable, AND re-Initialize,
    ///  - external un-plants (e.g. being launched by your own blast — see
    ///    VesselImpulseByExplosionEffectSO) are reconciled lazily in ReapplyRegen.
    /// </summary>
    public sealed class GrizzlyDigInActionExecutor : ShipActionExecutorBase
    {
        [Header("Scene Refs")]
        [SerializeField] VesselPrismController vesselPrismController;

        [Header("Events")]
        [SerializeField] ScriptableEventBool stationaryModeChanged;
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;

        /// <summary>For the HUD: dug-in state changes.</summary>
        public event System.Action<bool> OnDugInChanged;
        public bool IsDugIn { get; private set; }

        IVesselStatus _status;
        GrizzlyDigInActionSO _activeConfig;
        int _lastToggleFrame = -1;

        void OnEnable()
        {
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised += End;
        }

        void OnDisable()
        {
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised -= End;
            End();
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            // A re-initialize (vessel swap / respawn) must never inherit a stale plant.
            if (_status != null && !ReferenceEquals(_status, shipStatus))
                End();

            _status = shipStatus;

            if (vesselPrismController == null)
                vesselPrismController = shipStatus?.VesselPrismController;
        }

        public void Toggle(GrizzlyDigInActionSO so, IVesselStatus status)
        {
            if (!so || status == null) return;

            // Local press + server RPC echo can land in one frame — dedupe.
            if (Time.frameCount == _lastToggleFrame) return;
            _lastToggleFrame = Time.frameCount;

            var controller = status.Vessel as VesselController;
            if (!controller)
            {
                CSDebug.LogWarning("[GrizzlyDigIn] No VesselController — cannot toggle safely.");
                return;
            }

            bool netActive = NetworkManager.Singleton && NetworkManager.Singleton.IsListening &&
                             (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer);
            bool hasAuthority = !netActive || NetworkManager.Singleton.IsServer || controller.IsOwner;
            if (!hasAuthority) return;

            bool digIn = !status.IsTranslationRestricted;

            controller.SetTranslationRestricted(digIn);

            if (digIn)
            {
                vesselPrismController?.StopSpawn();
                _activeConfig = so;
            }
            else
            {
                vesselPrismController?.StartSpawn();
            }

            ApplyRegen(so, digIn);
            if (!digIn) _activeConfig = null;

            IsDugIn = digIn;
            OnDugInChanged?.Invoke(digIn);
            stationaryModeChanged?.Raise(digIn);
        }

        /// <summary>
        /// Recomputes the boosted rate from base values — safe to call repeatedly and
        /// used by the HUD/elemental layer when Charge levels shift while dug in.
        /// Also reconciles external un-plants (self-launch blasts a dug-in Grizzly out
        /// of stance without going through Toggle).
        /// </summary>
        public void ReapplyRegen()
        {
            if (_activeConfig == null || _status == null) return;

            if (!_status.IsTranslationRestricted)
            {
                // Something external un-planted us — restore base rate and clear state.
                End();
                return;
            }

            ApplyRegen(_activeConfig, true);
        }

        void ApplyRegen(GrizzlyDigInActionSO so, bool boosted)
        {
            if (_status?.ResourceSystem == null || so == null) return;
            var resources = _status.ResourceSystem.Resources;
            if (so.EnergyIndex < 0 || so.EnergyIndex >= resources.Count) return;

            var resource = resources[so.EnergyIndex];
            if (boosted)
            {
                float chargeMul = ElementalScaling.Multiplier(
                    _status, Element.Charge, so.ChargeScaleAtFull, so.ChargeScaleMinMul);
                resource.resourceGainRate = resource.initialResourceGainRate *
                                            so.StationaryGainMultiplier * chargeMul;
            }
            else
            {
                resource.resourceGainRate = resource.initialResourceGainRate;
            }
        }

        void End()
        {
            var so = _activeConfig;
            _activeConfig = null;

            if (_status != null)
            {
                // Idempotent restore even if we were never (or are no longer) planted.
                if (so != null) ApplyRegen(so, false);

                if (_status.IsTranslationRestricted)
                {
                    // Netvar-safe when the controller is available; the raw write is a
                    // last-resort fallback so a local vessel can never be stuck planted.
                    if (_status.Vessel is VesselController controller)
                        controller.SetTranslationRestricted(false);
                    else
                        _status.IsTranslationRestricted = false;

                    vesselPrismController?.StartSpawn();
                }
            }

            if (IsDugIn)
            {
                IsDugIn = false;
                OnDugInChanged?.Invoke(false);
                stationaryModeChanged?.Raise(false);
            }
        }
    }
}
