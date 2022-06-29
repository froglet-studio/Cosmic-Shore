using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialInput : MonoBehaviour
{

    [SerializeField]
    bool Left;
    [SerializeField]
    Vector2 offset;


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (UnityEngine.Input.touches.Length == 2)
        {
            Vector2 leftTouch, rightTouch;

            if (UnityEngine.Input.touches[0].position.x <= UnityEngine.Input.touches[1].position.x)
            {
                leftTouch = UnityEngine.Input.touches[0].position;
                rightTouch = UnityEngine.Input.touches[1].position;
            }
            else
            {
                leftTouch = UnityEngine.Input.touches[1].position;
                rightTouch = UnityEngine.Input.touches[0].position;
            }
            if (Left)
            {
                transform.position = Vector2.Lerp(transform.position,leftTouch + offset,.2f);
            }
            else 
            {
                transform.position = Vector2.Lerp(transform.position, rightTouch + offset, .2f);
                //transform.localScale = (1 - Vector2.Dot(rightTouch, offset)) * Vector2.one; //TODO make elements change to indicate deviation from intended lesson 
            }
        }
    }
}
