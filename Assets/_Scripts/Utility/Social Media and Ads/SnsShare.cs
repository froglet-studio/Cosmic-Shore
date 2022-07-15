using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SnsShare : MonoBehaviour
{
    public void Share()
    {
        StartCoroutine(TakeScreenshotAndShare());
    }

    private IEnumerator TakeScreenshotAndShare()
    {
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
    }
}
