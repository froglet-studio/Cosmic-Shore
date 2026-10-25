#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// B35 (Docs/PartySystem/BUGS.md): every join failed with "NetworkConfig mismatch" and nothing said
    /// which prefab the two editors disagreed on. Each peer's test log now records its config hash and
    /// the prefab keys inside it; `net_logs.py show` diffs two players' lines. This pins the line's shape.
    /// </summary>
    [TestFixture]
    public class NetworkConfigMarkTests
    {
        [Test]
        public void Describe_SortsKeysAsNetcodeHashesThem()
        {
            string line = MultiplayerSetup.DescribeNetworkConfig(0xabcUL,
                new[] { (3190346835u, "Player"), (3347286452u, "Goby"), (234294682u, "Rhino") });
            Assert.AreEqual("hash=0000000000000abc prefabs=3 234294682:Rhino 3190346835:Player 3347286452:Goby", line);
        }

        [Test]
        public void Describe_ADifferentKeyChangesTheLine()
        {
            string fixedHash = MultiplayerSetup.DescribeNetworkConfig(1UL, new[] { (3190346835u, "Player") });
            string staleHash = MultiplayerSetup.DescribeNetworkConfig(1UL, new[] { (3939330856u, "Player") });
            Assert.AreNotEqual(fixedHash, staleHash, "Player.prefab's committed (stale) and regenerated hashes read differently");
        }

        [Test]
        public void Describe_AMissingPrefabStillShowsItsKey()
        {
            Assert.AreEqual("hash=0000000000000000 prefabs=1 7:?",
                MultiplayerSetup.DescribeNetworkConfig(0UL, new[] { (7u, (string)null) }));
        }
    }
}
#endif
