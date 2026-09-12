#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// DisconnectNotice Tests - the surface that tells a player their connection went away.
    ///
    /// WHY THIS MATTERS:
    /// Every way this feature can fail is SILENT. It is built at runtime rather than authored
    /// into a scene (a game scene has no toast surface at all, which is the gap it closes), so
    /// there is no prefab to open and check. If its config asset is missing from Resources it
    /// warns once and never shows; if AppManager stops installing it, nothing shows and nothing
    /// says so; if its copy is blank it renders an empty panel. None of that fails a build, and
    /// none of it is visible until someone loses their connection - the one moment the player is
    /// least able to report a bug.
    /// </summary>
    [TestFixture]
    public class DisconnectNoticeTests
    {
        const string ConfigPath = "Assets/Resources/DisconnectNoticeConfig.asset";
        const string AppManagerPath = "Assets/_Scripts/System/AppManager.cs";

        static DisconnectNoticeConfigSO LoadConfig()
        {
            // Resources.Load is the path the runtime actually uses; the AssetDatabase fallback
            // only reports a clearer failure when the asset exists but is outside Resources.
            var config = Resources.Load<DisconnectNoticeConfigSO>("DisconnectNoticeConfig");
            if (config == null)
                config = UnityEditor.AssetDatabase.LoadAssetAtPath<DisconnectNoticeConfigSO>(ConfigPath);
            return config;
        }

        [Test]
        public void TheConfig_IsLoadableFromResources()
        {
            Assert.IsNotNull(Resources.Load<DisconnectNoticeConfigSO>("DisconnectNoticeConfig"),
                "Resources/DisconnectNoticeConfig is missing. DisconnectNotice.Install warns once "
                + "and returns null without it, so the notice would never appear and nothing "
                + "on screen would say why.");
        }

        [Test]
        public void TheConfig_HasCopyForEveryStateItCanShow()
        {
            var config = LoadConfig();
            Assert.IsNotNull(config, "config asset not found");
            foreach (var (label, value) in new[]
                     {
                         ("TitleConnectionLost", config.TitleConnectionLost),
                         ("BodyConnectionLost", config.BodyConnectionLost),
                         ("ReconnectLabel", config.ReconnectLabel),
                         ("ReconnectingLabel", config.ReconnectingLabel),
                         ("DismissLabel", config.DismissLabel),
                         ("TitleConnectionRestored", config.TitleConnectionRestored),
                     })
                Assert.IsFalse(string.IsNullOrWhiteSpace(value),
                    $"{label} is blank - the notice would render an empty panel.");
        }

        [Test]
        public void TheNotice_DrawsAboveEverythingElse()
        {
            var config = LoadConfig();
            Assert.IsNotNull(config);
            // The in-game HUD canvas is sort order 1 and the menu's are single digits. A
            // disconnect notice the player cannot see is the whole defect this closes.
            Assert.Greater(config.SortingOrder, 1000,
                "SortingOrder must put the notice above the game and menu canvases.");
            Assert.Greater(config.ScrimColor.a, 0f, "an invisible scrim does not read as modal");
        }

        [Test]
        public void AppManager_StillInstallsTheNotice()
        {
            // A structural assertion, in the spirit of the platform-law tests: the notice has
            // exactly one install site, and losing it is invisible at runtime.
            var path = Path.Combine(Directory.GetCurrentDirectory(), AppManagerPath);
            Assert.IsTrue(File.Exists(path), $"{AppManagerPath} not found");
            var source = File.ReadAllText(path);
            Assert.IsTrue(source.Contains("DisconnectNotice.Install("),
                "AppManager no longer installs the DisconnectNotice, so no scene has one. "
                + "It is installed there because it must outlive the scene reload a disconnect "
                + "triggers - see Docs/UI_ARCHITECTURE_AUDIT.md section 4.2.1.");
        }

        [Test]
        public void TheNotice_ExposesTheInstallSeamTheAppManagerUses()
        {
            var install = typeof(DisconnectNotice).GetMethod(
                "Install", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(install, "DisconnectNotice.Install(...) is the only entry point.");
            Assert.AreEqual(3, install.GetParameters().Length,
                "Install takes the network data, the game data and the reconnect service - "
                + "resolved by the caller because a runtime-created object is not injected.");
        }
    }
}
#endif
