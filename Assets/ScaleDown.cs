using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScaleDown : MonoBehaviour
{
    [SerializeField]
    float minMagnitude = 5;
    [SerializeField]
    float scaleRate = .999f;


    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(shrinkCoroutine());   
    }

    IEnumerator shrinkCoroutine()
    {
        yield return new WaitForSeconds(1);
        while (transform.localScale.magnitude > minMagnitude)
        {
            yield return new WaitForSeconds(.2f);
            transform.localScale *= scaleRate;
        }
            
        
    }


    // Update is called once per frame
    void Update()
    {

            
    }
}
