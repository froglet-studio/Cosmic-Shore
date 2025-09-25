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

    const float ScaleEpsilon = 0.0025f;
    const float DistEpsilon  = 0.0005f;
    private float _lastScale;
    
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

        _lastScale = _baseScale = Mathf.Max(provider.CurrentScale, 0.0001f);

        float currentZ = _controller.GetCameraDistance();
        _baseDistance = (_controller.NeutralOffsetZ != 0f) ? _controller.NeutralOffsetZ : currentZ;
        if (Mathf.Approximately(_baseDistance, 0f))
            _baseDistance = -20f;


        _retracting = false;
        _active     = true;
    }

    public void End()
    {
        if (_controller == null || !_so)
        {
            _active = false;
            _retracting = false;
            _so = null;
            return;
        }

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
        if (!_active || _status == null || _status.AutoPilotEnabled) return;
        if (!_so) return;

        var provider = Provider();
        if (provider == null) { _active = false; _retracting = false; _so = null; return; }

        _controller ??= CameraManager.Instance?.GetActiveController();
        if (_controller == null) return;

        float currentScale = Mathf.Max(provider.CurrentScale, 0.0001f);
        float ratio        = currentScale / _baseScale;
        float target       = _baseDistance * ratio;

        if (!Mathf.Approximately(Mathf.Sign(target), Mathf.Sign(_baseDistance)))
            target = -Mathf.Abs(target);

        target = Mathf.Clamp(target, -maxDistanceAbs, -0.01f);

        float currentZ = _controller.GetCameraDistance();
        if (Mathf.Abs(target - currentZ) > DistEpsilon)
            _controller.SetCameraDistance(target);

        if (_controller is CustomCameraController concrete)
        {
            var cam = concrete.Camera;
            float need = Mathf.Abs(target) * 1.05f;
            if (need > cam.farClipPlane * 0.95f)
                cam.farClipPlane = need * farClipPadding;
        }

        if (_retracting && Mathf.Abs(ratio - 1f) <= ScaleEpsilon)
        {
            _controller.SetCameraDistance(_baseDistance);
            _retracting = false;
            _active     = false;
            _so = null;
        }

        if (_so && _so.DebugLogs)
            Debug.Log($"[CamZoom/Exact] ratio={ratio:0.###} baseZ={_baseDistance:0.###} -> targetZ={target:0.###} (upd)");
    }
}
