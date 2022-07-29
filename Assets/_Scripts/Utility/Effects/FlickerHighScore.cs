using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FlickerHighScore : MonoBehaviour
{
    [SerializeField]
    Image highScoreImageFull;
    [SerializeField]
    Image highScoreImageOne;
    [SerializeField]
    Image highScoreImageTwo;
    [SerializeField]
    Image highScoreImageThree;

    [SerializeField]
    private float fullWaitTime = 2.9f;
    [SerializeField]
    private float minWaitTime = 0.01f;
    [SerializeField]
    private float maxWaitTime = 0.25f;

    // Start is called before the first frame update
    void Start()
    {
        highScoreImageFull.enabled = true;
        StartCoroutine(ToggleBetweenImages());
    }

    IEnumerator ToggleBetweenImages()
    {
        while (gameObject.activeInHierarchy)
        {
            
            int randNum = Random.Range(0,4);

            switch (randNum)
            {
                case 0:
                    highScoreImageFull.enabled = true;
                    highScoreImageOne.enabled = false;
                    highScoreImageTwo.enabled = false;
                    highScoreImageThree.enabled = false;
                    yield return new WaitForSeconds(fullWaitTime);
                    break;
                case 1:
                    highScoreImageFull.enabled = false;
                    highScoreImageOne.enabled = true;
                    highScoreImageTwo.enabled = false;
                    highScoreImageThree.enabled = false;
                    yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime));
                    break;
                case 2:
                    highScoreImageFull.enabled = false;
                    highScoreImageOne.enabled = false;
                    highScoreImageTwo.enabled = true;
                    highScoreImageThree.enabled = false;
                    yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime));
                    break;
                case 3:
                    highScoreImageFull.enabled = false;
                    highScoreImageOne.enabled = false;
                    highScoreImageTwo.enabled = false;
                    highScoreImageThree.enabled = true;
                    yield return new WaitForSeconds(minWaitTime);
                    break;
                
            }
        }
    }
}
