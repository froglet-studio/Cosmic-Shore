#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using CosmicShore.Core;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Port's faction chat rules (Docs/MENU_REDESIGN_B.md section 90): what a message may hold, how often a player
    /// may send, who may send, how the sender's avatar and name ride with a message, and the feed's log, which must
    /// hold each message once in time order even though Vivox's history and its live feed overlap when the Port opens.
    /// </summary>
    [TestFixture]
    public class FactionChatTests
    {
        static readonly DateTime T0 = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

        static FactionMessage Msg(string id, int seconds, string sender = "p1") =>
            new(id, sender, "Pilot", 3, "hi " + id, T0.AddSeconds(seconds), false);

        [Test]
        public void Clean_RemovesControlCharactersAndLineBreaks_AndCollapsesSpaces()
        {
            Assert.AreEqual("a b c d", FactionChatRules.Clean("  a\u0000b\n\nc\u2028d \t"));
            Assert.AreEqual(string.Empty, FactionChatRules.Clean(" \n\t "));
            Assert.AreEqual(string.Empty, FactionChatRules.Clean(null));
        }

        [Test]
        public void Length_CountsAnEmojiAsOneCharacter()
        {
            Assert.AreEqual(3, FactionChatRules.Length("a\U0001F680b"));
        }

        [Test]
        public void Check_RefusesEmptyTooLongAndTooSoon()
        {
            var never = DateTime.MinValue;
            Assert.AreEqual(FactionSendRefusal.Empty, FactionChatRules.Check("", 140, never, T0, 3f, true));
            Assert.AreEqual(FactionSendRefusal.None, FactionChatRules.Check(new string('x', 140), 140, never, T0, 3f, true));
            Assert.AreEqual(FactionSendRefusal.TooLong, FactionChatRules.Check(new string('x', 141), 140, never, T0, 3f, true));
            Assert.AreEqual(FactionSendRefusal.TooSoon, FactionChatRules.Check("hi", 140, T0, T0.AddSeconds(1), 3f, true));
            Assert.AreEqual(FactionSendRefusal.None, FactionChatRules.Check("hi", 140, T0, T0.AddSeconds(3), 3f, true));
            Assert.AreEqual(FactionSendRefusal.ReadOnly, FactionChatRules.Check("hi", 140, never, T0, 3f, false));
        }

        [Test]
        public void WaitSeconds_RoundsUp()
        {
            Assert.AreEqual(3, FactionChatRules.WaitSeconds(T0, T0.AddMilliseconds(100), 3f));
            Assert.AreEqual(1, FactionChatRules.WaitSeconds(T0, T0.AddSeconds(2.5), 3f));
            Assert.AreEqual(0, FactionChatRules.WaitSeconds(T0, T0.AddSeconds(3), 3f));
        }

        [Test]
        public void CanPost_ANotSurely13YearOldReadsOnly_AnUnknownYearPosts()
        {
            Assert.IsFalse(FactionChatRules.CanPost(2016, 2026, 13));
            Assert.IsFalse(FactionChatRules.CanPost(2013, 2026, 13));   // may still be 12 this year
            Assert.IsTrue(FactionChatRules.CanPost(2012, 2026, 13));
            Assert.IsTrue(FactionChatRules.CanPost(0, 2026, 13));
        }

        [Test]
        public void Metadata_RoundTripsAvatarAndName_AndToleratesJunk()
        {
            FactionChatRules.DecodeMetadata(FactionChatRules.EncodeMetadata(7, "Kestrel|Prime"), out int avatar, out string name);
            Assert.AreEqual(7, avatar);
            Assert.AreEqual("Kestrel|Prime", name);
            FactionChatRules.DecodeMetadata("nonsense", out avatar, out name);
            Assert.AreEqual(-1, avatar);
            Assert.IsNull(name);
            FactionChatRules.DecodeMetadata(null, out avatar, out name);
            Assert.AreEqual(-1, avatar);
        }

        [Test]
        public void ChannelFor_DefaultsToTheOneFaction()
        {
            Assert.AreEqual("faction_all", FactionChatRules.ChannelFor(null));
            Assert.AreEqual("faction_all", FactionChatRules.ChannelFor("all"));
        }

        [Test]
        public void Log_HoldsEachMessageOnce_InTimeOrder()
        {
            var log = new FactionChatLog(10);
            Assert.IsTrue(log.Add(Msg("b", 20)));
            Assert.IsTrue(log.Add(Msg("a", 10)));
            Assert.IsTrue(log.Add(Msg("c", 30)));
            Assert.IsFalse(log.Add(Msg("b", 20)), "the history and the live feed overlap; a message is held once");
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, log.Messages.Select(m => m.Id).ToArray());
        }

        [Test]
        public void Log_KeepsTheNewest_AndRefusesOneOlderThanAFullLog()
        {
            var log = new FactionChatLog(3);
            for (int i = 0; i < 5; i++) log.Add(Msg("m" + i, i * 10));
            CollectionAssert.AreEqual(new[] { "m2", "m3", "m4" }, log.Messages.Select(m => m.Id).ToArray());
            Assert.IsFalse(log.Add(Msg("old", 1)));
            Assert.IsFalse(log.Add(Msg("m0", 0)), "an evicted message coming back late stays out");
            Assert.AreEqual(3, log.Messages.Count);
        }

        [Test]
        public void Mutes_ParseAndJoinRoundTrip()
        {
            var set = FactionMutes.Parse(" a, b,,c ");
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, set);
            CollectionAssert.AreEquivalent(set, FactionMutes.Parse(FactionMutes.Join(set)));
            Assert.AreEqual(0, FactionMutes.Parse(null).Count);
        }
    }
}
#endif
