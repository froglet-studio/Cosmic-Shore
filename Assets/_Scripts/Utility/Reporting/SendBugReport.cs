using UnityEngine;
using StarWriter.Utility.Email;
using UnityEngine.UI;
using TMPro;

namespace StarWriter.Utility.Reports
{
    public class SendBugReport : MonoBehaviour
    {
        public GameObject bugReportPopUpWindow;
        public Button sendButton;
        public TMP_InputField subjectInputField;
        public TMP_InputField contentsInputField;
        public string recipient = "support@frogletgames.zendesk.com";  //for support reporting though Zendesk

        public void OnButtonPushedBugReportPopUp()
        {
            bugReportPopUpWindow.SetActive(true);
        }
        
        public void OnButtonPushedSendBugReport()
        {
            ShareByEmail shareByEmail = new ShareByEmail(subjectInputField.text, contentsInputField.text, recipient);
            shareByEmail.SendEmail();

            bugReportPopUpWindow.SetActive(false);
            sendButton.enabled = false;
        }
    }
}