using CosmicShore.Game.CameraSystem;
using UnityEngine;

public class ZoomOutAction : ShipAction
{
    private IScaleProvider _scaleSource;
    private ICameraController _controller;
    
    [Header("Zoom multipliers")]
    [Min(0)] [SerializeField]
    private float zoomOutMultiplier = 6f;
    [Min(0)] [SerializeField] private float zoomInMultiplier  = 1f;
    [SerializeField] private bool debugLogs = false;

    private enum ZoomDir
    {
        None, Out, In
    }

    private const float EPS = 1e-4f;
    private ZoomDir _zoomDir   = ZoomDir.None;
    private float _prevRatio = 1f;
    private float _vel; 
    private const float MaxZoomSpeed = 150f;  
    private bool _autoPilotEnabled;
    
    public override void StartAction()
    {
        if (!Ship.ShipStatus.AutoPilotEnabled)
        {
        }
    }
    
    public override void StopAction()
    {
        if (!Ship.ShipStatus.AutoPilotEnabled)
        {

        }
    }

    public void Initialise(IScaleProvider provider)
    {
        if (Ship.ShipStatus.AutoPilotEnabled) return;
        _scaleSource = provider;
        Debug.Log("Initialized Zoom Out Action"); 
    }

    private void LateUpdate()
    {
        if (Ship == null || Ship.ShipStatus.AutoPilotEnabled) return;
        _controller ??= CameraManager.Instance.GetActiveController();
        if (_scaleSource == null || _controller == null) return;
        
        var ratio = _scaleSource.CurrentScale / Mathf.Max(_scaleSource.MinScale, 0.0001f);

        if (ratio > _prevRatio + EPS) _zoomDir = ZoomDir.Out;
        else if (ratio < _prevRatio - EPS) _zoomDir = ZoomDir.In;
        
        var newDir = ratio > _prevRatio + EPS ? ZoomDir.Out
            : ratio < _prevRatio - EPS ? ZoomDir.In
            : _zoomDir;

        if (newDir != _zoomDir) _vel = 0f;
        _zoomDir = newDir;
        
        var eff = _zoomDir switch
        {
            ZoomDir.Out => ratio * zoomOutMultiplier,
            ZoomDir.In => ratio / Mathf.Max(zoomInMultiplier, 0.0001f),
            _ => ratio
        };

        var neutralZ = _controller.NeutralOffsetZ; // e.g. –45
        var targetZ = Mathf.Min(neutralZ * eff, neutralZ); // never inside neutral

        var newZ = Mathf.SmoothDamp(_controller.GetCameraDistance(), targetZ, ref _vel, _controller.ZoomSmoothTime, MaxZoomSpeed);    
        
        _controller.SetCameraDistance(newZ);
        
        Debug.Log($"[CamZoom] {_zoomDir,3} | Ratio={ratio:0.#} → Eff={eff:0.#} " + $"TargetZ={targetZ:0.#} ActZ={newZ:0.#}");

        _prevRatio = ratio;
    }
}