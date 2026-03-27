using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.DialogueSystem.Models;
using Obvious.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.DialogueSystem.Controller
{
    /// <summary>
    /// Plays tutorial sequences linearly. Subscribes to SOAP trigger events
    /// and drives the UI — typewriter text, avatar, arrows, Next button.
    /// Each instruction can optionally wait for its own SOAP event before showing.
    /// </summary>
    public class TutorialSequencePlayer : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private TutorialSequenceLibrary library;

        [Header("UI References")]
        [SerializeField] private GameObject tutorialCanvas;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Button nextButton;
        [SerializeField] private AudioSource audioSource;

        [Header("Arrow References")]
        [Tooltip("Map ArrowTarget enum values to GameObjects that display arrows.")]
        [SerializeField] private List<ArrowMapping> arrowMappings = new();

        [Header("Typewriter Settings")]
        [SerializeField] private float charDelay = 0.04f;

        private TutorialSequence _activeSequence;
        private int _currentIndex;
        private Coroutine _typewriterCoroutine;
        private bool _isTyping;
        private string _fullText;
        private CanvasGroup _canvasGroup;
        private readonly Dictionary<ScriptableEventNoParam, Action> _eventBindings = new();
        private Action _pendingInstructionHandler;
        private ScriptableEventNoParam _pendingEvent;

        [Serializable]
        public class ArrowMapping
        {
            public ArrowTarget target;
            public GameObject arrowObject;
        }

        private void Awake()
        {
            nextButton.onClick.AddListener(OnNextPressed);

            if (tutorialCanvas.TryGetComponent(out CanvasGroup cg))
                _canvasGroup = cg;
            else
                _canvasGroup = tutorialCanvas.AddComponent<CanvasGroup>();

            SetCanvasVisible(false);
        }

        private void OnEnable()
        {
            if (library == null) return;
            foreach (var seq in library.sequences)
            {
                if (seq == null || seq.triggerEvent == null) continue;
                var captured = seq;
                Action handler = () => PlaySequence(captured);
                _eventBindings[seq.triggerEvent] = handler;
                seq.triggerEvent.OnRaised += handler;
            }
        }

        private void OnDisable()
        {
            foreach (var kvp in _eventBindings)
                kvp.Key.OnRaised -= kvp.Value;
            _eventBindings.Clear();
            CleanupPendingEvent();
        }

        /// <summary>
        /// Start playing a tutorial sequence from the beginning.
        /// </summary>
        public void PlaySequence(TutorialSequence sequence)
        {
            if (sequence == null || sequence.instructions.Count == 0) return;

            _activeSequence = sequence;
            _currentIndex = 0;
            SetCanvasVisible(true);
            ShowCurrentInstruction();
        }

        /// <summary>
        /// Play a sequence by its ID from the library.
        /// </summary>
        public void PlaySequenceById(string sequenceId)
        {
            if (library == null) return;
            var seq = library.GetSequenceById(sequenceId);
            if (seq != null) PlaySequence(seq);
        }

        private void ShowCurrentInstruction()
        {
            if (_activeSequence == null) return;
            if (_currentIndex >= _activeSequence.instructions.Count)
            {
                FinishSequence();
                return;
            }

            var instruction = _activeSequence.instructions[_currentIndex];

            // Check if this instruction waits for a SOAP event
            if (instruction.waitForEvent != null)
            {
                // Hide canvas while waiting
                SetCanvasVisible(false);
                HideAllArrows();

                // Subscribe to the event
                CleanupPendingEvent();
                _pendingEvent = instruction.waitForEvent;
                _pendingInstructionHandler = () =>
                {
                    CleanupPendingEvent();
                    DisplayInstruction(instruction);
                };
                _pendingEvent.OnRaised += _pendingInstructionHandler;
                return;
            }

            DisplayInstruction(instruction);
        }

        private void DisplayInstruction(TutorialInstruction instruction)
        {
            SetCanvasVisible(true);

            // Avatar
            if (avatarImage != null)
            {
                if (instruction.avatarIcon != null)
                {
                    avatarImage.sprite = instruction.avatarIcon;
                    avatarImage.gameObject.SetActive(true);
                }
                else
                {
                    avatarImage.gameObject.SetActive(false);
                }
            }

            // Arrows
            UpdateArrows(instruction.arrowTargets);

            // Audio
            if (audioSource != null && instruction.audioClip != null)
            {
                audioSource.clip = instruction.audioClip;
                audioSource.Play();
            }

            // Typewriter text
            _fullText = instruction.dialogueText ?? "";
            if (_typewriterCoroutine != null)
                StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = StartCoroutine(TypewriterRoutine(instruction));
        }

        private void CleanupPendingEvent()
        {
            if (_pendingEvent != null && _pendingInstructionHandler != null)
            {
                _pendingEvent.OnRaised -= _pendingInstructionHandler;
            }
            _pendingEvent = null;
            _pendingInstructionHandler = null;
        }

        private IEnumerator TypewriterRoutine(TutorialInstruction instruction)
        {
            _isTyping = true;
            dialogueText.text = "";

            foreach (char c in _fullText)
            {
                dialogueText.text += c;
                yield return new WaitForSecondsRealtime(charDelay);
            }

            _isTyping = false;

            if (instruction.autoAdvance)
            {
                if (instruction.delayBeforeNext > 0f)
                    yield return new WaitForSecondsRealtime(instruction.delayBeforeNext);
                AdvanceToNext();
            }
        }

        private void OnNextPressed()
        {
            if (_isTyping)
            {
                // Complete the text immediately
                if (_typewriterCoroutine != null)
                    StopCoroutine(_typewriterCoroutine);
                dialogueText.text = _fullText;
                _isTyping = false;

                if (audioSource != null && audioSource.isPlaying)
                    audioSource.Stop();
            }
            else
            {
                AdvanceToNext();
            }
        }

        private void AdvanceToNext()
        {
            _currentIndex++;
            if (_currentIndex >= _activeSequence.instructions.Count)
                FinishSequence();
            else
                ShowCurrentInstruction();
        }

        private void FinishSequence()
        {
            SetCanvasVisible(false);
            HideAllArrows();
            CleanupPendingEvent();

            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();

            // Raise completion event for chaining
            if (_activeSequence.completionEvent != null)
                _activeSequence.completionEvent.Raise();

            _activeSequence = null;
        }

        private void UpdateArrows(ArrowTarget targets)
        {
            foreach (var mapping in arrowMappings)
            {
                if (mapping.arrowObject != null)
                    mapping.arrowObject.SetActive(targets.HasFlag(mapping.target));
            }
        }

        private void HideAllArrows()
        {
            foreach (var mapping in arrowMappings)
            {
                if (mapping.arrowObject != null)
                    mapping.arrowObject.SetActive(false);
            }
        }

        private void SetCanvasVisible(bool visible)
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }
}
