using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Hard release-build gate for the in-game credits manifest.
    ///
    /// <para>FMOD's EULA (<c>Assets/Plugins/FMOD/LICENSE.txt</c>, clause 3) requires an in-game
    /// credit line containing the words <c>FMOD</c> and <c>Firelight Technologies Pty Ltd.</c> —
    /// on <b>every</b> tier, free/Indie/Basic alike, with no exemption. We ship paid Steam Early
    /// Access builds, so a build with no credits data is a licence breach, not a missing feature.
    /// This guard fails any NON-development build whose <c>Assets/Resources/CreditsManifest.asset</c>
    /// is absent or has lost either required string.</para>
    ///
    /// <para>A credits screen that can be silently emptied is worth very little: the manifest is an
    /// ordinary authored asset that anyone can edit, and nothing else in the project would notice
    /// the line going missing. That is the whole reason this exists, and it is modelled directly on
    /// <see cref="UnityPipelineReleaseGuard"/>.</para>
    ///
    /// <para>Development builds are exempt, exactly as the pipeline guard does it — an artist
    /// iterating in a dev build should not be blocked on legal text. The offline twin,
    /// <c>Tools/Build/check_credits_manifest.py --check</c>, answers the same question in CI and in
    /// a session with no editor.</para>
    ///
    /// <para>Lives under an <c>Editor/</c> folder → compiles into <c>Assembly-CSharp-Editor</c>,
    /// which is never in a player build, so the guard cannot reach the IL2CPP linker.</para>
    ///
    /// <para><b>What this does NOT prove:</b> that the screen is reachable. It checks the DATA. A
    /// manifest with a perfect FMOD line and no way to open the credits screen still discharges
    /// nothing — reachability is verified in the editor and recorded in
    /// <c>Docs/THIRD_PARTY_REGISTER.md</c> §7.</para>
    /// </summary>
    public sealed class CreditsReleaseGuard : IPreprocessBuildWithReport
    {
        const string ManifestAssetPath = "Assets/Resources/CreditsManifest.asset";

        // Alongside the pipeline guard (-10000): a doomed release build should fail in seconds,
        // before any expensive build work starts.
        public int callbackOrder => -9999;

        public void OnPreprocessBuild(BuildReport report)
        {
            // EditorUserBuildSettings.development covers editor-driven builds; the report options
            // cover scripted builds. Either one marks the build as development → allowed.
            bool development = EditorUserBuildSettings.development ||
                               (report.summary.options & BuildOptions.Development) != 0;
            if (development)
                return;

            // Load through the same Resources path the RUNTIME uses, not by asset path: that is
            // what proves the shipped game can actually find it. An asset sitting outside a
            // Resources folder would load fine by path here and be missing in the player.
            var manifest = CreditsManifestSO.Load();
            if (manifest == null)
            {
                Fail($"no credits manifest was found at Resources/{CreditsManifestSO.ResourcePath} " +
                     $"(expected the asset at {ManifestAssetPath}).",
                     "Restore the manifest asset. It must live under a Resources/ folder so the " +
                     "runtime can load it with no per-scene wiring.");
                return;
            }

            if (manifest.SatisfiesFmodCredit())
                return;

            Fail("the credits manifest does not contain FMOD's required credit line.\n" +
                 $"  Required, verbatim: \"{CreditsManifestSO.RequiredFmodName}\" and " +
                 $"\"{CreditsManifestSO.RequiredFmodCompany}\"",
                 "Restore the credit in the manifest's MIDDLEWARE section. The conventional " +
                 "accepted form (www.fmod.com/attribution) is:\n" +
                 "    Made with FMOD Studio by Firelight Technologies Pty Ltd.\n" +
                 "  Note the trailing period on the company name — it is inside the words the " +
                 "EULA quotes.");
        }

        static void Fail(string what, string fix)
        {
            throw new BuildFailedException(
                "RELEASE BUILD BLOCKED — the in-game FMOD credit is missing.\n\n" +
                "  " + what + "\n\n" +
                "FMOD's EULA (Assets/Plugins/FMOD/LICENSE.txt, clause 3) requires an in game credit " +
                "line containing the words \"FMOD\" and \"Firelight Technologies Pty Ltd.\" on ALL " +
                "licence tiers — free, Indie and Basic. These builds go to paying Steam Early Access " +
                "customers, so shipping without it is a licence breach rather than a missing feature.\n\n" +
                "Fix: " + fix + "\n\n" +
                "To iterate without this gate, make a DEVELOPMENT build — it only blocks release builds.\n" +
                "Offline equivalent: python3 Tools/Build/check_credits_manifest.py --check\n" +
                "Guard: Assets/_Scripts/Editor/Build/CreditsReleaseGuard.cs");
        }
    }
}
