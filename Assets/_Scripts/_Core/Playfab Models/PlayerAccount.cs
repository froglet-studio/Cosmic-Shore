using PlayFab;

namespace _Scripts._Core.Playfab_Models
{
    public class PlayerAccount{
        // Player Master ID in PlayFab
        public string PlayFabId { get; set; }
        // PlayFab Authentication context 
        public PlayFabAuthenticationContext AuthContext { get; set; }
        // Player Display Name in PlayFab
        public string PlayerDisplayName { get; set; }
        // Flag for newly created account
        public bool IsNewlyCreated { get; set; }
    }
}
