using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;



namespace CosmicShore.Utilities
{
    public class ClientLoadingScreen : MonoBehaviour
    {
        [SerializeField]
        CanvasGroup m_CanvasGroup;

        [SerializeField]
        float m_DelayBeforeFadeOut = 0.5f;

        [SerializeField]
        float m_FadeOutDuration = 0.1f;

        [SerializeField]
        TextMeshProUGUI m_SceneNameText;

        [SerializeField]
        Slider _slider;

        /// <summary>
        /// This is the AsyncOperation of the current load operation. This property should be set each time a new
        /// loading operation begins.
        /// </summary>
        public AsyncOperation LocalLoadOperation
        {
            set
            {
                m_LoadingScreenRunning = true;
                _slider.value = 0;
                m_LocalLoadOperation = value;
            }
        }

        AsyncOperation m_LocalLoadOperation;

        bool m_LoadingScreenRunning;

        Coroutine m_FadeOutCoroutine;

        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        void Start()
        {
            SetCanvasVisibility(false);
        }

        void Update()
        {
            if (m_LocalLoadOperation != null && m_LoadingScreenRunning)
            {
                if (m_LocalLoadOperation.isDone)
                {
                    m_LoadingScreenRunning = false;
                    _slider.value = 1;
                }
                else
                {
                    _slider.value = m_LocalLoadOperation.progress;
                }
            }
        }

        public void StartLoadingScreen(string sceneName)
        {
            SetCanvasVisibility(true);
            m_LoadingScreenRunning = true;
            UpdateLoadingScreen(sceneName);
        }

        public void UpdateLoadingScreen(string sceneName)
        {
            if (m_LoadingScreenRunning)
            {
                m_SceneNameText.text = sceneName;
                if (m_FadeOutCoroutine != null)
                {
                    StopCoroutine(m_FadeOutCoroutine);
                }
            }
        }

        public void StopLoadingScreen()
        {
            if (m_LoadingScreenRunning)
            {
                if (m_FadeOutCoroutine != null)
                {
                    StopCoroutine(m_FadeOutCoroutine);
                }
                m_FadeOutCoroutine = StartCoroutine(FadeOutCoroutine());
            }
        }

        IEnumerator FadeOutCoroutine()
        {
            yield return new WaitForSecondsRealtime(m_DelayBeforeFadeOut);
            m_LoadingScreenRunning = false;

            float currentTime = 0;
            while (currentTime < m_FadeOutDuration)
            {
                m_CanvasGroup.alpha = Mathf.Lerp(1, 0, currentTime / m_FadeOutDuration);
                yield return null;
                currentTime += Time.unscaledDeltaTime;
            }

            SetCanvasVisibility(false);
        }

        void SetCanvasVisibility(bool visible)
        {
            m_CanvasGroup.alpha = visible ? 1 : 0;
            m_CanvasGroup.blocksRaycasts = visible;
        }
    }
}
