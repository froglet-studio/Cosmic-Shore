using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Utility.ScreenShots
{
    public class CaptureScreenShot : MonoBehaviour
    {
        public int supersize = 2;
        private int screenShotIndex = 0;


        private void Update()
        {
            if (Input.GetKeyUp(KeyCode.C))
            {
                ScreenCapture.CaptureScreenshot($"Screenshot {screenShotIndex}.png", supersize);
                screenShotIndex++;
                Debug.Log($"Screenshot {screenShotIndex}.png hass been taken.");
            }
        }

        public void CaptureScreenShotToDisk(string path)
        {
            ScreenCapture.CaptureScreenshot($"{path}Screenshot {screenShotIndex}.png", supersize);
            screenShotIndex++;
            Debug.Log($"{path}Screenshot {screenShotIndex}.png has been taken.");
        }

        public Texture CaptureScreenShotAsTexture(int superSize)
        {
            Texture screenshotTexture = ScreenCapture.CaptureScreenshotAsTexture(superSize);
            return screenshotTexture;
            
            Debug.Log("Screenshot texture has been created.");
        }

        public void CaptureScreenShotIntoRenderTexture(RenderTexture renderTexture)
        {
            ScreenCapture.CaptureScreenshotIntoRenderTexture(renderTexture);

            Debug.Log("Screenshot has been applied to RenderTexture.");
        }

        public void DeleteAllScreenShots(string path)
        {
            //TODO 
        }


    }
}


