using CosmicShore.Game.IO;
using System.Collections;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class ThumbPerimeter : MonoBehaviour
    {
        
        [SerializeField] bool LeftThumb;
        bool PerimeterActive = false;
        [SerializeField] float Scaler = 3.5f; //scale to max radius

        [SerializeField] Sprite ActivePerimeterImage;
        [SerializeField] Player player;
        public float alpha = 0f;        
        
        Image image;
        Color color = Color.white;

        bool initialized;

        Vector2 leftTouch, rightTouch;
        InputController controller;

        void Start()
        {
            controller = Player.ActivePlayer.Ship.InputController;
            image = GetComponentInChildren<Image>();
            image.sprite = ActivePerimeterImage;
            //set Image color alpha
            color.a = 0;
            image.color = color;
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
                    //set Image color alpha
                    color.a = 0;
                    image.color = color;
                    if (PerimeterActive)
                    {
                        PerimeterActive = false;
                    }
                }

                else
                {
                    float normalizedJoystickDistance;
                    if (Input.touches.Length == 1)
                    {
                        PerimeterActive = controller.OneTouchLeft == LeftThumb;
                    }                  
                    if (LeftThumb)
                    {

                        // check if left perimeter is active
                        if (!PerimeterActive)
                        {
                            PerimeterActive = true;
                            leftTouch = Player.ActivePlayer.Ship.InputController.LeftJoystickStart;
                            transform.position = leftTouch;
                        }
                        normalizedJoystickDistance = controller.LeftNormalizedJoystickPosition.magnitude;
                    }
                    else
                    {
                        // check if right perimeter is active
                        if (!PerimeterActive)
                        {
                            PerimeterActive = true;
                            rightTouch = Player.ActivePlayer.Ship.InputController.RightJoystickStart;
                            transform.position = rightTouch;

                        }
                        normalizedJoystickDistance = controller.RightNormalizedJoystickPosition.magnitude;
                    }
                    image.rectTransform.localScale = Vector2.one * Scaler;
                    image.sprite = ActivePerimeterImage;
                    //set Image color alpha
                   
                    color.a = normalizedJoystickDistance;
                    image.color = color;

                    alpha = color.a; //testing only
                }
                

               


            }
        }
        public Vector2 GetLeftPerimeterOrgin()
        {
            return leftTouch;
        }

        public Vector2 GetRightPerimeterOrgin()
        {
            return rightTouch;
        }
    }
}