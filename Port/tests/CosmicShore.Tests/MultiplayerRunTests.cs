using System;
using System.IO;
using System.Linq;
using Prisma;

namespace CosmicShore.Tests
{
    /// <summary><see cref="MultiplayerRun"/>'s rules: a party is four, each player its own profile, and the stats line it shows.</summary>
    public class MultiplayerRunTests
    {
        static string FakePlayer()
        {
            var f = Path.Combine(Path.GetTempPath(), $"mprun-{Guid.NewGuid():N}.dll");
            File.WriteAllText(f, "");
            return f;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(6)]
        public void MoreThanAParty_OrNobody_IsRefused(int players)
        {
            var e = Assert.Throws<ArgumentException>(() => MultiplayerRun.Start(new MultiplayerRun.Options { Players = players, PlayerPath = FakePlayer() }));
            Assert.Contains("1-4", e.Message);
        }

        [Fact]
        public void TwoPlayers_OnOneProfile_AreRefused()
        {
            var o = new MultiplayerRun.Options { Players = 2, PlayerPath = FakePlayer(), WorkDir = Path.Combine(Path.GetTempPath(), $"mprun-{Guid.NewGuid():N}") };
            o.PerPlayer.Add(new MultiplayerRun.PlayerOptions { Profile = "Same" });
            o.PerPlayer.Add(new MultiplayerRun.PlayerOptions { Profile = "same" });
            using var _ = new Cleanup(o.WorkDir);
            Assert.Throws<ArgumentException>(() => MultiplayerRun.Start(o));
            Assert.False(Directory.Exists(Path.Combine(o.WorkDir, "sessions")), "a refused run must start nothing");
        }

        [Fact]
        public void AMissingPlayer_IsReportedAsNotBuilt()
            => Assert.Throws<FileNotFoundException>(() => MultiplayerRun.Start(new MultiplayerRun.Options { Players = 2, PlayerPath = "/nope/CosmicShore.dll" }));

        [Fact]
        public void DefaultProfiles_ArePilotAToD()
            => Assert.Equal(new[] { "PilotA", "PilotB", "PilotC", "PilotD" }, new[] { 0, 1, 2, 3 }.Select(MultiplayerRun.DefaultProfile));

        [Fact]
        public void NetLine_SummarisesTheStatsJson()
        {
            const string output = "{\"role\":\"server\",\"bytesInPerSecond\":5836,\"bytesOutPerSecond\":4506,\"simulator\":\"off\"," +
                                  "\"peers\":[{\"peer\":\"client 1\",\"rttMs\":27.7},{\"peer\":\"client 2\",\"rttMs\":null}]}";
            Assert.Equal("server · in 5.7 KB/s out 4.4 KB/s · rtt client 1 27.7 ms, client 2 - · sim off", MultiplayerRun.NetLine(output));
            Assert.Equal("", MultiplayerRun.NetLine("[input] unknown action 'net'"));
            Assert.EndsWith("rtt - (no peers) · sim off", MultiplayerRun.NetLine("{\"role\":\"server\",\"peers\":[],\"simulator\":\"off\"}"));
        }

        sealed class Cleanup : IDisposable
        {
            readonly string _dir;
            public Cleanup(string dir) => _dir = dir;
            public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
        }
    }
}
