using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <b>"Which device is the player using?" must have exactly ONE answer.</b>
    ///
    /// <para>It had two. <c>InputDeviceIconSetSwitcher</c> detected by last meaningful ACTUATION,
    /// so a connected-but-idle pad never stole the ability chips from the keyboard — while
    /// <c>InputController.SelectStrategy</c> keyed on device PRESENCE (<c>Gamepad.current !=
    /// null</c>) and handed the pad every frame regardless. A player with a controller plugged in
    /// watched the chips correctly follow their keyboard and mouse while the ship ignored both,
    /// and unplugging the pad was the only fix.</para>
    ///
    /// <para>Neither component was wrong on its own, which is why nothing caught it: the defect
    /// was that the question had two implementations. These are source-text laws in the shape
    /// <c>SpeedTunnelLawTests</c> uses, because that is the level the invariant lives at — you
    /// cannot detect a second detector by calling the first one.</para>
    /// </summary>
    [TestFixture]
    public class InputDeviceUnificationTests
    {
        const string ActuationPath = "Assets/_Scripts/Controller/IO/InputDeviceActuation.cs";
        const string ControllerPath = "Assets/_Scripts/Controller/IO/InputController.cs";
        const string SwitcherPath = "Assets/_Scripts/UI/Elements/InputDeviceIconSetSwitcher.cs";
        const string OverviewPath = "Assets/_Scripts/Controller/IO/OverviewGesture.cs";
        const string GameHudPath = "Assets/_Scripts/UI/MiniGameHUD.cs";
        const string MenuHudPath = "Assets/_Scripts/UI/MenuMiniGameHUD.cs";

        static string Read(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing.");
            return File.ReadAllText(path);
        }

        /// <summary>Source with // and /* */ comments stripped, so prose about a rule is never
        /// mistaken for a violation of it.</summary>
        static string Code(string path)
        {
            string text = Read(path);
            text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            text = Regex.Replace(text, @"//.*?$", string.Empty, RegexOptions.Multiline);
            return text;
        }

        [Test]
        public void OnlyInputDeviceActuationPollsRawPadControls()
        {
            // buttonSouth / leftStick / leftTrigger polling is how you build a SECOND detector.
            // Exactly one file is allowed to contain it.
            var padControls = new[] { "buttonSouth", "buttonNorth", "buttonWest",
                                      "leftShoulder", "leftTrigger", "rightTrigger",
                                      "leftStick.ReadValue", "rightStick.ReadValue" };

            foreach (var path in new[] { ControllerPath, SwitcherPath })
            {
                string code = Code(path);
                foreach (var control in padControls)
                    Assert.IsFalse(code.Contains(control),
                        $"{Path.GetFileName(path)} polls '{control}' directly. Device detection " +
                        "belongs to InputDeviceActuation alone - a second implementation is how " +
                        "the chips and the ship came to disagree about who was flying.");
            }

            string actuation = Code(ActuationPath);
            foreach (var control in padControls)
                Assert.IsTrue(actuation.Contains(control),
                    $"InputDeviceActuation no longer polls '{control}'.");
        }

        [Test]
        public void BothConsumersRouteThroughTheSharedDetector()
        {
            foreach (var path in new[] { ControllerPath, SwitcherPath })
                Assert.IsTrue(Code(path).Contains("InputDeviceActuation."),
                    $"{Path.GetFileName(path)} does not use the shared detector.");
        }

        [Test]
        public void StrategySelectionDoesNotKeyOnPadPRESENCE()
        {
            // The exact regression: `if (Gamepad.current != null) return gamepadStrategy;`.
            // Presence may still be checked (a pad can be unplugged mid-frame), but never as the
            // sole gate that hands the pad the strategy.
            string code = Code(ControllerPath);
            var match = Regex.Match(code,
                @"if\s*\(\s*Gamepad\.current\s*!=\s*null\s*\)\s*\r?\n?\s*return\s+gamepadStrategy");

            Assert.IsFalse(match.Success,
                "SelectStrategy is keying on pad PRESENCE again. A controller left plugged in " +
                "then locks out the keyboard and mouse for the whole session.");
            Assert.IsTrue(code.Contains("activeDeviceFamily == InputDeviceFamily.Gamepad"),
                "SelectStrategy must gate the pad on the family the player is USING.");
        }

        [Test]
        public void EscapeIsNotAFullscreenToggle()
        {
            // Escape is the OVERVIEW gesture. It used to toggle fullscreen here, which both
            // stole the key and gave the player no way to reach the overview from the keyboard.
            string code = Code(ControllerPath);
            Assert.IsFalse(Regex.IsMatch(code, @"escapeKey[\s\S]{0,120}Screen\.fullScreen"),
                "Escape is bound to fullscreen again; it belongs to OverviewGesture.");
            Assert.IsTrue(code.Contains("f11Key"),
                "Fullscreen still needs a key of its own - a windowed build must not be a trap.");
        }

        [Test]
        public void BothHudsAskTheSameOverviewGesture()
        {
            // One gesture, so Escape means the same thing in a game scene and in menu freestyle.
            foreach (var path in new[] { GameHudPath, MenuHudPath })
                Assert.IsTrue(Code(path).Contains("OverviewGesture.RequestedThisFrame"),
                    $"{Path.GetFileName(path)} does not route the overview gesture through the " +
                    "shared predicate, so Escape can come to mean two different things.");

            string overview = Code(OverviewPath);
            Assert.IsTrue(overview.Contains("escapeKey"), "Escape must be an overview gesture.");
            Assert.IsTrue(overview.Contains("startButton"), "Pad Start must be an overview gesture.");
        }

        [Test]
        public void TheOverviewGestureInvokesTheButtonRatherThanReimplementingIt()
        {
            // The key presses the HUD's own Volume/Pause button, so whatever that button is
            // authored to do in a given scene is exactly what the key does.
            foreach (var path in new[] { GameHudPath, MenuHudPath })
            {
                string code = Code(path);
                Assert.IsTrue(code.Contains("onClick.Invoke") || code.Contains("ToggleTransition"),
                    $"{Path.GetFileName(path)}'s overview gesture must drive the same call its " +
                    "volume/pause button does, not a parallel path that can drift from it.");
            }
        }
    }
}
