using UnityEngine;
using CosmicShore.Utility;
using UnityEngine.UI;
using TMPro;

namespace CosmicShore.Utility
{
    public class SendBugReport : MonoBehaviour
    {
        [SerializeField] GameObject bugReportPopUpWindow;
        public GameObject BugReportPopUpWindow => bugReportPopUpWindow;
        [SerializeField] Button sendButton;
        public Button SendButton => sendButton;
        [SerializeField] Button returnButton;
        public Button ReturnButton => returnButton;
        [SerializeField] TMP_InputField subjectInputField;
        public TMP_InputField SubjectInputField => subjectInputField;
        [SerializeField] TMP_InputField contentsInputField;
        public TMP_InputField ContentsInputField => contentsInputField;
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