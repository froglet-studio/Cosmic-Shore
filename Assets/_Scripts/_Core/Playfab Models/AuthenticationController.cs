using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts._Core.Playfab_Models
{
    public class AuthenticationController : MonoBehaviour
    {
        [Header("Player Display Name")]
        [SerializeField] private TMP_InputField displayNameInputField;
        [SerializeField] private Button setDisplayNameButton;
        [SerializeField] private TMP_Text displayNameResultMessage;
        [SerializeField] private string displayNameDefaultText;

        [Header("Email Register and Login")] 
        [SerializeField] private TMP_InputField emailInputField;
        [SerializeField] private TMP_InputField passwordField;
        [SerializeField] private Button registerButton;
        [SerializeField] private Button loginButton;
        
        
        // Start is called before the first frame update
        private void Start()
        {
            AuthenticationManager.Instance.AnonymousLogin();
            
            // Subscribe Button OnClick Events
            setDisplayNameButton.onClick.AddListener(SetPlayerNameButton_OnClicked);
            
            // Set default player display name
            displayNameResultMessage.text = displayNameDefaultText;
        }

        public string RandomGenerateName()
        {
            AuthenticationManager.Instance.LoadRandomNameList();
            var adjectives = AuthenticationManager.Adjectives;
            var nouns = AuthenticationManager.Nouns;
            var random = new System.Random();
            var adj_index = random.Next(adjectives.Count);
            var noun_index = random.Next(nouns.Count);
            var displayName = $"{adjectives[adj_index]} {nouns[noun_index]}";
            Debug.Log($"Generated display name: {displayName}");
            return displayName;
        }

        public void SetPlayerNameButton_OnClicked()
        {
            // TODO: input null or empty string check
            if (displayNameInputField is { text: not null })
            {
                displayNameInputField.text = RandomGenerateName();
            }
            // var displayNameText = displayNameInputField.text;
            AuthenticationManager.Instance.SetPlayerDisplayName(displayNameInputField.text, SettingPlayerDisplayName);
            Debug.Log($"Current player display name: {displayNameInputField.text}");
        }

        private void SettingPlayerDisplayName()
        {
            Debug.Log("Setting Player Display Name.");
            displayNameResultMessage.text = "Success";
            displayNameResultMessage.gameObject.SetActive(true);
        }
    }
}
