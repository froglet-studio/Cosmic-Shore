using System.Collections;
using UnityEngine;

public class FadeIn : MonoBehaviour
{
    [SerializeField]
    Material MutonMaterial;

    [SerializeField]
    private float effect = .01f;

    [SerializeField]
    private float fadeInRate = 0.04f;

    void Start()
    {
        StartCoroutine(FadeInCoroutine());
    }

    public IEnumerator FadeInCoroutine()
    {
        effect = .01f;
        while (effect <= 1)
        {
            yield return null;
            effect += fadeInRate;
            MutonMaterial.SetFloat("_opacity", effect);
        }
    }
}