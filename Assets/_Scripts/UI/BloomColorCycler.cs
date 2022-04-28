using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BloomColorCycler : MonoBehaviour
{
    [SerializeField]
    Bloom Bloom;

    [SerializeField]
    float BloomCycleIncrementAmount = .004f;

    // Start is called before the first frame update
    void Start()
    {
        var v = GetComponent<Volume>();
        v.profile.TryGet(out Bloom);
    }

    // Update is called once per frame
    void Update()
    {
        float h, s, v;
        var tint = Bloom.tint;
        Color.RGBToHSV(tint.value, out h, out s, out v);
        Bloom.tint.value = Color.HSVToRGB(h + BloomCycleIncrementAmount, s, v);
    }
}
