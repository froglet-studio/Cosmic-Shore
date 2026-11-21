using CosmicShore.Engine;
using CosmicShore.Engine.Audio.Fmod;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// A headless training run reloads the same scene hundreds of times and never
// renders. Found by a 4-process training run that was OOM-killed at generation 10:
// engine registries that only the render backend pruned, a diagnostic log that
// kept every FMOD start, and a destroyed transform's world cache still pointing
// at its old parent. Each must stay bounded without a frame ever being drawn.
// ─────────────────────────────────────────────────────────────────────────────
public class LongRunMemoryTests
{
    [Fact]
    public void RendererRegistry_StaysBounded_WithoutRendering()
    {
        using var loop = new GameLoop();
        for (int i = 0; i < 20_000; i++)
        {
            var go = new GameObject("r" + i);
            go.AddComponent<MeshRenderer>();
            Object.DestroyImmediate(go);
        }
        var alive = new GameObject("alive");
        var kept = alive.AddComponent<MeshRenderer>();
        Assert.True(Renderer.RegisteredCount < 9000, $"registry holds {Renderer.RegisteredCount} renderers");

        var live = new System.Collections.Generic.List<Renderer>();
        Renderer.CollectLive(live);
        Assert.Contains(kept, live);
    }

    [Fact]
    public void FmodStartLog_IsBounded_ButTheTalliesAreComplete()
    {
        RuntimeManager.ResetForTests();
        try
        {
            for (int i = 0; i < 10_000; i++)
                RuntimeManager.RecordStart(new EventInstanceState { Path = i % 4 == 0 ? "event:/a" : "event:/b" });
            Assert.True(RuntimeManager.StartedInstances.Count <= RuntimeManager.StartedLogCapacity);
            Assert.Equal(10_000, RuntimeManager.StartedTotal);
            Assert.Equal(2_500, RuntimeManager.StartedByPath["event:/a"]);
            Assert.Equal(7_500, RuntimeManager.StartedByPath["event:/b"]);
        }
        finally { RuntimeManager.ResetForTests(); }
    }

    [Fact]
    public void DestroyedTransform_ForgetsTheParentItsWorldCacheWasComputedAgainst()
    {
        using var loop = new GameLoop();
        var parent = new GameObject("parent");
        parent.transform.position = new Vector3(3f, 0f, 0f);
        var child = new GameObject("child");
        child.transform.SetParent(parent.transform, false);
        _ = child.transform.position; // fills the world cache
        Assert.Same(parent.transform, child.transform.WorldCacheParent);

        Object.DestroyImmediate(child);
        Assert.Null(child.transform.WorldCacheParent);

        var root = new GameObject("root");
        _ = root.transform.position;
        Object.DestroyImmediate(root);
        Assert.Null(root.transform.WorldCacheParent);
    }
}
