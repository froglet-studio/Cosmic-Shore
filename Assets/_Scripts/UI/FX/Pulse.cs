using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.FX
{
    public class Pulse : MonoBehaviour
    {
        Image image;
        [SerializeField] float angularFrequency = 1.5f; //frequency / (2 * PI)
        [SerializeField] float alphaFloor = 0f;

        void Start()
        {
            image = GetComponent<Image>();
        }

        void Update()
        {
            Color currentColor = image.color;
            float alpha = (Mathf.Sin(Time.unscaledTime * angularFrequency) + 1)
                * ((1 - alphaFloor)/2) + alphaFloor; //sin wave ranges from alphaFloor to 1
            Color newColor = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);
            image.color = newColor;
        }
    }
}