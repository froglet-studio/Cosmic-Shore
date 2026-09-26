using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Toy Box's <b>shuffle</b> tile - the right of the two big buttons at the top of the
    /// window. One press re-rolls everything the toys can set: a new world, a new domain, a new
    /// hull.
    ///
    /// <para><b>A VIEW.</b> <c>ToyShuffle</c> is the authority on what a shuffle IS and
    /// <see cref="ToyboxModal"/> owns the press and the in-flight state; this only draws them. The
    /// same split as <see cref="ToyboxCard"/> and <see cref="DailyActivityCard"/>, and every field
    /// is optional for the same reason.</para>
    ///
    /// <para>It redraws on a 1 Hz tick rather than on an event, because what it has to reflect is
    /// whether any toy can answer right now - which changes with a cell swap that nothing here
    /// subscribes to, and which the modal's own registry refresh already re-binds it on.</para>
    /// </summary>
    public class ToyboxShuffleCard : MonoBehaviour
    {
        [Header("Labels (all optional)")]
        [SerializeField, Tooltip("Heading - 'SHUFFLE THE TOY BOX'. Taken from the config asset.")]
        TMP_Text titleText;

        [SerializeField, Tooltip("Second line: what a press will do, or what the last one did.")]
        TMP_Text detailText;

        [Header("Art (all optional)")]
        [SerializeField, Tooltip("Flat fill behind the card.")]
        Image accentFill;

        ToyboxModal _modal;
        Button _button;
        float _tickAccumulator;

        void Awake() => _button = GetComponent<Button>();

        void OnEnable() => Redraw();

        void Update()
        {
            _tickAccumulator += Time.unscaledDeltaTime;
            if (_tickAccumulator < 1f) return;
            _tickAccumulator = 0f;
            Redraw();
        }

        /// <summary>Called by <see cref="ToyboxModal"/> as it refreshes, so a press can route
        /// without the card hunting for the window.</summary>
        public void Bind(ToyboxModal modal)
        {
            _modal = modal;

            if (!_button) _button = GetComponent<Button>();
            if (_button)
            {
                _button.onClick.RemoveListener(HandleClicked);
                _button.onClick.AddListener(HandleClicked);
            }

            Redraw();
        }

        void HandleClicked()
        {
            if (_modal) _modal.ShuffleToyBox();
        }

        void Redraw()
        {
            var config = ToyboxDailyActivityConfigSO.Instance;
            SetText(titleText, config != null ? config.ShuffleTitle : "SHUFFLE THE TOY BOX");

            bool shuffling = _modal && _modal.IsShuffling;
            string last = _modal ? _modal.LastShuffleSummary : "";

            // What the button says, in priority order: what it is doing, then what it just did,
            // then what it would do. The last-shuffle line is what makes the press legible at all
            // on a shuffle that landed on a cell you cannot see from the menu.
            string detail =
                shuffling ? "SHUFFLING..."
                : !string.IsNullOrEmpty(last) ? last
                : config != null ? config.ShuffleDetail
                : "NEW WORLD, NEW COLOURS, NEW HULL";

            SetText(detailText, detail);

            // Dead while a shuffle is in flight, and while no toy can answer - a second press
            // mid-shuffle would plan against toys the first press is in the middle of replacing.
            if (_button) _button.interactable = !shuffling && (!_modal || _modal.CanShuffle);
        }

        /// <summary>
        /// Write a label only when the visible string actually CHANGED - a TMP_Text assignment
        /// dirties the mesh, and this redraws once a second for the life of the scene (the window
        /// hides by CanvasGroup alpha rather than deactivating, so the tick never stops).
        /// </summary>
        static void SetText(TMP_Text label, string value)
        {
            if (!label || label.text == value) return;
            label.text = value;
        }
    }
}
