using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Game.IO
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public class ControllerDropdown : MonoBehaviour
    {
        TMP_Dropdown dropdown;

        void Start()
        {
            dropdown = GetComponent<TMP_Dropdown>();
        }


        void Update()
        {
            if (Gamepad.current != null)
            {
                if (Gamepad.current.dpad.up.wasPressedThisFrame)
                    Up();
                if (Gamepad.current.dpad.down.wasPressedThisFrame)
                    Down();
            }
        }

        void Up()
        {
            dropdown.value = (dropdown.value - 1) % dropdown.options.Count;
        }
        void Down()
        {
            dropdown.value = (dropdown.value + 1) % dropdown.options.Count;
        }
    }
}