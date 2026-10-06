// Stubs for R_VesselActionHandler's world. Every signature is transcribed from the tree
// (grep'd 2026-10-06); Netcode/UniTask/Unity shapes are the minimum the handler touches.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace UnityEngine
{
    public class Object { public bool Destroyed; public string name = "";
        public static implicit operator bool(Object o) => o is { Destroyed: false }; }
    public class Component : Object { }
    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { }
    public struct Vector3 { public float x, y, z; public static Vector3 zero => default; }
    public static class Time { public static float time; }
    public static class Mathf { public static float Max(float a, float b) => a > b ? a : b; }
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
}

namespace Unity.Netcode
{
    public enum NetworkVariableReadPermission { Everyone, Owner }
    public enum NetworkVariableWritePermission { Server, Owner }
    public class NetworkVariable<T>
    {
        public T Value;
        public NetworkVariable(T value = default, NetworkVariableReadPermission readPerm = NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission writePerm = NetworkVariableWritePermission.Server) => Value = value;
    }
    public abstract class NetworkBehaviour : UnityEngine.MonoBehaviour
    {
        public bool IsSpawned { get; set; }
        public bool IsOwner { get; set; }
        public virtual void OnNetworkSpawn() { }
        public virtual void OnNetworkDespawn() { }
    }
    [AttributeUsage(AttributeTargets.Method)] public class ServerRpcAttribute : Attribute { public bool RequireOwnership = true; }
    [AttributeUsage(AttributeTargets.Method)] public class ClientRpcAttribute : Attribute { }
}

namespace Cysharp.Threading.Tasks
{
    public enum PlayerLoopTiming { Update }
    public readonly struct UniTask
    {
        public static UniTask Yield(PlayerLoopTiming t, CancellationToken ct) => default;
        public Awaiter GetAwaiter() => default;
        public readonly struct Awaiter : INotifyCompletion
        { public bool IsCompleted => true; public void GetResult() { } public void OnCompleted(Action a) => a(); }
    }
    [AsyncMethodBuilder(typeof(UniTaskVoidBuilder))]
    public readonly struct UniTaskVoid { public void Forget() { } }
    public struct UniTaskVoidBuilder
    {
        public static UniTaskVoidBuilder Create() => default;
        public UniTaskVoid Task => default;
        public void Start<TStateMachine>(ref TStateMachine sm) where TStateMachine : IAsyncStateMachine => sm.MoveNext();
        public void SetStateMachine(IAsyncStateMachine sm) { }
        public void SetResult() { }
        public void SetException(Exception e) => throw e;
        public void AwaitOnCompleted<TA, TSM>(ref TA a, ref TSM sm) where TA : INotifyCompletion where TSM : IAsyncStateMachine { }
        public void AwaitUnsafeOnCompleted<TA, TSM>(ref TA a, ref TSM sm) where TA : ICriticalNotifyCompletion where TSM : IAsyncStateMachine { }
    }
}

namespace Obvious.Soap
{
    public abstract class ScriptableEvent<T> : UnityEngine.ScriptableObject
    {
        public event Action<T> OnRaised;
        public int Raised;
        public void Raise(T param) { Raised++; OnRaised?.Invoke(param); }
    }
}

namespace CosmicShore.ScriptableObjects
{
    public class ScriptableEventInputEvents : Obvious.Soap.ScriptableEvent<CosmicShore.Data.InputEvents> { }
    public class ScriptableEventAbilityStats : Obvious.Soap.ScriptableEvent<CosmicShore.Gameplay.AbilityStats> { }
}

namespace CosmicShore.UI
{
    public struct InputEventBlockPayload { public CosmicShore.Data.InputEvents Input; public bool Ended; public float TotalSeconds; public bool Started; }
    public sealed class ScriptableEventInputEventBlock : UnityEngine.ScriptableObject
    {
        public event Action<InputEventBlockPayload> OnRaised;
        public void Raise(InputEventBlockPayload payload) => OnRaised?.Invoke(payload);
    }
}

namespace CosmicShore.Utility.PerformanceBenchmark
{
    public static class NetMarkers
    {
        public struct Marker { public Scope Auto() => default; }
        public struct Scope : IDisposable { public void Dispose() { } }
        public static readonly Marker RpcDispatch = default;
        public static void CountRpc(int n = 1) { }
    }
    public class DiagnosticsHUD : UnityEngine.MonoBehaviour { public static void SetStat(string section, string label, string value) { } }
}

namespace CosmicShore.Gameplay
{
    using CosmicShore.Data;
    public struct AbilityStats { public string PlayerName; public InputEvents ControlType; public float Duration; }
    public interface IPlayer { }
    public interface IInputStatus
    {
        event Action<bool> OnToggleInputPaused;
        bool Paused { get; set; }
        InputDeviceType ActiveInputDevice { get; set; }
    }
    public interface IVesselStatus
    {
        IPlayer Player { get; set; }
        IInputStatus InputStatus { get; }
        bool IsLocalUser { get; }
        bool AutoPilotEnabled { get; }
        string PlayerName { get; }
    }
    public class ActionExecutorRegistry : UnityEngine.MonoBehaviour { public void InitializeAll(IVesselStatus s) { } }
    public abstract class ShipActionSO : UnityEngine.ScriptableObject
    {
        public abstract void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus);
        public abstract void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus);
    }
    public interface IAimTelegraphAction { }
    public class AIPilot { }
    public class InputStatus { }
    public static class ShipHelper
    {
        // Body transcribed from VesselHelper.cs (InitializeShipControlActions / InitializeClassResourceActions).
        public static void InitializeShipControlActions(IVesselStatus vesselStatus, List<InputEventShipActionMapping> inputEventShipActions,
            Dictionary<InputEvents, List<ShipActionSO>> shipControlActions)
        {
            shipControlActions.Clear();
            if (inputEventShipActions == null) return;
            foreach (var map in inputEventShipActions)
            {
                if (!shipControlActions.TryGetValue(map.InputEvent, out var list))
                { list = new List<ShipActionSO>(); shipControlActions.Add(map.InputEvent, list); }
                foreach (var a in map.ShipActions) if (a) list.Add(a);
            }
        }
        public static void InitializeClassResourceActions(List<ResourceEventShipActionMapping> m, Dictionary<ResourceEvents, List<ShipActionSO>> d) => d.Clear();
        public static void DestroyRuntimeActions(List<ShipActionSO> runtimeInstances) => runtimeInstances?.Clear();
    }
}

// The slice of NUnit the shipped CarriedInputDeviceTests uses, so the harness RUNS that file.
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)] public class TestFixtureAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute { }
    public class AssertionException : Exception { public AssertionException(string m) : base(m) { } }
    public static class Assert
    {
        public static void That(bool condition, string message = null) { if (!condition) throw new AssertionException(message ?? "Assert.That"); }
        public static void IsTrue(bool condition, string message = null) => That(condition, message);
        public static void IsFalse(bool condition, string message = null) => That(!condition, message);
        public static void IsNull(object o, string message = null) => That(o == null, message ?? "expected null");
        public static void IsNotNull(object o, string message = null) => That(o != null, message ?? "expected non-null");
        public static void AreSame(object a, object b, string message = null) => That(ReferenceEquals(a, b), message ?? "expected same");
        public static void AreEqual(object a, object b, string message = null) => That(Equals(a, b), message ?? $"expected {a}, was {b}");
    }
}
