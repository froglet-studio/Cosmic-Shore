using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        enum LatchInput { None, Front, Rear }
        enum LatchState { Idle, FrontLocked }

        const float LatchRetrySeconds = 1f;
        const float LatchWindowMultiplier = 1.55f;
        const float MinimumEffectiveLatchWindow = 42f;
        const float RearLatchGraceSeconds = 2f;

        LatchState _latchState;
        float _frontLatchTimer;
        float _frontLatchSignedDelta;
        float _frontRingShotTimer;
        float _rearRingShotTimer;

        void TickLatchState(float dt)
        {
            _frontRingShotTimer = Mathf.Max(0f, _frontRingShotTimer - dt);
            _rearRingShotTimer = Mathf.Max(0f, _rearRingShotTimer - dt);

            if (_latchState != LatchState.FrontLocked)
                return;

            _frontLatchTimer = Mathf.Max(0f, _frontLatchTimer - dt);
            if (_frontLatchTimer > 0f)
                return;

            LogLatchAttempt("front_expired", LatchInput.Rear, false);
            ResetLatchTransferState();
        }

        void TryTransferLatch(LatchInput input)
        {
            if (_currentFilamentIndex >= _targetTransfers)
                return;

            if (input == LatchInput.Front)
            {
                FireFrontLatch();
                return;
            }

            if (input == LatchInput.Rear)
                FireRearLatch();
        }

        void FireFrontLatch()
        {
            if (_latchState == LatchState.FrontLocked)
            {
                LogLatchAttempt("front_already_locked", LatchInput.Front, true);
                return;
            }

            PlayLatchFireSound();
            _frontRingShotTimer = 0.35f;

            if (_missTimer > 0f)
            {
                PlayLatchMissSound();
                LogLatchAttempt("front_cooldown", LatchInput.Front, false);
                return;
            }

            float signedDelta = CurrentFilament.TransferDistance - _distanceOnFilament;
            if (IsInFrontLatchWindow(signedDelta))
            {
                LockFrontLatch(signedDelta, "front_locked");
                return;
            }

            MissLatch("front_missed", LatchInput.Front, signedDelta);
        }

        void FireRearLatch()
        {
            if (_latchState != LatchState.FrontLocked)
            {
                PlayLatchMissSound();
                LogLatchAttempt("rear_blocked_no_front", LatchInput.Rear, false);
                return;
            }

            PlayLatchFireSound();
            _rearRingShotTimer = 0.35f;

            PlayLatchSurgeSound();
            LogLatchAttempt("rear_locked_transfer", LatchInput.Rear, true);
            CompleteTransfer();
        }

        void TryHeldLatchRequests()
        {
            if (_currentFilamentIndex >= _targetTransfers)
                return;

            if (_latchState == LatchState.FrontLocked)
            {
                if (IsRearLatchHeld())
                    FireRearLatch();
                return;
            }

            if (_missTimer > 0f || !IsFrontLatchHeld())
                return;

            float signedDelta = CurrentFilament.TransferDistance - _distanceOnFilament;
            if (IsInFrontLatchWindow(signedDelta))
                LockFrontLatch(signedDelta, "front_locked_held");
        }

        void LockFrontLatch(float signedDelta, string result)
        {
            _latchState = LatchState.FrontLocked;
            _frontLatchTimer = RearLatchGraceSeconds;
            _frontLatchSignedDelta = signedDelta;
            _missTimer = 0f;
            PlayLatchLockSound();
            SpawnContactBurst(AttachPoint(CurrentFilament, _distanceOnFilament), new Color(0.12f, 0.9f, 1f, 1f), 1.1f);
            LogLatchAttempt(result, LatchInput.Front, true, signedDelta);
        }

        bool IsInFrontLatchWindow(float signedDelta)
        {
            return Mathf.Abs(signedDelta) <= CurrentLatchWindow();
        }

        void MissLatch(string result, LatchInput input, float signedDelta)
        {
            ResetLatchTransferState();
            _missTimer = Mathf.Max(missCooldown, LatchRetrySeconds);
            _speed *= 0.94f;
            _impactTimer = 0.28f;
            PlayLatchMissSound();
            SpawnContactBurst(_vessel?.Transform.position ?? AttachPoint(CurrentFilament, _distanceOnFilament), new Color(1f, 0.18f, 0.08f, 1f), 0.9f);
            LogLatchAttempt(result, input, false, signedDelta);
        }

        void ResetLatchTransferState()
        {
            _latchState = LatchState.Idle;
            _frontLatchTimer = 0f;
            _frontLatchSignedDelta = 0f;
        }

        void LogLatchAttempt(string result, LatchInput input, bool success, float? signedDeltaOverride = null)
        {
            float signedDelta = signedDeltaOverride ?? (CurrentFilament.TransferDistance - _distanceOnFilament);
            CSDebug.Log(
                $"[BulkFilamentsInput] t={_elapsedTime:0.00} input={input} phase={_latchState} result={result} success={success} " +
                $"transfer={_successfulTransfers}/{_targetTransfers} delta={signedDelta:0.0} window={CurrentLatchWindow():0.0} " +
                $"frontTimer={_frontLatchTimer:0.00} speed={_speed:0.0} frontDelta={_frontLatchSignedDelta:0.0}");
        }

        float CurrentLatchWindow()
        {
            float speed01 = Mathf.InverseLerp(minimumSpeed, CurrentMaximumSpeed, _speed);
            float authoredWindow = Mathf.Lerp(slowSpeedLatchWindow, fastSpeedLatchWindow, speed01);
            return Mathf.Max(MinimumEffectiveLatchWindow, authoredWindow * LatchWindowMultiplier);
        }
    }
}
