using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FlickerUIEffect : MonoBehaviour
{
    [SerializeField]
    Image flickerImage;

    [SerializeField]
    private float minWaitOffTime = 0.01f;
    [SerializeField]
    private float maxWaitOffTime = 0.1f;
    [SerializeField]
    private float minWaitOnTime = 1f;
    [SerializeField]
    private float maxWaitOnTime = 2f;

    // Start is called before the first frame update
    void Start()
    {
        flickerImage = GetComponent<Image>();
        StartCoroutine(ToggleBetweenImages());
    }

    IEnumerator ToggleBetweenImages()
    {
        while (gameObject.activeInHierarchy)
        {
            flickerImage.enabled = true;
            yield return new WaitForSeconds(Random.Range(minWaitOnTime, maxWaitOnTime));
            
            flickerImage.enabled = false;
            yield return new WaitForSeconds(Random.Range(minWaitOffTime, maxWaitOffTime));
        }      
    }  
}
