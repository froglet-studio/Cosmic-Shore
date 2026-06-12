using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Pool;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// V9: ObjectPool (engine E9), GenericPoolManager, ShipHelper, theme container,
// VesselCustomization (#10c Customization member restored).
public class ObjectPoolTests
{
    [Fact]
    public void Get_CreatesThenReuses_AndCountsTrack()
    {
        int created = 0;
        var pool = new ObjectPool<object>(() => { created++; return new object(); });

        var first = pool.Get();
        pool.Release(first);
        var second = pool.Get();

        Assert.Same(first, second);
        Assert.Equal(1, created);
        Assert.Equal(1, pool.CountAll);
        Assert.Equal(1, pool.CountActive);
        Assert.Equal(0, pool.CountInactive);
    }

    [Fact]
    public void Release_DoubleRelease_ThrowsWithCollectionCheck()
    {
        var pool = new ObjectPool<object>(() => new object(), collectionCheck: true);
        var item = pool.Get();
        pool.Release(item);
        Assert.Throws<System.InvalidOperationException>(() => pool.Release(item));
    }

    [Fact]
    public void Release_PastMaxSize_DestroysInsteadOfRetaining()
    {
        var destroyed = new List<object>();
        var pool = new ObjectPool<object>(() => new object(),
            actionOnDestroy: destroyed.Add, collectionCheck: false, maxSize: 1);

        var a = pool.Get();
        var b = pool.Get();
        pool.Release(a);
        pool.Release(b);

        Assert.Single(destroyed);
        Assert.Same(b, destroyed[0]);
        Assert.Equal(1, pool.CountInactive);
    }

    [Fact]
    public void Clear_DestroysRetained()
    {
        var destroyed = new List<object>();
        var pool = new ObjectPool<object>(() => new object(), actionOnDestroy: destroyed.Add);
        pool.Release(pool.Get());
        pool.Clear();
        Assert.Single(destroyed);
        Assert.Equal(0, pool.CountAll);
    }
}

public class GenericPoolManagerTests
{
    class Pooled : MonoBehaviour { }

    class TestPool : GenericPoolManager<Pooled>
    {
        public override Pooled Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
            => Get_(position, rotation, parent, worldPositionStays);
        public override void Release(Pooled instance) => Release_(instance);
    }

    const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    static void Set(object target, string field, object value)
    {
        var t = target.GetType();
        FieldInfo f = null;
        while (t != null && (f = t.GetField(field, Priv)) == null) t = t.BaseType;
        f.SetValue(target, value);
    }

    static TestPool Make(GameLoop loop)
    {
        var prefabGo = new GameObject("prefab");
        prefabGo.SetActive(false);
        var prefab = prefabGo.AddComponent<Pooled>();

        var go = new GameObject("pool");
        go.SetActive(false); // configure before Awake
        var manager = go.AddComponent<TestPool>();
        Set(manager, "prefab", prefab);
        Set(manager, "defaultCapacity", 2);
        Set(manager, "bufferSizeTarget", 2);
        Set(manager, "enableBufferMaintenance", false);
        go.SetActive(true);
        loop.Tick(1f / 60f); // run Awake/prewarm
        return manager;
    }

    [Fact]
    public void Get_ActivatesPositionsAndTracks_Release_DeactivatesAndReparents()
    {
        using var loop = new GameLoop();
        var manager = Make(loop);

        var instance = manager.Get(new Vector3(3f, 4f, 5f), Quaternion.identity);

        Assert.True(instance.gameObject.activeSelf);
        Assert.Equal(3f, instance.transform.position.x, 3);

        manager.Release(instance);
        Assert.False(instance.gameObject.activeSelf);
        Assert.Same(manager.transform, instance.transform.parent);
    }

    [Fact]
    public void ReleaseAllActive_ReturnsEverything()
    {
        using var loop = new GameLoop();
        var manager = Make(loop);
        var a = manager.Get(Vector3.zero, Quaternion.identity);
        var b = manager.Get(Vector3.one, Quaternion.identity);

        manager.ReleaseAllActive();

        Assert.False(a.gameObject.activeSelf);
        Assert.False(b.gameObject.activeSelf);
    }
}

public class ShipHelperTests
{
    class RecordingActionSO : ShipActionSO
    {
        public IVesselStatus InitializedWith;
        public override void Initialize(IVesselStatus vs) { base.Initialize(vs); InitializedWith = vs; }
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
    }

    class RecordingShipAction : ShipAction
    {
        public int Started, Stopped;
        public override void StartAction() => Started++;
        public override void StopAction() => Stopped++;
    }

    [Fact]
    public void InitializeShipControlActions_GroupsByEvent_AndInitializesAssets()
    {
        var status = new StubVesselStatus();
        var actionA = ScriptableObject.CreateInstance<RecordingActionSO>();
        var actionB = ScriptableObject.CreateInstance<RecordingActionSO>();
        var mappings = new List<InputEventShipActionMapping>
        {
            new() { InputEvent = InputEvents.Button1Action, ShipActions = new List<ShipActionSO> { actionA } },
            new() { InputEvent = InputEvents.Button1Action, ShipActions = new List<ShipActionSO> { actionB } },
        };
        var result = new Dictionary<InputEvents, List<ShipActionSO>>();

        ShipHelper.InitializeShipControlActions(status, mappings, result);

        Assert.Equal(2, result[InputEvents.Button1Action].Count);
        Assert.Same(status, actionA.InitializedWith);
        Assert.Same(status, actionB.InitializedWith);
    }

    [Fact]
    public void PerformAndStopShipControllerActions_DriveActions_AndStampStartTime()
    {
        using var loop = new GameLoop();
        var go = new GameObject("actions");
        var action = go.AddComponent<RecordingShipAction>();
        var actions = new Dictionary<InputEvents, List<ShipAction>>
            { [InputEvents.Button2Action] = new() { action } };
        var startTimes = new Dictionary<InputEvents, float>();

        ShipHelper.PerformShipControllerActions(InputEvents.Button2Action, startTimes, actions);
        ShipHelper.StopShipControllerActions(InputEvents.Button2Action, actions);

        Assert.Equal(1, action.Started);
        Assert.Equal(1, action.Stopped);
        Assert.True(startTimes.ContainsKey(InputEvents.Button2Action));
    }

    [Fact]
    public void ApplyShipMaterial_WritesSkinnedSlotZero_AndMeshSlotOne()
    {
        using var loop = new GameLoop();
        var material = new Material(Shader.Find("Standard"));
        var other = new Material(Shader.Find("Standard"));

        var skinnedGo = new GameObject("skinned");
        var skinned = skinnedGo.AddComponent<SkinnedMeshRenderer>();
        skinned.materials = new[] { other };

        var meshGo = new GameObject("mesh");
        var mesh = meshGo.AddComponent<MeshRenderer>();
        mesh.materials = new[] { other, other };

        ShipHelper.ApplyShipMaterial(material, new List<GameObject> { skinnedGo, meshGo });

        Assert.Same(material, skinned.materials[0]);
        Assert.Same(material, mesh.materials[1]);
    }

    [Fact]
    public void SetShipProperties_PushesDomainMaterialSetAndTrailColors()
    {
        using var loop = new GameLoop();
        var status = new StubVesselStatus(); // null Player → Domain falls back to Jade
        var vessel = new StubVessel { VesselStatus = status };

        var materialSet = ScriptableObject.CreateInstance<SO_MaterialSet>();
        materialSet.ShipMaterial = new Material(Shader.Find("Standard"));
        materialSet.SkimmerMaterial = new Material(Shader.Find("Standard"));

        var colorSet = ScriptableObject.CreateInstance<SO_ColorSet>();
        colorSet.JadeColors = new DomainColorSet
            { TrailHighlightColor = new Color(0f, 1f, 0.5f, 1f), TrailCoreColor = new Color(0f, 0.5f, 0.2f, 1f) };

        var theme = ScriptableObject.CreateInstance<ThemeManagerDataContainerSO>();
        theme.ColorSet = colorSet;
        theme.TeamMaterialSets = new Dictionary<Domains, SO_MaterialSet> { [Domains.Jade] = materialSet };

        ShipHelper.SetShipProperties(theme, vessel);

        Assert.Same(materialSet.ShipMaterial, vessel.LastShipMaterial);
        Assert.Same(materialSet.SkimmerMaterial, vessel.LastSkimmerMaterial);
        Assert.Equal(new Color(0f, 1f, 0.5f, 1f), vessel.LastTrailColors.Value.highlight);
    }

    [Fact]
    public void ThemeContainer_DomainUIColor_FallsBackToGrayWithoutColorSet()
    {
        var theme = ScriptableObject.CreateInstance<ThemeManagerDataContainerSO>();
        Assert.Equal(Color.gray, theme.GetDomainUIColor(Domains.Ruby));
    }
}

public class VesselCustomizationTests
{
    const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void Initialize_AdoptsGeometries_AndPaintsShipMaterial()
    {
        using var loop = new GameLoop();
        var material = new Material(Shader.Find("Standard"));
        var geometryGo = new GameObject("geometry");
        var renderer = geometryGo.AddComponent<MeshRenderer>();
        renderer.materials = new[] { material, new Material(Shader.Find("Standard")) };

        var go = new GameObject("customization");
        go.SetActive(false);
        var customization = go.AddComponent<VesselCustomization>();
        typeof(VesselCustomization).GetField("_shipGeometries", Priv)
            .SetValue(customization, new List<GameObject> { geometryGo });
        go.SetActive(true);

        var status = new StubVesselStatus { ShipMaterial = material };
        customization.Initialize(status);

        Assert.Contains(geometryGo, status.ShipGeometries);
        Assert.Same(material, renderer.materials[1]); // mesh renderers take slot 1
    }
}
