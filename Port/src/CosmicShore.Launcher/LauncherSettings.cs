using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CosmicShore.Launcher
{
    /// <summary>Where the launcher keeps its workspace when the user has no clone of their own.</summary>
    public enum WorkspaceMode
    {
        /// <summary>A shallow clone the launcher owns under its data folder. Nothing of the user's is touched.</summary>
        Managed = 0,
        /// <summary>A git worktree beside the user's existing clone: shares its objects, never touches its checkout.</summary>
        WorktreeOfMyClone = 1,
    }

    /// <summary>How the BUILD page makes an iPhone build.</summary>
    public enum IosMode
    {
        /// <summary>GitHub's free Mac runner compiles an unsigned .ipa; Sideloadly signs it. No Mac needed.</summary>
        GitHub = 0,
        /// <summary>An Xcode project to open on a Mac, like Unity's iOS export.</summary>
        Xcode = 1,
        /// <summary>This Mac builds and signs the .ipa itself.</summary>
        ThisMac = 2,
    }

    /// <summary>
    /// Everything the launcher remembers between runs. Persisted as JSON in the launcher's data
    /// folder; unknown or missing fields fall back to these defaults.
    /// </summary>
    public sealed class LauncherSettings
    {
        public string RemoteUrl { get; set; } = "https://github.com/froglet-studio/Cosmic-Shore.git";
        public string Branch { get; set; } = "bleeding-edge";
        public WorkspaceMode Workspace { get; set; } = WorkspaceMode.Managed;
        public string MyClonePath { get; set; } = "";
        /// <summary>The clone Unity has open (Launch Prisma passes it with --clone): PLAY follows its branch.</summary>
        public string UnityClonePath { get; set; } = "";
        /// <summary>PLAY's branch follows the branch Unity / GitHub Desktop has checked out, until another is picked.</summary>
        public bool FollowClone { get; set; } = true;
        /// <summary>Optional GitHub token (read-only is enough) for testers whose git has no stored sign-in.</summary>
        public string GitHubToken { get; set; } = "";
        public string ManagedPath { get; set; } = "";

        // Play
        public bool PullBeforePlay { get; set; } = true;
        public string Resolution { get; set; } = "1600x900";
        public bool Fullscreen { get; set; }
        public string StartScene { get; set; } = "";
        public bool Audio { get; set; } = true;
        public bool Network { get; set; } = true;
        public bool MobileRenderPath { get; set; }
        public bool VerboseLogs { get; set; }
        public string Profile { get; set; } = "";
        public string ExtraArgs { get; set; } = "";
        public bool ReleaseBuild { get; set; } = true;

        // Build
        public string AndroidAbis { get; set; } = "arm64";
        public bool AndroidBundle { get; set; }
        public bool DebugBuild { get; set; }
        public string KeystorePath { get; set; } = "";
        public string KeystoreAlias { get; set; } = "";
        public IosMode Ios { get; set; } = OperatingSystem.IsMacOS() ? IosMode.ThisMac : IosMode.GitHub;

        // Claude
        // Look (SETTINGS > LOOK)
        public int Background { get; set; }
        public int Theme { get; set; }
        public string BackgroundImage { get; set; } = "";
        public float BackgroundMotion { get; set; } = 1f;
        public float BackgroundDim { get; set; } = 0.25f;
        public bool Animations { get; set; } = true;
        public bool TourDone { get; set; }

        public string AnthropicApiKey { get; set; } = "";
        public string ClaudeModel { get; set; } = "";
        public string ClaudeEffort { get; set; } = "";
        public bool VoiceReplies { get; set; }
        public string ClaudePath { get; set; } = "";
        public int ChatMode { get; set; }
        // A milestone run's budget (each message is one run): agentic turns, wall-clock minutes,
        // and an optional dollar cap. A run that hits one stops and leaves what it tried on the board.
        public int MilestoneMaxTurns { get; set; } = 80;
        public int MilestoneMaxMinutes { get; set; } = 60;
        public double MilestoneMaxUsd { get; set; }

        // Toolchain
        public string DotnetPath { get; set; } = "";
        public string GitPath { get; set; } = "";

        [JsonIgnore] public static string DataDir { get; } = ResolveDataDir();
        [JsonIgnore] static string FilePath => Path.Combine(DataDir, "launcher.json");

        public string ResolvedManagedPath =>
            string.IsNullOrWhiteSpace(ManagedPath) ? Path.Combine(DataDir, "workspace") : ManagedPath;

        static string ResolveDataDir()
        {
            // A second, separate Prisma (and the launcher tests) can point everything elsewhere.
            if (Environment.GetEnvironmentVariable("PRISMA_DATA_DIR") is { Length: > 0 } own)
            {
                Directory.CreateDirectory(own);
                return own;
            }
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(baseDir))
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            var dir = Path.Combine(baseDir, "Prisma");
            // Settings, sessions, tracks and versions from before the rename move over once.
            var legacy = Path.Combine(baseDir, "FrogletEngine");
            if (!Directory.Exists(dir) && Directory.Exists(legacy))
                try { Directory.Move(legacy, dir); } catch { /* in use: start fresh beside it */ }
            Directory.CreateDirectory(dir);
            return dir;
        }

        static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        /// <summary>True when no settings file existed yet (first launch on this machine).</summary>
        [JsonIgnore] public static bool FirstRun { get; private set; }

        public static LauncherSettings Load()
        {
            FirstRun = !File.Exists(FilePath);
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(FilePath), Json) ?? new();
            }
            catch (Exception)
            {
                // A corrupt settings file must never stop the launcher opening; start from defaults.
            }
            return new LauncherSettings();
        }

        public void Save()
        {
            try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json)); }
            catch (Exception) { /* read-only profile: settings simply do not persist */ }
        }
    }
}
