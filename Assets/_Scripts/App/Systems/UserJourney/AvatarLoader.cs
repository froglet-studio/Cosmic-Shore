using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace CosmicShore.App.Systems.UserJourney
{
    public class AvatarLoader
    {
        public UnityWebRequest WebRequest { get; private set; } 
        // the UI can use the following code to load the web request
        // example:
        // Texture2D iconTexture = DownloadHandlerTexture.GetContent(WebRequest);
        // Sprite iconSprite = Sprite.Create(iconTexture, new Rect(0, 0, iconTexture.width, iconTexture.height), Vector2.zero);
        // iconImage.sprite = iconSprite;

        // The UI can use coroutine to laod a list of urls without being blocked
        public IEnumerator Load(string url)
        {
            WebRequest = UnityWebRequestTexture.GetTexture(url);
            yield return WebRequest.SendWebRequest();
            if (WebRequest.error != null)
            {
                Debug.LogError("Error loading icon: " + WebRequest.error);
            }
        }
    }
}
