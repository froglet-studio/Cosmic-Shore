using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility.ScreenShots
{
    public class CaptureScreenShot : MonoBehaviour
    {
        public int supersize = 2;
        private int screenShotIndex = 0;

        private void Update()
        {
#if UNITY_EDITOR
            if (Input.GetKeyUp(KeyCode.C))
            {
                ScreenCapture.CaptureScreenshot($"Screenshot {screenShotIndex}.png", supersize);
                Debug.Log($"Screenshot {screenShotIndex}.png hass been taken.");
                
                screenShotIndex++;
            }
#endif
        }

        public void CaptureScreenShotToDisk(string path)
        {
            ScreenCapture.CaptureScreenshot($"{path}Screenshot {screenShotIndex}.png", supersize);
            Debug.Log($"CaptureScreenShot.CaptureScreenShotToDisk - {path}Screenshot {screenShotIndex}.png has been taken.");

            screenShotIndex++;
        }

        public Texture CaptureScreenShotAsTexture(int superSize)
        {
            Texture screenshotTexture = ScreenCapture.CaptureScreenshotAsTexture(superSize);
            Debug.Log($"CaptureScreenShot.CaptureScreenShotAsTexture superSize:{superSize}");
            return screenshotTexture;
        }

        public void CaptureScreenShotIntoRenderTexture(RenderTexture renderTexture)
        {
            ScreenCapture.CaptureScreenshotIntoRenderTexture(renderTexture);
            Debug.Log("Screenshot has been applied to RenderTexture.");
        }
    }
}


