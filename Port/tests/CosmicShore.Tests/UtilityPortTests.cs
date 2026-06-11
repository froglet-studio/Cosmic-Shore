using System.Threading.Tasks;
using CosmicShore.Engine;
using Object = CosmicShore.Engine.Object;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

public class CSDebugTests
{
    [Fact]
    public void LogLevel_Presets_MapToFlags()
    {
        CSDebug.LogLevel = CSLogLevel.WarningsAndErrors;
        Assert.False(CSDebug.LogEnabled);
        Assert.True(CSDebug.WarningsEnabled);
        Assert.True(CSDebug.ErrorsEnabled);
        Assert.Equal(CSLogLevel.WarningsAndErrors, CSDebug.LogLevel);

        CSDebug.LogLevel = CSLogLevel.Off;
        Assert.Equal(CSLogLevel.Off, CSDebug.LogLevel);

        CSDebug.LogLevel = CSLogLevel.All;
        Assert.Equal(CSLogLevel.All, CSDebug.LogLevel);
    }

    [Fact]
    public void RuntimeGating_SuppressesByLevel()
    {
        var sink = new CapturingLogSink();
        var previousSink = Debug.Sink;
        Debug.Sink = sink;
        try
        {
            CSDebug.LogLevel = CSLogLevel.WarningsAndErrors;
            CSDebug.Log("info");          // suppressed by flag (and compiled in: DEBUG build)
            CSDebug.LogWarning("warn");
            CSDebug.LogError("err");

            Assert.DoesNotContain(sink.Entries, e => e.Type == LogType.Log);
            Assert.Contains(sink.Entries, e => e.Type == LogType.Warning && e.Message == "warn");
            Assert.Contains(sink.Entries, e => e.Type == LogType.Error && e.Message == "err");
        }
        finally
        {
            Debug.Sink = previousSink;
            CSDebug.LogLevel = CSLogLevel.All;
        }
    }
}

public class DebugExtensionsTests
{
    [Fact]
    public void LogColored_WrapsMessageInColorTag()
    {
        var sink = new CapturingLogSink();
        var previousSink = Debug.Sink;
        Debug.Sink = sink;
        try
        {
            CSDebug.LogLevel = CSLogLevel.All;
            DebugExtensions.LogColored("hello", Color.red);
            Assert.Contains(sink.Entries, e => e.Message == "<color=#FF0000>hello</color>");
        }
        finally { Debug.Sink = previousSink; }
    }

    [Fact]
    public void LogWithClassMethod_FormatsTypeAndMethod()
    {
        var sink = new CapturingLogSink();
        var previousSink = Debug.Sink;
        Debug.Sink = sink;
        try
        {
            CSDebug.LogLevel = CSLogLevel.All;
            "subject".LogWithClassMethod("TestMethod", "the message");
            Assert.Contains(sink.Entries, e => e.Message == "System.String - TestMethod: the message");
        }
        finally { Debug.Sink = previousSink; }
    }
}

public class ColorUtilityTests
{
    [Fact]
    public void ToHtmlStringRGB_KnownColors()
    {
        Assert.Equal("FF0000", ColorUtility.ToHtmlStringRGB(Color.red));
        Assert.Equal("00FF00", ColorUtility.ToHtmlStringRGB(Color.green));
        Assert.Equal("FFFFFF", ColorUtility.ToHtmlStringRGB(Color.white));
        Assert.Equal("000000", ColorUtility.ToHtmlStringRGB(Color.black));
    }

    [Fact]
    public void TryParseHtmlString_RoundTrips()
    {
        Assert.True(ColorUtility.TryParseHtmlString("#FF8000", out var color));
        Assert.Equal("FF8000", ColorUtility.ToHtmlStringRGB(color));
        Assert.True(ColorUtility.TryParseHtmlString("4080C0FF", out _));
        Assert.False(ColorUtility.TryParseHtmlString("notacolor", out _));
    }
}

public class GameObjectExtensionTests
{
    class Marker : MonoBehaviour { }

    interface IMarked { }

    class InterfaceMarker : MonoBehaviour, IMarked { }

    [Fact]
    public void GetOrAdd_AddsOnceThenReuses()
    {
        using var loop = new GameLoop();
        var go = new GameObject("g");

        var first = go.GetOrAdd<Marker>();
        var second = go.GetOrAdd<Marker>();

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Single(go.GetComponents<Marker>());
    }

    [Fact]
    public void OrNull_ReturnsNullForDestroyed()
    {
        using var loop = new GameLoop();
        var go = new GameObject("g");
        Assert.Same(go, go.OrNull());

        Object.Destroy(go);
        loop.Tick(0.016f);
        Assert.Null(go.OrNull());
    }

    [Fact]
    public void DestroyChildren_RemovesAllChildren_EndOfFrame()
    {
        using var loop = new GameLoop();
        var parent = new GameObject("parent");
        var c1 = new GameObject("c1");
        var c2 = new GameObject("c2");
        c1.transform.SetParent(parent.transform);
        c2.transform.SetParent(parent.transform);

        parent.DestroyChildren();
        loop.Tick(0.016f);

        Assert.True(c1 == null);
        Assert.True(c2 == null);
        Assert.False(parent == null);
        Assert.Equal(0, parent.transform.childCount);
    }

    [Fact]
    public void EnableDisableChildren_TogglesActiveSelf()
    {
        using var loop = new GameLoop();
        var parent = new GameObject("parent");
        var child = new GameObject("child");
        child.transform.SetParent(parent.transform);

        parent.DisableChildren();
        Assert.False(child.activeSelf);
        parent.EnableChildren();
        Assert.True(child.activeSelf);
    }

    [Fact]
    public void TryGetInterface_FindsImplementingComponent()
    {
        using var loop = new GameLoop();
        var go = new GameObject("g");
        var marker = go.AddComponent<InterfaceMarker>();

        Assert.True(go.TryGetInterface<IMarked>(out var found));
        Assert.Same(marker, found);
        Assert.False(go.TryGetInterface<System.IDisposable>(out _));
    }

    [Fact]
    public void IsLayer_ComparesAgainstRegistry()
    {
        using var loop = new GameLoop();
        var go = new GameObject("g") { layer = 5 };
        Assert.True(go.IsLayer("UI"));
        Assert.False(go.IsLayer("Default"));
    }
}

public class TransformExtensionsTests
{
    const float Dt = 1f / 60f;

    [Fact]
    public void ToGlobal_MatchesTransformPoint_ForUnscaledTransforms()
    {
        using var loop = new GameLoop();
        var go = new GameObject("g");
        go.transform.position = new Vector3(1f, 2f, 3f);
        go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        var local = new Vector3(0f, 0f, 5f);
        var viaExtension = go.transform.ToGlobal(local);
        var viaEngine = go.transform.TransformPoint(local);

        Assert.True((viaExtension - viaEngine).magnitude < 1e-4f);
    }

    [Fact]
    public void ResizeForSeconds_ScalesUp_Holds_AndRestores()
    {
        using var loop = new GameLoop();
        var go = new GameObject("resizer");
        var task = go.transform.ResizeForSeconds(2f, holdDuration: 0.5f);

        // Mid-transition (t≈0.5s of the 1s ramp): scale should be between 1 and 2.
        loop.Run(30, Dt);
        float midScale = go.transform.localScale.x;
        Assert.InRange(midScale, 1.01f, 1.99f);

        // Ramp(1s) + hold(0.5s) + ramp(1s) = 2.5s ≈ 150 frames; allow slack for frame boundaries.
        loop.Run(170, Dt);
        Assert.True(task.IsCompletedSuccessfully, $"task status: {task.Status}");
        Assert.True((go.transform.localScale - Vector3.one).magnitude < 1e-3f,
            $"final scale: {go.transform.localScale}");
    }

    [Fact]
    public void ResizeForSeconds_SecondCall_CancelsFirst_RestoresFromOriginal()
    {
        using var loop = new GameLoop();
        var go = new GameObject("resizer");

        var first = go.transform.ResizeForSeconds(2f, holdDuration: 5f);
        loop.Run(30, Dt);
        Assert.False(first.IsCompleted);

        // Starting a second resize cancels the first, which restores the original scale
        // before the second one begins animating from it.
        var second = go.transform.ResizeForSeconds(3f, holdDuration: 0.1f);
        loop.Run(200, Dt);

        Assert.True(second.IsCompletedSuccessfully);
        Assert.True((go.transform.localScale - Vector3.one).magnitude < 1e-3f,
            $"final scale: {go.transform.localScale}");
    }

    [Fact]
    public void CancelResize_RestoresOriginalScale()
    {
        using var loop = new GameLoop();
        var go = new GameObject("resizer");
        var task = go.transform.ResizeForSeconds(4f, holdDuration: 5f);

        loop.Run(40, Dt);
        Assert.True(go.transform.localScale.x > 1.1f);

        go.transform.CancelResize();
        loop.Run(2, Dt);

        Assert.True(task.IsCompleted);
        Assert.True((go.transform.localScale - Vector3.one).magnitude < 1e-3f,
            $"final scale: {go.transform.localScale}");
    }
}
