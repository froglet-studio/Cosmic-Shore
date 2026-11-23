using System;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One replay in the <see cref="ReplayBrowserModal"/>: what was played, when, how it ended, and
    /// Watch / Delete. A replay recorded on another device lists the same facts with no buttons -
    /// its frames never left that device - and says so.
    ///
    /// <para>Delete asks twice: the first press turns its label into the authored confirm word, a
    /// second press within <see cref="confirmSeconds"/> deletes. A replay is one match nobody can
    /// play again, so it is never one press from gone.</para>
    /// </summary>
    public class ReplayRowView : MonoBehaviour
    {
        [Header("Words (empty shows nothing)")]
        [SerializeField, Tooltip("Delete's label while it is waiting for the second press.")]
        string confirmDeleteLabel;

        [Header("Facts")]
        [SerializeField, Tooltip("The mode's name.")] TMP_Text title;
        [SerializeField, Tooltip("When it was played and for how long.")] TMP_Text when;
        [SerializeField, Tooltip("Who won, or that it ended without results.")] TMP_Text result;
        [SerializeField, Tooltip("Pilots and intensity.")] TMP_Text detail;

        [Header("This device")]
        [SerializeField, Tooltip("Watch and Delete. Hidden on a replay from another device.")] CanvasGroup actions;
        [SerializeField] Button watchButton;
        [SerializeField] Button deleteButton;
        [SerializeField, Tooltip("Delete's label - swapped for the confirm word while it waits.")] TMP_Text deleteText;

        [Header("Another device")]
        [SerializeField, Tooltip("The note shown instead of the buttons on a replay recorded elsewhere.")]
        CanvasGroup elsewhere;

        [Header("Delete")]
        [SerializeField, Range(1f, 10f), Tooltip("Seconds Delete waits for its second press.")]
        float confirmSeconds = 3f;

        Action _watch;
        Action _delete;
        string _deleteLabel;
        float _armedUntil = -1f;

        /// <summary>The replay this row shows.</summary>
        public ReplaySummary Summary { get; private set; }

        /// <summary>The Watch button, for the browser to put the pad's focus on. Null on a replay from another device.</summary>
        public Selectable WatchButton => _watch != null ? watchButton : null;

        void Awake()
        {
            if (deleteText) _deleteLabel = deleteText.text;
            if (watchButton) watchButton.onClick.AddListener(HandleWatch);
            if (deleteButton) deleteButton.onClick.AddListener(HandleDelete);
        }

        void OnDestroy()
        {
            if (watchButton) watchButton.onClick.RemoveListener(HandleWatch);
            if (deleteButton) deleteButton.onClick.RemoveListener(HandleDelete);
        }

        void Update()
        {
            if (_armedUntil >= 0f && Time.unscaledTime > _armedUntil) Disarm();
        }

        /// <summary>
        /// Show one replay. <paramref name="watch"/> and <paramref name="delete"/> are null for a
        /// replay from another device, which hides the buttons and shows the note.
        /// </summary>
        public void Bind(ReplaySummary summary, string titleText, string whenText, string resultText, string detailText,
                         Action watch, Action delete)
        {
            Summary = summary;
            if (title) title.text = titleText;
            if (when) when.text = whenText;
            if (result) result.text = resultText;
            if (detail) detail.text = detailText;

            _watch = watch;
            _delete = delete;
            bool here = watch != null;
            KitVisibility.SetShown(actions, here);
            KitVisibility.SetShown(elsewhere, !here);
            Disarm();
        }

        void HandleWatch() => _watch?.Invoke();

        void HandleDelete()
        {
            if (_delete == null) return;
            if (_armedUntil < 0f)
            {
                _armedUntil = Time.unscaledTime + confirmSeconds;
                if (deleteText) deleteText.text = confirmDeleteLabel;
                return;
            }
            Disarm();
            _delete.Invoke();
        }

        void Disarm()
        {
            _armedUntil = -1f;
            if (deleteText) deleteText.text = _deleteLabel;
        }
    }
}
