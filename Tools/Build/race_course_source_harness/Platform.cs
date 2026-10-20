// Stubs for the NON-math types the course sources touch. Deliberately thin: a source must never
// reach past these (no scene, no live cell, no injected data) - that is the property the whole
// extraction exists for, and a stub that grew would be hiding a dependency that should not exist.
using System;
namespace UnityEngine
{
    public class Object
    {
        public string name = "";
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
        // RegattaCourseSource.TryResolveLiveRails (the only scene-resolving member, used by the pilot and
        // the recorder, never by CourseFor): no scene exists here, so it finds nothing.
        public static T FindAnyObjectByType<T>() where T : Object => null;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Object => new T[0];
    }
    public enum FindObjectsSortMode { None, InstanceID }
    public class ScriptableObject : Object { }
    public class Component : Object { }
    public class MonoBehaviour : Component { }
    public class MinAttribute : Attribute { public MinAttribute(float v) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
}

namespace CosmicShore.Utility
{
    public static class CSDebug
    {
        public static int Warnings;
        public static void LogWarning(string s) { Warnings++; }
        public static void LogError(string s) { Console.Error.WriteLine("ERROR " + s); }
    }

    public class CellConfigDataSO : UnityEngine.ScriptableObject
    {
        public CosmicShore.Gameplay.SpawnableBase EnvironmentPrefab;
    }
}

namespace CosmicShore.ScriptableObjects
{
    // Instance is null here, so every source falls through to the Default* constants - the
    // same path a build without the overrides asset takes. The constants themselves are
    // EXTRACTED from the shipped EndConditionOverridesSO.cs by run.sh (Generated.cs), never typed.
    public partial class EndConditionOverridesSO : UnityEngine.ScriptableObject
    {
        public static EndConditionOverridesSO Instance => null;
        public int GetSwitchbackGateTarget() => 0;
        public int GetHeadlongGateTarget() => 0;
        public int GetRedlineGateTarget() => 0;
        public int GetBreakwaterLaps() => 0;
        public int GetBreakwaterCrossingTarget() => 0;
        public int GetSkeinRingTarget() => 0;
        public int GetRegattaGateTarget() => 0;
        public int GetWaystationRingTarget() => 0;
    }
}

namespace CosmicShore.Gameplay
{
    public class SpawnableBase : UnityEngine.MonoBehaviour { }

    public class SpawnableSkein : SpawnableBase
    {
        public int CableSeed => 1;
        public SkeinCourseSettings CourseSettings => SkeinCourseSettings.ForIntensity(1);
    }

    public class Trail { public System.Collections.Generic.List<object> TrailList = new System.Collections.Generic.List<object>(); }

    public class Cell : UnityEngine.MonoBehaviour
    {
        public CosmicShore.Utility.CellConfigDataSO Config => null;
        public CosmicShore.Utility.CellConfigDataSO ExpectedConfig => null;
    }

    public class SpawnableRegattaRails : SpawnableBase
    {
        public System.Collections.Generic.List<Trail> GetTrails() => null;
        public System.Collections.Generic.List<RaceGate> BuildGatesNow() =>
            RegattaCourse.BuildGates(RegattaCourse.DefaultSeed, 1);
    }
}
