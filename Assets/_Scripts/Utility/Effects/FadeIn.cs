using System.Collections;
using UnityEngine;

public class FadeIn : MonoBehaviour
{
    [SerializeField] float fadeInRate;

    Material material;
    Coroutine fadeInCoroutine;

    void Start()
    {
        StartFadeIn();
        material = new Material(gameObject.GetComponent<Renderer>().material);
        gameObject.GetComponent<Renderer>().material = material;
    }

    public void StartFadeIn()
    {
        // Set the opacity to zero before starting the coroutine so there is no delay in the start of the effect
        gameObject.GetComponent<Renderer>().material.SetFloat("_opacity", 0f);

        if (fadeInCoroutine != null)
            StopCoroutine(fadeInCoroutine);

        fadeInCoroutine = StartCoroutine(FadeInCoroutine());
    }

    IEnumerator FadeInCoroutine()
    {
        fadeInRate = .001f;
        var opacity = 0f;
        while (opacity < 1)
        {
            yield return null;
            fadeInRate *= 1.00f + Time.deltaTime;
            opacity += fadeInRate;
            gameObject.GetComponent<Renderer>().material.SetFloat("_opacity", opacity);
        }
    }
}