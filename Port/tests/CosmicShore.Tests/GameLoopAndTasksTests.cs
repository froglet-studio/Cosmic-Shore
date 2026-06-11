using System;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Engine;
using CosmicShore.Engine.Tasks;

namespace CosmicShore.Tests;

public class GameLoopTests
{
    class FixedCounter : MonoBehaviour
    {
        public int FixedUpdates;
        public float ObservedFixedDelta;
        void FixedUpdate()
        {
            FixedUpdates++;
            ObservedFixedDelta = Time.deltaTime; // must report fixedDeltaTime here
        }
    }

    [Fact]
    public void FixedUpdate_AccumulatorProducesCorrectStepCount()
    {
        using var loop = new GameLoop();
        Time.fixedDeltaTime = 1f / 60f;
        var go = new GameObject("fixed");
        var counter = go.AddComponent<FixedCounter>();

        loop.Run(10, 1f / 30f); // each frame is two fixed steps

        Assert.Equal(20, counter.FixedUpdates);
        Assert.Equal(1f / 60f, counter.ObservedFixedDelta, 5);
    }

    [Fact]
    public void TimeScale_Zero_SuspendsFixedAndScaledTime()
    {
        using var loop = new GameLoop();
        Time.fixedDeltaTime = 1f / 60f;
        var go = new GameObject("paused");
        var counter = go.AddComponent<FixedCounter>();

        Time.timeScale = 0f;
        loop.Run(10, 1f / 60f);

        Assert.Equal(0, counter.FixedUpdates);
        Assert.Equal(0f, Time.time, 5);
        Assert.True(Time.unscaledTime > 0.16f);
        Time.timeScale = 1f;
    }

    [Fact]
    public void SecondLoop_FailsLoud()
    {
        using var loop = new GameLoop();
        Assert.Throws<InvalidOperationException>(() => new GameLoop());
    }

    class ThrowingBehaviour : MonoBehaviour
    {
        void Update() => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void ExceptionInOneBehaviour_DoesNotBreakOthers()
    {
        using var loop = new GameLoop();
        var sink = new CapturingLogSink();
        var previousSink = Debug.Sink;
        Debug.Sink = sink;
        try
        {
            var bad = new GameObject("bad");
            bad.AddComponent<ThrowingBehaviour>();
            var good = new GameObject("good");
            var probe = good.AddComponent<ProbeCounter>();

            loop.Tick(0.016f);

            Assert.Equal(1, probe.Updates);
            Assert.Contains(sink.Entries, e => e.Type == LogType.Exception && e.Message.Contains("boom"));
        }
        finally { Debug.Sink = previousSink; }
    }

    class ProbeCounter : MonoBehaviour
    {
        public int Updates;
        void Update() => Updates++;
    }
}

public class GameTaskTests
{
    [Fact]
    public void Yield_ResumesOnNextFrame()
    {
        using var loop = new GameLoop();
        int resumedAtFrame = -1;

        async Task Routine()
        {
            await GameTask.Yield();
            resumedAtFrame = Time.frameCount;
        }

        var task = Routine();
        Assert.Equal(-1, resumedAtFrame); // suspended immediately

        loop.Tick(0.016f);
        Assert.Equal(1, resumedAtFrame);
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public void Delay_CompletesAfterGameTimeElapses()
    {
        using var loop = new GameLoop();
        bool done = false;

        async Task Routine()
        {
            await GameTask.Delay(0.5f);
            done = true;
        }

        Routine().Forget();
        loop.Run(29, 1f / 60f);
        Assert.False(done);
        loop.Run(2, 1f / 60f);
        Assert.True(done);
    }

    [Fact]
    public void Delay_RespectsTimeScale()
    {
        using var loop = new GameLoop();
        bool done = false;

        async Task Routine()
        {
            await GameTask.Delay(0.5f);
            done = true;
        }

        Routine().Forget();
        Time.timeScale = 0f;
        loop.Run(120, 1f / 60f); // 2s of wall time, 0s of game time
        Assert.False(done);

        Time.timeScale = 1f;
        loop.Run(31, 1f / 60f);
        Assert.True(done);
    }

    [Fact]
    public void WaitUntil_ResumesWhenPredicateTurnsTrue()
    {
        using var loop = new GameLoop();
        bool flag = false;
        bool resumed = false;

        async Task Routine()
        {
            await GameTask.WaitUntil(() => flag);
            resumed = true;
        }

        Routine().Forget();
        loop.Run(5, 0.016f);
        Assert.False(resumed);

        flag = true;
        loop.Tick(0.016f);
        Assert.True(resumed);
    }

    [Fact]
    public void Cancellation_PropagatesAsTaskCancellation()
    {
        using var loop = new GameLoop();
        using var cts = new CancellationTokenSource();
        bool reachedEnd = false;

        async Task Routine(CancellationToken ct)
        {
            await GameTask.Delay(10f, ct);
            reachedEnd = true;
        }

        var task = Routine(cts.Token);
        loop.Tick(0.016f);
        cts.Cancel();
        loop.Tick(0.016f);

        Assert.True(task.IsCanceled);
        Assert.False(reachedEnd);
    }

    [Fact]
    public void WaitUntil_PredicateException_FaultsTheTask()
    {
        using var loop = new GameLoop();

        async Task Routine()
        {
            await GameTask.WaitUntil(() => throw new InvalidOperationException("bad predicate"));
        }

        var task = Routine();
        loop.Tick(0.016f);

        Assert.True(task.IsFaulted);
        Assert.IsType<InvalidOperationException>(task.Exception!.GetBaseException());
    }

    [Fact]
    public void Forget_LogsFaults_InsteadOfThrowing()
    {
        using var loop = new GameLoop();
        var sink = new CapturingLogSink();
        var previousSink = Debug.Sink;
        Debug.Sink = sink;
        try
        {
            async Task Failing()
            {
                await GameTask.Yield();
                throw new InvalidOperationException("forgotten failure");
            }

            Failing().Forget();
            loop.Tick(0.016f);

            // Forget awaits a standard Task, so its logging continuation may hop through
            // the ambient (xunit) sync context asynchronously — wait for it to land.
            for (int i = 0; i < 500 && sink.Entries.Count == 0; i++) Thread.Sleep(2);

            Assert.Contains(sink.Entries, e => e.Type == LogType.Exception && e.Message.Contains("forgotten failure"));
        }
        finally { Debug.Sink = previousSink; }
    }

    [Fact]
    public void ExternalThread_SwitchToMainThread_ResumesOnLoop()
    {
        using var loop = new GameLoop();
        bool resumedOnLoop = false;
        var scheduled = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            async Task Hop()
            {
                await GameTask.SwitchToMainThread();
                resumedOnLoop = GameLoop.Current.IsOnLoopThread;
            }
            Hop().Forget();
            scheduled.Set();
        });
        thread.Start();
        Assert.True(scheduled.Wait(5000));
        thread.Join();

        Assert.False(resumedOnLoop);
        loop.Tick(0.016f); // pump marshals the continuation onto the loop
        Assert.True(resumedOnLoop);
    }

    [Fact]
    public void WaitForEndOfFrame_RunsAfterLateUpdate_SameFrame()
    {
        using var loop = new GameLoop();
        var order = new System.Collections.Generic.List<string>();
        var go = new GameObject("late");
        var probe = go.AddComponent<LateProbe>();
        probe.Order = order;

        async Task Routine()
        {
            await GameTask.WaitForEndOfFrame();
            order.Add("endOfFrame");
        }

        Routine().Forget();
        loop.Tick(0.016f);

        Assert.Equal(new[] { "lateUpdate", "endOfFrame" }, order);
    }

    class LateProbe : MonoBehaviour
    {
        public System.Collections.Generic.List<string> Order;
        void LateUpdate() => Order?.Add("lateUpdate");
    }
}
