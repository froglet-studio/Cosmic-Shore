using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Core
{
    /// <summary>
    /// Menu dialogue panel: one captain portrait + animated (typewriter) text with
    /// Next / Skip buttons.
    ///
    /// Primary entry point is <see cref="PlayLines"/> — the quest graph's Dialogue nodes
    /// drive the panel directly with lines authored on the node (no dialogue system).
    /// The <see cref="IDialogueView"/> implementation is kept so the panel can ALSO serve
    /// the legacy DialogueManager pipeline if ever wired there.
    ///
    /// Next while typing = complete the line instantly; Next after = advance.
    /// Skip = fast-forward the rest (remaining lines complete instantly).
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

        Sprite _defaultPortrait;
        System.Collections.Generic.IReadOnlyList<string> _playLines;
        int _playIndex;
        Action _playCompletion;

        /// <summary>True while a PlayLines sequence is running.</summary>
        public bool IsPlaying { get; private set; }

        bool _initialized;

        void Awake()
        {
            EnsureInitialized();
            SetVisible(false, instant: true);
        }

        /// <summary>
        /// Idempotent setup. The hand-built scene panel usually starts INACTIVE, so Awake may
        /// not have run when a quest node first drives it — every public entry point calls
        /// this before touching state.
        /// </summary>
        void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            if (panel == null && !TryGetComponent(out panel))
                panel = gameObject.AddComponent<CanvasGroup>();

            if (nextButton != null) nextButton.onClick.AddListener(OnNextPressed);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkipPressed);

            if (captainImage != null)
                _defaultPortrait = captainImage.sprite;
        }

        // ── Direct API (quest graph — no dialogue system) ─────────────

        /// <summary>
        /// Show the panel and step through <paramref name="lines"/> with the Next button;
        /// hides itself after the last line and invokes <paramref name="onComplete"/>.
        /// </summary>
        public void PlayLines(string speaker, Sprite portrait,
            System.Collections.Generic.IReadOnlyList<string> lines, Action onComplete)
        {
            if (lines == null || lines.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            EnsureInitialized();

            // The scene panel is typically saved inactive — wake it up before playing
            // (StartCoroutine on an inactive object throws, which would hang the quest).
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (!isActiveAndEnabled)
            {
                // A parent is still inactive (or the component is disabled) — we can't run
                // coroutines. Fail OPEN: report, complete, and let the quest continue.
                Debug.LogError($"[Quest] Dialogue panel '{name}' cannot play — a parent object is inactive " +
                               "or the component is disabled. Skipping this dialogue beat.");
                onComplete?.Invoke();
                return;
            }

            _skipRequested = false;
            IsPlaying = true;
            _playLines = lines;
            _playIndex = 0;
            _playCompletion = onComplete;

            if (speakerNameText != null)
                speakerNameText.text = speaker;

            if (captainImage != null)
            {
                captainImage.sprite = portrait != null ? portrait : _defaultPortrait;
                captainImage.enabled = captainImage.sprite != null;
            }

            SetVisible(true);
            ShowPlayLine();
        }

        /// <summary>Abort a running PlayLines sequence (no completion callback) and hide.</summary>
        public void CancelIfPlaying()
        {
            if (!IsPlaying) return;

            IsPlaying = false;
            _playLines = null;
            _playCompletion = null;
            _onLineComplete = null;
            if (_typing != null) StopCoroutine(_typing);
            SetVisible(false);
        }

        void ShowPlayLine()
        {
            _fullLine = _playLines[_playIndex] ?? string.Empty;
            _lineFullyShown = false;
            _onLineComplete = OnPlayLineComplete;

            if (_typing != null) StopCoroutine(_typing);

            if (_skipRequested)
            {
                CompleteLineText();
                AdvanceLine();
                return;
            }

            _typing = StartCoroutine(Typewriter());
        }

        void OnPlayLineComplete()
        {
            _playIndex++;
            if (_skipRequested || _playLines == null || _playIndex >= _playLines.Count)
            {
                var completion = _playCompletion;
                IsPlaying = false;
                _playLines = null;
                _playCompletion = null;
                SetVisible(false, onDone: completion);
                return;
            }

            ShowPlayLine();
        }

        // ── IDialogueView ──────────────────────────────────────────────

        public void ShowDialogueSet(DialogueSet set)
        {
            EnsureInitialized();
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
