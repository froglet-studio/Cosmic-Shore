using System;
using System.Globalization;
using CosmicShore.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Account tab's window over the settings (Docs/MENU_REDESIGN_B.md §54): the link conflict - "This Google account
    /// already has a pilot", the two pilots side by side and a choice of which to fly (the approved R9Conflict board) - and
    /// the sign-out / delete confirmations, which use the same window without the pilot cards. Every line comes from
    /// <see cref="AccountSettingsView"/>'s authored fields; the stat captions ("Crystals"...) are authored on the prefab.
    /// </summary>
    public class AccountPromptWindow : MonoBehaviour
    {
        [Serializable]
        public struct PilotCard
        {
            public TMP_Text caption;
            public TMP_Text name;
            public TMP_Text crystals;
            public TMP_Text vessels;
            public TMP_Text questsDone;
        }

        [SerializeField] CanvasGroup group;
        [Tooltip("The window; its foot and buttons are anchored to its bottom edge, so the confirmations shorten it.")]
        [SerializeField] RectTransform window;
        [SerializeField] float conflictHeight = 680f;
        [SerializeField] float confirmHeight = 330f;
        [SerializeField] TMP_Text eyebrow;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [SerializeField] TMP_Text foot;
        [SerializeField] GameObject cards;
        [SerializeField] PilotCard thisPilot;
        [SerializeField] PilotCard otherPilot;
        [SerializeField] Button primaryButton;
        [SerializeField] TMP_Text primaryLabel;
        [SerializeField] Button secondaryButton;
        [SerializeField] TMP_Text secondaryLabel;
        [SerializeField] Button cancelButton;
        [SerializeField] TMP_Text cancelLabel;

        Action _primary, _secondary;
        GameObject _returnFocus;

        public bool IsOpen => group && group.alpha > 0f;

        void Awake()
        {
            if (primaryButton) primaryButton.onClick.AddListener(() => Close(_primary));
            if (secondaryButton) secondaryButton.onClick.AddListener(() => Close(_secondary));
            if (cancelButton) cancelButton.onClick.AddListener(() => Close(null));
            SetShown(false);
        }

        void OnDisable() => SetShown(false);

        public void ShowConflict(string eyebrowLine, string titleLine, string bodyLine, string footLine,
                                 string thisCaption, PilotSummary thisSummary, string otherCaption, PilotSummary otherSummary,
                                 string primaryCaption, Action onPrimary, string secondaryCaption, Action onSecondary, string cancelCaption)
        {
            Fill(eyebrowLine, titleLine, bodyLine, footLine);
            if (cards) cards.SetActive(true);
            SetHeight(conflictHeight);
            FillCard(thisPilot, thisCaption, thisSummary);
            FillCard(otherPilot, otherCaption, otherSummary);
            Open(primaryCaption, onPrimary, secondaryCaption, onSecondary, cancelCaption);
        }

        public void ShowConfirm(string titleLine, string bodyLine, string confirmCaption, Action onConfirm, string cancelCaption)
        {
            Fill(string.Empty, titleLine, bodyLine, string.Empty);
            if (cards) cards.SetActive(false);
            SetHeight(confirmHeight);
            Open(confirmCaption, onConfirm, null, null, cancelCaption);
        }

        void Fill(string e, string t, string b, string f)
        {
            Set(eyebrow, e);
            Set(title, t);
            Set(body, b);
            Set(foot, f);
        }

        void Open(string primaryCaption, Action onPrimary, string secondaryCaption, Action onSecondary, string cancelCaption)
        {
            _primary = onPrimary;
            _secondary = onSecondary;
            SetButton(primaryButton, primaryLabel, primaryCaption, onPrimary != null);
            SetButton(secondaryButton, secondaryLabel, secondaryCaption, onSecondary != null);
            SetButton(cancelButton, cancelLabel, cancelCaption, true);
            var events = EventSystem.current;
            _returnFocus = events ? events.currentSelectedGameObject : null;
            SetShown(true);
            if (events && primaryButton) events.SetSelectedGameObject(primaryButton.gameObject);
        }

        void Close(Action then)
        {
            SetShown(false);
            var events = EventSystem.current;
            if (events && _returnFocus) events.SetSelectedGameObject(_returnFocus);
            then?.Invoke();
        }

        void SetHeight(float h)
        {
            if (window) window.sizeDelta = new Vector2(window.sizeDelta.x, h);
        }

        void SetShown(bool on)
        {
            if (!group) return;
            group.alpha = on ? 1f : 0f;
            group.interactable = on;
            group.blocksRaycasts = on;
        }

        static void FillCard(PilotCard card, string caption, PilotSummary s)
        {
            Set(card.caption, caption);
            Set(card.name, s.Name);
            Set(card.crystals, s.Crystals.ToString("N0", CultureInfo.InvariantCulture));
            Set(card.vessels, s.Vessels.ToString(CultureInfo.InvariantCulture));
            Set(card.questsDone, s.QuestsDone.ToString(CultureInfo.InvariantCulture));
        }

        static void SetButton(Button b, TMP_Text label, string caption, bool shown)
        {
            if (!b) return;
            b.gameObject.SetActive(shown && !string.IsNullOrEmpty(caption));
            if (label) label.text = caption;
        }

        static void Set(TMP_Text t, string s)
        {
            if (t) t.text = s ?? string.Empty;
        }
    }
}
