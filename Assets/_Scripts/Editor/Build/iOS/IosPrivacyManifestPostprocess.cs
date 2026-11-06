using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using System.IO;
using UnityEditor.iOS.Xcode;
#endif

namespace CosmicShore.Editor
{
    /// <summary>
    /// Puts the app's privacy manifest (<see cref="SourcePath"/>) into every iOS build's Xcode project, as a resource of
    /// the app target (launch blocker #14; Docs/IOS_BUILD.md "Privacy manifest"). Without one, App Store Connect rejects
    /// an upload that uses required-reason APIs (ITMS-91053), and the game does: PlayerPrefs, file timestamps and
    /// elapsed-time clocks.
    ///
    /// <para>If the project already has a <c>PrivacyInfo.xcprivacy</c> at the root of the app target (Unity or a plugin
    /// wrote one), ours is merged into it rather than added beside it, because two files of that name in one target do
    /// not build. The merge only adds (<see cref="PrivacyManifest.Merge"/>), and a re-run on an appended Xcode project
    /// changes nothing. Unity's own manifest inside UnityFramework is a different bundle and is left alone.</para>
    ///
    /// <para>The body is guarded by <c>UNITY_IOS</c>, like <see cref="IosExportCompliancePostprocess"/>: the Xcode API
    /// exists only where iOS Build Support is installed. Lives under an <c>Editor/</c> folder, so no player compiles it.</para>
    /// </summary>
    public sealed class IosPrivacyManifestPostprocess : IPostprocessBuildWithReport
    {
        public const string SourcePath = "Assets/_Scripts/Editor/Build/iOS/" + PrivacyManifest.FileName;

        public int callbackOrder => 10;

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            if (report.summary.platform != BuildTarget.iOS) return;
            if (!File.Exists(SourcePath))
                throw new BuildFailedException($"{SourcePath} is missing; an iOS build without its privacy manifest is rejected by App Store Connect.");

            string output = report.summary.outputPath;
            string manifestPath = Path.Combine(output, PrivacyManifest.FileName);
            string existing = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null;
            File.WriteAllText(manifestPath, PrivacyManifest.Merge(existing, File.ReadAllText(SourcePath)));

            string projectPath = PBXProject.GetPBXProjectPath(output);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            if (project.FindFileGuidByProjectPath(PrivacyManifest.FileName) == null)
            {
                string fileGuid = project.AddFile(PrivacyManifest.FileName, PrivacyManifest.FileName, PBXSourceTree.Source);
                project.AddFileToBuild(project.GetUnityMainTargetGuid(), fileGuid);
                project.WriteToFile(projectPath);
            }
#endif
        }
    }
}
