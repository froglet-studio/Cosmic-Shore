using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class Pulse : MonoBehaviour
    {
        [SerializeField] Image image;
        [SerializeField] float angularFrequency = 1.5f; //frequency / (2 * PI)
        [SerializeField] float alphaFloor = .4f;

        void Start()
        {
            image = GetComponent<Image>();
        }

        void Update()
        {
            Color currentColor = image.color;
            float alpha = (Mathf.Sin(Time.timeSinceLevelLoad * angularFrequency) + 1)
                * ((1 - alphaFloor)/2) + alphaFloor; //sin wave ranges from alphaFloor to 1
            Color newColor = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);
            image.color = newColor;
        }
    }
}
