using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ThumbPerimeter : MonoBehaviour
{
    [SerializeField] bool LeftThumb;
    [SerializeField] bool LeftPerimeterActive;
    [SerializeField] bool RightPerimeterActive;
    [SerializeField] float leftPerimeterScaler = 2; //scale to max radius
    [SerializeField] float rightPerimeterScaler = 2; //scale to max radius

    [SerializeField] Sprite InactivePerimeterImage;
    [SerializeField] Sprite ActivePerimeterImage;
    [SerializeField] Player player;

    Image perimeterImage;

    bool initialized;

    void Start()
    {
        
        perimeterImage = GetComponentInChildren<Image>();
        perimeterImage.sprite = InactivePerimeterImage;
        StartCoroutine(InitializeCoroutine());
    }

    // Wait until the input controller is wired up then only show if there is no gamepad and the left one when flying with single stick controls 
    IEnumerator InitializeCoroutine()
    {
        yield return new WaitUntil(() => Player.ActivePlayer != null && Player.ActivePlayer.Ship != null && Player.ActivePlayer.Ship.InputController != null);

        if (!Player.ActivePlayer.Ship.ShipStatus.AutoPilotEnabled)
            gameObject.SetActive(Gamepad.current == null && (LeftThumb || !Player.ActivePlayer.Ship.ShipStatus.SingleStickControls));

        initialized = true;
    }

    void Update()
    {
        if (initialized && !Player.ActivePlayer.Ship.ShipStatus.AutoPilotEnabled)
        {
            if (Input.touches.Length == 0)
            {
                perimeterImage.sprite = InactivePerimeterImage;
                if(LeftPerimeterActive || RightPerimeterActive)
                {
                    LeftPerimeterActive = RightPerimeterActive = false;
                }               
            }
            else if (Input.touches.Length > 0)
            {
                if (LeftThumb)
                {

                    // check if left perimeter is active
                    if (!LeftPerimeterActive)
                    {
                        LeftPerimeterActive = true;
                        perimeterImage.rectTransform.localPosition = Player.ActivePlayer.Ship.InputController.LeftClampedPosition;
                        perimeterImage.rectTransform.localScale = Vector2.one * leftPerimeterScaler;
                        perimeterImage.sprite = ActivePerimeterImage;
                    }
                }
                else
                {
                    // check if right perimeter is active
                    if (!RightPerimeterActive)
                    {
                        RightPerimeterActive = true;
                        perimeterImage.rectTransform.localPosition = Player.ActivePlayer.Ship.InputController.RightClampedPosition;
                        perimeterImage.rectTransform.localScale = Vector2.one * rightPerimeterScaler;
                        perimeterImage.sprite = ActivePerimeterImage;
                    }
                }
            }
        }
    }
}
