using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

public class CoroutineTests
{
    class CoroutineProbe : MonoBehaviour
    {
        public List<string> Log = new();

        // IEnumerator Start: must auto-run as a coroutine (original contract).
        IEnumerator Start()
        {
            Log.Add($"start@{Time.frameCount}");
            yield return null;
            Log.Add($"resumed@{Time.frameCount}");
            yield return new WaitForSeconds(0.5f);
            Log.Add($"afterWait@{Time.time:F2}");
            yield return StartCoroutine(Nested());
            Log.Add("afterNested");
        }

        IEnumerator Nested()
        {
            Log.Add("nestedBegin");
            yield return null;
            Log.Add("nestedEnd");
        }
    }

    [Fact]
    public void IEnumeratorStart_RunsAsCoroutine_WithWaitsAndNesting()
    {
        using var loop = new GameLoop();
        var go = new GameObject("co");
        var probe = go.AddComponent<CoroutineProbe>();

        loop.Run(40, 1f / 60f); // ~0.67s
        Assert.Contains("start@1", probe.Log);
        // Original loop-order contract: a coroutine started during Start that yields
        // null resumes after Update in the SAME frame (Start phase precedes Update).
        Assert.Contains("resumed@1", probe.Log);
        Assert.Contains(probe.Log, e => e.StartsWith("afterWait@0.5"));

        loop.Run(5, 1f / 60f);
        Assert.Equal("afterNested", probe.Log[^1]);
        Assert.Contains("nestedBegin", probe.Log);
        Assert.Contains("nestedEnd", probe.Log);
    }

    class LoopingProbe : MonoBehaviour
    {
        public int Ticks;
        IEnumerator Start()
        {
            while (true)
            {
                Ticks++;
                yield return new WaitForSeconds(1f);
            }
        }
    }

    [Fact]
    public void WaitForSeconds_UsesScaledTime_AndStopsOnDestroy()
    {
        using var loop = new GameLoop();
        var go = new GameObject("loop");
        var probe = go.AddComponent<LoopingProbe>();

        loop.Run(130, 1f / 60f); // ~2.17s → ticks at 0s, 1s, 2s
        Assert.Equal(3, probe.Ticks);

        Object.Destroy(go);
        loop.Run(120, 1f / 60f);
        Assert.Equal(3, probe.Ticks); // coroutine died with its object
    }
}

public class ResourceSystemTests
{
    static ResourceSystem MakeSystem(params Resource[] resources)
    {
        var go = new GameObject("vessel");
        go.SetActive(false); // configure before Awake (runtime-AddComponent pattern)
        var system = go.AddComponent<ResourceSystem>();
        system.Resources = new List<Resource>(resources);
        go.SetActive(true);
        return system;
    }

    [Fact]
    public void Start_InitializesResources_AndGainCoroutineRegenerates()
    {
        using var loop = new GameLoop();
        var system = MakeSystem(new Resource { Name = "boost", resourceGainRate = 0.25f });
        system.Resources[0].CurrentAmount = 0f;

        var changes = new List<(int index, float amount)>();
        system.OnResourceChanged += (i, current, max) => changes.Add((i, current));

        loop.Tick(1f / 60f); // Start: snap to InitialAmount (1.0) and begin gain loop
        Assert.Equal(1f, system.Resources[0].CurrentAmount, 3);

        system.SetResourceAmount(0, 0f);
        loop.Run(130, 1f / 60f); // ≥2 gain ticks at 1s cadence
        Assert.InRange(system.Resources[0].CurrentAmount, 0.49f, 0.76f);
        Assert.Contains(changes, c => c.index == 0);
    }

    [Fact]
    public void ChangeResourceAmount_ClampsToMax_AndZero()
    {
        using var loop = new GameLoop();
        var system = MakeSystem(new Resource { Name = "ammo", resourceGainRate = 0f });
        loop.Tick(1f / 60f);

        system.ChangeResourceAmount(0, 5f);
        Assert.Equal(1f, system.Resources[0].CurrentAmount, 3); // clamped to MaxAmount

        system.ChangeResourceAmount(0, -9f);
        Assert.Equal(0f, system.Resources[0].CurrentAmount, 3);
    }

    [Fact]
    public void GetLevel_IsFloorOfNormalizedTimesTen()
    {
        using var loop = new GameLoop();
        var system = MakeSystem();
        system.InitializeElementLevels(new ResourceCollection(mass: 0.55f, charge: -0.5f, space: 1.5f, time: 0f));

        Assert.Equal(5, system.GetLevel(Element.Mass));
        Assert.Equal(-5, system.GetLevel(Element.Charge));  // floor of clamped min
        Assert.Equal(15, system.GetLevel(Element.Space));   // ceiling of clamped max
        Assert.Equal(0, system.GetLevel(Element.Time));
    }

    [Fact]
    public void AdjustLevel_ClampsAndReportsIntegerRise()
    {
        using var loop = new GameLoop();
        var system = MakeSystem();
        system.InitializeElementLevels(new ResourceCollection(0f, 0f, 0f, 0f));

        Assert.True(system.AdjustLevel(Element.Charge, 0.1f));   // 0 → 1: rose
        Assert.False(system.AdjustLevel(Element.Charge, 0.05f)); // 1 → 1: no integer rise
        system.AdjustLevel(Element.Charge, 99f);
        Assert.Equal(15, system.GetLevel(Element.Charge));       // clamped at +1.5
    }

    [Fact]
    public void TemporaryElementalEffect_DecaysBackToBase_PermanentSticks()
    {
        using var loop = new GameLoop();
        var system = MakeSystem();
        system.InitializeElementLevels(new ResourceCollection(0f, 0f, 0f, 0f));
        loop.Tick(1f / 60f);

        var levelEvents = new List<(Element element, int level)>();
        system.OnElementLevelChange += (e, l) => levelEvents.Add((e, l));

        system.ApplyElementalEffect(Element.Mass, 0.5f, duration: 1f); // temporary buff
        loop.Tick(1f / 60f);
        Assert.True(system.GetLevel(Element.Mass) >= 4, $"buff active: {system.GetLevel(Element.Mass)}");

        loop.Run(70, 1f / 60f); // past the 1s decay
        Assert.Equal(0, system.GetLevel(Element.Mass)); // base untouched

        system.ApplyElementalEffect(Element.Time, 0.3f, duration: 0f); // permanent
        Assert.Equal(3, system.GetLevel(Element.Time));
        Assert.Contains(levelEvents, e => e.element == Element.Mass);
    }
}
