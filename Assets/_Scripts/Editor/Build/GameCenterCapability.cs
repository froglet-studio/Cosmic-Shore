#if UNITY_IOS
using System.IO;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Adds the Game Center capability to the Xcode project when <see cref="MobileSignInConfigSO.SignInWithGameCenter"/>
    /// is on (fair play, Threat Board T1; GameCenterSignIn). Off, the project is left exactly as before, so a build
    /// whose App ID has no Game Center still signs. The App ID needs Game Center enabled on the Apple developer site
    /// before the switch goes on.
    /// </summary>
    public static class GameCenterCapability
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS || !MobileSignInConfigSO.LoadOrDefault().SignInWithGameCenter) return;

            var projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var entitlements = Path.Combine("Unity-iPhone", "CosmicShore.entitlements");
            var manager = new ProjectCapabilityManager(projectPath, entitlements, null, project.GetUnityMainTargetGuid());
            manager.AddGameCenter();
            manager.WriteToFile();
        }
    }
}
#endif
