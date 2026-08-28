using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Utility
{
    public class ShareByEmail //: MonoBehaviour
    {
        public string subject = ""; //Subject line of email
        public string text = "";    //Content of email
        public string recipient = "support@frogletgames.zendesk.com"; // Default Recipient of email's addresses 

        
        public ShareByEmail(string subject, string text, string recipient) 
        { 
            this.subject = subject;
            this.text = text;
            this.recipient = recipient;
        }

        public void SendEmail()
        {
            // NativeShare has no desktop implementation - on a Steam build it silently does nothing,
            // so the support button would look broken. Hand desktop players their mail client.
            if (DesktopPlatformServices.IsDesktop)
            {
                DesktopPlatformServices.TryOpenMailClient(recipient, subject, text);
                return;
            }

            NativeShare nativeShare = new();

            // Set email
            nativeShare.AddEmailRecipient(recipient);
            nativeShare.SetSubject(subject);
            nativeShare.SetText(text);
            nativeShare.SetCallback(HelpEmailCallback);
            // Share the email
            nativeShare.Share();
        }

        public void SendEmailwithAttachment(string attachmentPath)
        {
            // Desktop mail clients cannot be handed an attachment through mailto:, so put the file
            // somewhere the player can find it and open the composer alongside.
            if (DesktopPlatformServices.IsDesktop)
            {
                string saved = DesktopPlatformServices.SaveAndReveal(attachmentPath);
                string body = string.IsNullOrEmpty(saved)
                    ? text
                    : $"{text}\n\n(Attachment saved to: {saved} - please attach it to this email.)";
                DesktopPlatformServices.TryOpenMailClient(recipient, subject, body);
                return;
            }

            NativeShare nativeShare = new NativeShare();

            // Set email
            nativeShare.AddEmailRecipient(recipient);
            nativeShare.SetSubject(subject);
            nativeShare.SetText(text);
            nativeShare.AddFile(attachmentPath);
            nativeShare.SetCallback(HelpEmailCallback);
            // Share the email
            nativeShare.Share();
        }

        void HelpEmailCallback(NativeShare.ShareResult result, string shareTarget)
        {
            CSDebug.Log("Send Email - Result: " + result.ToString());
            CSDebug.Log("Send Email - shareTarget: " + shareTarget);

            // TODO Give the player a thumbs up if the result was successful
        }
    }
}