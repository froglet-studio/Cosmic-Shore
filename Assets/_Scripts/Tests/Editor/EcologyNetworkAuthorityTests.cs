using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The one rule every ecology decision site is gated on: WHO simulates. It is a two-input
    /// truth table and it decides whether a peer spawns, feeds, breeds and kills its own
    /// wildlife or renders somebody else's — so it is worth pinning rather than re-reading.
    ///
    /// The property that is easy to get backwards, and the reason the rule reads
    /// "!live || isServer" rather than "isServer": under the locked EAGER-Relay design the
    /// NetworkManager is ALWAYS listening, so a naive `IsServer` test would have made every
    /// offline and tool scene a non-authority and stood the whole ecology down.
    /// </summary>
    public class EcologyNetworkAuthorityTests
    {
        [Test]
        public void OfflineSimulates()
        {
            // No session at all: offline mode, tool scenes, the edit-mode suite itself.
            Assert.IsTrue(FaunaNetworkSync.ComputeIsSimAuthority(networkSessionLive: false, isServer: false));
            Assert.IsTrue(FaunaNetworkSync.ComputeIsSimAuthority(networkSessionLive: false, isServer: true));
        }

        [Test]
        public void HostSimulates()
        {
            // Solo play and a party host are the same case: host == server, so the ecology
            // runs exactly as it did before replication existed.
            Assert.IsTrue(FaunaNetworkSync.ComputeIsSimAuthority(networkSessionLive: true, isServer: true));
        }

        [Test]
        public void PartyClientDoesNotSimulate()
        {
            // The ONLY case that turns a peer into a puppet renderer.
            Assert.IsFalse(FaunaNetworkSync.ComputeIsSimAuthority(networkSessionLive: true, isServer: false));
        }

        [Test]
        public void FloraSharesTheFaunaAuthorityRule()
        {
            // "Who simulates the ecology" is ONE question. A second copy of the rule is a
            // second thing to forget to update, so FloraNetworkSync delegates rather than
            // restating - this test is what fails if somebody re-implements it.
            Assert.AreEqual(FaunaNetworkSync.IsSimAuthority, FloraNetworkSync.IsSimAuthority);
        }
    }
}
