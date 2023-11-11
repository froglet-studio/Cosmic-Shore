using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using CosmicShore.Utility.Tools;

public class PipUI : MonoBehaviour
{

    bool isSmall = true;
    public bool mirrored = true;

    [SerializeField] Vector3 smallScale = new Vector3(1, 1, 1);
    [SerializeField] Vector3 largeScale = new Vector3(2, 2, 1);
    [SerializeField] Vector3 smallPosition = new Vector3(0, 445, 0);
    [SerializeField] Vector3 largePosition = new Vector3(0, 375, 0);


    private void Start()
    {
        SetMirrored(mirrored);
    }

    public void SetMirrored(bool mirrored)
    {
        if (mirrored) smallScale = new Vector3(-smallScale.x, smallScale.y, smallScale.z);
        if (mirrored) largeScale = new Vector3(-largeScale.x, largeScale.y, largeScale.z);
        ((RectTransform)transform).localScale = smallScale;
        ((RectTransform)transform).localPosition = smallPosition;
    }

    public void ToggleSizeAndPosition()
    {
        Debug.Log("pip button pressed");
        isSmall= !isSmall;

        // negative x is to get the mirror image without loosing the raycast target. the y values are whack and we don't know why.
        if (isSmall)
        {
            StartCoroutine(Tools.LerpingCoroutine(((RectTransform)transform).localScale, smallScale, .5f, (i) => { ((RectTransform)transform).localScale = i;}));
            StartCoroutine(Tools.LerpingCoroutine(((RectTransform)transform).localPosition, smallPosition, .5f, (i) => { ((RectTransform)transform).localPosition = i; }));
        }
        else
        {
            StartCoroutine(Tools.LerpingCoroutine(((RectTransform)transform).localScale, largeScale, .5f, (i) => { ((RectTransform)transform).localScale = i; }));
            StartCoroutine(Tools.LerpingCoroutine(((RectTransform)transform).localPosition, largePosition, .5f, (i) => { ((RectTransform)transform).localPosition = i; }));
        }
        
    }
}
