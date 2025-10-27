using UnityEngine;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;

[DefaultExecutionOrder(-1000)]
public sealed class ZoomOutActionExecutor : ShipActionExecutorBase
{
    [Header("Optional refs (auto-resolved if null)")]
    [SerializeField] private GrowTrailActionExecutor   trailProvider;
    [SerializeField] private GrowSkimmerActionExecutor skimmerProvider;

    private IVesselStatus _status;
    private ICameraController _controller;
    private ZoomOutActionSO _so;

    private bool _active;
    private bool _retracting;

    // Canonical baseline (never rebased during a session)
    private float _baseScale;     // provider.MinScale (stable world baseline)
    private float _baseDistance;  // camera Z captured on first Begin from Idle

    [SerializeField] private float farClipPadding = 1.3f;
    [SerializeField] private float maxDistanceAbs = 10000f;

    private bool _hadAdaptiveZoom = false;

    private enum State { Idle, Expanding, Retracting }
    private State _state = State.Idle;

    // How close ratio must be to baseline to consider retract finished
    private const float RatioEpsilon = 0.0025f;
    // Don't thrash distance for sub-mm deltas
    private const float DistDeadband = 0.0005f;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
    }

    public void Begin(ZoomOutActionSO so, IVesselStatus status)
    {
        _so = so;
        _controller ??= CameraManager.Instance?.GetActiveController();
        if (_controller == null) return;

        var provider = Provider();
        if (provider == null) return;

        // Only capture baselines when coming from Idle.
        if (_state == State.Idle)
        {
            // Canonical baseline: stable minimal world scale from the provider.
            _baseScale    = Mathf.Max(provider.MinScale, 0.0001f);
            _baseDistance = _controller.GetCameraDistance();

            // Take control of Z (pause adaptive zoom while action is active).
            if (_controller is CustomCameraController cc)
            {
                _hadAdaptiveZoom = cc.adaptiveZoomEnabled;
                cc.adaptiveZoomEnabled = false;
            }
        }

        // If we were retracting, just flip to expanding; if already expanding, do nothing special.
        _state      = State.Expanding;
        _retracting = false;
        _active     = true;
    }

    public void End()
    {
        if (_controller == null || _so == null)
        {
            CleanupToIdle();
            return;
        }

        // If already retracting, ignore.
        if (_state == State.Retracting) return;

        _state      = State.Retracting;
        _retracting = true;
        _active     = true;
    }

    private IScaleProvider Provider()
    {
        if (_so == null) return null;
        return _so.Source == ZoomOutActionSO.ScaleSource.Skimmer
            ? skimmerProvider
            : trailProvider;
    }

    private void LateUpdate()
    {
        if (!_active || _status == null || _status.AutoPilotEnabled || _so == null) return;

        var provider = Provider();
        if (provider == null) { CleanupToIdle(); return; }

        _controller ??= CameraManager.Instance?.GetActiveController();
        if (_controller == null) { CleanupToIdle(); return; }

        // Direct ratio against canonical baseline — no re-basing, no last-scale memory.
        float currentScale = Mathf.Max(provider.CurrentScale, 0.0001f);
        float currentRatio = currentScale / Mathf.Max(_baseScale, 0.0001f);

        float target = _baseDistance * currentRatio;

        // Keep camera behind the target (negative).
        if (!Mathf.Approximately(Mathf.Sign(target), Mathf.Sign(_baseDistance)))
            target = -Mathf.Abs(target);

        // Hard safety
        target = Mathf.Clamp(target, -maxDistanceAbs, -0.01f);

        // Apply only if meaningfully different (avoid micro jitter).
        float zNow = _controller.GetCameraDistance();
        if (Mathf.Abs(target - zNow) > DistDeadband)
            _controller.SetCameraDistance(target);

        // Far clip guard for very large scales
        if (_controller is CustomCameraController concrete)
        {
            var cam = concrete.Camera;
            float need = Mathf.Abs(target) * 1.05f;
            if (need > cam.farClipPlane * 0.95f)
                cam.farClipPlane = need * farClipPadding;
        }

        // Finish retract cleanly when we're basically back to baseline (ratio≈1).
        if (_state == State.Retracting && Mathf.Abs(currentRatio - 1f) <= RatioEpsilon)
        {
            _controller.SetCameraDistance(_baseDistance);
            // Provider will finish its own shrink; no need to force scale here.

            // Restore adaptive zoom ownership and go idle.
            if (_controller is CustomCameraController ccRestore)
                ccRestore.adaptiveZoomEnabled = _hadAdaptiveZoom;
            _hadAdaptiveZoom = false;

            _retracting = false;
            _active     = false;
            _state      = State.Idle;
            _so         = null;
        }
    }

    private void CleanupToIdle()
    {
        // Restore adaptive zoom if we had disabled it.
        if (_controller is CustomCameraController ccRestore)
            ccRestore.adaptiveZoomEnabled = _hadAdaptiveZoom;
        _hadAdaptiveZoom = false;

        _retracting = false;
        _active     = false;
        _state      = State.Idle;
        _so         = null;
    }
}
