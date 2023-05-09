using StarWriter.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ThreeButtonPanel : MonoBehaviour
{
    public float fadeDuration = 0.2f;
    Image[] buttonImages;
    Color[] originalColors;
    bool opaque = false;

    void Start()
    {
        
        buttonImages = GetComponentsInChildren<Image>();
        originalColors = new Color[buttonImages.Length];

        for (int i = 0; i < buttonImages.Length; i++)
        {
            originalColors[i] = buttonImages[i].color;
        }
        FadeOutButtons();
    }    

    public void FadeOutButtons()
    {
        if (buttonImages != null && opaque)
        {
            opaque = false;
            for (int i = 0; i < buttonImages.Length; i++)
            {
                if (this.isActiveAndEnabled) StartCoroutine(FadeButton(buttonImages[i], originalColors[i], 0f));
            }
        }
    }

    public void FadeInButtons()
    {
        if (buttonImages != null && !opaque)
        {
            opaque = true;
            for (int i = 0; i < buttonImages.Length; i++)
            {
                if (this.isActiveAndEnabled) StartCoroutine(FadeButton(buttonImages[i], originalColors[i], 1f));
            }
        }
    }

    IEnumerator FadeButton(Image buttonImage, Color originalColor, float targetAlpha)
    {
        float timeElapsed = 0f;

        while (timeElapsed < fadeDuration)
        {
            timeElapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(buttonImage.color.a, targetAlpha, timeElapsed / fadeDuration);
            buttonImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }
        buttonImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, targetAlpha);
    }

}
