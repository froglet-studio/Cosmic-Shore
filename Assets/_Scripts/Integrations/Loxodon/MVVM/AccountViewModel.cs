using System;
using Loxodon.Framework.Observables;

namespace CosmicShore.Integrations.Loxodon.MVVM
{
    public class AccountViewModel : ObservableObject
    {
        private int _id;
        private string _username;
        private string _password;
        private string _email;
        private DateTime _birthday;
        private readonly ObservableProperty<string> _address = new();

        public int ID
        {
            get => _id;
            set => Set(ref _id, value);
        }

        public string Useranme
        {
            get => _username;
            set => Set(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => Set(ref _password, value);
        }
        
        public string Email
        {
            get => _email;
            set => Set(ref _email, value);
        }

        public DateTime Birthday
        {
            get => _birthday;
            set => Set(ref _birthday, value);
        }

        public ObservableProperty<string> Address => _address;
    }
}
