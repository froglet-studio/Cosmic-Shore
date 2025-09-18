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

    YawsteryActionSO _so;
    float _accumulatedYawThisRun; // NEW: tracks how much yaw applied this hold

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
        _so = so;
        _accumulatedYawThisRun = 0f; // reset lock counter

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
        //TryTriggerAnimator(_so.AnimStart);
        OnYawsteryStarted?.Invoke();

        float t = 0f;
        float rampIn = Mathf.Max(0.01f, _so.RampInSeconds);
        while (_running && _intensity < 1f)
        {
            t += Time.deltaTime;
            _intensity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / rampIn));
            OnYawsteryIntensityChanged?.Invoke(_intensity);

            ApplyYawThisFrame(_intensity);
            yield return null;
        }

        while (_running)
        {
            OnYawsteryIntensityChanged?.Invoke(_intensity);
            ApplyYawThisFrame(_intensity);
            yield return null;
        }

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

        //TryTriggerAnimator(_so.AnimEnd);
        OnYawsteryEnded?.Invoke();
        _turnRoutine = null;
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
