#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Truth table for <see cref="FaunaNetworkSync.ComputeIsSimAuthority"/> — the one
    /// rule every ecology decision-site (seeder loops, reproduction, starvation,
    /// predation) checks under networked fauna sync (Docs/ECOSYSTEM_NETWORK_SYNC.md §2).
    /// A peer simulates unless a network session is live AND it is not the server:
    /// offline/tool scenes and solo hosts behave exactly as before; only party clients
    /// become puppet-renderers.
    /// </summary>
    [TestFixture]
    public class FaunaNetworkAuthorityTests
    {
        [Test]
        public void Offline_NoNetworkSession_IsAuthority()
        {
            // Tool/test scenes with no (listening) NetworkManager: full local sim.
            Assert.IsTrue(FaunaNetworkSync.ComputeIsSimAuthority(networkSessionLive: false, isServer: false));
        }

        [Test]
        public void Offline_ServerFlagWithoutSession_IsAuthority()
        {
            // Degenerate flag combination (server bit without a live session) must
            // never disable the local sim.
            Assert.IsTrue(FaunaNetworkSync.ComputeIsSimAuthority(networkSessionLive: false, isServer: true));
        }

        [Test]
        public void ListeningServer_IsAuthority()
        {
            // Solo menu host and party host alike: the server runs the ONE simulation.
            Assert.IsTrue(FaunaNetworkSync.ComputeIsSimAuthority(networkSessionLive: true, isServer: true));
        }

        [Test]
        public void ListeningClient_IsNotAuthority()
        {
            // Party client: fauna are replicated puppets; the client never originates
            // spawns, births, or deaths.
            Assert.IsFalse(FaunaNetworkSync.ComputeIsSimAuthority(networkSessionLive: true, isServer: false));
        }
    }
}
#endif
