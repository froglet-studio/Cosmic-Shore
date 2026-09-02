using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Re-ranges a <see cref="Slider"/> without ever raising <c>onValueChanged</c>.
    ///
    /// <para><b>Why this has to exist.</b> Unity's <c>minValue</c> / <c>maxValue</c> /
    /// <c>wholeNumbers</c> setters each end in <c>Set(m_Value)</c> with <c>sendCallback: true</c>,
    /// so narrowing a slider's window CLAMPS the value it is carrying and then broadcasts the
    /// clamped result to every listener — including the persistent listeners authored on the prefab,
    /// which code cannot conveniently detach. When that listener is the one that PERSISTS the
    /// setting, simply binding the control overwrites the player's saved value.</para>
    ///
    /// <para>That is not hypothetical: the Music, SFX and Haptics sliders in
    /// <c>OptionsMenuContent.prefab</c> shipped as copies of the field-of-view slider — range
    /// <c>60..90</c>, whole numbers, value <c>71</c>. Binding them to the 0..1 audio window clamped
    /// 71 to the new maximum of 1, raised <c>onValueChanged(1)</c>, and the prefab's persistent
    /// <c>AudioLevelSlider.SetVolume</c> wrote FULL VOLUME over whatever the player had saved. The
    /// panel then displayed the value it had just destroyed, so the slider sat at the top on every
    /// launch and no amount of correctness in the persistence layer could survive it.</para>
    ///
    /// <para><b>The order below is the whole trick</b>: widen the window so it contains both the
    /// value the slider is carrying and the value we are about to write, move the value silently,
    /// and only then narrow to the real window. Every assignment is a no-op clamp, so the callback
    /// never fires — no listener bookkeeping, no suppression flag anyone has to remember.</para>
    /// </summary>
    public static class SliderRange
    {
        /// <summary>
        /// Applies <paramref name="min"/>/<paramref name="max"/>/<paramref name="wholeNumbers"/> and
        /// seats the slider on <paramref name="value"/>, guaranteeing no <c>onValueChanged</c> call.
        /// <paramref name="value"/> is clamped (and rounded, when whole numbers are requested) into
        /// the new window first, so a caller may pass a saved setting straight through.
        /// </summary>
        public static void ApplyWithoutNotify(Slider slider, float min, float max, bool wholeNumbers, float value)
        {
            if (slider == null) return;

            float target = Mathf.Clamp(value, min, max);
            if (wholeNumbers) target = Mathf.Round(target);

            // 1. Drop quantization first. Removing a constraint can only ever leave the current
            //    value where it is, so this can neither clamp nor notify.
            slider.wholeNumbers = false;

            // 2. Widen to cover the value the slider is carrying AND the one we are about to write.
            //    Widening never clamps, so still no notification - and doing it in this order is
            //    what makes step 4 a no-op instead of a broadcast.
            float carried = slider.value;
            slider.minValue = Mathf.Min(min, Mathf.Min(carried, target));
            slider.maxValue = Mathf.Max(max, Mathf.Max(carried, target));

            // 3. Move to the target while the window still contains it. Explicitly silent.
            slider.SetValueWithoutNotify(target);

            // 4. Narrow to the real window. The value already sits inside it, so each clamp is a
            //    no-op and onValueChanged stays quiet.
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
        }
    }
}
