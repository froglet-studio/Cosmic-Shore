using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FlickerHighScore : MonoBehaviour
{
    [SerializeField]
    Image highScoreImageOne;
    [SerializeField]
    Image highScoreImageTwo;
    [SerializeField]
    Image highScoreImageThree;

    [SerializeField]
    private float minWaitTime = 0.01f;
    [SerializeField]
    private float maxWaitTime = 1f;

    // Start is called before the first frame update
    void Start()
    {
        highScoreImageOne.enabled = true;
        StartCoroutine(ToggleBetweenImages());
    }

    IEnumerator ToggleBetweenImages()
    {
        while (gameObject.activeInHierarchy)
        {
            yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime));
            int randNum = Random.Range(1,4);

            switch (randNum)
            {
                case 1:
                    highScoreImageOne.enabled = true;
                    highScoreImageTwo.enabled = false;
                    highScoreImageThree.enabled = false;
                    break;
                case 2:
                    highScoreImageOne.enabled = false;
                    highScoreImageTwo.enabled = true;
                    highScoreImageThree.enabled = false;
                    break;
                case 3:
                    highScoreImageOne.enabled = false;
                    highScoreImageTwo.enabled = false;
                    highScoreImageThree.enabled = true;
                    break;
            }
        }
    }
}
