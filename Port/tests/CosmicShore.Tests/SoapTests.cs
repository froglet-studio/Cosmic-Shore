using System.Collections.Generic;
using CosmicShore.Engine.Soap;

namespace CosmicShore.Tests;

public class ScriptableVariableTests
{
    [Fact]
    public void Value_Change_FiresEvent_WithNewValue()
    {
        var variable = new IntVariable();
        int observed = -1;
        variable.OnValueChanged += v => observed = v;

        variable.Value = 42;

        Assert.Equal(42, observed);
        Assert.Equal(42, variable.Value);
    }

    [Fact]
    public void Value_SameValue_DoesNotFire()
    {
        var variable = new IntVariable();
        variable.Value = 5;
        int fireCount = 0;
        variable.OnValueChanged += _ => fireCount++;

        variable.Value = 5;

        Assert.Equal(0, fireCount);
    }

    [Fact]
    public void PreviousValue_TracksLastValue()
    {
        var variable = new FloatVariable();
        variable.Value = 1f;
        variable.Value = 2f;

        Assert.Equal(1f, variable.PreviousValue);
        Assert.Equal(2f, variable.Value);
    }

    [Fact]
    public void ResetToInitialValue_RestoresAndNotifies()
    {
        var variable = new IntVariable();
        variable.SetInitialValue(10);
        variable.Value = 99;

        var observed = new List<int>();
        variable.OnValueChanged += v => observed.Add(v);
        variable.ResetToInitialValue();

        Assert.Equal(10, variable.Value);
        Assert.Equal(new[] { 10 }, observed);
    }
}

public class ScriptableEventTests
{
    [Fact]
    public void Raise_InvokesAllListenersInOrder()
    {
        var evt = new ScriptableEventInt();
        var calls = new List<string>();
        evt.OnRaised += v => calls.Add($"first:{v}");
        evt.OnRaised += v => calls.Add($"second:{v}");

        evt.Raise(7);

        Assert.Equal(new[] { "first:7", "second:7" }, calls);
    }

    [Fact]
    public void Raise_NoListeners_DoesNotThrow()
    {
        var evt = new ScriptableEventNoParam();
        evt.Raise();
    }

    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        var evt = new ScriptableEventNoParam();
        int count = 0;
        System.Action handler = () => count++;
        evt.OnRaised += handler;
        evt.Raise();
        evt.OnRaised -= handler;
        evt.Raise();

        Assert.Equal(1, count);
    }

    [Fact]
    public void LastValue_RetainsMostRecentPayload()
    {
        var evt = new ScriptableEventString();
        evt.Raise("alpha");
        evt.Raise("beta");

        Assert.Equal("beta", evt.LastValue);
    }
}

public class ScriptableListTests
{
    [Fact]
    public void Add_FiresItemAddedAndCountChanged()
    {
        var list = new ScriptableList<string>();
        string added = null;
        int countEvents = 0;
        list.OnItemAdded += item => added = item;
        list.OnItemCountChanged += () => countEvents++;

        list.Add("vessel");

        Assert.Equal("vessel", added);
        Assert.Equal(1, countEvents);
        Assert.Single(list);
        Assert.False(list.IsEmpty);
    }

    [Fact]
    public void Remove_FiresItemRemoved()
    {
        var list = new ScriptableList<int> { 1, 2, 3 };
        int removed = -1;
        list.OnItemRemoved += item => removed = item;

        Assert.True(list.Remove(2));
        Assert.Equal(2, removed);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void Remove_Missing_ReturnsFalse_NoEvent()
    {
        var list = new ScriptableList<int> { 1 };
        int events = 0;
        list.OnItemRemoved += _ => events++;

        Assert.False(list.Remove(99));
        Assert.Equal(0, events);
    }

    [Fact]
    public void Clear_FiresClearedOnce_OnlyWhenNonEmpty()
    {
        var list = new ScriptableList<int> { 1, 2 };
        int cleared = 0;
        list.OnCleared += () => cleared++;

        list.Clear();
        list.Clear(); // already empty — no event

        Assert.Equal(1, cleared);
        Assert.True(list.IsEmpty);
    }
}
