using UnityEngine;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;

public sealed class ZoomOutActionExecutor : ShipActionExecutorBase
{
    [Header("Optional refs (auto-resolved if null)")]
    [SerializeField] private GrowTrailActionExecutor   trailProvider;
    [SerializeField] private GrowSkimmerActionExecutor skimmerProvider;

    private IVesselStatus _status;
    private ICameraController _controller;
    private ZoomOutActionSO _so;
    private bool _active;

    // Baselines captured on Begin()
    private float _baseScale;     // provider.MinScale (world)
    private float _baseDistance;  // controller.GetCameraDistance() AT BEGIN (negative for perspective rigs)

    // Camera safety
    [SerializeField] private float farClipPadding = 1.3f; // expand far clip by this factor beyond needed
    [SerializeField] private float maxDistanceAbs = 10000f; // hard clamp, just in case

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        var reg = GetComponent<ActionExecutorRegistry>();
        if (!trailProvider)   trailProvider   = reg?.Get<GrowTrailActionExecutor>();
        if (!skimmerProvider) skimmerProvider = reg?.Get<GrowSkimmerActionExecutor>();
    }

    public void Begin(ZoomOutActionSO so, IVesselStatus status)
    {
        _so = so;
        _controller ??= CameraManager.Instance?.GetActiveController();
        if (_controller == null) return;

        // resolve which scaler drives us
        var provider = Provider();
        if (provider == null) return;

        // capture baselines now
        _baseScale    = Mathf.Max(provider.MinScale, 0.0001f);
        _baseDistance = _controller.GetCameraDistance();      // <- works for both Dynamic/Non-Dynamic
        if (Mathf.Approximately(_baseDistance, 0f))
        {
            // fallback to neutral if available, else a small negative default
            _baseDistance = _controller.NeutralOffsetZ != 0f ? _controller.NeutralOffsetZ : -20f;
        }

        _active = true;
    }

    public void End()
    {
        _active = false;
        _so = null;
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
        if (_so == null) return;

        var provider = Provider();
        if (provider == null) return;

        _controller ??= CameraManager.Instance?.GetActiveController();
        if (_controller == null) return;

        float currentScale = Mathf.Max(provider.CurrentScale, 0.0001f);
        float ratio        = currentScale / _baseScale;

        float target = _baseDistance * ratio;

        if (!Mathf.Approximately(Mathf.Sign(target), Mathf.Sign(_baseDistance)))
            target = -Mathf.Abs(target);

        target = Mathf.Clamp(target, -maxDistanceAbs, -0.01f);

        _controller.SetCameraDistance(target);

        if (_controller is CustomCameraController concrete)
        {
            var cam = concrete.Camera;
            float need = Mathf.Abs(target) * 1.05f; 
            if (need > cam.farClipPlane * 0.95f)
                cam.farClipPlane = need * farClipPadding; 
        }

        if (_so.DebugLogs)
            Debug.Log($"[CamZoom/Exact] scale={currentScale:0.###} baseS={_baseScale:0.###} ratio={ratio:0.###} " +
                      $"baseZ={_baseDistance:0.###} -> targetZ={target:0.###}");
    }
}
