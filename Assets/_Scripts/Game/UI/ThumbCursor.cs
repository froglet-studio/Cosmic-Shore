using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using CosmicShore.Core;
using System;

namespace CosmicShore.Game.UI
{
    public class ThumbCursor : MonoBehaviour
    {
        [SerializeField] bool LeftThumb;
        [SerializeField] Vector2 offset;
        [SerializeField] Sprite InactiveImage;
        [SerializeField] Sprite ActiveImage;
        [SerializeField] R_Player player;

        Image image;
        bool initialized;
        bool imageEnabled = true;
        Vector2 leftTouch, rightTouch;

        IInputStatus inputStatus => player.InputController.InputStatus;

        private void OnEnable()
        {
            GameSetting.OnChangeJoystickVisualsStatus += OnToggleJoystickVisuals;
        }

        private void OnDisable()
        {
            GameSetting.OnChangeJoystickVisualsStatus -= OnToggleJoystickVisuals;
        }

        private void OnToggleJoystickVisuals(bool status)
        {
            Debug.Log($"GameSettings.OnChangeJoystickVisualsStatus - status: {status}");
            imageEnabled = status;
        }

        void Start()
        {
            image = GetComponent<Image>();
            image.sprite = InactiveImage;
            imageEnabled = GameSetting.Instance.JoystickVisualsEnabled;
            StartCoroutine(InitializeCoroutine());
        }

        // Wait until the input controller is wired up then only show if there is no gamepad and the left one when flying with single stick controls 
        IEnumerator InitializeCoroutine()
        {
            // TODO - Can't have ActivePlayer as static
            /*yield return new WaitUntil(() => Player.ActivePlayer != null && Player.ActivePlayer.Ship != null && Player.ActivePlayer.Ship.ShipStatus.InputController != null);

            if (!Player.ActivePlayer.Ship.ShipStatus.AutoPilotEnabled)
                gameObject.SetActive(Gamepad.current == null && !Player.ActivePlayer.Ship.ShipStatus.CommandStickControls && (LeftThumb || !Player.ActivePlayer.Ship.ShipStatus.SingleStickControls));

            initialized = true;*/

            // TEMP for SUSPEND
            yield return null;
            enabled = false;
        }

        void Update()
        {
            // TODO - Can't have ActivePlayer as static
            // if (initialized && !Player.ActivePlayer.Ship.ShipStatus.AutoPilotEnabled)
            // TEMP
            {
                if (Input.touches.Length == 0)
                {
                    transform.position = LeftThumb ? Vector2.Lerp(transform.position, inputStatus.LeftJoystickHome, .2f) : Vector2.Lerp(transform.position, inputStatus.RightJoystickHome, .2f);
                    image.sprite = InactiveImage;
                }
                else if (LeftThumb)
                {
                    leftTouch = inputStatus.LeftClampedPosition;
                    transform.position = Vector2.Lerp(transform.position, leftTouch, .2f);
                    imageEnabled = true ? image.sprite = ActiveImage : image.sprite = InactiveImage;
                    
                    //image.transform.localScale = (Player.ActivePlayer.Ship.InputController.LeftJoystickStart              //makes circles grow as they get close to perimeter
                    //    - Player.ActivePlayer.Ship.InputController.LeftClampedPosition).magnitude * .025f * Vector3.one;
                }
                else
                {
                    rightTouch = inputStatus.RightClampedPosition;
                    transform.position = Vector2.Lerp(transform.position, rightTouch, .2f);
                    imageEnabled = true ? image.sprite = ActiveImage : image.sprite = InactiveImage;
                    //image.transform.localScale = (Player.ActivePlayer.Ship.InputController.RightJoystickStart
                    //    - Player.ActivePlayer.Ship.InputController.RightClampedPosition).magnitude * .025f * Vector3.one;
                }
            }
        }
    }
}