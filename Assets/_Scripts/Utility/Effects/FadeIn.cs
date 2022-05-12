using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FadeIn : MonoBehaviour
{
    [SerializeField]
    Material MutonMaterial;


    private float effect = .01f;


    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(FadeInCoroutine());
    }

    public IEnumerator FadeInCoroutine()
    {
        effect = .01f;
        while (effect <= 1)
        {
            yield return new WaitForSeconds(.001f);
            effect += 0.04f;
            MutonMaterial.SetFloat("_opacity", effect);
        }

    }
}