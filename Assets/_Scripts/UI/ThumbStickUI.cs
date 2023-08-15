using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using StarWriter.Utility.Tools;

public class ThumbStickUI : MonoBehaviour
{
    [SerializeField] bool Left;
    [SerializeField] Vector2 offset;
    [SerializeField] Sprite InactiveImage;
    [SerializeField] Sprite ActiveImage;
    [SerializeField] Player player;

    Image image;
    Vector2 initialPos;

    //float JoystickRadius;
    Vector2 leftTouch, rightTouch;

    void Start()
    {
        //leftTouch = player.Ship.InputController.LeftClampedPosition;
        //rightTouch = player.Ship.InputController.RightClampedPosition;


        //JoystickRadius = Screen.dpi;
        //initialPos = Left ? new Vector2(JoystickRadius, JoystickRadius) : new Vector2(Screen.currentResolution.width - JoystickRadius, JoystickRadius);
        //initialPos = Left ? leftTouch : rightTouch;
        image = GetComponent<Image>();
        image.sprite = InactiveImage;

        if (!player.Ship.ShipStatus.AutoPilotEnabled) { StartCoroutine(InitializeCoroutine()); }
    }

    // wait until the input controller is wired up then only show if there is no gamepad and the left one when flying with single stick controls 
    IEnumerator InitializeCoroutine()
    {
        yield return new WaitUntil(() => player.Ship.InputController != null);
        gameObject.SetActive(Gamepad.current == null && (Left || !player.Ship.InputController.SingleStickControls)); ;
    }

    void Update()
    {

        if (!player.Ship.ShipStatus.AutoPilotEnabled)
        {
            if (Input.touches.Length == 0)
            {

                transform.position = Left ? Vector2.Lerp(transform.position, player.Ship.InputController.LeftJoystickHome, .2f) : Vector2.Lerp(transform.position, player.Ship.InputController.RightJoystickHome, .2f);
                image.sprite = InactiveImage;
            }
            else if (Left)
            {
                leftTouch = player.Ship.InputController.LeftClampedPosition;
                transform.position = Vector2.Lerp(transform.position, leftTouch, .2f);
                image.sprite = ActiveImage;
            }
            else
            {
                rightTouch = player.Ship.InputController.RightClampedPosition;
                transform.position = Vector2.Lerp(transform.position, rightTouch, .2f);
                image.sprite = ActiveImage;
            }
        }
       



        //PortraitCheck();
        ////If there are no touches, move both controls to start positions
        //if (Input.touches.Length == 0)
        //{
        //    transform.position = Vector2.Lerp(transform.position, initialPos, .2f);
        //    image.sprite = InactiveImage;
        //}

        ////If there is only one touch, move closer control to the finger and start following
        //else if (Input.touches.Length == 1)
        //{
        //    if (Input.touches[0].position.x <= Screen.currentResolution.width / 2)
        //    {
        //        if (Left)
        //        {
        //            leftTouch = Input.touches[0].position;
        //            transform.position = Vector2.Lerp(transform.position, leftTouch + offset, .2f);
        //            image.sprite = ActiveImage;
        //        }
        //        else 
        //        {
        //            image.sprite = InactiveImage;
        //            //transform.position = Input.touches[0].position;
        //            //transform.position = Vector2.Lerp(transform.position, initialPos, .2f);
        //        }
        //    }
        //    else
        //    {
        //        if (!Left)
        //        {
        //            rightTouch = Input.touches[0].position;
        //            transform.position = Vector2.Lerp(transform.position, rightTouch + offset, .2f);
        //            image.sprite = ActiveImage;
        //        }
        //        else
        //        {
        //            image.sprite = InactiveImage;
        //            //transform.position = Input.touches[0].position;
        //            //transform.position = Vector2.Lerp(transform.position, initialPos, .2f);
        //        }
        //    }
        //}

        //// If there are two touches assign one controller per touch and follow
        //else if (Input.touches.Length == 2)
        //{
        //    image.sprite = ActiveImage;
        //    if (Input.touches[0].position.x <= Input.touches[1].position.x)
        //    {
        //        leftTouch = Input.touches[0].position;
        //        rightTouch = Input.touches[1].position;
        //    }
        //    else
        //    {
        //        leftTouch = Input.touches[1].position;
        //        rightTouch = Input.touches[0].position;
        //    }
        //    if (Left)
        //    {
        //        transform.position = Vector2.Lerp(transform.position, leftTouch + offset, .2f);
        //    }
        //    else
        //    {
        //        transform.position = Vector2.Lerp(transform.position, rightTouch + offset, .2f);
        //    }
        //} 
        //else
        //{
        //    // Three finger fumble, aka phat hands - Sub select the two best touch inputs here
        //    // If we have more than two touches, find the closest to each of the last touch positions we used
        //    int leftTouchIndex = 0, rightTouchIndex = 0;
        //    float minLeftTouchDistance = Vector2.Distance(leftTouch, Input.touches[0].position);
        //    float minRightTouchDistance = Vector2.Distance(rightTouch, Input.touches[0].position);

        //    for (int i = 1; i < Input.touches.Length; i++)
        //    {
        //        if (Vector2.Distance(leftTouch, Input.touches[i].position) < minLeftTouchDistance)
        //        {
        //            minLeftTouchDistance = Vector2.Distance(leftTouch, Input.touches[i].position);
        //            leftTouchIndex = i;
        //        }
        //        if (Vector2.Distance(rightTouch, Input.touches[i].position) < minRightTouchDistance)
        //        {
        //            minRightTouchDistance = Vector2.Distance(rightTouch, Input.touches[i].position);
        //            rightTouchIndex = i;
        //        }
        //    }
        //    leftTouch = Input.touches[leftTouchIndex].position;
        //    rightTouch = Input.touches[rightTouchIndex].position;

        //    if (Left)
        //    {
        //        transform.position = Vector2.Lerp(transform.position, leftTouch + offset, .2f);
        //    }
        //    else
        //    {
        //        transform.position = Vector2.Lerp(transform.position, rightTouch + offset, .2f);
        //    }
        //}
    }
}