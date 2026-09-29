using System;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

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

        // ==================================================================
        // Mouse motion is using the mouse

        [Test]
        public void SustainedMouseMovementCountsAsUsingTheMouse()
        {
            // The defect this closes: actuation took buttons and keys only, so a desktop player
            // with a pad merely PLUGGED IN could not fly with the mouse. DetectInitial hands a
            // connected pad the ship, a click won it back until the pad's resting stick crossed
            // 0.25 (drift qualifies), and no amount of mouse movement could ever win it again -
            // movement was not evidence of anything. Cursor locked, buttons fired, ship never
            // turned.
            var motion = default(MouseMotionActuation);
            bool actuated = false;
            for (int i = 0; i < 12; i++)                       // 0.2 s of steering
                actuated |= motion.Tick(new Vector2(6f, 0f), 1f / 60f);

            Assert.IsTrue(actuated,
                "Moving the mouse must count as using the mouse, or a connected pad owns a " +
                "one-thumb hull forever.");
        }

        [Test]
        public void ADeskBumpDoesNotStealTheShipFromAPadPlayer()
        {
            // The guarantee the original buttons-only rule was protecting, kept by requiring the
            // movement to be SUSTAINED rather than merely large. A jolt is a couple of frames,
            // however violent; steering is not.
            var motion = default(MouseMotionActuation);
            bool actuated = motion.Tick(new Vector2(900f, 400f), 1f / 60f);   // a hard knock
            for (int i = 0; i < 30; i++)
                actuated |= motion.Tick(Vector2.zero, 1f / 60f);              // then stillness

            Assert.IsFalse(actuated,
                "One violent frame is a bumped desk, not a player steering.");
        }

        [Test]
        public void AStationaryMouseNeverActuates()
        {
            var motion = default(MouseMotionActuation);
            bool actuated = false;
            for (int i = 0; i < 120; i++)
                actuated |= motion.Tick(Vector2.zero, 1f / 60f);

            Assert.IsFalse(actuated, "A mouse nobody is touching must not hold the input family.");
        }

        [Test]
        public void ARestingStickCannotTakeTheShipFromAnActiveMousePlayer()
        {
            // Going mouse -> pad worked; coming BACK did not. A stick resting past 0.25 re-claimed
            // the family every frame, and every claim runs OnStrategyDeactivated ->
            // ResetStrategyState -> stick = Vector2.zero, so the mouse could never accumulate a
            // deflection even on the frames it owned. Taking the ship from someone using it now
            // costs a real push.
            float ordinary = InputDeviceActuation.DefaultStickActuationThreshold;

            Assert.AreEqual(InputDeviceActuation.StickClaimThreshold,
                InputDeviceActuation.StickThresholdFor(InputDeviceFamily.KeyboardMouse, ordinary),
                1e-5f,
                "A pad must push PAST the claim threshold to take the family from the mouse.");

            foreach (var family in new[] { InputDeviceFamily.Gamepad, InputDeviceFamily.Touch,
                                           InputDeviceFamily.None })
                Assert.AreEqual(ordinary,
                    InputDeviceActuation.StickThresholdFor(family, ordinary), 1e-5f,
                    $"The claim threshold must not apply when the family is {family} - a pad " +
                    "player steering normally would stop registering.");

            Assert.Greater(InputDeviceActuation.StickClaimThreshold, ordinary * 2f,
                "The claim threshold has to be far enough above the ordinary one that drift " +
                "cannot reach it; that is the entire mechanism.");
            Assert.Less(InputDeviceActuation.StickClaimThreshold, 0.9f,
                "...and low enough that a deliberate push clears it without being pinned to the " +
                "corner of the gate.");
        }

        [Test]
        public void BothConsumersApplyTheClaimHysteresis()
        {
            // §4.0's law is that ONE question has ONE answer, and that has to include the
            // tie-breaks: chips that follow a different rule from the ship is the exact defect
            // InputDeviceActuation was extracted to end.
            foreach (var path in new[] { ControllerPath, SwitcherPath })
                Assert.IsTrue(Regex.IsMatch(Code(path),
                        @"DetectActuatedThisFrame\(\s*?
?\s*ref \w+, [^,]+, \w+"),
                    $"{Path.GetFileName(path)} does not pass its CURRENT family to the detector, " +
                    "so it cannot apply the claim threshold.");
        }

        [Test]
        public void AHeldStickIsRankedBelowEverythingAPlayerActuallyDid()
        {
            // A stick off centre is the ONE signal here that worn hardware produces on its own, so
            // it has to be the last thing asked - otherwise drift outranks every deliberate act.
            string code = Code(ActuationPath);
            int motion = code.IndexOf("mouseMotion.Tick", StringComparison.Ordinal);
            int keys = code.IndexOf("anyKey.wasPressedThisFrame", StringComparison.Ordinal);
            int axes = code.IndexOf("IsGamepadAxisActuated(pad", StringComparison.Ordinal);

            Assert.Greater(motion, 0, "Mouse motion must be part of actuation.");
            Assert.Greater(axes, motion,
                "Pad sticks are checked before mouse motion, so drift beats steering.");
            Assert.Greater(axes, keys,
                "Pad sticks are checked before the keyboard, so drift beats a keypress.");
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
