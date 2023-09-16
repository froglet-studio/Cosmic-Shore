using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static _Scripts._Core.Playfab_Models.AuthenticationManager;

namespace _Scripts._Core.Playfab_Models
{
    public class AuthenticationView : MonoBehaviour
    {
        [Header("Player Display Name")]
        [SerializeField] TMP_InputField displayNameInputField;
        [SerializeField] Button setDisplayNameButton;
        [SerializeField] TMP_Text displayNameResultMessage;
        [SerializeField] string displayNameDefaultText;

        [Header("Email Register and Login")] 
        [SerializeField] TMP_InputField emailInputField;
        [SerializeField] TMP_InputField passwordField;
        [SerializeField] Button registerButton;
        [SerializeField] Button loginButton;
        
        // Start is called before the first frame update
        void Start()
        {
            // Subscribe Button OnClick Events
            setDisplayNameButton.onClick.AddListener(SetPlayerNameButton_OnClicked);

            // Set default player display name
            displayNameResultMessage.text = displayNameDefaultText;

            // This one is secret secret
            if (passwordField != null )
                passwordField.contentType = TMP_InputField.ContentType.Password;

            AuthenticationManager.OnProfileLoaded += InitializePlayerDisplayNameView;
        }

        public string RandomGenerateName()
        {
            var adjectives = AuthenticationManager.Adjectives;
            var nouns = AuthenticationManager.Nouns;
            var random = new System.Random();
            var adj_index = random.Next(adjectives.Count);
            var noun_index = random.Next(nouns.Count);
            var displayName = $"{adjectives[adj_index]} {nouns[noun_index]}";
            
            Debug.Log($"AuthenticationView - Generated display name: {displayName}");
            
            return displayName;
        }

        IEnumerator AssignRandomNameCoroutine()
        {
            AuthenticationManager.Instance.LoadRandomNameList();

            yield return new WaitUntil(() => AuthenticationManager.Adjectives != null);
            
            displayNameInputField.text = RandomGenerateName();
        } 

        public void SetPlayerNameButton_OnClicked()
        {
            displayNameResultMessage.gameObject.SetActive(false);

            if (!CheckDisplayNameLength(displayNameInputField.text))
                return;

            AuthenticationManager.Instance.SetPlayerDisplayName(displayNameInputField.text, UpdatePlayerDisplayNameView);

            // TODO: a spinning icon in the ui here would be great

            Debug.Log($"Current player display name: {displayNameInputField.text}");
        }

        public void GenerateRandomNameButton_OnClicked()
        {
            // TODO: a spinning icon in the ui here would be great

            StartCoroutine(AssignRandomNameCoroutine());
        }

        bool CheckDisplayNameLength(string displayName)
        {
            if (displayName.Length > 25 || displayName.Length < 3)
            {
                displayNameResultMessage.text = "Display name must be between 3 and 25 characters long";
                displayNameResultMessage.gameObject.SetActive(true);
                
                return false;
            }

            return true;
        }

        void UpdatePlayerDisplayNameView()
        {
            Debug.Log("Successfully Set Player Display Name.");
            displayNameResultMessage.text = "Success";
            displayNameResultMessage.gameObject.SetActive(true);
        }

        void InitializePlayerDisplayNameView()
        {
            displayNameResultMessage.text = "Display Name Loaded";
            displayNameResultMessage.gameObject.SetActive(true);

            displayNameInputField.text = AuthenticationManager.PlayerProfile.DisplayName;
        }
    }
}
