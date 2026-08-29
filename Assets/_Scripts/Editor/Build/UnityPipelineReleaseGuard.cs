using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Hard release-build gate for the Unity CLI pipeline package (<c>com.unity.pipeline</c>).
    ///
    /// The package exists so a terminal session can drive the OPEN editor (compile checks, Play
    /// mode, <c>unity command</c> / <c>eval</c>). That is development tooling with a remote command
    /// surface — it must NEVER reach a build a customer runs, and we ship paid Steam Early Access
    /// builds from this pipeline. This guard fails any NON-development build whose shipped content
    /// (build scenes — including every prefab they instantiate — plus preloaded assets and
    /// everything under a player-visible <c>Resources/</c> folder) would carry the package's
    /// runtime component or reference any of its runtime assets.
    ///
    /// Lives under an <c>Editor/</c> folder → compiles into <c>Assembly-CSharp-Editor</c>, which is
    /// never included in a player build, so the guard itself can't trip the IL2CPP linker.
    ///
    /// Development builds are exempt on purpose: they are the sanctioned way to debug the CLI
    /// integration in a player. If the package is not installed there is nothing to guard and the
    /// callback is a no-op, so removing the package some day requires no change here.
    /// </summary>
    public sealed class UnityPipelineReleaseGuard : IPreprocessBuildWithReport
    {
        const string PackageRoot = "Packages/com.unity.pipeline";

        // Run before every other preprocess step (EndConditionBuildRestore sits at 0) so a doomed
        // release build fails in seconds, before any expensive build work starts.
        public int callbackOrder => -10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            // EditorUserBuildSettings.development covers editor-driven builds; the report options
            // cover scripted builds (CosmicShoreBuildPipeline passes BuildOptions.Development
            // explicitly and does not necessarily touch the editor setting). Either one marks the
            // build as development → allowed.
            bool development = EditorUserBuildSettings.development ||
                               (report.summary.options & BuildOptions.Development) != 0;
            if (development)
                return;

            if (!AssetDatabase.IsValidFolder(PackageRoot))
                return; // package not installed — nothing to guard

            // Every MonoBehaviour the package could attach to shipped content, keyed by script
            // path, resolved at build time so this never hardcodes a type name from an
            // experimental package that renames things between versions.
            var runtimeComponents = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { PackageRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                var cls = script != null ? script.GetClass() : null;
                if (cls != null && typeof(MonoBehaviour).IsAssignableFrom(cls))
                    runtimeComponents[path] = cls.FullName;
            }

            var offenders = new List<string>();

            // 1) Enabled build scenes. GetDependencies is recursive, so a pipeline component nested
            //    inside a prefab a scene instantiates is caught too.
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled || string.IsNullOrEmpty(scene.path))
                    continue;
                Collect(scene.path, AssetDatabase.GetDependencies(scene.path, true),
                    runtimeComponents, offenders);
            }

            // 2) Preloaded assets and everything under a non-Editor Resources/ folder — all of it
            //    ships and is loadable at runtime with no scene reference at all (this is also how
            //    a package could self-bootstrap a runtime component into the player).
            string[] shippedLoose = PlayerSettings.GetPreloadedAssets()
                .Where(a => a != null)
                .Select(AssetDatabase.GetAssetPath)
                .Concat(AssetDatabase.GetAllAssetPaths()
                    .Where(p => p.Contains("/Resources/") && !p.Contains("/Editor/")))
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToArray();
            if (shippedLoose.Length > 0)
                Collect("Resources / preloaded assets",
                    AssetDatabase.GetDependencies(shippedLoose, true), runtimeComponents, offenders);

            if (offenders.Count == 0)
                return;

            throw new BuildFailedException(
                "RELEASE BUILD BLOCKED — the com.unity.pipeline (Unity CLI) runtime component would ship.\n\n" +
                string.Join("\n", offenders.Distinct()) + "\n\n" +
                "The Unity CLI pipeline is experimental development tooling that exposes a command/eval " +
                "surface into the editor. Its runtime component must never be included in a " +
                "non-development build: these builds go to paying Steam Early Access customers, and " +
                "shipping a dev command surface in one is a security and certification incident, not a bug.\n\n" +
                "Fix: remove the offending component / reference from the content listed above (the package " +
                "itself may stay in Packages/manifest.json — only shipped CONTENT referencing it is blocked). " +
                "To debug the CLI integration inside a player, make a DEVELOPMENT build instead " +
                "(EditorUserBuildSettings.development, or BuildOptions.Development in a scripted build) — " +
                "this guard only blocks release builds.\n" +
                "Guard: Assets/_Scripts/Editor/Build/UnityPipelineReleaseGuard.cs");
        }

        static void Collect(string owner, string[] dependencies,
            Dictionary<string, string> runtimeComponents, List<string> offenders)
        {
            foreach (string dep in dependencies)
            {
                if (!dep.StartsWith(PackageRoot + "/"))
                    continue;
                offenders.Add(runtimeComponents.TryGetValue(dep, out string typeName)
                    ? $"  • {owner} → component {typeName} ({dep})"
                    : $"  • {owner} → {dep}");
            }
        }
    }
}
