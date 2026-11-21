using System;
using System.Collections.Generic;
using CosmicShore.Engine;

// The QUIT GAME button did nothing in Amoebius: Application.Quit() only raised `quitting`, never sent
// OnApplicationQuit and never closed the player. These pin Unity's quit sequence and the focus message.

class QuitProbe : MonoBehaviour
{
    public static readonly List<string> Calls = new();
    public string Tag = "a";
    void OnApplicationQuit() => Calls.Add("quit:" + Tag);
    void OnApplicationFocus(bool focused) => Calls.Add($"focus:{Tag}:{focused}");
}

class ThrowingQuitProbe : MonoBehaviour
{
    void OnApplicationQuit() => throw new InvalidOperationException("one behaviour's failure");
}

public class ApplicationMessagesTests
{
    static QuitProbe Probe(string tag)
    {
        var p = new GameObject("probe-" + tag).AddComponent<QuitProbe>();
        p.Tag = tag;
        return p;
    }

    [Fact]
    public void Quit_WaitsForTheFrameEnd_ThenSendsOnApplicationQuit_ThenQuitting_Once()
    {
        using var loop = new GameLoop();
        QuitProbe.Calls.Clear();
        Probe("a");
        Probe("b");
        var order = new List<string>();
        Action onQuitting = () => order.Add("quitting:" + QuitProbe.Calls.Count);
        Application.quitting += onQuitting;
        try
        {
            Application.Quit();
            Assert.True(Application.QuitRequested);
            Assert.Empty(QuitProbe.Calls); // nothing happens inside the frame that asked

            Assert.True(Application.RunQuitSequence());
            Assert.False(Application.QuitRequested);
            Assert.Equal(new[] { "quit:a", "quit:b" }, QuitProbe.Calls.ToArray());
            Assert.Equal(new[] { "quitting:2" }, order.ToArray()); // after every OnApplicationQuit

            // The window closing afterwards does not send it twice.
            Assert.True(Application.RunQuitSequence(cancellable: false));
            Assert.Equal(2, QuitProbe.Calls.Count);
            Assert.Single(order);
        }
        finally { Application.quitting -= onQuitting; }
    }

    [Fact]
    public void WantsToQuit_ReturningFalse_CancelsTheQuit_AndTheGameKeepsRunning()
    {
        using var loop = new GameLoop();
        QuitProbe.Calls.Clear();
        Probe("a");
        Func<bool> refuse = () => false;
        Application.wantsToQuit += refuse;
        try
        {
            Application.Quit();
            Assert.False(Application.RunQuitSequence());
            Assert.False(Application.QuitRequested);
            Assert.Empty(QuitProbe.Calls);
        }
        finally { Application.wantsToQuit -= refuse; }

        Application.Quit();
        Assert.True(Application.RunQuitSequence());
        Assert.Equal(new[] { "quit:a" }, QuitProbe.Calls.ToArray());
    }

    [Fact]
    public void OneBehavioursException_DoesNotKeepTheOthersFromHearingQuit()
    {
        using var loop = new GameLoop();
        QuitProbe.Calls.Clear();
        new GameObject("thrower").AddComponent<ThrowingQuitProbe>();
        Probe("after");
        Assert.True(Application.RunQuitSequence());
        Assert.Contains("quit:after", QuitProbe.Calls);
    }

    [Fact]
    public void AnInactiveObject_DoesNotHearQuit()
    {
        using var loop = new GameLoop();
        QuitProbe.Calls.Clear();
        Probe("off").gameObject.SetActive(false);
        Probe("on");
        Application.RunQuitSequence();
        Assert.Equal(new[] { "quit:on" }, QuitProbe.Calls.ToArray());
    }

    [Fact]
    public void FocusChanges_ReachEveryBehaviour_OncePerChange()
    {
        using var loop = new GameLoop();
        QuitProbe.Calls.Clear();
        Probe("a");
        bool? heard = null;
        Action<bool> onFocus = f => heard = f;
        Application.focusChanged += onFocus;
        try
        {
            Application.SetFocus(false);
            Application.SetFocus(false); // no change, no message
            Application.SetFocus(true);
            Assert.Equal(new[] { "focus:a:False", "focus:a:True" }, QuitProbe.Calls.ToArray());
            Assert.True(heard);
            Assert.True(Application.isFocused);
        }
        finally { Application.focusChanged -= onFocus; Application.SetFocus(true); }
    }
}
