using CosmicShore.Core;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The scene's own hold on the game's loading screen (the voyage scene keeps it up until its opening stands): a claim
    /// holds, a release lets go, and a claim whose owner was destroyed never strands the player behind the splash.
    /// </summary>
    public class SceneRevealHoldTests
    {
        GameObject _owner;

        [TearDown]
        public void TearDown()
        {
            if (_owner) Object.DestroyImmediate(_owner);
        }

        [Test]
        public void Claim_Holds_UntilReleased()
        {
            var owner = new object();
            SceneRevealHold.Claim(owner, 60f, "test");
            Assert.IsTrue(SceneRevealHold.IsHeld);
            Assert.IsTrue(SceneRevealHold.Holds(owner));

            SceneRevealHold.Release(owner);
            Assert.IsFalse(SceneRevealHold.Holds(owner));
        }

        [Test]
        public void DestroyedOwner_LetsGo()
        {
            _owner = new GameObject("SceneRevealHoldOwner");
            var component = _owner.AddComponent<BoxCollider>();
            SceneRevealHold.Claim(component, 60f, "test");
            Assert.IsTrue(SceneRevealHold.Holds(component));

            Object.DestroyImmediate(_owner);
            Assert.IsFalse(SceneRevealHold.Holds(component));
        }

        [Test]
        public void NullOwner_IsIgnored()
        {
            SceneRevealHold.Claim(null, 60f, "test");
            SceneRevealHold.Release(null);
            Assert.IsFalse(SceneRevealHold.Holds(null));
        }
    }
}
