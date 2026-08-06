#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// DisplayNameValidator Tests - Validates the display-name rule set.
    ///
    /// WHY THIS MATTERS:
    /// Display names are the one piece of player-authored free text every other player
    /// sees. If the length/charset rules drift per-UI again, or the profanity filter
    /// misses a leetspeak/separator variant, offensive names ship to production and the
    /// duplicate check (which keys off NormalizeForUniqueness) silently stops matching.
    /// These tests pin the rules, the evasion handling, and the false-positive
    /// protections in place.
    /// </summary>
    [TestFixture]
    public class DisplayNameValidatorTests
    {
        DisplayNameValidationConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            // In-memory config with field-initializer defaults, so the tests exercise the
            // same rule values that ship, without depending on the Resources asset.
            _config = ScriptableObject.CreateInstance<DisplayNameValidationConfigSO>();
            DisplayNameValidator.SetConfigOverride(_config);
        }

        [TearDown]
        public void TearDown()
        {
            DisplayNameValidator.SetConfigOverride(null);
            Object.DestroyImmediate(_config);
        }

        // ── Valid names ─────────────────────────────────────────────────────

        [TestCase("Nova")]
        [TestCase("Sky Walker")]
        [TestCase("Pilot_42")]
        [TestCase("Ace.One")]
        [TestCase("Zip-Zap")]
        [TestCase("abc")] // exactly min length
        public void Validate_AcceptsWellFormedNames(string name)
        {
            var result = DisplayNameValidator.Validate(name);
            Assert.IsTrue(result.IsValid, $"'{name}' should be valid but got {result.Error}: {result.Message}");
            Assert.AreEqual(DisplayNameError.None, result.Error);
        }

        [Test]
        public void Validate_AcceptsNameAtMaxLength()
        {
            string name = new string('a', _config.MaxLength);
            Assert.IsTrue(DisplayNameValidator.Validate(name).IsValid);
        }

        [Test]
        public void Validate_SanitizesWhitespacePadding()
        {
            var result = DisplayNameValidator.Validate("  Sky   Walker  ");
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("Sky Walker", result.SanitizedName);
        }

        // ── Length rules ────────────────────────────────────────────────────

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Validate_RejectsEmptyNames(string name)
        {
            Assert.AreEqual(DisplayNameError.Empty, DisplayNameValidator.Validate(name).Error);
        }

        [Test]
        public void Validate_RejectsTooShortNames()
        {
            string name = new string('a', _config.MinLength - 1);
            Assert.AreEqual(DisplayNameError.TooShort, DisplayNameValidator.Validate(name).Error);
        }

        [Test]
        public void Validate_RejectsTooLongNames()
        {
            string name = new string('a', _config.MaxLength + 1);
            Assert.AreEqual(DisplayNameError.TooLong, DisplayNameValidator.Validate(name).Error);
        }

        // ── Character / format rules ────────────────────────────────────────

        [TestCase("N@va")]     // disallowed symbol
        [TestCase("Nova!")]    // disallowed symbol
        [TestCase("Пилот")]    // non-ASCII (homoglyph evasion vector)
        [TestCase("Nova🚀")]   // emoji
        public void Validate_RejectsDisallowedCharacters(string name)
        {
            Assert.AreEqual(DisplayNameError.InvalidCharacters, DisplayNameValidator.Validate(name).Error);
        }

        [TestCase("-Nova")]    // leading special
        [TestCase("Nova.")]    // trailing special
        [TestCase("No..va")]   // consecutive specials
        [TestCase("No-_va")]   // consecutive specials (mixed)
        [TestCase("12345")]    // no letter
        public void Validate_RejectsMalformedNames(string name)
        {
            Assert.AreEqual(DisplayNameError.InvalidFormat, DisplayNameValidator.Validate(name).Error);
        }

        // ── Profanity / slur filter ─────────────────────────────────────────

        [TestCase("nigger")]         // direct
        [TestCase("NIGGER")]         // casing
        [TestCase("N1gg3r")]         // leetspeak
        [TestCase("n.i.g.g.e.r")]    // separator padding
        [TestCase("niiiggger")]      // letter repetition
        [TestCase("xXNiggerXx")]     // embedded
        [TestCase("FaggotLord")]     // embedded slur
        [TestCase("k1ke")]           // leet slur
        [TestCase("FuckThis")]       // profanity embedded
        [TestCase("Sh1tPilot")]      // leet profanity
        [TestCase("HeilHitler")]     // hate term
        [TestCase("KKKrew")]         // hate acronym embedded
        [TestCase("Ching Chong")]    // two-word slur
        [TestCase("ass")]            // whole-word term as the whole name
        [TestCase("a.s.s")]          // whole-word term, separator padded
        [TestCase("Big Ass")]        // whole-word term as a token
        [TestCase("Dick Face")]      // whole-word term as a token
        public void Validate_RejectsOffensiveNames(string name)
        {
            var result = DisplayNameValidator.Validate(name);
            Assert.AreEqual(DisplayNameError.Inappropriate, result.Error,
                $"'{name}' should be rejected as inappropriate but got {result.Error}");
        }

        [TestCase("Cassandra")]   // contains "ass"
        [TestCase("Passage")]     // contains "ass"
        [TestCase("Peacock")]     // contains "cock"
        [TestCase("Therapist")]   // contains "rapist"
        [TestCase("Grape")]       // contains "rape"
        [TestCase("Spice")]       // contains "spic"
        [TestCase("Raccoon")]     // contains "coon"
        [TestCase("Japan Fan")]   // contains "jap"
        [TestCase("Basement")]    // contains "semen"
        [TestCase("Torpedo")]     // contains "pedo"
        [TestCase("Arsenal")]     // contains "arse"
        [TestCase("Analyst")]     // contains "anal"
        [TestCase("Essex")]       // contains "sex"
        [TestCase("Titan")]       // contains "tit"
        [TestCase("Cucumber")]    // contains "cum"
        public void Validate_AcceptsInnocentNamesContainingAmbiguousTerms(string name)
        {
            var result = DisplayNameValidator.Validate(name);
            Assert.IsTrue(result.IsValid,
                $"'{name}' is a legitimate name and must not be a false positive ({result.Error}: {result.Message})");
        }

        [Test]
        public void Validate_ConfigCanExtendBlockedTerms()
        {
            var so = new SerializedObjectShim(_config);
            so.AddToList("additionalBlockedAnywhere", "frogletsux");

            Assert.AreEqual(DisplayNameError.Inappropriate,
                DisplayNameValidator.Validate("FrogletSux99").Error);
        }

        [Test]
        public void Validate_AllowlistRescuesExactName()
        {
            var so = new SerializedObjectShim(_config);
            so.AddToList("allowedNames", "Scunthorpe");

            Assert.IsTrue(DisplayNameValidator.Validate("Scunthorpe").IsValid,
                "Exact allowlisted names must bypass the content filter");
            Assert.AreEqual(DisplayNameError.Inappropriate,
                DisplayNameValidator.Validate("Scunthorpe Fan").Error,
                "The allowlist is exact-match only");
        }

        // ── Reserved names ──────────────────────────────────────────────────

        [TestCase("Admin")]
        [TestCase("moderator")]
        [TestCase("Adm1n")]         // leet evasion of a reserved name
        [TestCase("Admin Bob")]     // reserved word as a token (impersonation)
        [TestCase("Cosmic Shore")]  // brand impersonation
        public void Validate_RejectsReservedNames(string name)
        {
            Assert.AreEqual(DisplayNameError.Reserved, DisplayNameValidator.Validate(name).Error);
        }

        // ── Uniqueness normalization ────────────────────────────────────────

        [Test]
        public void NormalizeForUniqueness_IgnoresCaseSpacingAndPunctuation()
        {
            string expected = DisplayNameValidator.NormalizeForUniqueness("skywalker");
            Assert.AreEqual(expected, DisplayNameValidator.NormalizeForUniqueness("Sky Walker"));
            Assert.AreEqual(expected, DisplayNameValidator.NormalizeForUniqueness("sky.walker"));
            Assert.AreEqual(expected, DisplayNameValidator.NormalizeForUniqueness("SKY_WALKER"));
        }

        [Test]
        public void NormalizeForUniqueness_KeepsDigitsDistinct()
        {
            Assert.AreNotEqual(
                DisplayNameValidator.NormalizeForUniqueness("Pilot1234"),
                DisplayNameValidator.NormalizeForUniqueness("Pilot5678"));
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Writes into the config's private serialized lists the same way the inspector
        /// would, so the tests don't need public mutators on the config SO.
        /// </summary>
        readonly struct SerializedObjectShim
        {
            readonly UnityEditor.SerializedObject _so;

            public SerializedObjectShim(DisplayNameValidationConfigSO config)
            {
                _so = new UnityEditor.SerializedObject(config);
            }

            public void AddToList(string propertyName, string value)
            {
                var prop = _so.FindProperty(propertyName);
                prop.arraySize++;
                prop.GetArrayElementAtIndex(prop.arraySize - 1).stringValue = value;
                _so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
#endif
