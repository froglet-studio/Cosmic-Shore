using UnityEngine;
using TMPro;
using System.Collections;

namespace CosmicShore.FTUE
{
    public class TypewriterText : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI textTarget;
        [SerializeField] private float charDelay = 0.02f;

        private Coroutine _typingCoroutine;
        private string _fullText;
        private bool _isTyping = false;

        public bool IsTyping => _isTyping;

        public void StartTyping(string text)
        {
            _fullText = text;
            if (_typingCoroutine != null)
                StopCoroutine(_typingCoroutine);

            _typingCoroutine = StartCoroutine(TypeText());
        }

        public void SkipToFullText()
        {
            if (_typingCoroutine != null)
                StopCoroutine(_typingCoroutine);

            textTarget.text = _fullText;
            _isTyping = false;
        }

        private IEnumerator TypeText()
        {
            _isTyping = true;
            textTarget.text = "";

            foreach (char c in _fullText)
            {
                textTarget.text += c;
                yield return new WaitForSeconds(charDelay);
            }

            _isTyping = false;
        }
    }
}
