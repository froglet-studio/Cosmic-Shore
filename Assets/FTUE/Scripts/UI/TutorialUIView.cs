using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Audio;

namespace CosmicShore.FTUE
{
    public class TutorialUIView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI textDisplay;
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

        private void Start()
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

        public void ShowStep(string text, bool showArrow, System.Action onComplete)
        {
            _fullText = "";

            gameObject.SetActive(true);
            _fullText = text;
            _onStepComplete = onComplete;
            arrowIndicator.SetActive(showArrow);

            if (_typingCoroutine != null)
                StopCoroutine(_typingCoroutine);
            _typingCoroutine = StartCoroutine(Typewriter());
        }

        private IEnumerator Typewriter()
        {
            Debug.Log($"[Typewriter] START, length={_fullText?.Length}");
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
                //Debug.Log($"[Typewriter] +'{c}' => \"{textDisplay.text}\"");
                yield return new WaitForSecondsRealtime(0.04f);
            }

            StopTypingAudio();
            _isTyping = false;
            Debug.Log("[Typewriter] COMPLETE");
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

            // 2) Always fire the completion callback
            _onStepComplete?.Invoke();
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
            // Show or hide visuals without deactivating GameObject
            _ftueCanvasGroup.alpha = visible ? 1f : 0f;
            _ftueCanvasGroup.interactable = visible;
            _ftueCanvasGroup.blocksRaycasts = visible;

            // Put it above or below other UI if you need
            var canvas = _ftueCanvas.GetComponent<Canvas>();
            canvas.sortingOrder = visible ? 100 : -1;
        }


    }
}
