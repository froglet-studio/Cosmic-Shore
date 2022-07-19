using System.Collections;
using UnityEngine.UI;
using UnityEngine;
/// <summary>
/// Requires Native Share to function in Unity
/// </summary>
public class SnsShare : MonoBehaviour
{
    public Button screenshotButton; 
    public void Share()
    {
        StartCoroutine(TakeScreenshotAndShare());
    }

    private IEnumerator TakeScreenshotAndShare()
    {
        screenshotButton.gameObject.SetActive(false);
        yield return new WaitForEndOfFrame();

        Texture2D ss = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        ss.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);

        string filePath = System.IO.Path.Combine(Application.temporaryCachePath, "share.png");
        System.IO.File.WriteAllBytes(filePath, ss.EncodeToPNG());

        Destroy(ss); // Must destroy to prevent memory leaks

        new NativeShare().AddFile(filePath)
            .SetSubject("").SetText("").SetUrl("")
            .SetCallback((res, target) => Debug.Log($"result {res}, target app: {target}"))
            .Share();
        screenshotButton.gameObject.SetActive(true);
    }
}
