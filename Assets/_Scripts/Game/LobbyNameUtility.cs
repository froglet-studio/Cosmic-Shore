using System;
using System.Text;
using UnityEngine;

public static class LobbyNameUtility
{
    const string PREF_KEY = "LastLobbyName";
    const string CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// Generates a random alphanumeric string of the given length,
    /// stores it in PlayerPrefs, and returns it.
    /// </summary>
    public static string GenerateRandomLobbyName(int length = 8)
    {
        var sb = new StringBuilder(length);
        var rng = new System.Random();

        for (int i = 0; i < length; i++)
            sb.Append(CHARS[rng.Next(CHARS.Length)]);

        string name = sb.ToString();
        PlayerPrefs.SetString(PREF_KEY, name);
        PlayerPrefs.Save();
        return name;
    }

    /// <summary>
    /// Retrieves the last generated lobby name, or null if none exists.
    /// </summary>
    public static string GetLastLobbyName()
    {
        if (PlayerPrefs.HasKey(PREF_KEY))
            return PlayerPrefs.GetString(PREF_KEY);
        return null;
    }

    /// <summary>
    /// Clears the stored lobby name (if you ever need to reset).
    /// </summary>
    public static void ClearLastLobbyName()
    {
        PlayerPrefs.DeleteKey(PREF_KEY);
    }
}
