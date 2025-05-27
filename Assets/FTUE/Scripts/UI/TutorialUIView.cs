using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using CosmicShore.Events;

namespace CosmicShore.FTUE
{
    public class TutorialUIView : MonoBehaviour
    {
        [SerializeField] internal TextMeshProUGUI textDisplay;
        [SerializeField] private GameObject arrowIndicator;
        [SerializeField] private Button nextButton;
        [SerializeField] internal Button skipButton;
        [SerializeField] private AudioSource typingAudioSource;
        [SerializeField] private AudioClip typingLoopClip;
        [SerializeField] internal GameObject _ftueCanvas;

        private string _fullText;
        private Coroutine _typingCoroutine;
        private bool _isTyping = false;
        private System.Action _onStepComplete;

        private CanvasGroup _ftueCanvasGroup;

        private void Awake()
        {
            nextButton.onClick.AddListener(OnNextPressed);
            skipButton.onClick.AddListener(() => _onStepComplete?.Invoke());
        }

        private void Start() => AddOrGetCanvasGroup();

        public void ShowStep(string text,  System.Action onComplete)
        {
            textDisplay.text = "";

            gameObject.SetActive(true);
            _fullText = text;
            _onStepComplete = onComplete;

            if (_typingCoroutine != null)
                StopCoroutine(_typingCoroutine);
            _typingCoroutine = StartCoroutine(Typewriter());
        }

        private IEnumerator Typewriter()
        {
            _isTyping = true;
            textDisplay.text = "";

            if (typingAudioSource && typingLoopClip)
            {
                typingAudioSource.clip = typingLoopClip;
                typingAudioSource.loop = false;
                typingAudioSource.Play();
            }

            foreach (char c in _fullText)
            {
                textDisplay.text += c;
                yield return new WaitForSecondsRealtime(0.04f);
            }

            StopTypingAudio();
            _isTyping = false;
        }

        private void OnNextPressed()
        {
            if (_isTyping)
            {
                StopCoroutine(_typingCoroutine);
                StopTypingAudio();
                textDisplay.text = _fullText;
                _isTyping = false;
            }

            _onStepComplete?.Invoke();
            FTUEEventManager.RaiseNextPressed();
        }

        private void StopTypingAudio()
        {
            if (typingAudioSource && typingAudioSource.isPlaying)
            {
                typingAudioSource.Stop();
                typingAudioSource.clip = null;
            }
        }

        public void ToggleFTUECanvas(bool visible)
        {
            if (_ftueCanvasGroup == null)
            {
                AddOrGetCanvasGroup();
            }

            _ftueCanvasGroup.alpha = visible ? 1f : 0f;
            _ftueCanvasGroup.interactable = visible;
            _ftueCanvasGroup.blocksRaycasts = visible;

            // Put it above or below other UI if you need
            var canvas = _ftueCanvas.GetComponent<Canvas>();
            canvas.sortingOrder = visible ? 100 : -1;
        }

        private void AddOrGetCanvasGroup()
        {
            if (_ftueCanvas.GetComponent<CanvasGroup>() == null)
            {
                _ftueCanvasGroup = _ftueCanvas.AddComponent<CanvasGroup>();
            }
            else
            {
                _ftueCanvasGroup = _ftueCanvas.GetComponent<CanvasGroup>();
            }
        }

    }
}
