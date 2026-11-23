#if UNITY_EDITOR
using System.IO;
using CosmicShore.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Web build and the per-device views (Shombith 2026-11-07): every Froglet Analytics event says which build
    /// (editor, port, web, development, release) and which kind of device (iPhone, iPad, Android phone or tablet,
    /// desktop, web on a desktop or a phone) sent it.
    ///
    /// WHY THIS MATTERS:
    /// 1. THE WEB TOYBOX MUST NOT READ AS RELEASE TRAFFIC, or the website's visitors are counted as store players.
    /// 2. THE PORT MUST STAY PORT even though it is not the editor and may not be a debug build: the server trusts a
    ///    stated build_kind over its own guess, so a wrong one here would undo the "-port" rule.
    /// 3. AN IPAD MUST NOT READ AS AN IPHONE: iOS reports IPhonePlayer on both, so only the model tells them apart.
    /// 4. THE SERVER REJECTS FIELDS ITS SCHEMA DOES NOT LIST, so the four new envelope keys must be in the schema.
    /// </summary>
    [TestFixture]
    public class AnalyticsDeviceTests
    {
        [Test]
        public void TheEditorIsEditorWhateverItsTarget()
        {
            Assert.AreEqual("editor", AnalyticsDevice.BuildKindOf(true, RuntimePlatform.WebGLPlayer, "1.0", true));
            Assert.AreEqual("editor", AnalyticsDevice.BuildKindOf(false, RuntimePlatform.OSXEditor, "1.0", false));
        }

        [Test]
        public void ThePortIsPortEvenAsAReleaseBuild()
        {
            Assert.AreEqual("port", AnalyticsDevice.BuildKindOf(false, RuntimePlatform.WindowsPlayer, "0.2.0-port", false));
            Assert.AreEqual("port", AnalyticsDevice.BuildKindOf(false, RuntimePlatform.LinuxPlayer, "0.2.0-port", true));
        }

        [Test]
        public void EveryWebGlBuildIsTheWebsites()
        {
            Assert.AreEqual("web", AnalyticsDevice.BuildKindOf(false, RuntimePlatform.WebGLPlayer, "1.4.0", false));
            Assert.AreEqual("web", AnalyticsDevice.BuildKindOf(false, RuntimePlatform.WebGLPlayer, "1.4.0", true));
        }

        [Test]
        public void ADebugBuildIsDevelopmentAndTheRestIsRelease()
        {
            Assert.AreEqual("development", AnalyticsDevice.BuildKindOf(false, RuntimePlatform.Android, "1.4.0", true));
            Assert.AreEqual("release", AnalyticsDevice.BuildKindOf(false, RuntimePlatform.IPhonePlayer, "1.4.0", false));
            Assert.AreEqual("release", AnalyticsDevice.BuildKindOf(false, RuntimePlatform.WindowsPlayer, null, false));
        }

        [Test]
        public void AnIpadIsNotAnIphone()
        {
            Assert.AreEqual("ipad", AnalyticsDevice.DeviceOf(RuntimePlatform.IPhonePlayer, "iPad13,1", true, 11f));
            Assert.AreEqual("iphone", AnalyticsDevice.DeviceOf(RuntimePlatform.IPhonePlayer, "iPhone15,2", true, 6.1f));
            Assert.AreEqual("iphone", AnalyticsDevice.DeviceOf(RuntimePlatform.IPhonePlayer, "", true, 0f));
        }

        [Test]
        public void AnAndroidTabletIsSevenInchesOrMore()
        {
            Assert.AreEqual("android_tablet", AnalyticsDevice.DeviceOf(RuntimePlatform.Android, "samsung SM-X200", true, 10.5f));
            Assert.AreEqual("android_tablet", AnalyticsDevice.DeviceOf(RuntimePlatform.Android, "x", true, AnalyticsDevice.TabletDiagonalInches));
            Assert.AreEqual("android_phone", AnalyticsDevice.DeviceOf(RuntimePlatform.Android, "Pixel 8", true, 6.2f));
            Assert.AreEqual("android", AnalyticsDevice.DeviceOf(RuntimePlatform.Android, "Pixel 8", true, 0f),
                "no dpi means the size is unknown, never a guess");
        }

        [Test]
        public void TheWebSplitsDesktopFromMobileAndDesktopsByOs()
        {
            Assert.AreEqual("web_mobile", AnalyticsDevice.DeviceOf(RuntimePlatform.WebGLPlayer, "", true, 0f));
            Assert.AreEqual("web_desktop", AnalyticsDevice.DeviceOf(RuntimePlatform.WebGLPlayer, "", false, 0f));
            Assert.AreEqual("windows", AnalyticsDevice.DeviceOf(RuntimePlatform.WindowsEditor, "", false, 0f));
            Assert.AreEqual("mac", AnalyticsDevice.DeviceOf(RuntimePlatform.OSXPlayer, "", false, 0f));
            Assert.AreEqual("linux", AnalyticsDevice.DeviceOf(RuntimePlatform.LinuxPlayer, "", false, 0f));
        }

        [Test]
        public void TextIsTrimmedAndCut()
        {
            Assert.AreEqual(string.Empty, AnalyticsDevice.Cut(null));
            Assert.AreEqual("iOS 17.1", AnalyticsDevice.Cut("  iOS 17.1 "));
            Assert.AreEqual(AnalyticsDevice.MaxTextLength, AnalyticsDevice.Cut(new string('x', 200)).Length);
        }

        [Test]
        public void TheSchemaListsTheFourNewEnvelopeFields()
        {
            var path = Path.Combine(Application.dataPath, "..", "Docs", "Analytics", "EVENT_SCHEMA.json");
            var envelope = (JObject)JObject.Parse(File.ReadAllText(path))["common_envelope"];
            foreach (var key in new[] { "build_kind", "device", "os", "device_model" })
                Assert.IsNotNull(envelope[key], $"common_envelope is missing {key}; the server would reject it");
        }
    }
}
#endif
