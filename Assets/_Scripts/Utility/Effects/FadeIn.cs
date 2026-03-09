using System.Collections;
using UnityEngine;

public class FadeIn : MonoBehaviour
{
    [SerializeField] float fadeInRate;

    private static readonly int OpacityID = Shader.PropertyToID("_opacity");

    Material material;
    Renderer cachedRenderer;
    Coroutine fadeInCoroutine;

    void Start()
    {
        cachedRenderer = GetComponent<Renderer>();
        material = new Material(cachedRenderer.material);
        cachedRenderer.material = material;
        StartFadeIn();
    }

    public void StartFadeIn()
    {
        if (cachedRenderer == null)
            cachedRenderer = GetComponent<Renderer>();

        cachedRenderer.material.SetFloat(OpacityID, 0f);

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
            material.SetFloat(OpacityID, opacity);
        }
    }
}
