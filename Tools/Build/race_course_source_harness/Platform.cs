// Stubs for the NON-math types the course sources touch. Deliberately thin: a source must never
// reach past these (no scene, no live cell, no injected data) - that is the property the whole
// extraction exists for, and a stub that grew would be hiding a dependency that should not exist.
using System;
namespace UnityEngine
{
    public class Object
    {
        public string name = "";
        public static implicit operator bool(Object o) => o != null;
        // Scene lookups: there is no scene here, so every lookup answers "nothing", the path a
        // source takes when no scene instance is authored (RegattaCourseSource.TryResolveLiveRails).
        public static T FindAnyObjectByType<T>() where T : Object => null;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Object => System.Array.Empty<T>();
    }
    public enum FindObjectsSortMode { None, InstanceID }
    public class ScriptableObject : Object { }
    public class Component : Object { }
    public class MonoBehaviour : Component { }
    public class MinAttribute : Attribute { public MinAttribute(float v) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    // TimeRaceCourseConfigSO (the Time Race Prototyper's asset) is compiled for real; with no
    // Resources here its Instance is null, so Slingshot / Warpline / Burrow Run fly their shipped
    // courses - the path a build without the asset takes.
    public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    public static class Resources { public static T Load<T>(string path) where T : Object => null; }
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
        public int GetWaystationClusterTarget() => 0;
        public int GetBreachGateTarget() => 0;
        public int GetThornlineGateTarget() => 0;
        public int GetThresherTimeGateTarget() => 0;
        public int GetGrizzlyTimeGateTarget() => 0;
        public int GetScarabRallyGateTarget() => 0;
        public int GetDecoyRunGateTarget() => 0;
        public int GetBurrowRunGateTarget() => 0;
        public int GetPerchRunGateTarget() => 0;
        public int GetWrasseRunGateTarget() => 0;
        public int GetLanternRunGateTarget() => 0;
        public int GetWarplineGateTarget() => 0;
        public int GetSlipLineGateTarget() => 0;
        public int GetBrittleStarTimeGateTarget() => 0;
        public int GetSlingshotGateTarget() => 0;
        public int GetSerpentTimeGateTarget() => 0;
    }
}

namespace CosmicShore.Gameplay
{
    public class Trail { public System.Collections.Generic.List<UnityEngine.Vector3> TrailList = new(); }

    public class SpawnableBase : UnityEngine.MonoBehaviour { }

    public class SpawnableSkein : SpawnableBase
    {
        public int CableSeed => 1;
        public SkeinCourseSettings CourseSettings => SkeinCourseSettings.ForIntensity(1);
    }

    public class Cell : UnityEngine.MonoBehaviour
    {
        public CosmicShore.Utility.CellConfigDataSO Config;
        public CosmicShore.Utility.CellConfigDataSO ExpectedConfig;
    }

    public class SpawnableRegattaRails : SpawnableBase
    {
        public System.Collections.Generic.List<Trail> GetTrails() => new();
        public System.Collections.Generic.List<RaceGate> BuildGatesNow() =>
            RegattaCourse.BuildGates(RegattaCourse.DefaultSeed, 1);
    }
}
