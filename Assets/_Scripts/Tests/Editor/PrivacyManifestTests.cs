#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CosmicShore.Editor;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The iOS privacy manifest (launch blocker #14): the committed file declares every required-reason API the shipped
    /// C# calls, its reason codes have Apple's shape, nothing is declared as tracking, and the merge into a manifest
    /// Unity already wrote only ever adds.
    /// </summary>
    public class PrivacyManifestTests
    {
        static readonly string[] KnownCategories =
        {
            "NSPrivacyAccessedAPICategoryFileTimestamp",
            "NSPrivacyAccessedAPICategorySystemBootTime",
            "NSPrivacyAccessedAPICategoryDiskSpace",
            "NSPrivacyAccessedAPICategoryActiveKeyboards",
            "NSPrivacyAccessedAPICategoryUserDefaults",
        };

        /// <summary>What a call in shipped C# reaches on iOS (IL2CPP), and the category Apple files it under.</summary>
        static readonly (string category, Regex use)[] Uses =
        {
            ("NSPrivacyAccessedAPICategoryUserDefaults", new Regex(@"\bPlayerPrefs\.")),
            ("NSPrivacyAccessedAPICategoryFileTimestamp",
                new Regex(@"\b(GetLastWriteTime|GetCreationTime|GetLastAccessTime)(Utc)?\b|\.(LastWriteTime|CreationTime|LastAccessTime)(Utc)?\b")),
            ("NSPrivacyAccessedAPICategorySystemBootTime", new Regex(@"\bStopwatch\b|\bEnvironment\.TickCount|\brealtimeSinceStartup")),
            ("NSPrivacyAccessedAPICategoryDiskSpace", new Regex(@"\bDriveInfo\b|\b(Available|Total)FreeSpace\b")),
        };

        static PrivacyManifest Committed() =>
            PrivacyManifest.Parse(File.ReadAllText(IosPrivacyManifestPostprocess.SourcePath));

        [Test]
        public void TheManifest_DeclaresEveryRequiredReasonApiTheShippedCodeCalls()
        {
            var manifest = Committed();
            var shipped = Directory.GetFiles("Assets/_Scripts", "*.cs", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .Where(p => !p.Contains("/Editor/") && !p.Contains("/Tests/"))
                .ToList();
            Assert.IsNotEmpty(shipped, "run from the project root");

            foreach (var (category, use) in Uses)
            {
                var callers = shipped.Where(p => use.IsMatch(File.ReadAllText(p))).ToList();
                if (callers.Count == 0) continue;
                Assert.IsNotEmpty(manifest.ReasonsFor(category),
                    $"{callers[0]} (and {callers.Count - 1} more) calls a {category} API; declare it with its reason in {IosPrivacyManifestPostprocess.SourcePath}");
            }
        }

        [Test]
        public void TheManifest_UsesKnownCategoriesAndApplesReasonShape()
        {
            var manifest = Committed();
            Assert.IsNotEmpty(manifest.AccessedApis);
            foreach (var api in manifest.AccessedApis)
            {
                CollectionAssert.Contains(KnownCategories, api.Key);
                Assert.IsNotEmpty(api.Value, $"{api.Key} has no reason");
                foreach (var reason in api.Value)
                    StringAssert.IsMatch(@"^[0-9A-F]{4}\.\d$", reason, $"{api.Key}: {reason}");
            }
        }

        [Test]
        public void TheManifest_DeclaresNoTracking()
        {
            var manifest = Committed();
            Assert.IsFalse(manifest.Tracking);
            Assert.IsEmpty(manifest.TrackingDomains);
            Assert.IsNotEmpty(manifest.Collected);
            foreach (var c in manifest.Collected)
            {
                Assert.IsFalse(c.Tracking, c.Type);
                Assert.IsNotEmpty(c.Purposes, c.Type);
            }
        }

        const string UnityLike = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
  <key>SomethingElse</key>
  <string>kept</string>
  <key>NSPrivacyTracking</key>
  <true/>
  <key>NSPrivacyCollectedDataTypes</key>
  <array>
    <dict>
      <key>NSPrivacyCollectedDataType</key><string>NSPrivacyCollectedDataTypeCrashData</string>
      <key>NSPrivacyCollectedDataTypeLinked</key><true/>
      <key>NSPrivacyCollectedDataTypeTracking</key><false/>
      <key>NSPrivacyCollectedDataTypePurposes</key><array><string>NSPrivacyCollectedDataTypePurposeAnalytics</string></array>
    </dict>
  </array>
  <key>NSPrivacyAccessedAPITypes</key>
  <array>
    <dict>
      <key>NSPrivacyAccessedAPIType</key><string>NSPrivacyAccessedAPICategoryUserDefaults</string>
      <key>NSPrivacyAccessedAPITypeReasons</key><array><string>1C8F.1</string></array>
    </dict>
  </array>
</dict>
</plist>";

        [Test]
        public void MergingIntoAnExistingManifest_OnlyAdds()
        {
            string ours = File.ReadAllText(IosPrivacyManifestPostprocess.SourcePath);
            string mergedXml = PrivacyManifest.Merge(UnityLike, ours);
            var merged = PrivacyManifest.Parse(mergedXml);

            CollectionAssert.AreEquivalent(new[] { "1C8F.1", "CA92.1" }, merged.ReasonsFor("NSPrivacyAccessedAPICategoryUserDefaults"));
            CollectionAssert.AreEquivalent(new[] { "C617.1" }, merged.ReasonsFor("NSPrivacyAccessedAPICategoryFileTimestamp"));
            Assert.IsTrue(merged.Tracking, "a base that tracks keeps saying so");
            var crash = merged.CollectedOf("NSPrivacyCollectedDataTypeCrashData");
            Assert.IsTrue(crash.Linked, "linked if either says so");
            CollectionAssert.AreEquivalent(new[] { "NSPrivacyCollectedDataTypePurposeAnalytics", "NSPrivacyCollectedDataTypePurposeAppFunctionality" }, crash.Purposes);
            StringAssert.Contains("<key>SomethingElse</key>", mergedXml);
            StringAssert.Contains("<string>kept</string>", mergedXml);
            Assert.AreEqual(1, Regex.Matches(mergedXml, "<key>NSPrivacyAccessedAPITypes</key>").Count, "the key is replaced, not repeated");
        }

        [Test]
        public void MergingTwice_ChangesNothing()
        {
            string ours = File.ReadAllText(IosPrivacyManifestPostprocess.SourcePath);
            string once = PrivacyManifest.Merge(null, ours);
            Assert.AreEqual(once, PrivacyManifest.Merge(once, ours));
            Assert.AreEqual(PrivacyManifest.Merge(UnityLike, ours), PrivacyManifest.Merge(PrivacyManifest.Merge(UnityLike, ours), ours));
        }
    }
}
#endif
