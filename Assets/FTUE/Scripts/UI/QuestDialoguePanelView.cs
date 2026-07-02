using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Core
{
    /// <summary>
    /// Menu dialogue panel: one captain portrait + animated (typewriter) text with
    /// Next / Skip buttons. Implements <see cref="IDialogueView"/> so the existing
    /// DialogueManager drives it — wire it into DialogueViewResolver's MainMenu channel
    /// (or swap it in for the current view) and every Dialogue node just works.
    ///
    /// Next while typing = complete the line instantly; Next after = advance.
    /// Skip = fast-forward the rest of the set (each remaining line completes instantly).
    /// </summary>
    public class QuestDialoguePanelView : MonoBehaviour, IDialogueView
    {
        [Header("References")]
        [Tooltip("Root CanvasGroup faded in/out. Auto-resolved/added if unset.")]
        [SerializeField] CanvasGroup panel;
        [SerializeField] Image captainImage;
        [SerializeField] TMP_Text speakerNameText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] Button nextButton;
        [SerializeField] Button skipButton;

        [Header("Typewriter")]
        [Tooltip("Characters revealed per second (unscaled).")]
        [Min(1f)] [SerializeField] float charsPerSecond = 45f;

        [Header("Timing")]
        [Min(0f)] [SerializeField] float fadeDuration = 0.2f;

        Coroutine _typing;
        Coroutine _fade;
        Action _onLineComplete;
        string _fullLine = string.Empty;
        bool _lineFullyShown;
        bool _skipRequested;

        void Awake()
        {
            if (panel == null && !TryGetComponent(out panel))
                panel = gameObject.AddComponent<CanvasGroup>();

            if (nextButton != null) nextButton.onClick.AddListener(OnNextPressed);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkipPressed);

            SetVisible(false, instant: true);
        }

        // ── IDialogueView ──────────────────────────────────────────────

        public void ShowDialogueSet(DialogueSet set)
        {
            _skipRequested = false;

            if (captainImage != null)
            {
                captainImage.sprite = set.portraitSpeaker1;
                captainImage.enabled = set.portraitSpeaker1 != null;
            }

            SetVisible(true);
        }

        public void ShowLine(DialogueSet set, DialogueLine line, Action onLineComplete)
        {
            _onLineComplete = onLineComplete;
            _fullLine = line.text ?? string.Empty;
            _lineFullyShown = false;

            if (speakerNameText != null)
                speakerNameText.text = line.speakerName;

            if (_typing != null) StopCoroutine(_typing);

            if (_skipRequested)
            {
                // Fast-forward mode: land the line instantly and move on.
                CompleteLineText();
                AdvanceLine();
                return;
            }

            _typing = StartCoroutine(Typewriter());
        }

        public void Hide(Action onHidden)
        {
            if (_typing != null) StopCoroutine(_typing);
            SetVisible(false, onDone: onHidden);
        }

        // ── Buttons ────────────────────────────────────────────────────

        void OnNextPressed()
        {
            if (!_lineFullyShown)
                CompleteLineText();
            else
                AdvanceLine();
        }

        void OnSkipPressed()
        {
            _skipRequested = true;
            if (!_lineFullyShown)
                CompleteLineText();
            AdvanceLine();
        }

        // ── Internals ──────────────────────────────────────────────────

        IEnumerator Typewriter()
        {
            _lineFullyShown = false;
            if (bodyText == null)
            {
                CompleteLineText();
                yield break;
            }

            bodyText.text = _fullLine;
            bodyText.maxVisibleCharacters = 0;
            float shown = 0f;

            while (bodyText.maxVisibleCharacters < _fullLine.Length)
            {
                shown += charsPerSecond * Time.unscaledDeltaTime;
                bodyText.maxVisibleCharacters = Mathf.Min(_fullLine.Length, Mathf.FloorToInt(shown));
                yield return null;
            }

            _lineFullyShown = true;
        }

        void CompleteLineText()
        {
            if (_typing != null) StopCoroutine(_typing);
            if (bodyText != null)
            {
                bodyText.text = _fullLine;
                bodyText.maxVisibleCharacters = int.MaxValue;
            }
            _lineFullyShown = true;
        }

        void AdvanceLine()
        {
            var callback = _onLineComplete;
            _onLineComplete = null;
            callback?.Invoke();
        }

        void SetVisible(bool visible, bool instant = false, Action onDone = null)
        {
            if (_fade != null) StopCoroutine(_fade);

            panel.blocksRaycasts = visible;
            panel.interactable = visible;

            if (instant || fadeDuration <= 0f || !isActiveAndEnabled)
            {
                panel.alpha = visible ? 1f : 0f;
                onDone?.Invoke();
                return;
            }

            _fade = StartCoroutine(FadeRoutine(visible ? 1f : 0f, onDone));
        }

        IEnumerator FadeRoutine(float target, Action onDone)
        {
            float start = panel.alpha;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                panel.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }
            panel.alpha = target;
            onDone?.Invoke();
        }
    }
}
