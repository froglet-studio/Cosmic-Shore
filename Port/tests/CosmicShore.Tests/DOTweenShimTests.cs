using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using DG.Tweening;
using DG.Tweening.Core.Easing;

namespace CosmicShore.Tests;

/// <summary>
/// Behavior tests for the first-party DOTween API shim (Port/src/CosmicShore.Compat/DOTween):
/// Penner ease math, frame-loop driving, Sequence timing, loops, callbacks, kill semantics,
/// SetLink and timescale independence.
/// </summary>
public class DOTweenShimTests : IDisposable
{
    const float Eps = 1e-4f;

    public void Dispose()
    {
        DOTween.KillAll();
        Time.timeScale = 1f;
    }

    // ── Ease math ──

    [Theory]
    [InlineData(Ease.Linear, 0.5f)]
    [InlineData(Ease.InQuad, 0.25f)]
    [InlineData(Ease.OutQuad, 0.75f)]
    [InlineData(Ease.InOutQuad, 0.5f)]
    [InlineData(Ease.InCubic, 0.125f)]
    [InlineData(Ease.OutCubic, 0.875f)]
    [InlineData(Ease.InOutCubic, 0.5f)]
    [InlineData(Ease.InQuart, 0.0625f)]
    [InlineData(Ease.OutQuart, 0.9375f)]
    [InlineData(Ease.InQuint, 0.03125f)]
    [InlineData(Ease.OutQuint, 0.96875f)]
    [InlineData(Ease.InSine, 0.29289322f)]
    [InlineData(Ease.OutSine, 0.70710678f)]
    [InlineData(Ease.InOutSine, 0.5f)]
    [InlineData(Ease.InExpo, 0.03125f)]
    [InlineData(Ease.OutExpo, 0.96875f)]
    [InlineData(Ease.InOutExpo, 0.5f)]
    [InlineData(Ease.InCirc, 0.13397460f)]
    [InlineData(Ease.OutCirc, 0.86602540f)]
    [InlineData(Ease.InBack, -0.08769750f)]
    [InlineData(Ease.OutBack, 1.08769750f)]
    [InlineData(Ease.InOutBack, 0.5f)]
    [InlineData(Ease.OutBounce, 0.765625f)]
    [InlineData(Ease.InBounce, 0.234375f)]
    [InlineData(Ease.InOutBounce, 0.5f)]
    public void Ease_Midpoint_MatchesPennerCurve(Ease ease, float expected)
    {
        float v = EaseManager.Evaluate(ease, null, 0.5f, 1f, 1.70158f, 0f);
        Assert.Equal(expected, v, 4);
    }

    [Fact]
    public void Ease_EveryStandardCurve_HitsBothEndpoints()
    {
        foreach (Ease ease in Enum.GetValues(typeof(Ease)))
        {
            if (ease is Ease.INTERNAL_Custom or Ease.INTERNAL_Zero or Ease.Unset) continue;
            if (EaseManager.IsFlashEase(ease)) continue;
            Assert.True(MathF.Abs(EaseManager.Evaluate(ease, null, 0f, 2f, 1.70158f, 0f)) < Eps, $"{ease} at 0");
            Assert.True(MathF.Abs(EaseManager.Evaluate(ease, null, 2f, 2f, 1.70158f, 0f) - 1f) < Eps, $"{ease} at end");
        }
    }

    [Fact]
    public void Ease_Elastic_OvershootsAndSettles()
    {
        float max = 0f;
        for (int i = 1; i < 100; i++)
            max = MathF.Max(max, EaseManager.Evaluate(Ease.OutElastic, null, i / 100f, 1f, 1.70158f, 0f));
        Assert.True(max > 1f, "OutElastic overshoots past the end value");
        Assert.Equal(6, (int)Ease.OutQuad);   // serialized ids match DOTween's
        Assert.Equal(27, (int)Ease.OutBack);
        Assert.Equal(4, (int)Ease.InOutSine);
    }

    [Fact]
    public void Defaults_MatchDocumentedDOTweenDefaults()
    {
        Assert.Equal(Ease.OutQuad, DOTween.defaultEaseType);
        Assert.Equal(1.70158f, DOTween.defaultEaseOvershootOrAmplitude);
        Assert.True(DOTween.defaultAutoKill);
    }

    // ── Frame loop driving ──

    [Fact]
    public void Tweener_AdvancesOnTheFrameLoop_AndAutoKillsAtEnd()
    {
        using var loop = new GameLoop();
        var go = new GameObject("t");
        go.transform.localScale = new Vector3(0f, 0f, 0f);
        var tw = go.transform.DOScale(1f, 1f).SetEase(Ease.Linear);

        Assert.Equal(0f, go.transform.localScale.x); // nothing applied until the next update
        loop.Tick(0.25f);
        Assert.Equal(0.25f, go.transform.localScale.x, 4);
        loop.Tick(0.25f);
        Assert.Equal(0.5f, go.transform.localScale.x, 4);
        loop.Run(4, 0.25f);
        Assert.Equal(1f, go.transform.localScale.x, 4);
        Assert.False(tw.IsActive()); // autoKill
    }

    [Fact]
    public void Delay_ElapsesBeforeTheTweenStarts()
    {
        using var loop = new GameLoop();
        float v = 0f;
        DOTween.To(() => v, x => v = x, 1f, 1f).SetEase(Ease.Linear).SetDelay(0.5f);
        loop.Tick(0.25f);
        Assert.Equal(0f, v);
        loop.Tick(0.5f); // 0.25 of overflow past the delay
        Assert.Equal(0.25f, v, 4);
    }

    [Fact]
    public void From_JumpsImmediately_ThenTweensBackToCurrent()
    {
        using var loop = new GameLoop();
        float v = 5f;
        DOTween.To(() => v, x => v = x, 1f, 1f).SetEase(Ease.Linear).From();
        Assert.Equal(1f, v);
        loop.Tick(0.5f);
        Assert.Equal(3f, v, 4);
        loop.Tick(0.5f);
        Assert.Equal(5f, v, 4);
    }

    [Fact]
    public void Relative_AddsEndValueToStart()
    {
        using var loop = new GameLoop();
        float v = 2f;
        DOTween.To(() => v, x => v = x, 3f, 1f).SetEase(Ease.Linear).SetRelative();
        loop.Tick(1f);
        Assert.Equal(5f, v, 4);
    }

    // ── Sequences ──

    [Fact]
    public void Sequence_AppendJoinInsert_Timing()
    {
        using var loop = new GameLoop();
        float a = 0, b = 0, c = 0, d = 0;
        var seq = DOTween.Sequence()
            .Append(DOTween.To(() => a, x => a = x, 1f, 1f).SetEase(Ease.Linear))
            .Join(DOTween.To(() => b, x => b = x, 1f, 1f).SetEase(Ease.Linear))
            .Append(DOTween.To(() => c, x => c = x, 1f, 0.5f).SetEase(Ease.Linear))
            .Insert(0.25f, DOTween.To(() => d, x => d = x, 1f, 0.5f).SetEase(Ease.Linear));

        Assert.Equal(1.5f, seq.Duration(), 4);
        loop.Tick(0.5f);
        Assert.Equal(0.5f, a, 4);
        Assert.Equal(0.5f, b, 4);
        Assert.Equal(0f, c);
        Assert.Equal(0.5f, d, 4);
        loop.Tick(0.75f);
        Assert.Equal(1f, a, 4);
        Assert.Equal(1f, d, 4);
        Assert.Equal(0.5f, c, 4);
        loop.Tick(0.5f);
        Assert.Equal(1f, c, 4);
        Assert.False(seq.IsActive());
    }

    [Fact]
    public void Sequence_NestedTweensCaptureStartValueWhenReached()
    {
        using var loop = new GameLoop();
        float v = 0f;
        DOTween.Sequence()
            .Append(DOTween.To(() => v, x => v = x, 2f, 1f).SetEase(Ease.Linear))
            .Append(DOTween.To(() => v, x => v = x, 0f, 1f).SetEase(Ease.Linear));
        loop.Tick(1.5f);
        Assert.Equal(1f, v, 4); // second tween started from 2, halfway to 0
        loop.Tick(0.5f);
        Assert.Equal(0f, v, 4);
    }

    [Fact]
    public void Sequence_IntervalsAndCallbacks_FireAtTheirPositions()
    {
        using var loop = new GameLoop();
        var log = new List<string>();
        DOTween.Sequence()
            .AppendCallback(() => log.Add("start"))
            .AppendInterval(0.5f)
            .AppendCallback(() => log.Add("mid"))
            .AppendInterval(0.5f)
            .OnComplete(() => log.Add("done"));
        loop.Tick(0.25f);
        Assert.Equal(new[] { "start" }, log);
        loop.Tick(0.5f);
        Assert.Equal(new[] { "start", "mid" }, log);
        loop.Tick(0.5f);
        Assert.Equal(new[] { "start", "mid", "done" }, log);
    }

    [Fact]
    public void Sequence_Loops_RestartAndYoyo()
    {
        using var loop = new GameLoop();
        float r = 0f, y = 0f;
        int rCallbacks = 0;
        DOTween.Sequence()
            .AppendCallback(() => rCallbacks++)
            .Append(DOTween.To(() => r, x => r = x, 1f, 1f).SetEase(Ease.Linear))
            .SetLoops(2, LoopType.Restart);
        DOTween.Sequence()
            .Append(DOTween.To(() => y, x => y = x, 1f, 1f).SetEase(Ease.Linear))
            .SetLoops(2, LoopType.Yoyo);
        loop.Tick(1.25f);
        Assert.Equal(0.25f, r, 4); // restarted from the start value
        Assert.Equal(0.75f, y, 4); // playing back
        Assert.Equal(2, rCallbacks); // the leading callback fires once per loop
        loop.Tick(1f);
        Assert.Equal(1f, r, 4);
        Assert.Equal(0f, y, 4);
    }

    // ── Loops ──

    [Fact]
    public void Loops_Restart()
    {
        using var loop = new GameLoop();
        float v = 0f;
        int steps = 0;
        var t = DOTween.To(() => v, x => v = x, 1f, 1f).SetEase(Ease.Linear).SetLoops(3).OnStepComplete(() => steps++);
        loop.Tick(1.5f);
        Assert.Equal(0.5f, v, 4);
        Assert.Equal(1, steps);
        loop.Tick(1.5f);
        Assert.Equal(1f, v, 4);
        Assert.Equal(3, steps);
        Assert.False(t.IsActive());
    }

    [Fact]
    public void Loops_Yoyo_PlaysBackOnOddCycles()
    {
        using var loop = new GameLoop();
        float v = 0f;
        DOTween.To(() => v, x => v = x, 1f, 1f).SetEase(Ease.InQuad).SetLoops(2, LoopType.Yoyo);
        loop.Tick(0.5f);
        Assert.Equal(0.25f, v, 4);
        loop.Tick(1f); // 0.5 into the backward cycle: mirrored time 0.5 → InQuad 0.25
        Assert.Equal(0.25f, v, 4);
        loop.Tick(0.5f);
        Assert.Equal(0f, v, 4);
    }

    [Fact]
    public void Loops_Incremental_ContinuesFromPreviousEnd()
    {
        using var loop = new GameLoop();
        float v = 0f;
        DOTween.To(() => v, x => v = x, 1f, 1f).SetEase(Ease.Linear).SetLoops(2, LoopType.Incremental);
        loop.Tick(1.5f);
        Assert.Equal(1.5f, v, 4);
        loop.Tick(0.5f);
        Assert.Equal(2f, v, 4);
    }

    [Fact]
    public void Loops_Infinite_NeverComplete()
    {
        using var loop = new GameLoop();
        float v = 0f;
        var t = DOTween.To(() => v, x => v = x, 1f, 1f).SetEase(Ease.Linear).SetLoops(-1, LoopType.Yoyo);
        loop.Run(5, 1f); // exactly 5 loops done: start of (backward) loop 5 = end value
        Assert.True(t.IsActive());
        Assert.Equal(1f, v, 3);
        loop.Tick(0.5f);
        Assert.Equal(0.5f, v, 3);
    }

    // ── Callbacks ──

    [Fact]
    public void Callbacks_FireInDocumentedOrder()
    {
        using var loop = new GameLoop();
        var log = new List<string>();
        float v = 0f;
        DOTween.To(() => v, x => v = x, 1f, 0.5f)
            .OnStart(() => log.Add("start"))
            .OnPlay(() => log.Add("play"))
            .OnUpdate(() => log.Add("update"))
            .OnStepComplete(() => log.Add("step"))
            .OnComplete(() => log.Add("complete"))
            .OnKill(() => log.Add("kill"));
        loop.Tick(1f);
        Assert.Equal(new[] { "start", "play", "update", "step", "complete", "kill" }, log);
    }

    [Fact]
    public void Callbacks_NestedCompletesBeforeSequence()
    {
        using var loop = new GameLoop();
        var log = new List<string>();
        float v = 0f;
        DOTween.Sequence()
            .Append(DOTween.To(() => v, x => v = x, 1f, 0.5f).OnComplete(() => log.Add("nested")))
            .AppendCallback(() => log.Add("callback"))
            .OnComplete(() => log.Add("sequence"));
        loop.Tick(1f);
        Assert.Equal(new[] { "nested", "callback", "sequence" }, log);
    }

    // ── Kill ──

    [Fact]
    public void Kill_StopsWhereItIs_KillTrueSnapsToEnd()
    {
        using var loop = new GameLoop();
        float a = 0f, b = 0f;
        bool aCompleted = false, bCompleted = false, aKilled = false;
        var ta = DOTween.To(() => a, x => a = x, 1f, 1f).SetEase(Ease.Linear)
            .OnComplete(() => aCompleted = true).OnKill(() => aKilled = true);
        var tb = DOTween.To(() => b, x => b = x, 1f, 1f).SetEase(Ease.Linear).OnComplete(() => bCompleted = true);
        loop.Tick(0.25f);
        ta.Kill();
        tb.Kill(true);
        loop.Tick(0.25f);
        Assert.Equal(0.25f, a, 4);
        Assert.False(aCompleted);
        Assert.True(aKilled);
        Assert.Equal(1f, b, 4);
        Assert.True(bCompleted);
        Assert.False(ta.IsActive());
        Assert.False(tb.IsActive());
        ta.Kill(); // killing twice / killing null are no-ops
        ((Tween)null).Kill();
    }

    [Fact]
    public void Kill_ByTarget_AndDOKill()
    {
        using var loop = new GameLoop();
        var go = new GameObject("k");
        go.transform.DOScale(2f, 1f);
        go.transform.DOLocalMove(new Vector3(1f, 0f, 0f), 1f);
        Assert.True(DOTween.IsTweening(go.transform));
        Assert.Equal(2, go.transform.DOKill());
        Assert.False(DOTween.IsTweening(go.transform));

        var seq = DOTween.Sequence().SetTarget(go.transform).AppendInterval(1f);
        Assert.Equal(1, DOTween.Kill(go.transform, complete: false));
        Assert.False(seq.IsActive());
    }

    // ── Link / timescale ──

    [Fact]
    public void SetLink_KillsTweenWhenGameObjectIsDestroyed()
    {
        using var loop = new GameLoop();
        var go = new GameObject("linked");
        float v = 0f;
        var t = DOTween.To(() => v, x => v = x, 1f, 1f).SetEase(Ease.Linear).SetLink(go);
        loop.Tick(0.25f);
        CosmicShore.Engine.Object.Destroy(go);
        loop.Tick(0.25f); // destroy flushes at end of this frame
        loop.Tick(0.25f);
        Assert.False(t.IsActive());
        Assert.True(v <= 0.5f + Eps);
    }

    [Fact]
    public void SetLink_PauseOnDisablePlayOnEnable()
    {
        using var loop = new GameLoop();
        var go = new GameObject("linked");
        float v = 0f;
        var t = DOTween.To(() => v, x => v = x, 1f, 1f).SetEase(Ease.Linear)
            .SetLink(go, LinkBehaviour.PauseOnDisablePlayOnEnable);
        loop.Tick(0.25f);
        go.SetActive(false);
        loop.Tick(0.25f);
        Assert.Equal(0.25f, v, 4);
        Assert.False(t.IsPlaying());
        go.SetActive(true);
        loop.Tick(0.25f);
        Assert.Equal(0.5f, v, 4);
    }

    [Fact]
    public void SetUpdate_True_IgnoresTimeScale()
    {
        using var loop = new GameLoop();
        float scaled = 0f, unscaled = 0f;
        DOTween.To(() => scaled, x => scaled = x, 1f, 1f).SetEase(Ease.Linear);
        DOTween.To(() => unscaled, x => unscaled = x, 1f, 1f).SetEase(Ease.Linear).SetUpdate(true);
        Time.timeScale = 0f;
        loop.Tick(0.5f);
        Assert.Equal(0f, scaled);
        Assert.Equal(0.5f, unscaled, 4);
        Time.timeScale = 0.5f;
        loop.Tick(0.5f);
        Assert.Equal(0.25f, scaled, 4);
        Assert.Equal(1f, unscaled, 4);
    }

    // ── Control ──

    [Fact]
    public void RewindAndRestart()
    {
        using var loop = new GameLoop();
        float v = 0f;
        var t = DOTween.To(() => v, x => v = x, 1f, 1f).SetEase(Ease.Linear).SetAutoKill(false);
        loop.Tick(0.5f);
        t.Rewind();
        Assert.Equal(0f, v, 4);
        Assert.False(t.IsPlaying());
        loop.Tick(0.5f);
        Assert.Equal(0f, v, 4);
        t.Restart();
        loop.Tick(0.25f);
        Assert.Equal(0.25f, v, 4);
        loop.Tick(1f);
        Assert.True(t.IsComplete());
        Assert.True(t.IsActive()); // autoKill off
    }

    [Fact]
    public void Punch_And_Shake_ReturnToStart()
    {
        using var loop = new GameLoop();
        var go = new GameObject("p");
        go.transform.localScale = new Vector3(1f, 1f, 1f);
        var punch = go.transform.DOPunchScale(new Vector3(0.5f, 0.5f, 0.5f), 0.5f, 8, 0.7f);
        loop.Tick(0.05f);
        Assert.True(go.transform.localScale.x > 1f); // pushed toward the punch
        loop.Run(20, 0.05f);
        Assert.False(punch.IsActive());
        Assert.Equal(1f, go.transform.localScale.x, 4);

        var shake = go.transform.DOShakePosition(0.5f, 2f, 20, 90f, false, false);
        loop.Tick(0.1f);
        Assert.NotEqual(0f, go.transform.localPosition.magnitude);
        loop.Run(20, 0.05f);
        Assert.False(shake.IsActive());
        Assert.Equal(0f, go.transform.localPosition.magnitude, 4);
    }

    [Fact]
    public void UiShortcuts_FadeCanvasGroupAndFillImage()
    {
        using var loop = new GameLoop();
        var go = new GameObject("ui", typeof(RectTransform));
        var group = go.AddComponent<CanvasGroup>();
        var image = go.AddComponent<Image>();
        group.alpha = 0f;
        group.DOFade(1f, 1f).SetEase(Ease.Linear);
        image.DOFillAmount(0f, 1f).SetEase(Ease.Linear);
        image.DOFade(0f, 1f).SetEase(Ease.Linear);
        ((RectTransform)go.transform).DOAnchorPos(new Vector2(10f, 0f), 1f).SetEase(Ease.Linear);
        loop.Tick(0.5f);
        Assert.Equal(0.5f, group.alpha, 4);
        Assert.Equal(0.5f, image.fillAmount, 4);
        Assert.Equal(0.5f, image.color.a, 4);
        Assert.Equal(5f, ((RectTransform)go.transform).anchoredPosition.x, 4);
    }

    [Fact]
    public void DelayedCall_FiresOnceAfterDelay()
    {
        using var loop = new GameLoop();
        int calls = 0;
        DOVirtual.DelayedCall(0.5f, () => calls++);
        loop.Tick(0.25f);
        Assert.Equal(0, calls);
        loop.Tick(0.5f);
        Assert.Equal(1, calls);
        loop.Tick(1f);
        Assert.Equal(1, calls);
    }
}
