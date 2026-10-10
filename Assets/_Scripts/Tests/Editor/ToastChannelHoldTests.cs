using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A failed party join bounces the client to its own menu and raises its notice between
    /// scenes, when no ToastService is subscribed. The channel holds that one line until the
    /// next service subscribes. This pins the mailbox contract: deliver now when heard, hold
    /// when not, hand over once, and never hold a "now or never" line.
    /// </summary>
    public class ToastChannelHoldTests
    {
        ToastChannel _channel;

        [SetUp]
        public void SetUp() => _channel = ScriptableObject.CreateInstance<ToastChannel>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_channel);

        [Test]
        public void Unheard_IsHeld_ThenHandedOverOnce()
        {
            Assert.IsFalse(_channel.HasListener);
            _channel.ShowPrefixOrHold("Couldn't join - returned to your menu.");

            Assert.IsTrue(_channel.TryTakeHeld(out var held), "the line raised with nobody listening is waiting");
            Assert.AreEqual("Couldn't join - returned to your menu.", held.Prefix);
            Assert.IsFalse(_channel.TryTakeHeld(out _), "a held line is handed over exactly once");
        }

        [Test]
        public void Heard_IsDeliveredNow_AndNothingIsHeld()
        {
            string delivered = null;
            _channel.OnChatToast += (req, done) => delivered = req.Prefix;
            Assert.IsTrue(_channel.HasListener);

            _channel.ShowPrefixOrHold("now");

            Assert.AreEqual("now", delivered);
            Assert.IsFalse(_channel.TryTakeHeld(out _), "a line a listener took is not also held");
        }

        [Test]
        public void NowOrNever_IsNotHeld()
        {
            _channel.ShowPrefixOrHold("drop me", holdSeconds: 0f);
            Assert.IsFalse(_channel.TryTakeHeld(out _));
        }

        [Test]
        public void ALaterHeldLine_ReplacesTheEarlierOne()
        {
            _channel.ShowPrefixOrHold("first");
            _channel.ShowPrefixOrHold("second");
            Assert.IsTrue(_channel.TryTakeHeld(out var held));
            Assert.AreEqual("second", held.Prefix, "the hold is a mailbox for the latest notice, not a queue");
        }

        [Test]
        public void PlainShowPrefix_StillDropsWhenUnheard()
        {
            // The existing API keeps its contract: a gameplay toast nobody hears is gone.
            _channel.ShowPrefix("unheard gameplay line");
            Assert.IsFalse(_channel.TryTakeHeld(out _));
        }
    }
}
