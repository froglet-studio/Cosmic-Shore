using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace CosmicShore.Game.IO
{
    [RequireComponent(typeof(Button))]
    public class ControllerButtonPress : MonoBehaviour
    {
        [SerializeField] GamepadButton activationButton;

        EventSystem eventSystem;
        Button button;
        void Start()
        {
            eventSystem = FindObjectOfType<EventSystem>();
            button = GetComponent<Button>();
        }

        void Update()
        {
            if (Gamepad.current != null && Gamepad.current[activationButton].wasPressedThisFrame)
            {
                button.onClick.Invoke();
                button.OnDeselect(new BaseEventData(eventSystem));
            }
        }
    }
}