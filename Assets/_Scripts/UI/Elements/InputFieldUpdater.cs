using TMPro;
using UnityEngine;

namespace CosmicShore.App.UI.Elements
{
    public class InputFieldUpdater : MonoBehaviour
    {
        public TMP_InputField inputField;
        public TMP_Text displayText;

        void Start()
        {
            // Attach the method to the InputField's OnValueChanged event.
            inputField.onValueChanged.AddListener(UpdateDisplayText);
        }

        // This method will be called whenever the InputField value changes.
        void UpdateDisplayText(string newValue)
        {
            // Update the display text with the current input field value.
            displayText.text = "Input Text: " + newValue;
        }
    }
}