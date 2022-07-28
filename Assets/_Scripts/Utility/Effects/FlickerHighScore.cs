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
        StartCoroutine(ToggleBetweenImages());
    }

    IEnumerator ToggleBetweenImages()
    {
        while (gameObject.activeInHierarchy)
        {
            yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime));
            int randNum = Random.Range(1, 3);

            switch (randNum)
            {
                case 1:
                    highScoreImageOne.enabled = highScoreImageOne.enabled;
                    highScoreImageTwo.enabled = !highScoreImageTwo.enabled;
                    highScoreImageThree.enabled = !highScoreImageThree.enabled;
                    break;
                case 2:
                    highScoreImageOne.enabled = highScoreImageOne.enabled;
                    highScoreImageTwo.enabled = !highScoreImageTwo.enabled;
                    highScoreImageThree.enabled = !highScoreImageThree.enabled;
                    break;
                case 3:
                    highScoreImageOne.enabled = highScoreImageOne.enabled;
                    highScoreImageTwo.enabled = !highScoreImageTwo.enabled;
                    highScoreImageThree.enabled = !highScoreImageThree.enabled;
                    break;
            }
        }
    }
}
