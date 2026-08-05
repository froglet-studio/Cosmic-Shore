using CosmicShore.Gameplay;
using System.Collections;
using UnityEngine.UI;
using UnityEngine;
using CosmicShore.Utility;
using Reflex.Attributes;
using System.Linq;
using System.IO;

namespace CosmicShore.Core
{
    /// <summary>
    /// Requires Native Share to function in Unity
    /// </summary>
    public class SnsShare : MonoBehaviour
    {
        public Button screenshotButton;
        public Button replayButton;
        public GameObject VersionTMP;

        [Inject] AnalyticsServiceFacade _analytics;
        [Inject] GameDataSO _gameData;

        public void Share()
        {
            _analytics?.RecordShareTriggered(_gameData != null ? _gameData.GameMode.ToString() : null);
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

            if (IInputStatus.CurrentOrientation == ScreenOrientation.LandscapeRight)
            {
                Color[] pixels = ss.GetPixels();
                System.Array.Reverse(pixels);
                ss.SetPixels(pixels);
                ss.Apply();
            }
        
            string filePath = System.IO.Path.Combine(Application.temporaryCachePath, "share.png");
            System.IO.File.WriteAllBytes(filePath, ss.EncodeToPNG());

            Destroy(ss); // Must destroy to prevent memory leaks

            Screen.orientation = IInputStatus.CurrentOrientation;

            if (DesktopPlatformServices.IsDesktop)
            {
                // No share sheet on desktop: drop the shot in the share folder and open it so the
                // player can post it themselves, instead of the button appearing to do nothing.
                DesktopPlatformServices.SaveAndReveal(
                    filePath, DesktopPlatformServices.TimestampedName("cosmic-shore", "png"));
            }
            else
            {
                new NativeShare().AddFile(filePath)
                    .SetSubject("").SetText("").SetUrl("")
                    .SetCallback((res, target) => { CSDebug.Log($"result {res}, target app: {target}"); Screen.orientation = ScreenOrientation.LandscapeLeft; })
                    .Share();
            }
            screenshotButton.gameObject.SetActive(true);
            //replayButton.gameObject.SetActive(true);
            VersionTMP.SetActive(false);
        }
    }
}
