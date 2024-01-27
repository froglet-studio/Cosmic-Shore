using System.Text.RegularExpressions;
using Loxodon.Framework.Observables;
using Loxodon.Framework.ViewModels;
using UnityEngine;

namespace CosmicShore.Integrations.Loxodon.MVVM
{
    public class DatabindingViewModel : ViewModelBase
    {
        private AccountViewModel _account;
        private bool _remember;
        private string _username;
        private string _email;
        private ObservableDictionary<string, string> _errors = new();

        public AccountViewModel Account
        {
            get => _account;
            set => Set(ref _account, value);
        }

        public string Username
        {
            get => _username;
            set => Set(ref _username, value);
        }

        public string Email
        {
            get => _email;
            set => Set(ref _email, value);
        }

        public bool Remeber
        {
            get => _remember;
            set => Set(ref _remember, value);
        }

        public ObservableDictionary<string, string> Errors
        {
            get => _errors;
            set => Set(ref _errors, value);
        }

        public void OnUsernameValueChanged(string username)
        {
            Debug.LogFormat("Username Value changed: {0}", username);
        }

        public void OnEmailValueChanged(string email)
        {
            Debug.LogFormat("Email value changed: {0}", email);
        }

        public void OnSubmit()
        {
            if (!string.IsNullOrEmpty(Username) && Regex.IsMatch(Username, "^[a-zA-Z0-9_-]{4,12}$")) return;
            _errors["errorMessage"] = "Please enter a valid username.";

            if (!string.IsNullOrEmpty(Email) &&
                Regex.IsMatch(Email, @"^\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$")) return;
            _errors["errorMessage"] = "Please enter a valid email.";
            
            _errors.Clear();
            Account.Useranme = Username;
            Account.Email = Email;
        }
    }
}