using System.Collections.Generic;
using CosmicShore.Engine;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

public class SceneModelTests
{
    class LifecycleProbe : MonoBehaviour
    {
        public static List<string> Log;
        void Awake() => Log?.Add("Awake");
        void OnEnable() => Log?.Add("OnEnable");
        void Start() => Log?.Add("Start");
        void Update() => Log?.Add("Update");
        void FixedUpdate() => Log?.Add("FixedUpdate");
        void LateUpdate() => Log?.Add("LateUpdate");
        void OnDisable() => Log?.Add("OnDisable");
        void OnDestroy() => Log?.Add("OnDestroy");
    }

    class CounterBehaviour : MonoBehaviour
    {
        public int Updates;
        public int FixedUpdates;
        void Update() => Updates++;
        void FixedUpdate() => FixedUpdates++;
    }

    [Fact]
    public void Lifecycle_FullSequence_InOrder()
    {
        using var loop = new GameLoop();
        LifecycleProbe.Log = new List<string>();

        var go = new GameObject("probe");
        go.AddComponent<LifecycleProbe>();
        Assert.Equal(new[] { "Awake", "OnEnable" }, LifecycleProbe.Log);

        Time.fixedDeltaTime = 1f / 60f;
        loop.Tick(1f / 60f);
        Assert.Equal(new[] { "Awake", "OnEnable", "Start", "FixedUpdate", "Update", "LateUpdate" }, LifecycleProbe.Log);

        LifecycleProbe.Log.Clear();
        Object.Destroy(go);
        loop.Tick(1f / 60f);
        Assert.Contains("OnDisable", LifecycleProbe.Log);
        Assert.Contains("OnDestroy", LifecycleProbe.Log);
        Assert.True(LifecycleProbe.Log.IndexOf("OnDisable") < LifecycleProbe.Log.IndexOf("OnDestroy"));
        LifecycleProbe.Log = null;
    }

    [Fact]
    public void DestroyedObject_ComparesEqualToNull_AfterFrameEnd()
    {
        using var loop = new GameLoop();
        var go = new GameObject("doomed");

        Object.Destroy(go);
        Assert.False(go == null);   // deferred: still alive within the frame
        loop.Tick(0.016f);
        Assert.True(go == null);    // fake-null after end of frame
        Assert.False(go);           // implicit bool
        Assert.False(go.activeInHierarchy);
    }

    [Fact]
    public void SetActive_TogglesEnableDisable_AndUpdates()
    {
        using var loop = new GameLoop();
        var go = new GameObject("toggle");
        var counter = go.AddComponent<CounterBehaviour>();

        loop.Tick(0.016f);
        Assert.Equal(1, counter.Updates);

        go.SetActive(false);
        loop.Tick(0.016f);
        Assert.Equal(1, counter.Updates); // no update while inactive

        go.SetActive(true);
        loop.Tick(0.016f);
        Assert.Equal(2, counter.Updates);
    }

    [Fact]
    public void ParentDeactivation_DisablesChildBehaviours()
    {
        using var loop = new GameLoop();
        var parent = new GameObject("parent");
        var child = new GameObject("child");
        child.transform.SetParent(parent.transform);
        var counter = child.AddComponent<CounterBehaviour>();

        loop.Tick(0.016f);
        Assert.Equal(1, counter.Updates);
        Assert.True(child.activeInHierarchy);

        parent.SetActive(false);
        Assert.False(child.activeInHierarchy);
        Assert.True(child.activeSelf); // own flag untouched
        loop.Tick(0.016f);
        Assert.Equal(1, counter.Updates);

        parent.SetActive(true);
        loop.Tick(0.016f);
        Assert.Equal(2, counter.Updates);
    }

    [Fact]
    public void Behaviour_EnabledFalse_StopsUpdates_StartDeferredUntilEnabled()
    {
        using var loop = new GameLoop();
        LifecycleProbe.Log = new List<string>();
        var go = new GameObject("g");
        var probe = go.AddComponent<LifecycleProbe>();

        probe.enabled = false;
        Assert.Contains("OnDisable", LifecycleProbe.Log);
        LifecycleProbe.Log.Clear();

        loop.Tick(0.016f);
        Assert.DoesNotContain("Start", LifecycleProbe.Log); // Start deferred while disabled

        probe.enabled = true;
        loop.Tick(0.016f);
        Assert.Contains("Start", LifecycleProbe.Log);
        Assert.Contains("Update", LifecycleProbe.Log);
        LifecycleProbe.Log = null;
    }

    [Fact]
    public void AddComponent_OnInactiveObject_DefersAwakeUntilActivation()
    {
        using var loop = new GameLoop();
        LifecycleProbe.Log = new List<string>();
        var go = new GameObject("inactive");
        go.SetActive(false);
        go.AddComponent<LifecycleProbe>();
        Assert.Empty(LifecycleProbe.Log);

        go.SetActive(true);
        Assert.Equal(new[] { "Awake", "OnEnable" }, LifecycleProbe.Log);
        LifecycleProbe.Log = null;
    }

    [Fact]
    public void GetComponent_Generic_Interface_And_TryGet()
    {
        using var loop = new GameLoop();
        var go = new GameObject("comp");
        var counter = go.AddComponent<CounterBehaviour>();

        Assert.Same(counter, go.GetComponent<CounterBehaviour>());
        Assert.Same(counter, go.GetComponent<MonoBehaviour>());
        Assert.True(go.TryGetComponent<CounterBehaviour>(out var found));
        Assert.Same(counter, found);
        Assert.Null(go.GetComponent<LifecycleProbe>());
        Assert.NotNull(go.transform);
        Assert.Same(go.transform, go.GetComponent<Transform>());
    }

    [Fact]
    public void GetComponentInChildren_FindsNested()
    {
        using var loop = new GameLoop();
        var root = new GameObject("root");
        var mid = new GameObject("mid");
        var leaf = new GameObject("leaf");
        mid.transform.SetParent(root.transform);
        leaf.transform.SetParent(mid.transform);
        var counter = leaf.AddComponent<CounterBehaviour>();

        Assert.Same(counter, root.GetComponentInChildren<CounterBehaviour>());
        Assert.Same(counter, leaf.GetComponentInParent<CounterBehaviour>());
        Assert.Single(root.GetComponentsInChildren<CounterBehaviour>());
    }

    [Fact]
    public void DestroyGameObject_DestroysChildren()
    {
        using var loop = new GameLoop();
        var parent = new GameObject("parent");
        var child = new GameObject("child");
        child.transform.SetParent(parent.transform);

        Object.Destroy(parent);
        loop.Tick(0.016f);

        Assert.True(parent == null);
        Assert.True(child == null);
        Assert.Equal(0, loop.Scene.rootCount);
    }

    [DefaultExecutionOrder(-100)]
    class EarlyBehaviour : MonoBehaviour
    {
        public static List<string> Order;
        void Update() => Order?.Add("early");
    }

    class LateBehaviour : MonoBehaviour
    {
        void Update() => EarlyBehaviour.Order?.Add("late");
    }

    [Fact]
    public void DefaultExecutionOrder_RunsLowerFirst()
    {
        using var loop = new GameLoop();
        EarlyBehaviour.Order = new List<string>();

        // Add the default-order behaviour first; -100 must still run before it.
        var go = new GameObject("ordered");
        go.AddComponent<LateBehaviour>();
        go.AddComponent<EarlyBehaviour>();

        loop.Tick(0.016f);
        Assert.Equal(new[] { "early", "late" }, EarlyBehaviour.Order);
        EarlyBehaviour.Order = null;
    }

    [Fact]
    public void GameObject_WithoutLoop_FailsLoud()
    {
        Assert.Throws<System.InvalidOperationException>(() => new GameObject("orphan"));
    }
}

public class TransformTests
{
    const float Tolerance = 1e-4f;

    static void AssertApprox(Vector3 expected, Vector3 actual)
        => Assert.True((expected - actual).magnitude < Tolerance, $"Expected {expected}, got {actual}");

    [Fact]
    public void WorldPosition_ComposesThroughParentChain()
    {
        using var loop = new GameLoop();
        var parent = new GameObject("parent");
        var child = new GameObject("child");
        child.transform.SetParent(parent.transform);

        parent.transform.position = new Vector3(10f, 0f, 0f);
        parent.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        child.transform.localPosition = new Vector3(0f, 0f, 5f); // 5 along parent's forward (= world +x)

        AssertApprox(new Vector3(15f, 0f, 0f), child.transform.position);
    }

    [Fact]
    public void WorldPosition_Setter_ComputesLocal()
    {
        using var loop = new GameLoop();
        var parent = new GameObject("parent");
        var child = new GameObject("child");
        child.transform.SetParent(parent.transform);
        parent.transform.position = new Vector3(1f, 2f, 3f);

        child.transform.position = new Vector3(1f, 2f, 8f);
        AssertApprox(new Vector3(0f, 0f, 5f), child.transform.localPosition);
    }

    [Fact]
    public void SetParent_WorldPositionStays()
    {
        using var loop = new GameLoop();
        var parent = new GameObject("parent");
        parent.transform.position = new Vector3(100f, 0f, 0f);
        var orphan = new GameObject("orphan");
        orphan.transform.position = new Vector3(5f, 5f, 5f);

        orphan.transform.SetParent(parent.transform); // worldPositionStays default true
        AssertApprox(new Vector3(5f, 5f, 5f), orphan.transform.position);
        AssertApprox(new Vector3(-95f, 5f, 5f), orphan.transform.localPosition);
    }

    [Fact]
    public void ParentScale_AffectsChildWorldPosition()
    {
        using var loop = new GameLoop();
        var parent = new GameObject("parent");
        parent.transform.localScale = new Vector3(2f, 2f, 2f);
        var child = new GameObject("child");
        child.transform.SetParent(parent.transform, worldPositionStays: false);
        child.transform.localPosition = new Vector3(1f, 0f, 0f);

        AssertApprox(new Vector3(2f, 0f, 0f), child.transform.position);
        AssertApprox(new Vector3(2f, 2f, 2f), child.transform.lossyScale);
    }

    [Fact]
    public void DirectionVectors_FollowRotation()
    {
        using var loop = new GameLoop();
        var go = new GameObject("dir");
        go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        AssertApprox(Vector3.right, go.transform.forward);
        AssertApprox(Vector3.up, go.transform.up);
        AssertApprox(Vector3.back, go.transform.right);
    }

    [Fact]
    public void Translate_SelfSpace_MovesAlongLocalAxes()
    {
        using var loop = new GameLoop();
        var go = new GameObject("mover");
        go.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // forward = +x
        go.transform.Translate(new Vector3(0f, 0f, 10f));      // Space.Self default

        AssertApprox(new Vector3(10f, 0f, 0f), go.transform.position);
    }

    [Fact]
    public void LookAt_PointsForwardAtTarget()
    {
        using var loop = new GameLoop();
        var go = new GameObject("looker");
        go.transform.position = Vector3.zero;
        go.transform.LookAt(new Vector3(0f, 0f, -10f));
        AssertApprox(Vector3.back, go.transform.forward);
    }

    [Fact]
    public void SetParent_UpdatesSceneRoots()
    {
        using var loop = new GameLoop();
        var a = new GameObject("a");
        var b = new GameObject("b");
        Assert.Equal(2, loop.Scene.rootCount);

        b.transform.SetParent(a.transform);
        Assert.Equal(1, loop.Scene.rootCount);

        b.transform.SetParent(null);
        Assert.Equal(2, loop.Scene.rootCount);
    }
}
