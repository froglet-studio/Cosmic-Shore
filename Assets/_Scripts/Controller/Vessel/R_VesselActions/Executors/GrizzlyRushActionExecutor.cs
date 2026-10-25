using System;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Rush: an Energy-costed forward momentum burst with banked charges.
    ///
    /// - Charges are executor-instance state (SO assets are shared across every
    ///   Grizzly — per-vessel state must never live on them).
    /// - Rushing while dug in un-plants first (netvar-safe via the dig-in executor's
    ///   own path), preserving the dig-in → bombard → rush-out rhythm.
    /// - Time 5 "Vector Control": the burst is split into sub-pulses applied along
    ///   the LIVE forward vector, so steering mid-rush redirects the charge.
    /// </summary>
    public sealed class GrizzlyRushActionExecutor : ShipActionExecutorBase
    {
        [Inject] AudioSystem audioSystem;

        [Header("Events")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;

        /// <summary>HUD pips: (current, max).</summary>
        public event Action<int, int> OnChargesChanged;

        IVesselStatus _status;
        int _charges = -1;              // -1 = uninitialized; set from SO on first use
        int _maxCharges;
        float _lastRushTime = float.NegativeInfinity;
        CancellationTokenSource _refillCts;
        CancellationTokenSource _pulseCts;

        void OnEnable()
        {
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised += HandleTurnEnd;
        }

        void OnDisable()
        {
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised -= HandleTurnEnd;
            CancelToken(ref _refillCts);
            CancelToken(ref _pulseCts);
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _charges = -1;              // re-seed from SO on next rush attempt
            _lastRushTime = float.NegativeInfinity;
            CancelToken(ref _refillCts);
            CancelToken(ref _pulseCts);
        }

        public void TryRush(GrizzlyRushActionSO so, IVesselStatus status)
        {
            if (!so || status?.VesselTransformer == null || status.ResourceSystem == null)
                return;

            SeedCharges(so);

            if (_charges <= 0) return;
            if (Time.time - _lastRushTime < so.CooldownSeconds) return;

            // Time element: cheaper rushes at higher levels.
            float cost = so.EnergyCost * ElementalScaling.Multiplier(
                status, Element.Time, so.TimeCostAtFull, so.TimeCostMinMul);

            var resources = status.ResourceSystem.Resources;
            if (so.EnergyIndex < 0 || so.EnergyIndex >= resources.Count) return;
            if (resources[so.EnergyIndex].CurrentAmount < cost) return;

            status.ResourceSystem.ChangeResourceAmount(so.EnergyIndex, -cost);

            // Rushing out of turret stance: un-plant through the dig-in executor so
            // regen restore + events + netvar all stay coherent.
            if (status.IsTranslationRestricted &&
                status.Vessel is VesselController controller)
            {
                controller.SetTranslationRestricted(false);
                GetComponent<ActionExecutorRegistry>()?.Get<GrizzlyDigInActionExecutor>()?.ReapplyRegen();
            }

            _charges--;
            _lastRushTime = Time.time;
            OnChargesChanged?.Invoke(_charges, _maxCharges);

            audioSystem.PlayGameplaySFX(GameplaySFXCategory.SpeedBurst);

            bool vectorControl = status.ElementalAbilityHandler != null &&
                                 status.ElementalAbilityHandler.IsUpgradeActive(Element.Time);
            if (vectorControl && so.SteeringPulses > 1)
            {
                CancelToken(ref _pulseCts);
                _pulseCts = CancellationTokenSource.CreateLinkedTokenSource(
                    this.GetCancellationTokenOnDestroy());
                SteeredRushAsync(so, status, _pulseCts.Token).Forget();
            }
            else
            {
                status.VesselTransformer.ModifyVelocity(
                    status.Vessel.Transform.forward * so.Magnitude, so.Duration);
            }

            EnsureRefill(so);
        }

        /// <summary>Time-5: the burst re-aims along live forward each sub-pulse.</summary>
        async UniTaskVoid SteeredRushAsync(GrizzlyRushActionSO so, IVesselStatus status, CancellationToken token)
        {
            try
            {
                int pulses = so.SteeringPulses;
                float pulseDuration = so.Duration / pulses;
                float pulseMagnitude = so.Magnitude; // each pulse is short; magnitude stays full for a continuous feel

                for (int i = 0; i < pulses && !token.IsCancellationRequested; i++)
                {
                    var fwd = status.Vessel?.Transform ? status.Vessel.Transform.forward : Vector3.forward;
                    status.VesselTransformer.ModifyVelocity(fwd * pulseMagnitude, pulseDuration);
                    await UniTask.Delay(TimeSpan.FromSeconds(pulseDuration),
                        DelayType.DeltaTime, PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        void SeedCharges(GrizzlyRushActionSO so)
        {
            if (_charges >= 0) return;
            _maxCharges = so.MaxCharges;
            _charges = so.MaxCharges;
            OnChargesChanged?.Invoke(_charges, _maxCharges);
        }

        void EnsureRefill(GrizzlyRushActionSO so)
        {
            if (_refillCts != null) return; // refill loop already running
            _refillCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            RefillAsync(so, _refillCts.Token).Forget();
        }

        async UniTaskVoid RefillAsync(GrizzlyRushActionSO so, CancellationToken token)
        {
            try
            {
                while (_charges < _maxCharges && !token.IsCancellationRequested)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(so.ChargeRefillSeconds),
                        DelayType.DeltaTime, PlayerLoopTiming.Update, token);
                    _charges = Mathf.Min(_maxCharges, _charges + 1);
                    OnChargesChanged?.Invoke(_charges, _maxCharges);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                _refillCts?.Dispose();
                _refillCts = null;
            }
        }

        void HandleTurnEnd()
        {
            CancelToken(ref _pulseCts);
            CancelToken(ref _refillCts);
            _charges = -1; // fresh bank next turn
        }

        static void CancelToken(ref CancellationTokenSource cts)
        {
            if (cts == null) return;
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }
    }
}
