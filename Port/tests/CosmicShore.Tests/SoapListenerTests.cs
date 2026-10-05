using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Engine;
using CosmicShore.Engine.Events;
using CosmicShore.Engine.Soap;

namespace CosmicShore.Tests;

// The SOAP listener components the game's prefabs are built on (EventListenerNoParam alone
// sits on 48 of them). Until these existed the whole family resolved to "no script", so no
// inspector-wired SOAP reaction ever ran — the in-game GO button never hid on Ready.
public class SoapListenerTests
{
    static void Set(object target, string field, object value)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f != null) { f.SetValue(target, value); return; }
        }
        throw new System.MissingFieldException(target.GetType().Name, field);
    }

    static (GameObject go, EventListenerBool listener, ScriptableEventBool channel, List<bool> seen) MakeBoolListener(int binding)
    {
        var channel = ScriptableObject.CreateInstance<ScriptableEventBool>();
        var seen = new List<bool>();
        var response = new EventListenerBool.EventResponse();
        Set(response, "_scriptableEvent", channel);
        var evt = new EventListenerBool.BoolUnityEvent();
        evt.AddListener(seen.Add);
        Set(response, "_response", evt);

        var go = new GameObject("Listener");
        go.SetActive(false);
        var listener = go.AddComponent<EventListenerBool>();
        Set(listener, "_eventResponses", new[] { response });
        Set(listener, "_binding", System.Enum.ToObject(typeof(EventListenerBase).GetNestedType("Binding", BindingFlags.NonPublic)!, binding));
        return (go, listener, channel, seen);
    }

    [Fact]
    public void UntilDestroy_KeepsReactingWhileDisabled()
    {
        using var loop = new GameLoop();
        var (go, _, channel, seen) = MakeBoolListener(binding: 0);
        go.SetActive(true);   // Awake subscribes
        go.SetActive(false);  // still subscribed (UNTIL_DESTROY)
        channel.Raise(false);
        Assert.Equal(new[] { false }, seen);

        Object.DestroyImmediate(go);
        channel.Raise(true);
        Assert.Single(seen);
    }

    [Fact]
    public void UntilDisable_FollowsEnableState()
    {
        using var loop = new GameLoop();
        var (go, _, channel, seen) = MakeBoolListener(binding: 1);
        go.SetActive(true);
        channel.Raise(true);
        go.SetActive(false);
        channel.Raise(false);
        Assert.Equal(new[] { true }, seen);
    }

    [Fact]
    public void DisableAfterSubscribing_HidesTheObjectButStillReacts()
    {
        using var loop = new GameLoop();
        var (go, listener, channel, seen) = MakeBoolListener(binding: 0);
        Set(listener, "_disableAfterSubscribing", true);
        go.SetActive(true);
        Assert.False(go.activeSelf);
        channel.Raise(true);
        Assert.Equal(new[] { true }, seen);
    }

    [Fact]
    public void Raise_CallsListenerComponentsNewestFirst_ThenCSharpSubscribers()
    {
        using var loop = new GameLoop();
        var channel = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        var order = new List<string>();
        channel.OnRaised += () => order.Add("csharp");

        GameObject Make(string tag)
        {
            var response = new EventListenerNoParam.EventResponse();
            Set(response, "_scriptableEvent", channel);
            var evt = new UnityEvent();
            evt.AddListener(() => order.Add(tag));
            Set(response, "_response", evt);
            var go = new GameObject(tag);
            go.SetActive(false);
            Set(go.AddComponent<EventListenerNoParam>(), "_eventResponses", new[] { response });
            go.SetActive(true);
            return go;
        }

        Make("first");
        Make("second");
        channel.Raise();
        Assert.Equal(new[] { "second", "first", "csharp" }, order);
    }
}
