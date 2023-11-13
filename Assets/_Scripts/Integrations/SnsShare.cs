using CosmicShore.Game.IO;
using System.Collections;
using UnityEngine.UI;
using UnityEngine;


/// <summary>
/// Requires Native Share to function in Unity
/// </summary>
public class SnsShare : MonoBehaviour
{
    public Button screenshotButton;
    public Button replayButton;
    public GameObject VersionTMP;

    public void Share()
    {
        StartCoroutine(TakeScreenshotAndShare());
    }

    private IEnumerator TakeScreenshotAndShare()
    {
        screenshotButton.gameObject.SetActive(false);
        //replayButton.gameObject.SetActive(false);
        VersionTMP.SetActive(true);

        yield return new WaitForEndOfFrame();

        Texture2D ss = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        ss.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);

        if (InputController.currentOrientation == ScreenOrientation.LandscapeRight)
        {
            Color[] pixels = ss.GetPixels();
            System.Array.Reverse(pixels);
            ss.SetPixels(pixels);
            ss.Apply();
        }
        
        string filePath = System.IO.Path.Combine(Application.temporaryCachePath, "share.png");
        System.IO.File.WriteAllBytes(filePath, ss.EncodeToPNG());

        Destroy(ss); // Must destroy to prevent memory leaks

        Screen.orientation = InputController.currentOrientation;

        new NativeShare().AddFile(filePath)
            .SetSubject("").SetText("").SetUrl("")
            .SetCallback((res, target) => { Debug.Log($"result {res}, target app: {target}"); Screen.orientation = ScreenOrientation.LandscapeLeft; })
            .Share();
        screenshotButton.gameObject.SetActive(true);
        //replayButton.gameObject.SetActive(true);
        VersionTMP.SetActive(false);
    }
}