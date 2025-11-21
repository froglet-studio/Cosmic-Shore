using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore.Game.CameraSystem
{
    [CreateAssetMenu(fileName = "ZoomOutAction", menuName = "ScriptableObjects/Vessel Actions/ZoomOut (Camera)")]
    public class ZoomOutActionSO : ShipActionSO
    {
        public enum ScaleSource { Trail, Skimmer }

        [Header("Source of scale")]
        [SerializeField] ScaleSource scaleSource = ScaleSource.Skimmer;

        [Header("Exact match mode (no perceived camera motion)")]
        [SerializeField] bool matchScaleExactly = true;

        [Header("Legacy multipliers (used only when matchScaleExactly = false)")]
        [Min(0)] [SerializeField] float zoomOutMultiplier = 6f;
        [Min(0)] [SerializeField] float zoomInMultiplier  = 1f;

        [Header("Debug")]
        [SerializeField] bool debugLogs = false;

        public ScaleSource Source          => scaleSource;
        public bool MatchScaleExactly      => matchScaleExactly;
        public float ZoomOutMultiplier     => zoomOutMultiplier;
        public float ZoomInMultiplier      => zoomInMultiplier;
        public bool DebugLogs              => debugLogs;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ZoomOutActionExecutor>()?.Begin(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ZoomOutActionExecutor>()?.End();
    }
}