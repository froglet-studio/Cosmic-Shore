using System;
using UnityEngine;

namespace CosmicShore.Integrations.PlayFab.PlayerModels

{
    public class PlayerSession
    {
        // Setting Names saved in Player Preferences
        private const string PlayerGuid = "PlayerGuid";
        private const string RememberLogin = "RememberLogin";

        // Login id is system guid
        // It will replace Device unique id as custom id for login on platform that is not IOS and Android if the session is remembered
        public string LoginId
        {
            get => PlayerPrefs.GetString(PlayerGuid, "");
            set
            {
                var guid = value ?? Guid.NewGuid().ToString();
                PlayerPrefs.SetString(RememberLogin, guid);
            }
        }
        // A flag in the UI that player can check if the session should be remembered
        // No login attempt is required for future sessions
        public bool IsRemembered
        {
            get => PlayerPrefs.GetInt(RememberLogin, 0) != 0;
            set => PlayerPrefs.SetInt(RememberLogin, value? 1:0);
        }

        // A flag that notifies player if they want to be force linked with current session (the session on the other device will be disconnected)
        public bool IsForceLink { get; set; }

        public void ForgetMe()
        {
            PlayerPrefs.DeleteKey(PlayerGuid);
            PlayerPrefs.DeleteKey(RememberLogin);
        }
    }
}