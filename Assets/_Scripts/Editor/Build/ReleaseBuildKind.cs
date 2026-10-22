using UnityEditor;
using UnityEditor.Build.Reporting;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The one answer every release guard asks: is the build being made a development build?
    /// (<see cref="UnityPipelineReleaseGuard"/>, <see cref="CreditsReleaseGuard"/>,
    /// <see cref="WeeklyChallengeTestModeBuildGuard"/> exempt development builds and fail release ones.)
    ///
    /// <para>The build's own options are the only authority. A player is a development build when
    /// <c>BuildOptions.Development</c> is in the options it was built with; the Build Profiles window,
    /// Unity Build Automation and every scripted build (<c>CosmicShoreBuildPipeline</c>,
    /// <c>WebToyboxBuild</c>) all pass it there. <c>EditorUserBuildSettings.development</c> is only the
    /// window's checkbox: a scripted build never reads it, so the guards used to OR it in and a release
    /// build made while it happened to be ticked was waved through all three guards.</para>
    /// </summary>
    public static class ReleaseBuildKind
    {
        public static bool IsDevelopment(BuildReport report) => IsDevelopment(report.summary.options);

        public static bool IsDevelopment(BuildOptions options) => (options & BuildOptions.Development) != 0;
    }
}
