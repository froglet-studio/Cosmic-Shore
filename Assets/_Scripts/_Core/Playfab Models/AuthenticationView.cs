using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts._Core.Playfab_Models
{
    public class AuthenticationView : MonoBehaviour
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
            var adjectives = AuthenticationManager.Adjectives;
            var nouns = AuthenticationManager.Nouns;
            var random = new System.Random();
            var adj_index = random.Next(adjectives.Count);
            var noun_index = random.Next(nouns.Count);
            var displayName = $"{adjectives[adj_index]} {nouns[noun_index]}";
            Debug.Log($"Generated display name: {displayName}");
            return displayName;
        }

        IEnumerator GenerateName()
        {
            AuthenticationManager.Instance.LoadRandomNameList();
            yield return new WaitUntil(() => AuthenticationManager.Adjectives != null);
            displayNameInputField.text = RandomGenerateName();
            AuthenticationManager.Instance.SetPlayerDisplayName(displayNameInputField.text, SettingPlayerDisplayName);
        } 

        public void SetPlayerNameButton_OnClicked()
        {
            // Input null or empty string check
            if (string.IsNullOrEmpty(displayNameInputField.text))
            {   
                // Waiting for the result
                StartCoroutine(GenerateName());
                
                // TODO: a spinning icon in the ui here would be great
            }
            else
            {
                AuthenticationManager.Instance.SetPlayerDisplayName(displayNameInputField.text, SettingPlayerDisplayName);
                
                Debug.Log($"Current player display name: {displayNameInputField.text}");
            }
            
            // TODO: Input length check, name length should be between 5 to 40
            
            // var displayNameText = displayNameInputField.text;
            
        }

        private void CheckDisplayNameLength(string displayName)
        {
            // if(displayName.Length>)
        }

        private void SettingPlayerDisplayName()
        {
            Debug.Log("Setting Player Display Name.");
            displayNameResultMessage.text = "Success";
            displayNameResultMessage.gameObject.SetActive(true);
        }
    }
}
