using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Hard release-build gate for the weekly challenge's TEST MODE
    /// (<see cref="WeeklyChallengeCatalogSO.TestSettings"/>).
    ///
    /// <para>The test settings can pin the challenge to one pool entry, shrink a "day" to a few
    /// minutes, lift the once-per-day limit and scale the clock. The runtime already refuses to
    /// apply any of it outside the editor and development builds, so a flag left on cannot change
    /// a customer's game — but "cannot change behaviour" is a promise about a code path, and this
    /// makes it a promise about the ASSET too. A build that ships with test mode set is a build
    /// whose data says something nobody meant, and the cheapest place to find that out is here.</para>
    ///
    /// <para>Development builds are exempt on purpose: that is the sanctioned way to test the
    /// cycle in a player. A missing catalog is a no-op — the weekly challenge is simply off.</para>
    ///
    /// <para>Lives under an <c>Editor/</c> folder → <c>Assembly-CSharp-Editor</c>, never in a
    /// player build, so the guard cannot itself reach the IL2CPP linker.</para>
    /// </summary>
    public sealed class WeeklyChallengeTestModeBuildGuard : IPreprocessBuildWithReport
    {
        // Alongside the pipeline guard: fail a doomed release build in seconds, before any
        // expensive build work starts.
        public int callbackOrder => -9999;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool development = EditorUserBuildSettings.development ||
                               (report.summary.options & BuildOptions.Development) != 0;
            if (development) return;

            var catalog = Resources.Load<WeeklyChallengeCatalogSO>(WeeklyChallengeCatalogSO.ResourcePath);
            if (catalog == null || catalog.test == null || !catalog.test.enabled) return;

            throw new BuildFailedException(
                "Weekly challenge TEST MODE is enabled in " +
                $"Assets/Resources/{WeeklyChallengeCatalogSO.ResourcePath}.asset.\n\n" +
                "Switch it off in FrogletTools > Game Modes > Weekly Challenge before making a " +
                "release build. (Development builds are exempt — that is the sanctioned way to " +
                "test the cycle in a player.)");
        }
    }
}
