using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

public class GamepadDebugger : MonoBehaviour
{
    void Start()
    {
        LogConnectedGamepads();
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void LogConnectedGamepads()
    {
        var gamepads = Gamepad.all;
        if (gamepads.Count == 0)
        {
            Debug.LogWarning("<color=yellow>No gamepads detected.</color>");
            return;
        }

        foreach (var pad in gamepads)
        {
            string type = GetControllerType(pad);
            string color = GetColorForType(type);

            string info = $"<color={color}><b>Gamepad Detected:</b> '{pad.displayName}' ({pad.device.description.product})";
            info += $"\n<b>Controller Type:</b> {type}";

            if (type == "Other")
            {
                info += $"\n<b>[Details]</b>";
                info += $"\nName: {pad.name}";
                info += $"\nProduct: {pad.device.description.product}";
                info += $"\nManufacturer: {pad.device.description.manufacturer}";
                info += $"\nInterface: {pad.device.description.interfaceName}";
                info += $"\nUsages: {string.Join(",", pad.usages.Select(u => u.ToString()))}";
                info += $"\n<b>Supported Controls:</b>";
                foreach (var c in pad.allControls)
                {
                    info += $"\n   - {c.name}: {c.path} [{c.layout}]";
                }
            }

            info += "</color>";
            Debug.Log(info);
        }
    }

    private string GetControllerType(Gamepad pad)
    {
        var product = (pad.device.description.product ?? "").ToLower();
        if (product.Contains("xbox")) return "XBOX";
        if (product.Contains("dualshock") || product.Contains("dualsense") || product.Contains("ps4") || product.Contains("ps5") || product.Contains("playstation"))
            return "PlayStation";
        if (product.Contains("logitech")) return "Logitech (Other)";
        return "Other";
    }

    private string GetColorForType(string type)
    {
        switch (type)
        {
            case "XBOX": return "green";
            case "PlayStation": return "blue";
            case "Logitech (Other)": return "orange";
            default: return "red";
        }
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is Gamepad)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    Debug.Log("<color=cyan>Gamepad connected/reconnected: " + device.displayName + "</color>");
                    LogConnectedGamepads();
                    break;
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    Debug.Log("<color=magenta>Gamepad disconnected: " + device.displayName + "</color>");
                    LogConnectedGamepads();
                    break;
            }
        }
    }
}
