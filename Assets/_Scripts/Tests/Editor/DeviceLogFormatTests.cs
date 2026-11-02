using CosmicShore.Utility.PerformanceBenchmark;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    public class DeviceLogFormatTests
    {
        [TestCase("ghp_0123456789abcdefghijABCDEFGHIJ012345", true)]
        [TestCase("github_pat_11ABCDEFG0abcdefghij_klmnop", true)]
        [TestCase("  ghp_0123456789abcdefghijABCDEFGHIJ012345\n", true)]
        [TestCase("ghp_short", false)]
        [TestCase("hello this is not a token at all", false)]
        [TestCase("ghp_0123456789abcdefghij with a space", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void LooksLikeToken_TellsATokenFromOtherClipboardText(string text, bool expected) =>
            Assert.AreEqual(expected, DeviceLogFormat.LooksLikeToken(text));

        [TestCase("devlog token ghp_abc123_DEF", "devlog token [token removed]")]
        [TestCase("x github_pat_11A_b y", "x [token removed] y")]
        [TestCase("capture S1 15", "capture S1 15")]
        [TestCase(null, null)]
        public void MaskTokens_BlanksATokenInALogLine(string text, string expected) =>
            Assert.AreEqual(expected, DeviceLogFormat.MaskTokens(text));

        [TestCase("YashPhone", "yashphone")]
        [TestCase("samsung SM-M366B", "samsung-sm-m366b")]
        [TestCase("  a/b..c  ", "a-b-c")]
        [TestCase("///", "fallback")]
        [TestCase(null, "fallback")]
        public void SafeName_MakesANameSafeInABranch(string text, string expected) =>
            Assert.AreEqual(expected, DeviceLogFormat.SafeName(text, "fallback"));

        [Test]
        public void Percentile_ReadsTheSortedRangeAndStaysInBounds()
        {
            var sorted = new[] { 1f, 2f, 3f, 4f, 99f };
            Assert.AreEqual(3f, DeviceLogFormat.Percentile(sorted, 4, .5f));
            Assert.AreEqual(4f, DeviceLogFormat.Percentile(sorted, 4, 1f), "the count bounds it, not the array");
            Assert.AreEqual(0f, DeviceLogFormat.Percentile(sorted, 0, .5f));
        }

        [Test]
        public void Headline_SaysTheWorstSceneAndMarksOnlyWhenThereAreAny()
        {
            Assert.AreEqual("12 min, 3 scenes, worst 41 fps in Menu_Main, 1 error kind, 2 marked",
                DeviceLogFormat.Headline(12.2, 3, 41.4f, "Menu_Main", 1, 2));
            Assert.AreEqual("0 min, 1 scene, 0 error kinds", DeviceLogFormat.Headline(0, 1, 0f, "", 0, 0));
        }
    }
}
