using System.Collections;
using UnityEngine;

public class FadeIn : MonoBehaviour
{
    [SerializeField]
    private float effect = .01f;

    [SerializeField]
    private float fadeInRate = 0.04f;

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
        effect = .01f;
        while (effect <= 1)
        {
            yield return null;
            effect += (fadeInRate * Time.deltaTime);
            gameObject.GetComponent<Renderer>().material.SetFloat("_opacity", effect);
        }
    }
}