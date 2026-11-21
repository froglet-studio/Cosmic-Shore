using System;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Who this build is: version, build number, commit, ref, release channel, the UGS environment
    /// it talks to, when it was built and whether it is a development build. One record, written
    /// into <c>Resources/Generated/build_stamp.json</c> by the editor's <c>BuildStampProcessor</c>
    /// while a player is built and removed again afterwards (the file is git-ignored), so every
    /// player carries its own identity and the working tree stays clean.
    ///
    /// <para><b>Where it shows.</b> The settings panel's version line
    /// (<c>GameSettingsPanelController</c>), crash-report metadata (<see cref="CrashReportingService"/>),
    /// analytics person properties (<see cref="AnalyticsServiceFacade.IdentifyPlayer"/>) and the UGS
    /// environment <see cref="AuthenticationServiceFacade"/> initializes with.</para>
    ///
    /// <para><b>In the Editor, or a player built without the stamp,</b> it answers an Editor stamp:
    /// commit <c>editor</c>, environment <c>development</c> (unless <c>CS_UGS_ENVIRONMENT</c> names
    /// one). A missing stamp never sends a player to production. Docs/LAUNCH/BUILD_STAMP.md.</para>
    /// </summary>
    public static class BuildStamp
    {
        /// <summary>Resources path of the generated stamp (no extension).</summary>
        public const string ResourcePath = "Generated/build_stamp";

        public const string Development = "development";
        public const string Production = "production";

        /// <summary>The commit and channel an Editor (or unstamped) run reports.</summary>
        public const string EditorValue = "editor";

        /// <summary>Environment variable naming the UGS environment (build machines; also read by an Editor run).</summary>
        public const string UgsEnvironmentEnv = "CS_UGS_ENVIRONMENT";

        /// <summary>Characters of the commit sha shown on screen.</summary>
        public const int ShortCommitLength = 8;

        static BuildStampData _current;

        /// <summary>This run's stamp, loaded once.</summary>
        public static BuildStampData Current => _current ??= Load();

        // Enter Play Mode without a domain reload keeps statics: drop the cache each play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => _current = null;

        static BuildStampData Load()
        {
            string explicitEnvironment = null;
            try { explicitEnvironment = Environment.GetEnvironmentVariable(UgsEnvironmentEnv); }
            catch (Exception) { /* no environment access on this platform: not given */ }

            if (Application.isEditor)
                return EditorStamp(explicitEnvironment);

            string error = null;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset != null && TryParse(asset.text, out var stamp, out error))
                return stamp;

            string why = asset == null
                ? $"[BuildStamp] This player carries no build stamp (Resources/{ResourcePath}); it reports itself as an Editor build and uses UGS '{Development}'."
                : $"[BuildStamp] The build stamp could not be read ({error}); this player reports itself as an Editor build and uses UGS '{Development}'.";
            // A release player without its stamp is a broken build: loud. A development player
            // (or another host running these scripts) only notes it.
            if (Debug.isDebugBuild) CSDebug.LogVerbose(CSLogChannel.Boot, why);
            else Debug.LogWarning(why);
            return EditorStamp(null);
        }

        /// <summary>
        /// The stamp an Editor run (or an unstamped player) reports. The environment follows
        /// <see cref="ResolveUgsEnvironment"/> with the Editor's answer; an invalid explicit value
        /// is reported and ignored, because a play session cannot be refused the way a build is.
        /// </summary>
        public static BuildStampData EditorStamp(string explicitEnvironment)
        {
            string environment;
            try
            {
                environment = ResolveUgsEnvironment(explicitEnvironment, developmentBuild: true, isEditor: true);
            }
            catch (ArgumentException ex)
            {
                Debug.LogError($"[BuildStamp] {ex.Message} Using '{Development}'.");
                environment = Development;
            }

            return new BuildStampData
            {
                version = Application.version,
                buildNumber = EditorValue,
                commit = EditorValue,
                gitRef = string.Empty,
                channel = EditorValue,
                ugsEnvironment = environment,
                builtAtUtc = string.Empty,
                development = true,
            };
        }

        /// <summary>
        /// The UGS environment a build talks to. An explicit value (build argument or environment
        /// variable) wins; otherwise a development build and the Editor use <c>development</c> and a
        /// release player uses <c>production</c>. Anything but those two names throws
        /// <see cref="ArgumentException"/>: the build refuses it (<c>BuildStampProcessor</c>).
        /// </summary>
        public static string ResolveUgsEnvironment(string explicitValue, bool developmentBuild, bool isEditor)
        {
            if (!string.IsNullOrWhiteSpace(explicitValue))
            {
                if (TryNormalizeEnvironment(explicitValue, out string normalized))
                    return normalized;
                throw new ArgumentException(
                    $"UGS environment '{explicitValue.Trim()}' is not one this game has. " +
                    $"Use '{Development}' or '{Production}' (-ugsEnvironment / {UgsEnvironmentEnv}).");
            }

            return developmentBuild || isEditor ? Development : Production;
        }

        /// <summary>True for <c>development</c> or <c>production</c> (any case, trimmed); the lower-case name out.</summary>
        public static bool TryNormalizeEnvironment(string value, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string v = value.Trim().ToLowerInvariant();
            if (v != Development && v != Production) return false;
            normalized = v;
            return true;
        }

        /// <summary>
        /// Reads a stamp. Refuses empty or malformed JSON, a stamp with no version, and an
        /// environment other than development / production.
        /// </summary>
        public static bool TryParse(string json, out BuildStampData stamp, out string error)
        {
            stamp = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "the stamp is empty";
                return false;
            }

            BuildStampData parsed;
            try
            {
                parsed = JsonUtility.FromJson<BuildStampData>(json);
            }
            catch (Exception ex)
            {
                error = $"the stamp is not JSON ({ex.Message})";
                return false;
            }

            if (parsed == null || string.IsNullOrWhiteSpace(parsed.version))
            {
                error = "the stamp names no version";
                return false;
            }

            if (!TryNormalizeEnvironment(parsed.ugsEnvironment, out string environment))
            {
                error = $"the stamp's UGS environment '{parsed.ugsEnvironment}' is not '{Development}' or '{Production}'";
                return false;
            }

            parsed.ugsEnvironment = environment;
            parsed.buildNumber ??= string.Empty;
            parsed.commit ??= string.Empty;
            parsed.gitRef ??= string.Empty;
            parsed.channel ??= string.Empty;
            parsed.builtAtUtc ??= string.Empty;

            stamp = parsed;
            error = null;
            return true;
        }

        /// <summary>The first <see cref="ShortCommitLength"/> characters of a sha; other values (editor, unknown) as they are.</summary>
        public static string ShortCommit(string commit)
        {
            if (string.IsNullOrEmpty(commit)) return string.Empty;
            return commit.Length > ShortCommitLength && IsHex(commit) ? commit.Substring(0, ShortCommitLength) : commit;
        }

        /// <summary>
        /// Fills an authored line's build tokens: {version} {build} {commit} {ref} {channel} {env}.
        /// An empty template stays empty (<see cref="TemplateText.Fill"/>).
        /// </summary>
        public static string Fill(string template, BuildStampData stamp)
        {
            if (stamp == null) return TemplateText.Fill(template);
            return TemplateText.Fill(template,
                "version", stamp.version,
                "build", stamp.buildNumber,
                "commit", ShortCommit(stamp.commit),
                "ref", stamp.gitRef,
                "channel", stamp.channel,
                "env", stamp.ugsEnvironment);
        }

        /// <summary>One line for logs: "0.2.0 (412) 1a2b3c4d playtest/production".</summary>
        public static string Describe(BuildStampData s) =>
            s == null ? string.Empty : $"{s.version} ({s.buildNumber}) {ShortCommit(s.commit)} {s.channel}/{s.ugsEnvironment}";

        static bool IsHex(string s)
        {
            foreach (char c in s)
                if (!Uri.IsHexDigit(c)) return false;
            return true;
        }
    }

    /// <summary>The build stamp's fields, as written to <c>build_stamp.json</c> (JsonUtility: public fields).</summary>
    [Serializable]
    public sealed class BuildStampData
    {
        public string version;
        public string buildNumber;
        public string commit;
        public string gitRef;
        public string channel;
        public string ugsEnvironment;
        public string builtAtUtc;
        public bool development;
    }
}
