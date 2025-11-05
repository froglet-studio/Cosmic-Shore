using UnityEngine;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;
using Obvious.Soap;

[DefaultExecutionOrder(-1000)]
public sealed class ZoomOutActionExecutor : ShipActionExecutorBase
{
    [Header("Optional refs (auto-resolved if null)")]
    [SerializeField] private GrowTrailActionExecutor   trailProvider;
    [SerializeField] private GrowSkimmerActionExecutor skimmerProvider;
    [Header("Events")]
    [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;
    
    private IVesselStatus _status;
    private ICameraController _controller;
    private ZoomOutActionSO _so;

    private bool _active;
    private bool _retracting;

    private float _baseScale;     // provider.MinScale (stable world baseline)
    private float _baseDistance;  // camera Z captured on first Begin from Idle

    [SerializeField] private float farClipPadding = 1.3f;
    [SerializeField] private float maxDistanceAbs = 10000f;

    private bool _hadAdaptiveZoom = false;

    private enum State { Idle, Expanding, Retracting }
    private State _state = State.Idle;

    private const float RatioEpsilon = 0.0025f;
    private const float DistDeadband = 0.0005f;
    
    void OnEnable()
    {
        if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }
    
    void OnTurnEndOfMiniGame() => End();

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

        if (_state == State.Idle)
        {
            _baseScale    = Mathf.Max(provider.MinScale, 0.0001f);
            _baseDistance = _controller.GetCameraDistance();

            if (_controller is CustomCameraController cc)
            {
                _hadAdaptiveZoom = cc.adaptiveZoomEnabled;
                cc.adaptiveZoomEnabled = false;
            }
        }

        _state      = State.Expanding;
        _retracting = false;
        _active     = true;
    }

    public void End()
    {
        if (_controller == null || !_so)
        {
            CleanupToIdle();
            return;
        }

        if (_state == State.Retracting) return;

        _state      = State.Retracting;
        _retracting = true;
        _active     = true;
    }

    private IScaleProvider Provider()
    {
        if (!_so) return null;
        return _so.Source == ZoomOutActionSO.ScaleSource.Skimmer
            ? skimmerProvider
            : trailProvider;
    }

    private void LateUpdate()
    {
        if (!_active || _status == null || _status.AutoPilotEnabled || !_so) return;

        var provider = Provider();
        if (provider == null) { CleanupToIdle(); return; }

        _controller ??= CameraManager.Instance?.GetActiveController();
        if (_controller == null) { CleanupToIdle(); return; }

        float currentScale = Mathf.Max(provider.CurrentScale, 0.0001f);
        float currentRatio = currentScale / Mathf.Max(_baseScale, 0.0001f);

        float target = _baseDistance * currentRatio;

        if (!Mathf.Approximately(Mathf.Sign(target), Mathf.Sign(_baseDistance)))
            target = -Mathf.Abs(target);

        target = Mathf.Clamp(target, -maxDistanceAbs, -0.01f);

        float zNow = _controller.GetCameraDistance();
        if (Mathf.Abs(target - zNow) > DistDeadband)
            _controller.SetCameraDistance(target);

        if (_controller is CustomCameraController concrete)
        {
            var cam = concrete.Camera;
            float need = Mathf.Abs(target) * 1.05f;
            if (need > cam.farClipPlane * 0.95f)
                cam.farClipPlane = need * farClipPadding;
        }

        if (_state != State.Retracting || !(Mathf.Abs(currentRatio - 1f) <= RatioEpsilon)) return;
        _controller.SetCameraDistance(_baseDistance); 
        if (_controller is CustomCameraController ccRestore)
            ccRestore.adaptiveZoomEnabled = _hadAdaptiveZoom;
        _hadAdaptiveZoom = false;

        _retracting = false;
        _active     = false;
        _state      = State.Idle;
        _so         = null;
    }

    private void CleanupToIdle()
    {
        if (_controller is CustomCameraController ccRestore)
            ccRestore.adaptiveZoomEnabled = _hadAdaptiveZoom;
        _hadAdaptiveZoom = false;

        _retracting = false;
        _active     = false;
        _state      = State.Idle;
        _so         = null;
    }
}
