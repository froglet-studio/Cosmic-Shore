using System.Collections;
using TMPro;
using UnityEngine;


namespace CosmicShore.Utilities
{
    public class NotificationUI : MonoBehaviour
    {
        const string DISPLAY = "Display";
        const string HIDE = "Hide";

        /*[SerializeField]
        Animator _animator;*/

        [SerializeField]
        TextMeshProUGUI _statusText;

        private void Start()
        {
            HidePanel();
        }

        public void DisplayStatus(string text, float secondsToWaitBeforeAutoClose)
        {
            _statusText.text = text;
            ShowPanel();

            if (secondsToWaitBeforeAutoClose > 0)
                StartCoroutine(ClosePanelAfterSeconds(secondsToWaitBeforeAutoClose));
        }

        IEnumerator ClosePanelAfterSeconds(float secondsToWaitBeforeAutoClose)
        {
            yield return new WaitForSeconds(secondsToWaitBeforeAutoClose);
            HidePanel();
        }

        private void ShowPanel()
        {
            // _animator.SetTrigger(DISPLAY);
            gameObject.SetActive(true);
        }

        private void HidePanel()
        {
            // _animator.SetTrigger(HIDE);
            gameObject.SetActive(false);
        }
    }

}
