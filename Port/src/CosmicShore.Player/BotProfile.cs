using System;
using System.IO;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary>
    /// The committed returning-user profile an unattended run plays as: <c>Port/parity/profile</c>
    /// (prefs with the age prompt answered and analytics consent OFF, a player named "parity",
    /// nothing earned), copied fresh into a folder of the run's own and made this process's
    /// persistent data path.
    /// A first-time profile stops in the Authentication scene at the birth-year, consent and
    /// username prompts, which nobody answers in a headless run: the weekly training routine's
    /// first run (2026-10-11, a fresh container) sat there for 30 minutes and never trained. Parity
    /// replays need the same profile for a second reason: the display name keys the state
    /// channel's stats. Each process gets its own copy, so parallel training workers never share
    /// a prefs file.
    /// </summary>
    static class BotProfile
    {
        public const string TemplateFolder = "Port/parity/profile";

        /// <summary>
        /// Copies the template into <paramref name="dir"/> (emptied first) and points
        /// <see cref="Application.persistentDataPathOverride"/> at it. Returns false, with a line
        /// saying so, when the checkout has no template; the machine's profile then plays.
        /// </summary>
        public static bool UseFreshCopy(string dir, string tag)
        {
            string template = FindTemplate();
            if (template == null)
            {
                Console.WriteLine($"[{tag}] no {TemplateFolder} template; the machine's profile plays (a first-time one stops at the birth-year prompt)");
                return false;
            }
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            Directory.CreateDirectory(dir);
            foreach (var file in Directory.GetFiles(template))
                File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));
            Application.persistentDataPathOverride = dir;
            Console.WriteLine($"[{tag}] profile: fresh copy of {template} at {dir}");
            return true;
        }

        /// <summary>
        /// The template in the content project (COSMIC_SHORE_PROJECT), else in the checkout the
        /// binary or the working directory sits in: the content project can be a sparse worktree
        /// of another branch that carries no Port/ (Port/docs/AI_TRAINING.md builds against one).
        /// </summary>
        static string FindTemplate()
        {
            string root = CosmicShore.Content.AssetDatabase.FindProjectRoot();
            if (root != null && Directory.Exists(Path.Combine(root, TemplateFolder))) return Path.Combine(root, TemplateFolder);
            foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            {
                for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                {
                    string candidate = Path.Combine(d.FullName, TemplateFolder);
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
            return null;
        }

        /// <summary>
        /// A training process's profile: the template, unless COSMIC_SHORE_PROFILE names a machine
        /// profile on purpose. <paramref name="runDir"/> is the run's output folder (null: the temp
        /// folder); a worker's copy is named for its index.
        /// </summary>
        public static void PrepareForTraining(string runDir, int worker)
        {
            string chosen = Environment.GetEnvironmentVariable("COSMIC_SHORE_PROFILE");
            if (!string.IsNullOrWhiteSpace(chosen))
            {
                Console.WriteLine($"[train] profile: the machine profile '{chosen.Trim()}' (COSMIC_SHORE_PROFILE)");
                return;
            }
            string parent = runDir ?? Path.Combine(Path.GetTempPath(), "cosmic-shore-train");
            UseFreshCopy(Path.Combine(parent, worker >= 0 ? $"profile-w{worker}" : "profile"), "train");
        }
    }
}
