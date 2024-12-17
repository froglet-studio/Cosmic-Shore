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
        //[SerializeField] 

        EventSystem eventSystem;
        ScreenSwitcher screenSwitcher;
        Button button;
        void Start()
        {
            eventSystem = FindObjectOfType<EventSystem>();
            screenSwitcher = FindObjectOfType<ScreenSwitcher>();
            button = GetComponent<Button>();

            if (Gamepad.current is DualShockGamepad)
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
                }
                Debug.Log("A DualShock controller is connected.");
            }
            else if (Gamepad.current is XInputController)
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