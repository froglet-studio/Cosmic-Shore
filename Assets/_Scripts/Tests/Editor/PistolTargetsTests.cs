#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>The Pistol never acts on the Goby's own trail (it is the ground it rides): identity of the Trail, never the name.</summary>
    public class PistolTargetsTests
    {
        [Test]
        public void IsOwnTrail_ByTrailIdentity()
        {
            var hull = new Trail(); var second = new Trail(); var other = new Trail();
            Assert.IsTrue(PistolTargets.IsOwnTrail(hull, hull, second));
            Assert.IsTrue(PistolTargets.IsOwnTrail(second, hull, second));
            Assert.IsFalse(PistolTargets.IsOwnTrail(other, hull, second), "another trail of the same domain is a target");
            Assert.IsFalse(PistolTargets.IsOwnTrail(null, hull, second));
            Assert.IsFalse(PistolTargets.IsOwnTrail(other, null, null));
        }
    }
}
#endif
