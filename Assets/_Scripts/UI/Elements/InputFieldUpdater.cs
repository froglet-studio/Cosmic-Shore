using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InputFieldUpdater : MonoBehaviour
{
    public TMP_InputField inputField;
    public TMP_Text displayText;

    private void Start()
    {
        // Attach the method to the InputField's OnValueChanged event.
        inputField.onValueChanged.AddListener(UpdateDisplayText);
    }

    // This method will be called whenever the InputField value changes.
    private void UpdateDisplayText(string newValue)
    {
        // Update the display text with the current input field value.
        displayText.text = "Input Text: " + newValue;
    }
}
