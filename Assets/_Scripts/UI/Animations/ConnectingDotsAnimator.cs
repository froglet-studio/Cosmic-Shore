using System.Collections;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Animates trailing dots after a base text string (e.g. "CONNECTING TO SHORE...").
    /// </summary>
    public class ConnectingDotsAnimator : MonoBehaviour
    {
        [SerializeField] private TMP_Text textDisplay;
        [SerializeField] private float dotInterval = 0.4f;
        [SerializeField] private int maxDots = 3;

        private string _baseText = "CONNECTING";
        private Coroutine _animCoroutine;

        public string BaseText
        {
            get => _baseText;
            set => _baseText = value;
        }

        public void StartAnimation()
        {
            StopAnimation();
            if (textDisplay != null)
                _animCoroutine = StartCoroutine(AnimateDots());
        }

        public void StopAnimation()
        {
            if (_animCoroutine != null)
            {
                StopCoroutine(_animCoroutine);
                _animCoroutine = null;
            }
        }

        private IEnumerator AnimateDots()
        {
            int dotCount = 0;
            while (true)
            {
                dotCount = (dotCount % maxDots) + 1;
                textDisplay.text = _baseText + new string('.', dotCount);
                yield return new WaitForSecondsRealtime(dotInterval);
            }
        }

        private void OnDisable()
        {
            StopAnimation();
        }
    }
}
