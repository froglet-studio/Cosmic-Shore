using CosmicShore.App.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XInput;
using UnityEngine.UI;
using static CosmicShore.App.UI.ScreenSwitcher;

namespace CosmicShore.Game.IO
{
    [RequireComponent(typeof(Button))]
    public class ControllerButtonPress : MonoBehaviour
    {
        [SerializeField] List<MenuScreens> ActiveMenuScreens;
        [SerializeField] List<ModalWindows> ActiveModalWindows;
        [SerializeField] GamepadButton activationButton;
        [SerializeField] Image activationButtonImage;
        [SerializeField] Sprite TriangleButtonSprite;
        [SerializeField] Sprite CircleButtonSprite;
        [SerializeField] Sprite CrossButtonSprite;
        [SerializeField] Sprite SquareButtonSprite;
        [SerializeField] Sprite YButtonSprite;
        [SerializeField] Sprite BButtonSprite;
        [SerializeField] Sprite AButtonSprite;
        [SerializeField] Sprite XButtonSprite;

        static EventSystem eventSystem;
        static ScreenSwitcher screenSwitcher;
        /// <summary>
        /// Calls to "Gamepad.current is DualShockGamepad" and "Gamepad.current is XInputController"
        /// are very expensive. Save off the results for performance.
        /// </summary>
        static bool GamepadTypeInitialized;
        static bool CurrentGamepadIsDualShock;
        static bool CurrentGamepadIsXInputController;
        
        Button button;

        void Start()
        {
            if (eventSystem == null)
                eventSystem = FindAnyObjectByType<EventSystem>();
            if (screenSwitcher == null)
                screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
            if (!GamepadTypeInitialized)
            {
                CurrentGamepadIsDualShock = Gamepad.current is DualShockGamepad;
                CurrentGamepadIsXInputController = Gamepad.current is XInputController;
            }

            button = GetComponent<Button>();

            if (CurrentGamepadIsDualShock)
            {
                switch(activationButton)
                {
                    case GamepadButton.North:
                        activationButtonImage.sprite = TriangleButtonSprite;
                        break;
                    case GamepadButton.East:
                        activationButtonImage.sprite = CircleButtonSprite;
                        break;
                    case GamepadButton.South:
                        activationButtonImage.sprite = CrossButtonSprite;
                        break;
                    case GamepadButton.West:
                        activationButtonImage.sprite = SquareButtonSprite;
                        break;
                    default:
                        activationButtonImage.gameObject.SetActive(false);
                        break;
                }
                Debug.Log("A DualShock controller is connected.");
            }
            else if (CurrentGamepadIsXInputController)
            {
                switch (activationButton)
                {
                    case GamepadButton.North:
                        activationButtonImage.sprite = YButtonSprite;
                        break;
                    case GamepadButton.East:
                        activationButtonImage.sprite = BButtonSprite;
                        break;
                    case GamepadButton.South:
                        activationButtonImage.sprite = AButtonSprite;
                        break;
                    case GamepadButton.West:
                        activationButtonImage.sprite = XButtonSprite;
                        break;
                    default:
                        activationButtonImage.gameObject.SetActive(false);
                        break;
                }
                Debug.Log("An Xbox controller is connected.");
            }
            else
            {
                activationButtonImage.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (screenSwitcher != null)
            {
                // If the screen context is active do the action
                foreach (var screen in ActiveMenuScreens)
                {
                    if (screenSwitcher.ScreenIsActive(screen))
                    {
                        if (Gamepad.current != null && Gamepad.current[activationButton].wasPressedThisFrame)
                        {
                            button.onClick.Invoke();
                            button.OnDeselect(new BaseEventData(eventSystem));
                            return;
                        }
                    }
                }

                // If the modal context is active do the action
                foreach (var modal in ActiveModalWindows)
                {
                    if (screenSwitcher.ModalIsActive(modal))
                    {
                        if (Gamepad.current != null && Gamepad.current[activationButton].wasPressedThisFrame)
                        {
                            button.onClick.Invoke();
                            button.OnDeselect(new BaseEventData(eventSystem));
                            return;
                        }
                    }
                }
            }
            // Kinda lame-o approach to keep in game UI working (which doesn't have nested modals or return to screens)
            else
            {
                if (Gamepad.current != null && Gamepad.current[activationButton].wasPressedThisFrame)
                {
                    button.onClick.Invoke();
                    button.OnDeselect(new BaseEventData(eventSystem));
                }
            }
        }
    }
}