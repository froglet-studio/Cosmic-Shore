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

    private float _baseScale;    
    private float _baseDistance; 

    [SerializeField] private float farClipPadding = 1.3f;
    [SerializeField] private float maxDistanceAbs = 10000f;
    private float _lastScale;
    
    private enum State { Idle, Expanding, Retracting }
    private State _state = State.Idle;
    
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

        float currentScale = Mathf.Max(provider.CurrentScale, 0.0001f);
        float currentZ     = _controller.GetCameraDistance();

        _baseScale    = currentScale;
        _baseDistance = ( _state == State.Idle && _controller.NeutralOffsetZ != 0f )
            ? _controller.NeutralOffsetZ   
            : currentZ;               

        _lastScale = _baseScale; 
        _state     = State.Expanding;
        _active    = true;
        _retracting = false;
    }

    public void End()
    {
        if (_controller == null || _so == null)
        {
            _active = false;
            _retracting = false;
            _state = State.Idle;
            _so = null;
            return;
        }

        _retracting = true;
        _active     = true;
        _state      = State.Retracting;
    }
    private IScaleProvider Provider()
    {
        if (!_so) return null;
        return _so.Source == ZoomOutActionSO.ScaleSource.Skimmer
            ? skimmerProvider
            : trailProvider;
    }

    void LateUpdate()
    {
        if (!_active || _status == null || _status.AutoPilotEnabled || !_so) return;

        var provider = Provider();
        if (provider == null)
        {
            _active = false;
            _retracting = false;
            _state = State.Idle;
            _so = null;
            return;
        }

        _controller ??= CameraManager.Instance?.GetActiveController();
        if (_controller == null) return;

        float rawScale = Mathf.Max(provider.CurrentScale, 0.0001f);

        bool shrinking = (_state == State.Retracting);
        float currentScale = shrinking
            ? Mathf.Min(rawScale, _lastScale + 0.0001f)
            : Mathf.Max(rawScale, _lastScale - 0.0001f);
        _lastScale = currentScale;

        float ratio = currentScale / _baseScale;
        float target = _baseDistance * ratio;

        if (!Mathf.Approximately(Mathf.Sign(target), Mathf.Sign(_baseDistance)))
            target = -Mathf.Abs(target);
        target = Mathf.Clamp(target, -maxDistanceAbs, -0.01f);

        float zNow = _controller.GetCameraDistance();
        if (Mathf.Abs(target - zNow) > 0.0005f) // deadband
            _controller.SetCameraDistance(target);

        if (_controller is CustomCameraController c)
        {
            float need = Mathf.Abs(target) * 1.05f;
            if (need > c.Camera.farClipPlane * 0.95f)
                c.Camera.farClipPlane = need * farClipPadding;
        }

        if (_state == State.Retracting && Mathf.Abs(ratio - 1f) <= 0.0025f)
        {
            _controller.SetCameraDistance(_baseDistance);

            var p = Provider();
            if (p is GrowSkimmerActionExecutor g && g != null)
                g.ResetToMinScale();

            _retracting = false;
            _active = false;
            _state = State.Idle;
            _so = null;
        }
    }

}
