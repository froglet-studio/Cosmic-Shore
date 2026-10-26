using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The configure window's intensity as one big numeral turned with LB / RB (the approved R20ConfigureG2
    /// board): the picked level's numeral, four pips under it (lit up to the picked level, dim where the card
    /// offers the level but it is still to be earned), and the line that says what opens the next one.
    ///
    /// <para>The four <see cref="KitIntensityPip"/>s ARE the panel's intensity buttons, so the modal drives
    /// them exactly as before. LB / RB and the two arrow buttons only press the neighbouring pip: a locked one
    /// raises its locked event as a tap on it would, and a client whose pips are not interactable changes
    /// nothing.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Intensity Stepper")]
    public class IntensityStepper : MonoBehaviour
    {
        [Header("Levels")]
        [Tooltip("The pips, level 1 first. They are the panel's intensityButtons.")]
        [SerializeField] private KitIntensityPip[] pips = { };

        [Header("Parts")]
        [SerializeField] private TMP_Text numeralText;
        [Tooltip("What opens the next level (the first locked pip's goal). Empty when nothing is locked.")]
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Header("Words")]
        [Tooltip("What levels 1 to 4 read as (Shombith, 2026-10-26: numbers, not Roman numerals).")]
        [SerializeField] private string[] numerals = { "1", "2", "3", "4" };
        [Tooltip("A locked level's line when its pip carries no goal. {level} is the level before it.")]
        [SerializeField] private string unlockLine = "Play {level} to unlock";

        [Header("Colour")]
        [SerializeField] private UIThemeSO theme;

        void Awake()
        {
            if (previousButton) previousButton.onClick.AddListener(() => Step(-1));
            if (nextButton) nextButton.onClick.AddListener(() => Step(+1));
        }

        void Update()
        {
            // LB / RB step the level. The preview holding the stick owns the pad (its shoulders are the
            // vessel's then); under a configure ScreenSwitcher leaves the shoulders alone.
            if (ModePreviewWindow.AnyHasFocus) return;
            var pad = Gamepad.current;
            if (pad == null) return;
            if (pad.rightShoulder.wasPressedThisFrame) Step(+1);
            else if (pad.leftShoulder.wasPressedThisFrame) Step(-1);
        }

        int SelectedIndex()
        {
            for (int i = 0; i < pips.Length; i++)
                if (pips[i] && pips[i].IsSelected) return i;
            return -1;
        }

        /// <summary>Press the pip one level up or down from the picked one.</summary>
        public void Step(int direction)
        {
            int from = SelectedIndex();
            if (from < 0) return;
            int to = from + direction;
            if (to < 0 || to >= pips.Length || !pips[to] || !pips[to].IsActiveLevel) return;
            if (pips[to].TryGetComponent(out Button button) && !button.IsInteractable()) return;
            pips[to].Select();
        }

        /// <summary>Redraw the numeral, the pips and the hint from the pips' state. The pips call it.</summary>
        public void Redraw()
        {
            int selected = SelectedIndex();
            if (numeralText)
                numeralText.text = selected >= 0 && selected < numerals.Length ? numerals[selected] : string.Empty;

            string hint = string.Empty;
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = pips[i];
                if (!pip) continue;
                bool locked = pip.IsLocked && pip.IsActiveLevel;
                var token = !pip.IsActiveLevel ? UIColorToken.SurfaceVeryDark
                    : i <= selected ? UIColorToken.Selection
                    : locked ? UIColorToken.SurfaceVeryDark
                    : UIColorToken.SurfaceDark;
                if (pip.Fill) pip.Fill.color = UITheme.Resolve(theme, token);

                if (locked && hint.Length == 0)
                    hint = !string.IsNullOrEmpty(pip.LockedGoal) ? pip.LockedGoal
                        : TemplateText.Fill(unlockLine, "level", i > 0 && i - 1 < numerals.Length ? numerals[i - 1] : string.Empty);
            }
            if (hintText) hintText.text = hint;

            if (previousButton) previousButton.interactable = selected > 0;
            if (nextButton) nextButton.interactable = selected >= 0 && selected < pips.Length - 1 && pips[selected + 1] && pips[selected + 1].IsActiveLevel;
        }
    }
}
