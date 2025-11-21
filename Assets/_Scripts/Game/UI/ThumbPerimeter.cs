using CosmicShore.Core;
using CosmicShore.Game.IO;
using System.Collections;
using UnityEngine;
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
        bool imageEnabled = true;

        Vector2 leftStartPosition, rightStartPosition;
        IInputStatus _inputStatus;

        private void OnEnable()
        {
            GameSetting.OnChangeJoystickVisualsStatus += OnToggleJoystickVisuals;
        }

        private void OnDisable()
        {
            GameSetting.OnChangeJoystickVisualsStatus -= OnToggleJoystickVisuals;
        }

        public void Initialize(IInputStatus inputStatus) => _inputStatus = inputStatus;

        private void OnToggleJoystickVisuals(bool status)
        {
            Debug.Log($"GameSettings.OnChangeJoystickVisualsStatus - status: {status}");
            imageEnabled = status;
        }

        void Start()
        {
            image = GetComponent<Image>();
            image.sprite = ActivePerimeterImage;
            imageEnabled = GameSetting.Instance.JoystickVisualsEnabled;

            color.a = 0;
            image.color = color;
            StartCoroutine(InitializeCoroutine());
        }

        // Wait until the input controller is wired up then only show if there is no gamepad and the left one when flying with single stick controls 
        IEnumerator InitializeCoroutine()
        {
            // TODO - Can't have LocalPlayer as static
            /*yield return new WaitUntil(() => Player.LocalPlayer != null && Player.LocalPlayer.Vessel != null && Player.LocalPlayer.Vessel.VesselStatus.InputController != null);
            bool isActive = Gamepad.current == null && !Player.LocalPlayer.Vessel.VesselStatus.CommandStickControls && (LeftThumb || !Player.LocalPlayer.Vessel.VesselStatus.IsSingleStickControls);
            if (!Player.LocalPlayer.Vessel.VesselStatus.AutoPilotEnabled)
            {
                gameObject.SetActive(isActive);
                _inputController = Player.LocalPlayer.Vessel.VesselStatus.InputController;
                initialized = isActive;
            }    */
            // TEMP Suspended
            yield return null;
            enabled = false;
        }

        void Update()
        {
            if(!imageEnabled) { return; }

            // TODO - Can't have LocalPlayer as static
            // if (initialized && !Player.LocalPlayer.Vessel.VesselStatus.AutoPilotEnabled)
            if (true) // TEMP  
            {
                if (Input.touches.Length == 0)
                {
                    color.a = 0;
                    image.color = color;
                }

                else
                {
                    float normalizedJoystickDistance;
                    float angle;
                    Vector2 normalizedJoystickPosition;
                    if (Input.touches.Length == 1)
                    {
                        PerimeterActive = _inputStatus.OneTouchLeft == LeftThumb;
                    }                  
                    if (LeftThumb)
                    {
                        transform.position = _inputStatus.LeftJoystickStart;
                        normalizedJoystickPosition = _inputStatus.LeftNormalizedJoystickPosition;
                    }
                    else
                    {
                        transform.position = _inputStatus.RightJoystickStart;
                        normalizedJoystickPosition = _inputStatus.RightNormalizedJoystickPosition;
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