#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The audio sliders reset themselves to full volume on every launch, and nothing in the
    /// persistence layer could have stopped it: Music / SFX / Haptics shipped in
    /// <c>OptionsMenuContent.prefab</c> as copies of the FIELD-OF-VIEW slider (range 60..90, whole
    /// numbers, value 71). Binding one to the 0..1 audio window clamps 71 down to the new maximum
    /// of 1 and — because Unity's range setters call <c>Set(value, sendCallback: true)</c> —
    /// broadcasts that 1 to the slider's persistent inspector listener, which SAVES it. The panel
    /// then displayed the value it had just destroyed.
    ///
    /// These lock the fix from both ends: the helper must be silent, and the naive assignment it
    /// replaced must still be demonstrably loud (otherwise the test proves nothing about the bug).
    /// </summary>
    public class SliderRangeTests
    {
        GameObject _go;
        Slider _slider;
        int _callbacks;
        float _lastCallbackValue;

        /// <summary>A slider authored exactly the way the three audio rows shipped.</summary>
        [SetUp]
        public void SetUp()
        {
            // RectTransform explicitly: Slider is a UI Selectable and a bare Transform is not a
            // shape it can be constructed against reliably outside play mode.
            _go = new GameObject("SliderRangeTest", typeof(RectTransform));
            _slider = _go.AddComponent<Slider>();
            _slider.minValue = 60f;
            _slider.maxValue = 90f;
            _slider.wholeNumbers = true;
            _slider.SetValueWithoutNotify(71f);

            _callbacks = 0;
            _lastCallbackValue = float.NaN;
            _slider.onValueChanged.AddListener(v => { _callbacks++; _lastCallbackValue = v; });
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Naive_range_assignment_saves_full_volume_over_the_players_setting()
        {
            // The negative control - this IS the shipped bug, and it must keep reproducing or the
            // test below is only asserting that some code path happens not to notify today.
            _slider.minValue = 0f;
            _slider.maxValue = 1f;
            _slider.wholeNumbers = false;

            Assert.AreEqual(1, _callbacks, "Narrowing the range must clamp and notify - that is the defect.");
            Assert.AreEqual(1f, _lastCallbackValue, 1e-6f, "The value broadcast to the persisting listener is full volume.");
        }

        [Test]
        public void ApplyWithoutNotify_never_notifies_when_narrowing_a_field_of_view_slider()
        {
            SliderRange.ApplyWithoutNotify(_slider, 0f, 1f, false, 0.25f);

            Assert.AreEqual(0, _callbacks, "Re-ranging must not reach the slider's persistent listeners.");
        }

        [Test]
        public void ApplyWithoutNotify_seats_the_saved_value_and_the_new_window()
        {
            SliderRange.ApplyWithoutNotify(_slider, 0f, 1f, false, 0.25f);

            Assert.AreEqual(0.25f, _slider.value, 1e-6f);
            Assert.AreEqual(0f, _slider.minValue, 1e-6f);
            Assert.AreEqual(1f, _slider.maxValue, 1e-6f);
            Assert.IsFalse(_slider.wholeNumbers);
        }

        [Test]
        public void A_muted_setting_survives_binding()
        {
            // The reported case: the player drags to 0, and the value must still be 0 after the
            // panel binds on the next launch.
            SliderRange.ApplyWithoutNotify(_slider, 0f, 1f, false, 0f);

            Assert.AreEqual(0, _callbacks);
            Assert.AreEqual(0f, _slider.value, 1e-6f);
        }

        [Test]
        public void Value_outside_the_new_window_is_clamped_silently()
        {
            SliderRange.ApplyWithoutNotify(_slider, 0f, 1f, false, 5f);

            Assert.AreEqual(0, _callbacks);
            Assert.AreEqual(1f, _slider.value, 1e-6f);
        }

        [Test]
        public void Widening_a_range_also_stays_silent()
        {
            // The mirror case: an audio-authored slider bound as the field-of-view row.
            _slider.minValue = 0f;
            _slider.maxValue = 1f;
            _slider.wholeNumbers = false;
            _slider.SetValueWithoutNotify(1f);
            _callbacks = 0;

            SliderRange.ApplyWithoutNotify(_slider, 60f, 90f, true, 90f);

            Assert.AreEqual(0, _callbacks);
            Assert.AreEqual(90f, _slider.value, 1e-6f);
            Assert.IsTrue(_slider.wholeNumbers);
        }

        [Test]
        public void Whole_number_targets_are_rounded_into_the_window()
        {
            SliderRange.ApplyWithoutNotify(_slider, 60f, 90f, true, 71.4f);

            Assert.AreEqual(0, _callbacks);
            Assert.AreEqual(71f, _slider.value, 1e-6f);
        }

        [Test]
        public void Null_slider_is_a_no_op()
        {
            Assert.DoesNotThrow(() => SliderRange.ApplyWithoutNotify(null, 0f, 1f, false, 0.5f));
        }
    }
}
#endif
