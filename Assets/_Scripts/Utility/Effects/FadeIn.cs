using System.Collections;
using UnityEngine;

public class FadeIn : MonoBehaviour
{
    [SerializeField]
    private float opacity;

    [SerializeField]
    private float fadeInRate;

    //[SerializeField]
    Material MutonMaterial;

    void Start()
    {
        StartCoroutine(FadeInCoroutine());
        MutonMaterial = new Material(gameObject.GetComponent<Renderer>().material);
        gameObject.GetComponent<Renderer>().material = MutonMaterial;
    }

    public IEnumerator FadeInCoroutine()
    {
        
        fadeInRate = .001f;
        opacity = 0;
        while (opacity < 1)
        {
            yield return null;
            fadeInRate *= 1.00f + Time.deltaTime;
            opacity += fadeInRate;
            gameObject.GetComponent<Renderer>().material.SetFloat("_opacity", opacity);
        }
    }
}