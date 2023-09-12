using UnityEngine;

public class SendBugReportEmail : MonoBehaviour
{
    public void SendToEmailClient()
    {
        Debug.Log("SendToEmailClient - Start");

        NativeShare nativeShare = new NativeShare();
        nativeShare.AddEmailRecipient("isaiah@froglet.studio");
        nativeShare.SetTitle("Title text shown to player in the widget selection window");
        nativeShare.SetText("Default body of the email");
        nativeShare.SetSubject("Default subject of the email");
        nativeShare.SetCallback(HelpEmailCallback);
        nativeShare.Share();

        Debug.Log("SendToEmailClient - End");
    }

    void HelpEmailCallback(NativeShare.ShareResult result, string shareTarget)
    {
        Debug.Log("SendToEmailClient - Result: " + result.ToString());
        Debug.Log("SendToEmailClient - shareTarget: " + shareTarget);

        // Give the player a thumbs up if the result was successful
    }
}