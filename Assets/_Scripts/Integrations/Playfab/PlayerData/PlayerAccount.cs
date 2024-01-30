using PlayFab;

namespace CosmicShore.Integrations.Playfab.PlayerModels
{
    public class PlayerAccount{
        // Player Master ID in PlayFab
        public string PlayFabId { get; set; }
        
        // Player Username, need to be unique, required when registering for an account
        public string Username { get; set; }
        // PlayFab Authentication context 
        public PlayFabAuthenticationContext AuthContext { get; set; }
        // Player Display Name in PlayFab
        public string PlayerDisplayName { get; set; }
        // Flag for newly created account
        public bool IsNewlyCreated { get; set; }
    }
}
