using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Controls : MonoBehaviour
{
    [SerializeField] bool Left;
    [SerializeField] Vector2 offset;
    [SerializeField] Sprite InactiveImage;
    [SerializeField] Sprite ActiveImage;

    Image image;
    Vector2 initialPos;

    void Start()
    {
        initialPos = transform.position;
        image = GetComponent<Image>();
        image.sprite = InactiveImage;
    }

    void Update()
    {
        Vector2 leftTouch, rightTouch;

        //If there are no touches, move both controls to start positions
        if (Input.touches.Length == 0)
        {
            transform.position = Vector2.Lerp(transform.position, initialPos, .2f);
            image.sprite = InactiveImage;
        }

        //If there is only one touch, move closer control to the finger and start following
        else if (Input.touches.Length == 1)
        {
            image.sprite = ActiveImage;
            if (Input.touches[0].position.x <= Screen.currentResolution.width / 2)
            {
                if (Left)
                {
                    leftTouch = Input.touches[0].position;
                    transform.position = Vector2.Lerp(transform.position, leftTouch + offset, .2f);
                }
                else 
                {
                    transform.position = Vector2.Lerp(transform.position, initialPos, .2f);
                }
            }
            else
            {
                if (!Left)
                {
                    rightTouch = Input.touches[0].position;
                    transform.position = Vector2.Lerp(transform.position, rightTouch + offset, .2f);
                }
                else
                {
                    transform.position = Vector2.Lerp(transform.position, initialPos, .2f);
                }

            }
        }

        // If there are two touches assign one controller per touch and follow
        else if (Input.touches.Length == 2)
        {
            image.sprite = ActiveImage;
            if (Input.touches[0].position.x <= Input.touches[1].position.x)
            {
                leftTouch = Input.touches[0].position;
                rightTouch = Input.touches[1].position;
            }
            else
            {
                leftTouch = Input.touches[1].position;
                rightTouch = Input.touches[0].position;
            }
            if (Left)
            {
                transform.position = Vector2.Lerp(transform.position, leftTouch + offset, .2f);
            }
            else
            {
                transform.position = Vector2.Lerp(transform.position, rightTouch + offset, .2f);
            }
        }
    }
}