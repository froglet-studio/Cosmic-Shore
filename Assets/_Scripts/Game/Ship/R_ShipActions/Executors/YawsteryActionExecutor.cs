using System;
using System.Threading;
using UnityEngine;
using CosmicShore.Game;
using Obvious.Soap;
using Cysharp.Threading.Tasks;

public sealed class YawsteryActionExecutor : ShipActionExecutorBase
{
    [Header("Scene Refs")]
    [SerializeField] private VesselTransformer vesselTransformer;
    [SerializeField] private Animator shipAnimator;

    [Header("Events")]
    [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;
    
    public event Action OnYawsteryStarted;
    public event Action OnYawsteryEnded;
    public event Action<float> OnYawsteryIntensityChanged;

    IVesselStatus _status;
    IVessel _ship;
    CancellationTokenSource _cts;
    float _intensity;
    bool _running;

    int _activeSign = 0;
    bool _swapRequested = false;
    YawsteryActionSO _so;
    YawsteryActionSO _requestedSo;
    float _accumulatedYawThisRun;

    void OnEnable()
    {
        OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        // hard cancel on disable
        if (_cts != null) { try { _cts.Cancel(); } catch { } _cts.Dispose(); _cts = null; }
        OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }

    void OnTurnEndOfMiniGame() => End();

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _ship   = shipStatus?.Vessel;

        if (vesselTransformer == null)
            vesselTransformer = _status?.VesselTransformer as VesselTransformer;
    }

    public void Begin(YawsteryActionSO so, IVesselStatus status)
    {
        if (!so || status == null) return;

        int newSign = (int)so.Steer;

        if (_running && _activeSign != 0 && newSign != _activeSign)
        {
            _requestedSo = so;
            _swapRequested = true;
            return;
        }

        _so = so;
        _activeSign = newSign;
        _accumulatedYawThisRun = 0f;

        if (!vesselTransformer)
        {
            Debug.LogWarning("[Yawstery] VesselTransformer not set/resolved.");
            return;
        }

        _running = true;

        // restart loop with fresh CTS
        if (_cts != null) { try { _cts.Cancel(); } catch { } _cts.Dispose(); }
        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        HoldToYawRoutineAsync(_cts.Token).Forget();
    }

    public void End()
    {
        _running = false; // loop will ramp out
    }

    async UniTaskVoid HoldToYawRoutineAsync(CancellationToken token)
    {
        OnYawsteryStarted?.Invoke();

        try
        {
            // Ramp-in
            float t = 0f;
            float rampIn = Mathf.Max(0.01f, _so.RampInSeconds);
            while (_running && _intensity < 1f)
            {
                if (_swapRequested)
                {
                    await SwapDirectionFlowAsync(token);
                    t = 0f; rampIn = Mathf.Max(0.01f, _so.RampInSeconds);
                }

                t += Time.deltaTime;
                _intensity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / rampIn));
                OnYawsteryIntensityChanged?.Invoke(_intensity);

                ApplyYawThisFrame(_intensity);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // Steady hold
            while (_running)
            {
                if (_swapRequested)
                {
                    await SwapDirectionFlowAsync(token);
                }

                OnYawsteryIntensityChanged?.Invoke(_intensity);
                ApplyYawThisFrame(_intensity);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // Ramp-out on release
            float start = _intensity;
            float rampOut = Mathf.Max(0.01f, _so.RampOutSeconds);
            float u = 0f;
            while (_intensity > 0f)
            {
                u += Time.deltaTime;
                _intensity = Mathf.Lerp(start, 0f, Mathf.Clamp01(u / rampOut));
                OnYawsteryIntensityChanged?.Invoke(_intensity);

                ApplyYawThisFrame(_intensity);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _intensity = 0f;
            OnYawsteryIntensityChanged?.Invoke(0f);
            OnYawsteryEnded?.Invoke();
        }
    }

    async UniTask SwapDirectionFlowAsync(CancellationToken token)
    {
        _swapRequested = false;

        float start = _intensity;
        float rampOut = Mathf.Max(0.01f, _so.RampOutSeconds);
        float u = 0f;
        while (_intensity > 0f)
        {
            u += Time.deltaTime;
            _intensity = Mathf.Lerp(start, 0f, Mathf.Clamp01(u / rampOut));
            OnYawsteryIntensityChanged?.Invoke(_intensity);
            ApplyYawThisFrame(_intensity);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
        _intensity = 0f;
        OnYawsteryIntensityChanged?.Invoke(0f);

        if (_requestedSo)
        {
            _so = _requestedSo;
            _requestedSo = null;
        }
        _activeSign = (int)_so.Steer;
        _accumulatedYawThisRun = 0f;

        // Accelerate
        float t = 0f;
        float rampIn = Mathf.Max(0.01f, _so.RampInSeconds);
        while (_running && _intensity < 1f)
        {
            if (_swapRequested) return; 

            t += Time.deltaTime;
            _intensity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / rampIn));
            OnYawsteryIntensityChanged?.Invoke(_intensity);
            ApplyYawThisFrame(_intensity);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }

    void ApplyYawThisFrame(float intensity01)
    {
        if (!vesselTransformer || _status == null || _ship == null) return;
        if (_status.IsTranslationRestricted) return;

        float speedFactor = Mathf.Pow(1f + Mathf.Max(0f, _status.Speed) * 0.01f, _so.SpeedExp);
        float yawPerSec = _so.MaxYawDegPerSec * Mathf.Max(0.0f, _so.SpeedScale) * speedFactor;

        float signed = yawPerSec * (int)_so.Steer;
        float deltaAngle = signed * intensity01 * Time.deltaTime;

        if (_so.LockToAngle)
        {
            float remaining = _so.MaxTurnDegrees - Mathf.Abs(_accumulatedYawThisRun);
            deltaAngle = Mathf.Clamp(deltaAngle, -remaining, remaining);
            _accumulatedYawThisRun += deltaAngle;

            if (Mathf.Abs(_accumulatedYawThisRun) >= _so.MaxTurnDegrees)
                return;
        }

        vesselTransformer.ApplyRotation(deltaAngle, _ship.Transform.up);
    }

    void TryTriggerAnimator(string trigger)
    {
        if (shipAnimator == null || string.IsNullOrEmpty(trigger)) return;
        try { shipAnimator.SetTrigger(trigger); } catch { }
    }

    void TrySetAnimatorFloat(string param, float value)
    {
        if (shipAnimator == null || string.IsNullOrEmpty(param)) return;
        try { shipAnimator.SetFloat(param, value); } catch { }
    }
}
