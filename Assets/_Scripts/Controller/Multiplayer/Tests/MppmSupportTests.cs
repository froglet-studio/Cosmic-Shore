using CosmicShore.Core;
using NUnit.Framework;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tests for MPPM (Multiplayer Play Mode) support helpers.
    /// Validates the pure-logic methods that derive auth profile names
    /// and local host ports from MPPM tags.
    /// </summary>
    [TestFixture]
    public class MppmSupportTests
    {
        #region Profile Name — AuthenticationServiceFacade.GetMppmProfileName

        [Test]
        public void GetMppmProfileName_SingleTag_ReturnsPrefixedTag()
        {
            var result = AuthenticationServiceFacade.GetMppmProfileName(new[] { "Player2" });

            Assert.AreEqual("mppm-Player2", result);
        }

        [Test]
        public void GetMppmProfileName_MultipleTags_JoinsWithDash()
        {
            var result = AuthenticationServiceFacade.GetMppmProfileName(new[] { "Player2", "Red" });

            Assert.AreEqual("mppm-Player2-Red", result);
        }

        [Test]
        public void GetMppmProfileName_NullTags_ReturnsFallback()
        {
            var result = AuthenticationServiceFacade.GetMppmProfileName(null);

            Assert.AreEqual("mppm-clone", result);
        }

        [Test]
        public void GetMppmProfileName_EmptyTags_ReturnsFallback()
        {
            var result = AuthenticationServiceFacade.GetMppmProfileName(new string[0]);

            Assert.AreEqual("mppm-clone", result);
        }

        [Test]
        public void GetMppmProfileName_DifferentTags_ProduceDifferentProfiles()
        {
            var profileA = AuthenticationServiceFacade.GetMppmProfileName(new[] { "Player2" });
            var profileB = AuthenticationServiceFacade.GetMppmProfileName(new[] { "Player3" });

            Assert.AreNotEqual(profileA, profileB,
                "Different MPPM tags should produce different auth profiles.");
        }

        #endregion

        #region Port Offset — MultiplayerSetup.GetMppmPort

        [Test]
        public void GetMppmPort_ReturnsPortInValidRange()
        {
            var port = MultiplayerSetup.GetMppmPort(new[] { "Player2" });

            Assert.GreaterOrEqual(port, 7778, "MPPM port must be >= 7778 to avoid default 7777.");
            Assert.LessOrEqual(port, 7877, "MPPM port must be <= 7877 (7778 + 99).");
        }

        [Test]
        public void GetMppmPort_NullTags_ReturnsPortInValidRange()
        {
            var port = MultiplayerSetup.GetMppmPort(null);

            Assert.GreaterOrEqual(port, 7778);
            Assert.LessOrEqual(port, 7877);
        }

        [Test]
        public void GetMppmPort_EmptyTags_ReturnsPortInValidRange()
        {
            var port = MultiplayerSetup.GetMppmPort(new string[0]);

            Assert.GreaterOrEqual(port, 7778);
            Assert.LessOrEqual(port, 7877);
        }

        [Test]
        public void GetMppmPort_SameTags_IsDeterministic()
        {
            var portA = MultiplayerSetup.GetMppmPort(new[] { "Player2" });
            var portB = MultiplayerSetup.GetMppmPort(new[] { "Player2" });

            Assert.AreEqual(portA, portB,
                "Same tags should always produce the same port.");
        }

        [Test]
        public void GetMppmPort_DifferentTags_ProduceDifferentPorts()
        {
            var portA = MultiplayerSetup.GetMppmPort(new[] { "Player2" });
            var portB = MultiplayerSetup.GetMppmPort(new[] { "Player3" });

            // Hash collisions are theoretically possible but extremely unlikely
            // for these specific inputs.
            Assert.AreNotEqual(portA, portB,
                "Different MPPM tags should produce different ports.");
        }

        [Test]
        public void GetMppmPort_NeverReturnsDefaultPort()
        {
            // Test a variety of tag inputs to ensure none collide with 7777.
            string[][] tagSets = {
                new[] { "Player2" },
                new[] { "Player3" },
                new[] { "Player4" },
                new[] { "Clone" },
                null,
                new string[0],
                new[] { "A", "B" },
            };

            foreach (var tags in tagSets)
            {
                var port = MultiplayerSetup.GetMppmPort(tags);
                Assert.AreNotEqual((ushort)7777, port,
                    $"MPPM port must never be 7777 (default). Tags: {(tags != null ? string.Join(",", tags) : "null")}");
            }
        }

        #endregion
    }
}
