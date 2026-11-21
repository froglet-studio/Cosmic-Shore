#if UNITY_ANDROID
using System.IO;
using CosmicShore.ScriptableObjects;
using UnityEditor.Android;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Writes the Play Games project ID into the Android app's manifest while
    /// <see cref="MobileSignInConfigSO.SignInWithPlayGames"/> is on (fair play, Threat Board T1; PlayGamesSignIn). Play
    /// Games reads it from the <c>com.google.android.gms.games.APP_ID</c> meta-data, which must name a string resource.
    /// Off, the generated project is left exactly as before.
    /// </summary>
    public class PlayGamesAppId : IPostGenerateGradleAndroidProject
    {
        const string ResourceName = "cs_play_games_app_id";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var config = MobileSignInConfigSO.LoadOrDefault();
            if (!config.SignInWithPlayGames) return;

            var values = Path.Combine(unityLibraryPath, "src", "main", "res", "values");
            Directory.CreateDirectory(values);
            File.WriteAllText(Path.Combine(values, "cs_play_games.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n" +
                $"    <string name=\"{ResourceName}\" translatable=\"false\">{config.PlayGamesProjectId}</string>\n" +
                "</resources>\n");

            var manifestPath = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
            var manifest = File.ReadAllText(manifestPath);
            if (manifest.Contains("com.google.android.gms.games.APP_ID")) return;
            const string close = "</application>";
            int at = manifest.LastIndexOf(close, System.StringComparison.Ordinal);
            if (at < 0)
            {
                Debug.LogError($"[PlayGames] No </application> in {manifestPath}; the Play Games project ID was not written.");
                return;
            }
            manifest = manifest.Insert(at,
                $"    <meta-data android:name=\"com.google.android.gms.games.APP_ID\" android:value=\"@string/{ResourceName}\" />\n    ");
            File.WriteAllText(manifestPath, manifest);
        }
    }
}
#endif
