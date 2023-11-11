using UnityEngine;
using CosmicShore.Utility.Email;
using UnityEngine.UI;
using TMPro;

namespace CosmicShore.Utility.Reports
{
    public class SendBugReport : MonoBehaviour
    {
        public GameObject bugReportPopUpWindow;
        public Button sendButton;
        public Button returnButton;
        public TMP_InputField subjectInputField;
        public TMP_InputField contentsInputField;
        [SerializeField] string recipient = "support@frogletgames.zendesk.com";  //for support reporting though Zendesk

        private void LateUpdate()
        {
            WaitOnInputFields();
        }

        public void OnButtonPushedEnableBugReportPopUp()
        {
            bugReportPopUpWindow.SetActive(true);
            sendButton.enabled = false;
        }
        public void OnButtonPushedDisableBugReportPopUp()
        {
            bugReportPopUpWindow.SetActive(false);
        }


        public void OnButtonPushedSendBugReport()
        {
            ShareByEmail shareByEmail = new ShareByEmail(subjectInputField.text, contentsInputField.text, recipient);
            shareByEmail.SendEmail();

            bugReportPopUpWindow.SetActive(false);
            sendButton.enabled = false;
        }

        public void WaitOnInputFields()
        {
            if(subjectInputField.text == "" || contentsInputField.text == "")
            {
                sendButton.enabled = false;
                return;
            }
            sendButton.enabled = true;
        }
    }
}