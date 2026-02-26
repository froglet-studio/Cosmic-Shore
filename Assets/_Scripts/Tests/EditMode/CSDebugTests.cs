using NUnit.Framework;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// CSDebug Tests — Validates the centralized logging system's level controls.
    ///
    /// WHY THIS MATTERS:
    /// CSDebug controls what gets logged at runtime. If log level presets don't
    /// set the correct flags, you could have silent failures in production
    /// (errors disabled) or performance-killing verbose logs (everything enabled
    /// in release builds). These tests ensure the preset/flag relationship is correct.
    /// </summary>
    [TestFixture]
    public class CSDebugTests
    {
        [SetUp]
        public void SetUp()
        {
            // Reset to known state before each test.
            CSDebug.LogLevel = CSLogLevel.All;
        }

        [TearDown]
        public void TearDown()
        {
            // Always restore to All after tests to not affect other test runs.
            CSDebug.LogLevel = CSLogLevel.All;
        }

        #region LogLevel Presets

        [Test]
        public void LogLevel_All_EnablesAllFlags()
        {
            CSDebug.LogLevel = CSLogLevel.All;

            Assert.IsTrue(CSDebug.LogEnabled);
            Assert.IsTrue(CSDebug.WarningsEnabled);
            Assert.IsTrue(CSDebug.ErrorsEnabled);
        }

        [Test]
        public void LogLevel_WarningsAndErrors_DisablesLogOnly()
        {
            CSDebug.LogLevel = CSLogLevel.WarningsAndErrors;

            Assert.IsFalse(CSDebug.LogEnabled, "Log (info) should be disabled.");
            Assert.IsTrue(CSDebug.WarningsEnabled, "Warnings should remain enabled.");
            Assert.IsTrue(CSDebug.ErrorsEnabled, "Errors should remain enabled.");
        }

        [Test]
        public void LogLevel_Off_DisablesEverything()
        {
            CSDebug.LogLevel = CSLogLevel.Off;

            Assert.IsFalse(CSDebug.LogEnabled);
            Assert.IsFalse(CSDebug.WarningsEnabled);
            Assert.IsFalse(CSDebug.ErrorsEnabled);
        }

        #endregion

        #region LogLevel Getter

        [Test]
        public void LogLevel_Getter_AllEnabled_ReturnsAll()
        {
            CSDebug.LogEnabled = true;
            CSDebug.WarningsEnabled = true;
            CSDebug.ErrorsEnabled = true;

            Assert.AreEqual(CSLogLevel.All, CSDebug.LogLevel);
        }

        [Test]
        public void LogLevel_Getter_LogDisabledWarningsErrorsEnabled_ReturnsWarningsAndErrors()
        {
            CSDebug.LogEnabled = false;
            CSDebug.WarningsEnabled = true;
            CSDebug.ErrorsEnabled = true;

            Assert.AreEqual(CSLogLevel.WarningsAndErrors, CSDebug.LogLevel);
        }

        [Test]
        public void LogLevel_Getter_AllDisabled_ReturnsOff()
        {
            CSDebug.LogEnabled = false;
            CSDebug.WarningsEnabled = false;
            CSDebug.ErrorsEnabled = false;

            Assert.AreEqual(CSLogLevel.Off, CSDebug.LogLevel);
        }

        [Test]
        public void LogLevel_Getter_CustomCombination_FallsBackToAll()
        {
            // A non-standard combo (e.g., only errors) isn't a named preset.
            // The getter should return All as the fallback.
            CSDebug.LogEnabled = false;
            CSDebug.WarningsEnabled = false;
            CSDebug.ErrorsEnabled = true;

            Assert.AreEqual(CSLogLevel.All, CSDebug.LogLevel,
                "Custom flag combos that don't map to a preset should return All.");
        }

        #endregion

        #region Roundtrip

        [Test]
        public void LogLevel_SetThenGet_Roundtrips()
        {
            // Set → Get should return the same preset.
            CSDebug.LogLevel = CSLogLevel.WarningsAndErrors;
            Assert.AreEqual(CSLogLevel.WarningsAndErrors, CSDebug.LogLevel);

            CSDebug.LogLevel = CSLogLevel.Off;
            Assert.AreEqual(CSLogLevel.Off, CSDebug.LogLevel);

            CSDebug.LogLevel = CSLogLevel.All;
            Assert.AreEqual(CSLogLevel.All, CSDebug.LogLevel);
        }

        #endregion

        #region CSLogLevel Enum

        [Test]
        public void CSLogLevel_All_IsZero()
        {
            Assert.AreEqual(0, (int)CSLogLevel.All);
        }

        [Test]
        public void CSLogLevel_WarningsAndErrors_IsOne()
        {
            Assert.AreEqual(1, (int)CSLogLevel.WarningsAndErrors);
        }

        [Test]
        public void CSLogLevel_Off_IsTwo()
        {
            Assert.AreEqual(2, (int)CSLogLevel.Off);
        }

        #endregion

        #region Individual Flag Toggle

        [Test]
        public void IndividualFlags_CanBeToggledIndependently()
        {
            CSDebug.LogEnabled = false;

            Assert.IsFalse(CSDebug.LogEnabled);
            Assert.IsTrue(CSDebug.WarningsEnabled, "Other flags should not be affected.");
            Assert.IsTrue(CSDebug.ErrorsEnabled, "Other flags should not be affected.");
        }

        [Test]
        public void IndividualFlags_PresetOverwritesAll()
        {
            // Start with custom state
            CSDebug.LogEnabled = true;
            CSDebug.WarningsEnabled = false;
            CSDebug.ErrorsEnabled = true;

            // Apply preset — should overwrite all flags
            CSDebug.LogLevel = CSLogLevel.Off;

            Assert.IsFalse(CSDebug.LogEnabled);
            Assert.IsFalse(CSDebug.WarningsEnabled);
            Assert.IsFalse(CSDebug.ErrorsEnabled);
        }

        #endregion
    }
}
