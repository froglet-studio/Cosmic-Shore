using System;
using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The launch panel's AI DIFFICULTY row: Easy, Medium, Hard - one lit.
    ///
    /// <para>It is a widget and nothing more, on the same terms as the intensity row: a press is
    /// RAISED (<see cref="OnPicked"/>), and <c>ArcadeGameConfigureModal</c> decides what it means
    /// - only the host may change it, the choice is replicated to every guest, and it is
    /// remembered with the rest of the host terms on launch. The picker never writes config and
    /// never lights a button on its own; it draws whatever <see cref="SetSelected"/> last told it,
    /// so a guest's row always shows the HOST's pick.</para>
    ///
    /// <para>Difficulty is the OPPONENT, intensity is the MAP - so the two rows are independent and
    /// a Hard AI can race intensity 1. The row is shown only on cards whose AI reads the setting
    /// (<see cref="AIDifficultyRules.IsOfferedFor"/>).</para>
    /// </summary>
    public class AIDifficultyPicker : MonoBehaviour
    {
        [Serializable]
        public struct Option
        {
            [Tooltip("The difficulty this button picks.")]
            public AIDifficulty Difficulty;

            [Tooltip("The button itself. Its onClick is subscribed by the picker at runtime - " +
                     "author no persistent call to the modal or the panel on it.")]
            public Button Button;

            [Tooltip("The plate drawn behind the label. Swapped between the selected and unselected " +
                     "sprite below.")]
            public Image Plate;

            [Tooltip("The label. Its TEXT is authored on the prefab (ASCII only - the UI font has " +
                     "no other glyphs); the picker only recolours it.")]
            public TMP_Text Label;
        }

        [Header("Buttons")]
        [SerializeField, Tooltip("One entry per button, easiest first.")]
        Option[] options = Array.Empty<Option>();

        [Header("Look - the intensity row's, so the two rows read as one panel")]
        [SerializeField, Tooltip("Plate sprite of the lit button (the intensity row's " +
                                 "Game_Option_Border_Active).")]
        Sprite plateSelected;

        [SerializeField, Tooltip("Plate sprite of an unlit button (the intensity row's " +
                                 "Game_Option_Border_Inactive).")]
        Sprite plateUnselected;

        [SerializeField, Tooltip("Label colour of the lit button.")]
        Color labelSelected = Color.white;

        [SerializeField, Tooltip("Label colour of an unlit button.")]
        Color labelUnselected = new Color32(125, 125, 125, 255);

        /// <summary>A button was pressed. Raised for every press, including the lit button's -
        /// the modal compares and ignores a no-op.</summary>
        public event Action<AIDifficulty> OnPicked;

        AIDifficulty _selected = AIDifficultyRules.Default;
        UnityAction[] _handlers;

        /// <summary>The difficulty the row is currently drawing.</summary>
        public AIDifficulty Selected => _selected;

        void Awake()
        {
            _handlers = new UnityAction[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                var difficulty = options[i].Difficulty;
                _handlers[i] = () => OnPicked?.Invoke(difficulty);
            }
        }

        void OnEnable()
        {
            for (int i = 0; i < options.Length; i++)
                if (options[i].Button) options[i].Button.onClick.AddListener(_handlers[i]);
            Redraw();
        }

        void OnDisable()
        {
            for (int i = 0; i < options.Length; i++)
                if (options[i].Button) options[i].Button.onClick.RemoveListener(_handlers[i]);
        }

        /// <summary>Light the button for <paramref name="difficulty"/> (an unknown value lights the
        /// default). Draws only; raises nothing.</summary>
        public void SetSelected(AIDifficulty difficulty)
        {
            _selected = AIDifficultyRules.Resolve(difficulty);
            Redraw();
        }

        /// <summary>Whether a press is accepted - false on a guest, who sees the host's pick
        /// but cannot change it.</summary>
        public void SetInteractable(bool interactable)
        {
            for (int i = 0; i < options.Length; i++)
                if (options[i].Button) options[i].Button.interactable = interactable;
        }

        void Redraw()
        {
            for (int i = 0; i < options.Length; i++)
            {
                bool lit = options[i].Difficulty == _selected;

                var plate = lit ? plateSelected : plateUnselected;
                if (options[i].Plate && plate) options[i].Plate.sprite = plate;
                if (options[i].Label) options[i].Label.color = lit ? labelSelected : labelUnselected;
            }
        }
    }
}
