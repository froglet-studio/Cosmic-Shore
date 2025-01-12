using UnityEngine;


namespace CosmicShore.Utilities
{
    /// <summary>
    /// Singleton class which saves/loads local-client settings.
    /// (This is just a wrapper around PlayerPrefs system,
    /// so that all the calls are in the same place.)
    /// </summary>
    public static class ClientPrefs
    {
        private const string MASTER_VOLUME_KEY = "MasterVolume";
        private const string MUSIC_VOLUME_KEY = "MusicVolume";
        private const string CLIENT_GUID_KEY = "Client_guid";
        private const string AVAILABLE_PROFILES_KEY = "AvailableProfiles";

        private const float DEFAULT_MASTER_VOLUME = 0.5f;
        private const float DEFAULT_MUSIC_VOLUME = 0.8f;

        public static float GetMasterVolume()
        {
            return PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, DEFAULT_MASTER_VOLUME);
        }

        public static void SetMasterVolume(float volume)
        {
            PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, volume);
        }

        public static float GetMusicVolume()
        {
            return PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, DEFAULT_MUSIC_VOLUME);
        }

        public static void SetMusicVolume(float volume)
        {
            PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, volume);
        }

        /// <summary>
        /// Either loads a GUID string from Unity preferences, or creates one and checkpoints it,
        /// then returns it.
        /// </summary>
        /// <returns>The GUID that uniquely identifies this client install, in string form.</returns>
        public static string GetGUID()
        {
            if (PlayerPrefs.HasKey(CLIENT_GUID_KEY))
            {
                return PlayerPrefs.GetString(CLIENT_GUID_KEY);
            }
            else
            {
                string guid = System.Guid.NewGuid().ToString();
                PlayerPrefs.SetString(CLIENT_GUID_KEY, guid);
                return guid;
            }
        }

        public static string GetAvailableProfiles()
        {
            return PlayerPrefs.GetString(AVAILABLE_PROFILES_KEY);
        }

        public static void SetAvailableProfiles(string availableProfiles)
        {
            PlayerPrefs.SetString(availableProfiles, availableProfiles);
        }
    }
}

