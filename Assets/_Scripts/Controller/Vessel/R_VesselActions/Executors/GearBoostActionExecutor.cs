using System;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runs a <see cref="GearBoostActionSO"/>'s gear ladder while the triggering input is
    /// held. Each gear engage is a discrete step: BoostMultiplier jumps to the gear's value
    /// and the transformer's smoothed cruise speed is snapped straight to the new target
    /// (no lerp ramp), plus a short decaying forward shove — the drag-race shift kick.
    /// Releasing the input drops back to no boost; the speed then eases down through the
    /// transformer's normal smoothing (engine-brake feel).
    /// </summary>
    public sealed class GearBoostActionExecutor : ShipActionExecutorBase
    {
        [Inject] AudioSystem _audioSystem;

        [SerializeField] public Obvious.Soap.ScriptableEventNoParam OnMiniGameTurnEnd;

        /// <summary>(engaged gear 1-based, total gears). Gear 0 = disengaged. For HUD juice.</summary>
        public event Action<int, int> OnGearChanged;

        IVesselStatus _status;
        GearBoostActionSO _activeSO;
        CancellationTokenSource _cts;
        float _restingMultiplier = 1f;

        public int CurrentGear { get; private set; }

        void OnEnable()
        {
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            End();
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
        }

        void OnTurnEndOfMiniGame() => End();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
        }

        public void Begin(GearBoostActionSO so, IVesselStatus status)
        {
            if (status == null || so == null || so.Gears.Count == 0) return;

            End();

            _status = status;
            _activeSO = so;
            _restingMultiplier = status.BoostMultiplier;
            status.IsBoosting = true;
            status.IsStationary = false;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            ShiftRoutineAsync(so, _cts.Token).Forget();
        }

        public void End()
        {
            if (_cts != null)
            {
                try { _cts.Cancel(); } catch { }
                _cts.Dispose();
                _cts = null;
            }

            if (_activeSO == null) return;

            int gearCount = _activeSO.Gears.Count;
            _activeSO = null;
            CurrentGear = 0;

            if (_status != null)
            {
                _status.BoostMultiplier = _restingMultiplier;
                _status.IsBoosting = false;
            }

            OnGearChanged?.Invoke(0, gearCount);
        }

        async UniTaskVoid ShiftRoutineAsync(GearBoostActionSO so, CancellationToken token)
        {
            try
            {
                for (int i = 0; i < so.Gears.Count; i++)
                {
                    EngageGear(so, i);

                    if (i == so.Gears.Count - 1) break; // top gear holds until release

                    // Element → parameter (Time → shift cadence). Anchored at 1x at resting
                    // level; a high Time element rows through the gears faster.
                    float timeMultiplier = _status?.ElementalAbilityHandler.Multiplier(Element.Time) ?? 1f;
                    float holdSeconds = so.Gears[i].SecondsToNext / Mathf.Max(0.01f, timeMultiplier);

                    await UniTask.Delay(TimeSpan.FromSeconds(holdSeconds),
                                        DelayType.DeltaTime,
                                        PlayerLoopTiming.Update,
                                        token);
                }
            }
            catch (OperationCanceledException) { }
        }

        void EngageGear(GearBoostActionSO so, int index)
        {
            CurrentGear = index + 1;
            _status.BoostMultiplier = so.Gears[index].Multiplier;

            var transformer = _status.VesselTransformer;
            if (transformer != null)
            {
                // The step IS the feel: snap the smoothed cruise speed straight to the new
                // gear's target so the shift lands as an instant jerk, not an ease-in.
                transformer.SnapSpeedToThrottleTarget();

                if (so.ShiftKickSpeed > 0f)
                    transformer.ModifyVelocity(_status.Course * so.ShiftKickSpeed, so.ShiftKickSeconds);
            }

            _audioSystem.PlayGameplaySFX(so.ShiftSFX);
            OnGearChanged?.Invoke(CurrentGear, so.Gears.Count);
        }
    }
}
