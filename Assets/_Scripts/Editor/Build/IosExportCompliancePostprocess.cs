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
    /// Writes <c>ITSAppUsesNonExemptEncryption = NO</c> into every iOS build's <c>Info.plist</c>, so
    /// App Store Connect stops holding each uploaded build at "Missing Compliance" until somebody
    /// answers the export-compliance questions by hand.
    ///
    /// <para>The declaration it makes: the game's only encryption is HTTPS to Unity Gaming Services
    /// and PostHog, and DTLS on the Relay transport. All of it uses standard algorithms and only
    /// supports gameplay, which the U.S. EAR's Category 5 Part 2 "Note 4" exclusion (games and
    /// gaming) covers. The company owns this answer. If legal decides otherwise, delete this file
    /// and answer the questions in App Store Connect instead. Recorded in
    /// <c>Docs/PLATFORM_UNIFICATION.md</c> §3.9.</para>
    ///
    /// <para>The body is guarded by <c>UNITY_IOS</c>, because <c>UnityEditor.iOS.Xcode</c> exists only
    /// where iOS Build Support is installed, and an iOS build always runs with the iOS target active.
    /// Lives under an <c>Editor/</c> folder, so no player ever compiles it.</para>
    /// </summary>
    public sealed class IosExportCompliancePostprocess : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            if (report.summary.platform != BuildTarget.iOS) return;

            string plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
#endif
        }
    }
}
