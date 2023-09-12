using UnityEngine;
using System.Collections;

namespace StarWriter.Utility.Email
{
    public class ShareByEmail : MonoBehaviour
    {
        public string subject = "";
        public string text = "";
        public string recipient = "support@frogletgames.zendesk.com"; // Recipient email addresses 


        // TODO: Attachments of screen shots (optional)
        public string[] attachmentPaths; // Add paths to attachments here 

        public void Start()
        {
            //Testing Only
            SetEmailSubjectLine("This is a ingame test!");
            SetEmailBoby("Testing a long string ...........................................................................................................................................................................");
            SendEmail();
        }

        public void SetEmailSubjectLine(string subjectline)
        {
            subject = subjectline;
        }

        public void SetEmailBoby(string body)
        {
            text = body;
        }

        public void SendEmail()
        {

            NativeShare nativeShare = new NativeShare();

            // Set email
            nativeShare.AddEmailRecipient(recipient);
            nativeShare.SetSubject(subject);
            nativeShare.SetText(text);

            // Add attachments (if any)
            foreach (string attachmentPath in attachmentPaths)
            {

                nativeShare.AddFile(attachmentPath);
            }


            // Share the email
            nativeShare.Share();
        }
    }
}



