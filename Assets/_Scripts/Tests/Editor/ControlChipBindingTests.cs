using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The ability lockup's control chip resolves ability → input → physical control → artwork,
    /// and every step is derived, so a wrong label is supposed to be impossible. What WAS possible
    /// — and shipped — is a blank one: <see cref="InputHintBindingMap.BindingFor"/> answers a
    /// keyboard query with <c>KeyLeftShift</c> while <c>ControlGlyphSet</c> authors the label on
    /// <c>PadLeftTrigger</c>, so the lookup missed and every keyboard chip in the project drew
    /// nothing. Half the row was blank for a second reason: no keyboard control was mapped to the
    /// pad face buttons at all.
    ///
    /// <para>These tests run against the SHIPPED <c>Resources/ControlGlyphSet</c>, because the
    /// failure was in the asset-to-code correspondence rather than in either alone.</para>
    /// </summary>
    [TestFixture]
    public class ControlChipBindingTests
    {
        /// <summary>The Sparrow's four authored ability inputs (Resources/ElementalAbilityMaps/Sparrow).</summary>
        static readonly InputEvents[] SparrowInputs =
        {
            InputEvents.LeftStickAction,    // Charge - skybursts
            InputEvents.RightStickAction,   // Space  - guns
            InputEvents.Button1Action,      // Mass
            InputEvents.Button2Action,      // Time
        };

        static ControlGlyphSetSO LoadSet()
        {
            var set = Resources.Load<ControlGlyphSetSO>("ControlGlyphSet");
            Assert.IsNotNull(set, "Resources/ControlGlyphSet is the fleet's one glyph table; " +
                                  "without it no vessel can draw a control chip at all.");
            return set;
        }

        [Test]
        public void EveryOneThumbAbilityHasAKeyboardLabel()
        {
            var set = LoadSet();

            foreach (var input in SparrowInputs)
            {
                var binding = InputHintBindingMap.BindingFor(input, keyboard: true);
                Assert.AreNotEqual(HintBinding.None, binding,
                    $"{input} has no keyboard control, so its chip is blank on the device most " +
                    "people play on.");

                var glyph = set.For(binding);
                Assert.IsNotNull(glyph,
                    $"{input} resolves to {binding}, which the glyph set cannot answer. This is " +
                    "the exact break that left every keyboard chip blank: the label is authored " +
                    "on the PAD twin and the lookup asked for the keyboard address.");
                Assert.IsNotEmpty(glyph.keyboardLabel,
                    $"{input} → {binding} found an entry with no keyboardLabel.");
            }
        }

        [Test]
        public void EveryOneThumbAbilityHasAPadGlyph()
        {
            var set = LoadSet();

            foreach (var input in SparrowInputs)
            {
                var binding = InputHintBindingMap.BindingFor(input, keyboard: false);
                Assert.AreNotEqual(HintBinding.None, binding, $"{input} has no pad control.");

                var glyph = set.For(binding);
                Assert.IsNotNull(glyph, $"{input} → {binding} has no glyph entry.");
                Assert.IsNotNull(glyph.padGlyph, $"{input} → {binding} has no pad artwork.");
            }
        }

        [Test]
        public void KeyboardAndPadResolveToTheSameEntry()
        {
            var set = LoadSet();

            // One authored row per logical control is the whole point of the canonical fallback -
            // if these ever diverge, a label and its glyph can drift apart.
            foreach (var input in SparrowInputs)
            {
                var pad = set.For(InputHintBindingMap.BindingFor(input, keyboard: false));
                var key = set.For(InputHintBindingMap.BindingFor(input, keyboard: true));

                Assert.AreSame(pad, key,
                    $"{input} resolves to two different glyph entries for pad and keyboard.");
            }
        }

        [Test]
        public void CanonicalMapsEveryKeyboardControlToAPadTwin()
        {
            HintBinding[] keyboardControls =
            {
                HintBinding.KeyLeftShift, HintBinding.KeyRightShift,
                HintBinding.KeySpace, HintBinding.KeyR, HintBinding.KeyQ,
            };

            foreach (var control in keyboardControls)
            {
                var canonical = InputHintBindingMap.Canonical(control);
                Assert.AreNotEqual(control, canonical,
                    $"{control} is a keyboard control the map raises, so it needs a pad twin - " +
                    "otherwise its glyph entry has to be duplicated and the two can drift.");
                Assert.Less((int)canonical, (int)HintBinding.KeyLeftShift,
                    $"{control}'s canonical form must be a PAD binding.");
            }
        }

        [Test]
        public void CanonicalLeavesAPadBindingAlone()
        {
            // The fallback must be a one-way alias: a pad lookup that misses has to stay a miss,
            // not bounce to some other control's artwork.
            foreach (var pad in new[] { HintBinding.PadLeftTrigger, HintBinding.PadRightTrigger,
                                        HintBinding.PadButtonSouth, HintBinding.PadButtonEast,
                                        HintBinding.PadButtonWest, HintBinding.None })
                Assert.AreEqual(pad, InputHintBindingMap.Canonical(pad));
        }

        [Test]
        public void KeyboardActionKeysStayInTheQwerSpaceCluster()
        {
            // The one-thumb scheme's whole point is a left hand that never leaves QWER + Space
            // while the right hand flies on the mouse. A control that drifts out of the cluster
            // (B and N were the historical bindings) is unreachable from that hand position, and
            // because there is ONE keyboardLabel per control it would also drag the dual-WASD
            // scheme with it or make the chip wrong for one of the two.
            var cluster = new[]
            {
                HintBinding.KeyQ, HintBinding.KeyE, HintBinding.KeyR, HintBinding.KeySpace,
                HintBinding.KeyLeftShift, HintBinding.KeyRightShift,
            };

            foreach (var input in new[] { InputEvents.Button1Action, InputEvents.Button2Action,
                                          InputEvents.Button3Action, InputEvents.LeftStickAction,
                                          InputEvents.RightStickAction })
            {
                var binding = InputHintBindingMap.BindingFor(input, keyboard: true);
                Assert.Contains(binding, cluster,
                    $"{input} is bound to {binding}, outside the QWER + Space cluster the " +
                    "desktop schemes are built around.");
            }
        }

        [Test]
        public void PassiveAbilityDrawsNothing()
        {
            // FullSpeedStraightAction is the "(open design slot)" sentinel AND a real passive
            // gesture. Neither has a button, and blank is the honest answer for both.
            Assert.AreEqual(HintBinding.None,
                InputHintBindingMap.BindingFor(InputEvents.FullSpeedStraightAction, keyboard: true));
            Assert.AreEqual(HintBinding.None,
                InputHintBindingMap.BindingFor(InputEvents.FullSpeedStraightAction, keyboard: false));
            Assert.IsNull(LoadSet().For(HintBinding.None));
        }
    }
}
