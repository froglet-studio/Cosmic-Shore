using CosmicShore.Game.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class ThumbPerimeter : MonoBehaviour
    {
        
        [SerializeField] bool LeftThumb;
        bool PerimeterActive = false;
        [SerializeField] float Scaler = 3f; //scale to max radius

        [SerializeField] Sprite ActivePerimeterImage;
        [SerializeField] Player player;
        public float alpha = 0f;        
        
        Image image;
        Color color = Color.white;

        bool initialized;

        Vector2 leftStartPosition, rightStartPosition;
        InputController controller;

        void Start()
        {
            image = GetComponent<Image>();
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
            bool isActive = Gamepad.current == null && (LeftThumb || !Player.ActivePlayer.Ship.ShipStatus.SingleStickControls);
            if (!Player.ActivePlayer.Ship.ShipStatus.AutoPilotEnabled)
            {
                gameObject.SetActive(isActive);
                controller = Player.ActivePlayer.Ship.InputController;
                initialized = isActive;
            }               
        }

        void Update()
        {
            if (initialized && !Player.ActivePlayer.Ship.ShipStatus.AutoPilotEnabled)
            {
                if (Input.touches.Length == 0)
                {
                    Debug.Log("ThumbPerimeter no touchy");
                    //set Image color alpha
                    color.a = 0;
                    image.color = color;
                    //if (PerimeterActive)
                    //{
                    //    PerimeterActive = false;
                    //}
                }

                else
                {
                    Debug.Log("ThumbPerimeter touchy");
                    float normalizedJoystickDistance;
                    float angle;
                    Vector2 normalizedJoystickPosition;
                    if (Input.touches.Length == 1)
                    {
                        PerimeterActive = controller.OneTouchLeft == LeftThumb;
                    }                  
                    if (LeftThumb)
                    {
                        transform.position = controller.LeftJoystickStart;
                        normalizedJoystickPosition = controller.LeftNormalizedJoystickPosition;
                    }
                    else
                    {
                        transform.position = controller.RightJoystickStart;
                        normalizedJoystickPosition = controller.RightNormalizedJoystickPosition;
                    }
                    normalizedJoystickDistance = normalizedJoystickPosition.magnitude;

                    image.rectTransform.localScale = Vector2.one * Scaler;
                    image.sprite = ActivePerimeterImage;
                    //set Image color alpha
                   
                    color.a = normalizedJoystickDistance - .5f;
                    image.color = color;
                    alpha = color.a;

                    angle = Vector3.Angle(normalizedJoystickPosition, Vector2.up);

                    transform.rotation = Vector2.Dot(normalizedJoystickPosition, Vector2.right) < 0 ?
                        Quaternion.Euler(0, 0, angle) :
                        Quaternion.Euler(0, 0, -angle);   
                }
            }
        }
    }
}