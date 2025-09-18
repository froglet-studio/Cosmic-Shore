using System;
using System.Collections;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;

public sealed class YawsteryActionExecutor : ShipActionExecutorBase
{
    [Header("Scene Refs")]
    [SerializeField] private VesselTransformer vesselTransformer;
    [SerializeField] private Animator shipAnimator;

    public event Action OnYawsteryStarted;
    public event Action OnYawsteryEnded;
    public event Action<float> OnYawsteryIntensityChanged;

    IVesselStatus _status;
    IVessel _ship;
    Coroutine _turnRoutine;
    float _intensity;
    bool _running;

    int _activeSign = 0;                  
    bool _swapRequested = false;
    YawsteryActionSO _so;
    YawsteryActionSO _requestedSo;
    float _accumulatedYawThisRun; 

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _ship   = shipStatus?.Vessel;

        if (vesselTransformer == null)
            vesselTransformer = _status?.VesselTransformer as VesselTransformer;
    }

    public void Begin(YawsteryActionSO so, IVesselStatus status)
    {
        if (so == null || status == null) return;

        int newSign = (int)so.Steer;

        if (_running && _activeSign != 0 && newSign != _activeSign)
        {
            _requestedSo = so;
            _swapRequested = true;
            return;
        }

        // Normal begin
        _so = so;
        _activeSign = newSign;
        _accumulatedYawThisRun = 0f;

        if (vesselTransformer == null)
        {
            Debug.LogWarning("[Yawstery] VesselTransformer not set/resolved.");
            return;
        }

        _running = true;
        if (_turnRoutine != null) StopCoroutine(_turnRoutine);
        _turnRoutine = StartCoroutine(HoldToYawRoutine());
    }

    public void End()
    {
        _running = false; // routine will ramp out
    }

    IEnumerator HoldToYawRoutine()
    {
        OnYawsteryStarted?.Invoke();

        // Ramp-in
        float t = 0f;
        float rampIn = Mathf.Max(0.01f, _so.RampInSeconds);
        while (_running && _intensity < 1f)
        {
            // If swap is requested during ramp-in → do swap flow
            if (_swapRequested)
            {
                yield return StartCoroutine(SwapDirectionFlow());
                // After swap, restart ramp-in timing
                t = 0f; rampIn = Mathf.Max(0.01f, _so.RampInSeconds);
            }

            t += Time.deltaTime;
            _intensity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / rampIn));
            OnYawsteryIntensityChanged?.Invoke(_intensity);

            ApplyYawThisFrame(_intensity);
            yield return null;
        }

        // Steady hold
        while (_running)
        {
            if (_swapRequested)
            {
                yield return StartCoroutine(SwapDirectionFlow());
            }

            OnYawsteryIntensityChanged?.Invoke(_intensity);
            ApplyYawThisFrame(_intensity);
            yield return null;
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
            yield return null;
        }

        _intensity = 0f;
        OnYawsteryIntensityChanged?.Invoke(0f);
        OnYawsteryEnded?.Invoke();
        _turnRoutine = null;
    }
    
    IEnumerator SwapDirectionFlow()
    {
        _swapRequested = false;

        // Decelerate
        float start = _intensity;
        float rampOut = Mathf.Max(0.01f, _so.RampOutSeconds);
        float u = 0f;
        while (_intensity > 0f)
        {
            u += Time.deltaTime;
            _intensity = Mathf.Lerp(start, 0f, Mathf.Clamp01(u / rampOut));
            OnYawsteryIntensityChanged?.Invoke(_intensity);
            ApplyYawThisFrame(_intensity);
            yield return null;
        }
        _intensity = 0f;
        OnYawsteryIntensityChanged?.Invoke(0f);

        // Swap to requested SO
        if (_requestedSo != null)
        {
            _so = _requestedSo;
            _requestedSo = null;
        }
        _activeSign = (int)_so.Steer;
        _accumulatedYawThisRun = 0f; // reset lock counter

        // Accelerate
        float t = 0f;
        float rampIn = Mathf.Max(0.01f, _so.RampInSeconds);
        while (_running && _intensity < 1f)
        {
            // If user requests another swap immediately, break to let outer loop handle it
            if (_swapRequested) yield break;

            t += Time.deltaTime;
            _intensity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / rampIn));
            OnYawsteryIntensityChanged?.Invoke(_intensity);
            ApplyYawThisFrame(_intensity);
            yield return null;
        }
    }

    void ApplyYawThisFrame(float intensity01)
    {
        if (vesselTransformer == null || _status == null || _ship == null) return;
        if (_status.IsStationary) return;

        float speedFactor = Mathf.Pow(1f + Mathf.Max(0f, _status.Speed) * 0.01f, _so.SpeedExp);
        float yawPerSec = _so.MaxYawDegPerSec * Mathf.Max(0.0f, _so.SpeedScale) * speedFactor;

        float signed = yawPerSec * (int)_so.Steer;
        float deltaAngle = signed * intensity01 * Time.deltaTime;

        // NEW: enforce lock-to-angle if enabled
        if (_so.LockToAngle)
        {
            float remaining = _so.MaxTurnDegrees - Mathf.Abs(_accumulatedYawThisRun);
            deltaAngle = Mathf.Clamp(deltaAngle, -remaining, remaining);
            _accumulatedYawThisRun += deltaAngle;

            if (Mathf.Abs(_accumulatedYawThisRun) >= _so.MaxTurnDegrees)
            {
                // hit the cap → stop applying further
                return;
            }
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

    void Update()
    {
        // if (_intensity > 0f)
        //     TrySetAnimatorFloat(_so.AnimFloat, _intensity);
        // else
        //     TrySetAnimatorFloat(_so.AnimFloat, 0f);
    }
}
