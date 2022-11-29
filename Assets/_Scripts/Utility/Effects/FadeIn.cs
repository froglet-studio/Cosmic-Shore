using System.Collections;
using UnityEngine;

public class FadeIn : MonoBehaviour
{
    [SerializeField] float fadeInRate;

    Material material;

    void Start()
    {
        StartCoroutine(FadeInCoroutine());
        material = new Material(gameObject.GetComponent<Renderer>().material);
        gameObject.GetComponent<Renderer>().material = material;
    }

    public IEnumerator FadeInCoroutine()
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