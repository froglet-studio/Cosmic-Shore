using System;
using System.Threading;
using CosmicShore.Engine;
using CosmicShore.Engine.Profiling;

namespace CosmicShore.Tests;

/// <summary>
/// The frame-time APIs the game's DiagnosticsHUD reads (Time.unscaledDeltaTime, FrameTimingManager,
/// ProfilerRecorder's frame stats) must report the real frame. In a scripted run the loop steps a fixed
/// 1/60 s, and the HUD said "FPS 60 · CPU (busy) 16.7 ms" while each frame took 110-200 ms.
/// </summary>
public class FrameTimingTests : IDisposable
{
    public void Dispose() => Time.maximumDeltaTime = float.PositiveInfinity;

    class Stall : MonoBehaviour
    {
        public int SleepMs;
        void Update() { if (SleepMs > 0) Thread.Sleep(SleepMs); }
    }

    static FrameTiming Latest()
    {
        FrameTimingManager.CaptureFrameTimings();
        var timings = new FrameTiming[1];
        Assert.Equal(1u, FrameTimingManager.GetLatestTimings(1, timings));
        return timings[0];
    }

    [Fact]
    public void SlowFrame_ShowsUpInFrameTimingManager_UnderAFixedStep()
    {
        using var loop = new GameLoop();
        var stall = new GameObject("stall").AddComponent<Stall>();

        loop.Tick(1f / 60f);
        stall.SleepMs = 80;
        loop.Tick(1f / 60f);           // the slow frame: its Update sleeps 80 ms
        stall.SleepMs = 0;
        loop.Tick(1f / 60f);           // starting this frame completes the slow one

        var slow = Latest();
        Assert.True(slow.cpuFrameTime >= 78, $"cpuFrameTime {slow.cpuFrameTime:F1} ms");
        Assert.True(slow.cpuMainThreadFrameTime - slow.cpuMainThreadPresentWaitTime >= 78,
            $"busy main thread {slow.cpuMainThreadFrameTime - slow.cpuMainThreadPresentWaitTime:F1} ms");
        // The game clock still took the fixed step: only the measured APIs see the stall.
        Assert.Equal(1f / 60f, Time.unscaledDeltaTime, 5);
    }

    [Fact]
    public void HostRender_IsRenderThreadTime_AndWhatFollowsIsPresentWait()
    {
        using var loop = new GameLoop();
        loop.Tick(1f / 60f);
        Thread.Sleep(30);                                      // the host issues the frame's draws
        FrameTimingManager.ReportRender(30.0, gpuMs: 5.0);
        Thread.Sleep(40);                                      // swap, vsync, frame limiter
        loop.Tick(1f / 60f);

        var frame = Latest();
        Assert.True(frame.cpuFrameTime >= 68, $"cpuFrameTime {frame.cpuFrameTime:F1} ms");
        Assert.Equal(30.0, frame.cpuRenderThreadFrameTime);
        Assert.Equal(5.0, frame.gpuFrameTime);
        Assert.True(frame.cpuMainThreadPresentWaitTime >= 38, $"present wait {frame.cpuMainThreadPresentWaitTime:F1} ms");
        Assert.True(frame.cpuMainThreadPresentWaitTime <= frame.cpuFrameTime - 28,
            $"present wait {frame.cpuMainThreadPresentWaitTime:F1} ms of {frame.cpuFrameTime:F1}: the draws are not waiting");
    }

    [Fact]
    public void Timings_ArriveAfterTheFrame_NewestFirst_FromTheLastCapture()
    {
        using var loop = new GameLoop();
        var timings = new FrameTiming[4];

        loop.Tick(1f / 60f);
        FrameTimingManager.CaptureFrameTimings();
        Assert.Equal(0u, FrameTimingManager.GetLatestTimings(4, timings)); // the first frame is still running

        var stall = new GameObject("stall").AddComponent<Stall>();
        stall.SleepMs = 40;
        loop.Tick(1f / 60f);
        stall.SleepMs = 0;
        loop.Tick(1f / 60f);
        FrameTimingManager.CaptureFrameTimings();
        loop.Tick(1f / 60f);                     // not captured yet: the snapshot still ends at the slow frame

        Assert.Equal(2u, FrameTimingManager.GetLatestTimings(4, timings));
        Assert.True(timings[0].cpuFrameTime >= 38, $"newest {timings[0].cpuFrameTime:F1} ms");
        Assert.True(timings[0].frameStartTimestamp > timings[1].frameStartTimestamp);
        Assert.Equal(1u, FrameTimingManager.GetLatestTimings(1, timings));
    }

    [Fact]
    public void UnscaledDeltaTime_IsTheRealFrame_DeltaTimeIsCappedAtMaximumDeltaTime()
    {
        using var loop = new GameLoop();
        Time.maximumDeltaTime = 1f / 3f;   // the project's Maximum Allowed Timestep

        loop.Tick(0.5f);                   // a 500 ms frame from a normal (unscripted) host

        Assert.Equal(0.5f, Time.unscaledDeltaTime, 5);
        Assert.Equal(1f / 3f, Time.deltaTime, 5);
    }

    [Fact]
    public void ProfilerRecorder_FrameTimeStats_ReadTheMeasuredFrame()
    {
        using var loop = new GameLoop();
        using var total = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Total Frame Time");
        using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        var stall = new GameObject("stall").AddComponent<Stall>();

        stall.SleepMs = 50;
        loop.Tick(1f / 60f);
        stall.SleepMs = 0;
        loop.Tick(1f / 60f);

        Assert.True(total.LastValue >= 48_000_000, $"CPU Total Frame Time {total.LastValue / 1e6:F1} ms");
        Assert.True(main.LastValue >= 48_000_000, $"Main Thread {main.LastValue / 1e6:F1} ms");
    }
}
