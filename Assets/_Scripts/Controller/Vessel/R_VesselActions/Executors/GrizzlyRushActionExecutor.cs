using System;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using CosmicShore.Soap;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Rush: an Energy-costed forward momentum burst with banked charges, and a RAM: for the burst's
    /// length every rival prism within <see cref="GrizzlyRushActionSO.RamRadius"/> of the hull is smashed (plain and
    /// shielded; super-shielded mass and a silenced pilot's ram deflect, Prism.Damage's own gates). Time sets the
    /// refill. The ram runs on the vessel's authority only, so a prism is broken once.
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
        CancellationTokenSource _ramCts;
        readonly System.Collections.Generic.List<Prism> _ramScratch = new();

        /// <summary>HUD: a ram began (the card flashes).</summary>
        public event Action OnRam;
        /// <summary>HUD: the share of the next charge's refill still to wait, 1..0 (0 when the bank is full).</summary>
        public float RefillRemaining01 { get; private set; }
        float _refillStartedAt, _refillSeconds;

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
            CancelToken(ref _ramCts);
        }

        void Update()
        {
            RefillRemaining01 = _refillCts != null && _refillSeconds > 0f
                ? Mathf.Clamp01(1f - (Time.time - _refillStartedAt) / _refillSeconds)
                : 0f;
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _charges = -1;              // re-seed from SO on next rush attempt
            _lastRushTime = float.NegativeInfinity;
            CancelToken(ref _refillCts);
            CancelToken(ref _pulseCts);
            CancelToken(ref _ramCts);
        }

        public void TryRush(GrizzlyRushActionSO so, IVesselStatus status)
        {
            if (!so || status?.VesselTransformer == null || status.ResourceSystem == null)
                return;

            SeedCharges(so);

            if (_charges <= 0) return;
            if (Time.time - _lastRushTime < so.CooldownSeconds) return;

            float cost = so.EnergyCost;

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

            OnRam?.Invoke();
            if (HasAuthority(status))
            {
                CancelToken(ref _ramCts);
                _ramCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
                RamAsync(so, status, _ramCts.Token).Forget();
            }

            EnsureRefill(so);
        }

        /// <summary>The ram: while the burst lasts, smash every rival prism the hull ploughs into.</summary>
        async UniTaskVoid RamAsync(GrizzlyRushActionSO so, IVesselStatus status, CancellationToken token)
        {
            try
            {
                float until = Time.time + so.Duration;
                float tick = Mathf.Max(0.02f, so.RamTickSeconds);
                while (!token.IsCancellationRequested && Time.time < until)
                {
                    RamSweep(so, status);
                    await UniTask.Delay(TimeSpan.FromSeconds(tick), DelayType.DeltaTime, PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        void RamSweep(GrizzlyRushActionSO so, IVesselStatus status)
        {
            var index = PrismSpatialIndex.Instance;
            var hull = status?.Vessel?.Transform;
            if (index == null || !index.IsAvailable || hull == null) return;

            Vector3 at = hull.position;
            Vector3 kick = hull.forward * so.RamImpactSpeed;
            _ramScratch.Clear();
            index.QuerySphere(at, so.RamRadius, _ramScratch);
            for (int i = 0; i < _ramScratch.Count; i++)
            {
                var prism = _ramScratch[i];
                if (!prism || prism.destroyed || prism.Domain == status.Domain) continue;
                // Damage owns every gate: silence deflects, super-shields deflect (the wobble), the rest breaks.
                prism.Damage(kick, status.Domain, status.PlayerName, devastate: true);
            }
            _ramScratch.Clear();
        }

        static bool HasAuthority(IVesselStatus status)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            bool net = nm && nm.IsListening && (nm.IsClient || nm.IsServer);
            if (!net) return true;
            return nm.IsServer || status.Vessel is VesselController { IsOwner: true };
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
                    // Time, read per charge: a crystal mid-refill speeds the next one, not this one.
                    _refillSeconds = so.RefillSecondsFor(_status);
                    _refillStartedAt = Time.time;
                    await UniTask.Delay(TimeSpan.FromSeconds(_refillSeconds),
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
            CancelToken(ref _ramCts);
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
