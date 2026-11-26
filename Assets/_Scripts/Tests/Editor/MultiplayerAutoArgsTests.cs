#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// MultiplayerAutoArgs - the command line of an automatic multiplayer run (-csMpAutoHost / -csMpAutoJoin) and the
    /// rendezvous file a host leaves for its joiners.
    ///
    /// WHY THIS MATTERS: the standalone players these arguments drive are how the team tests four players on one PC
    /// (two MPPM Editors already used ~24 GB, 2026-11-02). A parse that quietly accepts a bad value runs a test that
    /// cannot pass - two players sharing one UGS profile, a party of six, a joiner chasing last run's session - and
    /// the failure then reads as a multiplayer bug.
    /// </summary>
    [TestFixture]
    public class MultiplayerAutoArgsTests
    {
        static MultiplayerAutoArgs Parse(params string[] args)
        {
            var all = new string[args.Length + 1];
            all[0] = "CosmicShore.exe";
            args.CopyTo(all, 1);
            return MultiplayerAutoArgs.Parse(all);
        }

        [Test]
        public void NoArguments_IsInactive()
        {
            var a = Parse("-batchmode");
            Assert.IsFalse(a.IsActive);
            Assert.IsEmpty(a.Errors);
        }

        [Test]
        public void Host_ParsesEveryValue()
        {
            var a = Parse("-csMpProfile", "p1", "-csMpAutoHost", "C:/mp/rv.json", "-csMpExpect", "4",
                          "-csMpTimeout", "300", "-csMpResult", "C:/mp/host.json", "-csMpQuit");
            Assert.IsEmpty(a.Errors);
            Assert.AreEqual(MultiplayerAutoRole.Host, a.Role);
            Assert.AreEqual("p1", a.Profile);
            Assert.AreEqual("C:/mp/rv.json", a.RendezvousPath);
            Assert.AreEqual(4, a.Expect);
            Assert.AreEqual(300f, a.TimeoutSeconds);
            Assert.AreEqual("C:/mp/host.json", a.ResultPath);
            Assert.IsTrue(a.QuitWhenDone);
        }

        [Test]
        public void Host_Defaults()
        {
            var a = Parse("-csMpProfile", "p1", "-csMpAutoHost", "rv.json");
            Assert.AreEqual(MultiplayerAutoArgs.DefaultExpect, a.Expect);
            Assert.AreEqual(MultiplayerAutoArgs.DefaultTimeoutSeconds, a.TimeoutSeconds);
            Assert.IsFalse(a.QuitWhenDone);
            Assert.AreEqual("host-p1.json", a.DefaultResultFileName());
        }

        [Test]
        public void ArgumentNamesAreCaseInsensitive()
        {
            var a = Parse("-CSMPPROFILE", "p2", "-csmpautojoin", "rv.json");
            Assert.IsEmpty(a.Errors);
            Assert.AreEqual(MultiplayerAutoRole.Join, a.Role);
        }

        [Test]
        public void Join_TargetKinds()
        {
            var file = Parse("-csMpProfile", "p2", "-csMpAutoJoin", "rv.json");
            Assert.AreEqual(MultiplayerAutoTargetKind.RendezvousFile, file.TargetKind);
            Assert.AreEqual("rv.json", file.RendezvousPath);

            var id = Parse("-csMpProfile", "p2", "-csMpAutoJoin", "id:AbC123");
            Assert.AreEqual(MultiplayerAutoTargetKind.PlayerId, id.TargetKind);
            Assert.AreEqual("AbC123", id.TargetValue);

            var name = Parse("-csMpProfile", "p2", "-csMpAutoJoin", "name:Dragon Fly");
            Assert.AreEqual(MultiplayerAutoTargetKind.DisplayName, name.TargetKind);
            Assert.AreEqual("Dragon Fly", name.TargetValue);
            Assert.AreEqual("join-p2.json", name.DefaultResultFileName());
        }

        [Test]
        public void ProfileIsRequiredForAnAutomaticRun()
        {
            var a = Parse("-csMpAutoHost", "rv.json");
            Assert.IsTrue(a.IsActive);
            Assert.IsNotEmpty(a.Errors, "without a profile two players on one PC would be one UGS player");
        }

        [Test]
        public void ProfileAloneIsAllowed_ForAManualTwoInstanceSession()
        {
            var a = Parse("-csMpProfile", "manual2");
            Assert.IsFalse(a.IsActive);
            Assert.IsEmpty(a.Errors);
            Assert.AreEqual("manual2", a.Profile);
        }

        [TestCase("a b")]
        [TestCase("p/1")]
        [TestCase("ü")]
        [TestCase("0123456789012345678901234567890")]   // 31 characters
        public void InvalidProfile_IsAnError(string profile)
        {
            var a = Parse("-csMpProfile", profile, "-csMpAutoHost", "rv.json");
            Assert.IsNotEmpty(a.Errors);
            Assert.IsFalse(MultiplayerAutoArgs.IsValidProfile(profile));
        }

        [TestCase("1")]
        [TestCase("5")]
        [TestCase("6")]
        [TestCase("two")]
        public void Expect_OutsideTwoToFour_IsAnError(string expect)
        {
            var a = Parse("-csMpProfile", "p1", "-csMpAutoHost", "rv.json", "-csMpExpect", expect);
            Assert.IsNotEmpty(a.Errors, "a party is 4, never 6");
        }

        [Test]
        public void HostAndJoinTogether_IsAnError()
        {
            var a = Parse("-csMpProfile", "p1", "-csMpAutoHost", "rv.json", "-csMpAutoJoin", "rv.json");
            Assert.IsNotEmpty(a.Errors);
        }

        [Test]
        public void MissingValue_IsAnError_AndDoesNotSwallowTheNextArgument()
        {
            var a = Parse("-csMpAutoHost", "-csMpProfile", "p1");
            Assert.IsNotEmpty(a.Errors);
            Assert.AreEqual("p1", a.Profile, "the next -cs argument must not be read as the missing value");
        }

        [Test]
        public void Rendezvous_RoundTrips_AndAgesOut()
        {
            var r = new MultiplayerAutoRendezvous { playerId = "pid", displayName = "Host", sessionId = "sid", writtenUnixSeconds = 1000 };
            Assert.IsTrue(MultiplayerAutoRendezvous.TryParse(r.ToJson(), out var back));
            Assert.AreEqual("pid", back.playerId);
            Assert.AreEqual("sid", back.sessionId);
            Assert.IsTrue(back.IsFresh(1000 + MultiplayerAutoRendezvous.MaxAgeSeconds));
            Assert.IsFalse(back.IsFresh(1001 + MultiplayerAutoRendezvous.MaxAgeSeconds), "a previous run's file");
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{\"displayName\":\"Host\"}")]
        public void Rendezvous_WithoutAPlayer_IsRejected(string json) =>
            Assert.IsFalse(MultiplayerAutoRendezvous.TryParse(json, out _));

        [Test]
        public void Target_Matches()
        {
            Assert.IsTrue(MultiplayerAutoTarget.Matches(MultiplayerAutoTargetKind.PlayerId, "pid", "pid", "x"));
            Assert.IsFalse(MultiplayerAutoTarget.Matches(MultiplayerAutoTargetKind.PlayerId, "pid", "PID", "x"), "ids are exact");
            Assert.IsTrue(MultiplayerAutoTarget.Matches(MultiplayerAutoTargetKind.DisplayName, "dragon", "q", "Dragon"));
            Assert.IsFalse(MultiplayerAutoTarget.Matches(MultiplayerAutoTargetKind.DisplayName, "", "q", ""));
            Assert.IsFalse(MultiplayerAutoTarget.Matches(MultiplayerAutoTargetKind.RendezvousFile, "rv.json", "rv.json", "rv.json"),
                "a file is resolved to a player id before matching");
        }
    }
}
#endif
