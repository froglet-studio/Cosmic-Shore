// Stand-ins for the live-vessel surface DrillRunner reads. Only the members it touches.
using System;
using CosmicShore.Data;
using UnityEngine;
namespace CosmicShore.Gameplay
{
    public enum InputDeviceFamily { None = 0, Gamepad = 1, KeyboardMouse = 2, Touch = 3 }
    public static class InputDeviceActuation { public static InputDeviceFamily DetectInitial() => InputDeviceFamily.Gamepad; }
    public interface IInputStatus { float XDiff { get; } Vector2 EasedLeftJoystickPosition { get; } Vector2 EasedRightJoystickPosition { get; } }
    public class InputController : MonoBehaviour { public InputDeviceFamily Family = InputDeviceFamily.Gamepad; public InputDeviceFamily ActiveDeviceFamily => Family; }
    public class DriftActionSO { }
    public class R_VesselActionHandler : MonoBehaviour
    {
        public event Action<InputEvents> OnInputEventStarted;
        public bool HasDrift = true;
        public InputEvents DriftInput = InputEvents.LeftStickAction;
        public bool TryGetInputForAction<T>(out InputEvents e) where T : class { e = DriftInput; return HasDrift; }
        public void Raise(InputEvents e) => OnInputEventStarted?.Invoke(e);
        // What a ghost demo pressed and let go, in order ("+X" / "-X"). A press starts the input
        // event like the real handler does, so the harness proves the demo's presses are not counted.
        public readonly System.Collections.Generic.List<string> Log = new();
        public void PerformShipControllerActionsReplicated(InputEvents e) { Log.Add("+" + e); Raise(e); }
        public void StopShipControllerActionsReplicated(InputEvents e) => Log.Add("-" + e);
        public int Subscribers => OnInputEventStarted?.GetInvocationList().Length ?? 0;
    }
    public interface IVesselStatus
    {
        float Speed { get; } bool IsDrifting { get; } bool IsSingleStickControls { get; }
        VesselClassType VesselType { get; } R_VesselActionHandler ActionHandler { get; }
        InputController InputController { get; } IInputStatus InputStatus { get; }
        Vector3 Course { get; }
    }
    public interface IVessel { IVesselStatus VesselStatus { get; } Transform Transform { get; } }
    public class ModePreviewGateCourse : MonoBehaviour
    {
        public int Threaded;
        public int LapsCompleted;
        public bool HasGate;
        public Vector3 Gate;
        public bool TryGetNextGate(out Vector3 position) { position = Gate; return HasGate; }
    }
}
