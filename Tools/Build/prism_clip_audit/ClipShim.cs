// The project and Unity surface the Spawnable shapes name that the card-art shims do not carry.
// run.sh compiles PARTIAL copies of those shims so this file can extend them instead of forking
// them. Nothing here is reached by GenerateTrailData - these members exist so the shapes COMPILE,
// and the audit only ever asks a shape for its trail data; a lay path that runs reports a fault.
using System.Collections;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core { }

namespace CosmicShore.Utility
{
    public static partial class CSDebug
    {
        public static void LogWarning(object o, UnityEngine.Object context) => LogWarning(o);
        public static void LogError(object o, UnityEngine.Object context) => LogError(o);
    }
}

namespace CosmicShore.Gameplay
{
    public class Crystal : MonoBehaviour { public void ChangeDomain(Domains d) { } }
    public class Flora : MonoBehaviour { }
    public class ShapeSign : MonoBehaviour { }

    public abstract partial class SpawnableBase
    {
        public SpawnTrailData[] GetTrailData() => GenerateTrailData();
        /// <summary>A composite's points place its children; the game resolves leaves only.</summary>
        public bool AuditIsComposite => children.Count > 0;
    }

    public static partial class PrismTrailBuilder
    {
        public static IEnumerator LayGradual(Prism prefab, SpawnPoint[] points, Domains domain, Transform parent,
            Trail trail, string ownerPrefix, float interval, System.Action<Prism> onEach = null)
        {
            Debug.LogError("LayGradual reached from the clip audit");
            yield break;
        }
    }
}

public class ShapeDefinition : ScriptableObject { }

public class ShapeCollisionTrigger : MonoBehaviour
{
    public void Initialize(ShapeDefinition d, Domains domain) { }
    public void SetReady(bool ready) { }
}

namespace UnityEngine
{
    public partial struct Vector3
    {
        public static Vector3 positiveInfinity => new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        public static Vector3 negativeInfinity => new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
    }

    /// <summary>Blast Run's field context keeps per-station bounding spheres as Vector4 (centre, radius).</summary>
    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Vector4 zero => default;
    }

    public class Collider : Component { public bool isTrigger; }
    public class SphereCollider : Collider { public float radius; }
    public class Rigidbody : Component { public bool isKinematic, useGravity; }
    public class Renderer : Component { public Bounds bounds; }
    public struct Bounds { public Vector3 center, extents; }

    public partial class MonoBehaviour
    {
        public Coroutine StartCoroutine(IEnumerator e) { Debug.LogError("StartCoroutine reached from the clip audit"); return null; }
    }
    public class Coroutine { }

    public partial class Component
    {
        public T GetComponent<T>() where T : class => null;
    }

    public partial class GameObject
    {
        public T GetComponent<T>() where T : class => null;
        public T[] GetComponentsInChildren<T>() => System.Array.Empty<T>();
    }

    public partial class Transform
    {
        public Vector3 right => rotation * Vector3.right;
        public Vector3 up => rotation * Vector3.up;
        public Vector3 forward => rotation * Vector3.forward;
    }

    public static partial class Debug
    {
        public static void LogWarning(object m, Object context) => LogWarning(m);
        public static void LogError(object m, Object context) => LogError(m);
    }

    public partial class Object
    {
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object
        { Debug.LogError("Instantiate reached from the clip audit"); return original; }
    }
}
