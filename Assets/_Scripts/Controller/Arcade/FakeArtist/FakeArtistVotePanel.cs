using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Fake Artist mode's one UI surface: the round role card ("you are the fake
    /// artist - subject: SATURN" / "draw your strokes - subject hidden"), the timed
    /// two-question vote (what is it? who faked it?), the imposter's waiting card, and
    /// the round reveal. Built entirely at runtime on its own overlay canvas (no
    /// GameCanvas prefab surgery; the lava-lamp rule - individual panels, never a
    /// second full canvas prefab) and shown/hidden by CanvasGroup fade (continuity law -
    /// panels fade, never pop).
    ///
    /// Pure view: the controller feeds it data and receives the local player's answers
    /// through <see cref="VoteSubmitted"/> (subjectChoice, accusedRosterIndex; -1 for
    /// unanswered). Selections lock on first click - no takebacks, matching the ready
    /// button's anti-double-submit idiom.
    /// </summary>
    public class FakeArtistVotePanel : MonoBehaviour
    {
        const float FadeSpeed = 5f;

        static readonly Color CardColor = new(0.05f, 0.06f, 0.1f, 0.92f);
        static readonly Color ButtonColor = new(0.16f, 0.2f, 0.3f, 0.95f);
        static readonly Color ButtonLockedColor = new(0.36f, 0.5f, 0.4f, 0.95f);
        static readonly Color TextColor = new(0.92f, 0.95f, 1f, 1f);
        static readonly Color AccentTextColor = new(1f, 0.85f, 0.4f, 1f);

        /// <summary>(subjectChoiceIndex, accusedRosterIndex) - fired once, when both questions lock or time expires.</summary>
        public event Action<int, int> VoteSubmitted;

        CanvasGroup _group;
        RectTransform _card;
        float _targetAlpha;
        float _hideAtTime = -1f;

        // Vote state
        bool _voteActive;
        int _subjectChoice = -1;
        int _accusedChoice = -1;
        float _voteDeadline;
        TMP_Text _timerText;
        readonly List<Button> _subjectButtons = new();
        readonly List<Button> _accuseButtons = new();

        public static FakeArtistVotePanel Create()
        {
            var root = new GameObject("FakeArtistVotePanel",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // above the in-game HUD, below scene-transition fade

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = root.AddComponent<FakeArtistVotePanel>();
            panel.BuildCard();
            return panel;
        }

        void BuildCard()
        {
            var cardGO = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _card = (RectTransform)cardGO.transform;
            _card.SetParent(transform, false);
            // Anchor to the lower-center and grow UPWARD, so the top ~60% of the screen
            // stays clear for the gallery camera's view of the shared painting while
            // players study it and answer.
            _card.anchorMin = new Vector2(0.5f, 0f);
            _card.anchorMax = new Vector2(0.5f, 0f);
            _card.pivot = new Vector2(0.5f, 0f);
            _card.anchoredPosition = new Vector2(0f, 48f);
            _card.sizeDelta = new Vector2(980f, 100f);

            cardGO.GetComponent<Image>().color = CardColor;
            _group = cardGO.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var layout = cardGO.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 28, 28);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = cardGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        void Update()
        {
            if (_group == null) return;

            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Time.unscaledDeltaTime * FadeSpeed);
            bool interactive = _targetAlpha > 0.5f && _voteActive;
            _group.interactable = interactive;
            _group.blocksRaycasts = _targetAlpha > 0.5f;

            if (_hideAtTime > 0f && Time.unscaledTime >= _hideAtTime)
            {
                _hideAtTime = -1f;
                Hide();
            }

            if (_voteActive)
            {
                float remaining = Mathf.Max(0f, _voteDeadline - Time.unscaledTime);
                if (_timerText != null)
                    _timerText.text = $"{Mathf.CeilToInt(remaining)}s";
                if (remaining <= 0f)
                    SubmitVote();
            }
        }

        // ── Public surface ──────────────────────────────────────────────────

        /// <summary>The round-start role card. Auto-hides after <paramref name="seconds"/>.</summary>
        public void ShowRoleCard(bool isImposter, int imposterCount, string subject, string brushName, float seconds)
        {
            ClearCard();
            _voteActive = false;

            if (isImposter)
            {
                AddText("YOU ARE THE FAKE ARTIST", 40f, AccentTextColor, FontStyles.Bold);
                AddText($"The subject is  <b>{subject.ToUpper()}</b>", 30f, TextColor);
                AddText("You know WHAT it is - but your strokes show only where they start and stop.\nImprovise the middle. Blend in. Don't get caught.",
                    22f, TextColor);
                if (imposterCount > 1)
                    AddText("You're not alone - there is another fake artist.", 22f, AccentTextColor);
            }
            else
            {
                AddText("DRAW YOUR STROKES", 40f, AccentTextColor, FontStyles.Bold);
                AddText("Subject: <b>???</b>", 30f, TextColor);
                AddText("Follow your rings. Watch what emerges - and watch for the one\nwhose strokes don't quite belong.",
                    22f, TextColor);
            }
            AddText($"Your brush: <b>{brushName}</b>", 24f, TextColor);

            Show();
            _hideAtTime = Time.unscaledTime + Mathf.Max(2f, seconds);
        }

        /// <summary>
        /// The two-question vote. <paramref name="candidateNames"/> is the full round
        /// roster; <paramref name="selfIndex"/> is skipped (you can't accuse yourself).
        /// </summary>
        public void ShowVote(IReadOnlyList<string> subjectOptions, IReadOnlyList<string> candidateNames,
            int selfIndex, float seconds)
        {
            ClearCard();
            _voteActive = true;
            _subjectChoice = -1;
            _accusedChoice = -1;
            _voteDeadline = Time.unscaledTime + Mathf.Max(5f, seconds);

            AddText("THE GALLERY VOTES", 38f, AccentTextColor, FontStyles.Bold);

            AddText("What are we drawing?", 26f, TextColor);
            var subjectGrid = AddGrid(2, new Vector2(430f, 62f));
            for (int i = 0; i < subjectOptions.Count; i++)
                _subjectButtons.Add(AddChoiceButton(subjectGrid, subjectOptions[i], 26f, i, isSubject: true));

            AddText("Who is the fake artist?", 26f, TextColor);
            var accuseGrid = AddGrid(3, new Vector2(284f, 54f));
            for (int i = 0; i < candidateNames.Count; i++)
            {
                if (i == selfIndex) continue;
                _accuseButtons.Add(AddChoiceButton(accuseGrid, candidateNames[i], 22f, i, isSubject: false));
            }

            _timerText = AddText("", 30f, AccentTextColor, FontStyles.Bold);

            Show();

            if (EventSystem.current != null && _subjectButtons.Count > 0)
                EventSystem.current.SetSelectedGameObject(_subjectButtons[0].gameObject);
        }

        /// <summary>The fake artist's view while everyone else votes.</summary>
        public void ShowWaiting(float seconds)
        {
            ClearCard();
            _voteActive = false;

            AddText("THE GALLERY VOTES", 38f, AccentTextColor, FontStyles.Bold);
            AddText("You don't get a ballot - you get a poker face.\nHold tight while the artists deliberate...", 24f, TextColor);

            Show();
            _hideAtTime = Time.unscaledTime + Mathf.Max(2f, seconds) + 4f;
        }

        /// <summary>The round reveal. Auto-hides after <paramref name="seconds"/>.</summary>
        public void ShowReveal(string subject, string imposterName, IReadOnlyList<string> resultLines, float seconds)
        {
            ClearCard();
            _voteActive = false;

            AddText("REVEAL", 38f, AccentTextColor, FontStyles.Bold);
            AddText($"The subject was  <b>{subject.ToUpper()}</b>", 28f, TextColor);
            AddText($"The fake artist was  <b>{imposterName}</b>", 28f, AccentTextColor);
            if (resultLines != null && resultLines.Count > 0)
                AddText(string.Join("\n", resultLines), 22f, TextColor);

            Show();
            _hideAtTime = Time.unscaledTime + Mathf.Max(2f, seconds);
        }

        public void Hide()
        {
            _targetAlpha = 0f;
            _voteActive = false;
        }

        // ── Vote mechanics ──────────────────────────────────────────────────

        Button AddChoiceButton(Transform parent, string label, float fontSize, int choice, bool isSubject)
        {
            Button btn = null;
            btn = AddButton(parent, label, fontSize, () => LockChoice(isSubject, choice, btn));
            return btn;
        }

        void LockChoice(bool isSubject, int choice, Button picked)
        {
            if (isSubject)
            {
                if (_subjectChoice >= 0) return;
                _subjectChoice = choice;
            }
            else
            {
                if (_accusedChoice >= 0) return;
                _accusedChoice = choice;
            }

            var group = isSubject ? _subjectButtons : _accuseButtons;
            foreach (var btn in group)
            {
                if (btn != null) btn.interactable = false;
            }

            if (picked != null && picked.TryGetComponent<Image>(out var img))
                img.color = ButtonLockedColor;

            if (_subjectChoice >= 0 && _accusedChoice >= 0)
            {
                SubmitVote();
                return;
            }

            if (EventSystem.current != null)
            {
                var next = isSubject ? _accuseButtons : _subjectButtons;
                foreach (var btn in next)
                {
                    if (btn != null && btn.interactable)
                    {
                        EventSystem.current.SetSelectedGameObject(btn.gameObject);
                        break;
                    }
                }
            }
        }

        void SubmitVote()
        {
            if (!_voteActive) return;
            _voteActive = false;
            VoteSubmitted?.Invoke(_subjectChoice, _accusedChoice);

            ClearCard();
            AddText("VOTE CAST", 34f, AccentTextColor, FontStyles.Bold);
            AddText("Waiting for the rest of the gallery...", 22f, TextColor);
        }

        // ── Builders ────────────────────────────────────────────────────────

        void Show() => _targetAlpha = 1f;

        void ClearCard()
        {
            _hideAtTime = -1f;
            _timerText = null;
            _subjectButtons.Clear();
            _accuseButtons.Clear();
            for (int i = _card.childCount - 1; i >= 0; i--)
                Destroy(_card.GetChild(i).gameObject);
        }

        TMP_Text AddText(string text, float size, Color color, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(_card, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        RectTransform AddGrid(int columns, Vector2 cellSize)
        {
            var go = new GameObject("Grid", typeof(RectTransform));
            go.transform.SetParent(_card, false);
            var grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = new Vector2(12f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.MiddleCenter;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return (RectTransform)go.transform;
        }

        Button AddButton(Transform parent, string label, float fontSize, Action onClick)
        {
            var go = new GameObject("Option", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = ButtonColor;

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var rect = (RectTransform)textGO.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return btn;
        }
    }
}
