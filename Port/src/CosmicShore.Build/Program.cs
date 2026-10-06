using System;
using System.Collections.Generic;
using System.IO;

namespace CosmicShore.Build
{
    /// <summary>
    /// cs-build — Unity's File > Build Settings for the port.
    ///
    ///   cs-build content  [--out DIR]                     player data only (what ships)
    ///   cs-build android  [--out FILE.apk|FILE.aab] [--debug] [--keystore K --alias A]
    ///   cs-build ios      [--out DIR] [--unsigned] [--xcode]   Mac: an .ipa (--unsigned for Sideloadly); elsewhere / --xcode: an Xcode project
    ///
    /// Every target first writes the player data: the enabled build scenes, every Resources/
    /// asset and the preloaded assets, followed through their references — the same set Unity
    /// would put in a player — then hands it to that platform's toolchain.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                Console.WriteLine(Usage);
                return args.Length == 0 ? 1 : 0;
            }
            var opts = Options.Parse(args);
            var project = opts.Get("--project") ?? PlayerDataBuilder.FindProjectRoot();
            if (project == null) { Console.Error.WriteLine("No Unity project found above the current folder (or pass --project DIR)."); return 1; }
            try
            {
                switch (args[0])
                {
                    case "content":
                    {
                        var outDir = opts.Get("--out") ?? Path.Combine(project, "Builds", "PlayerData");
                        var report = new PlayerDataBuilder(project).Build(outDir, Console.WriteLine);
                        if (opts.Has("--pack")) PlayerDataBuilder.Pack(outDir, opts.Get("--pack")!);
                        return report.Missing.Count == 0 ? 0 : 0;
                    }
                    case "android": return new AndroidBuild(project, opts).Run();
                    case "ios": return new IosBuild(project, opts).Run();
                    default:
                        Console.Error.WriteLine($"unknown target '{args[0]}'\n\n{Usage}");
                        return 1;
                }
            }
            catch (BuildException e)
            {
                Console.Error.WriteLine("build failed: " + e.Message);
                return 2;
            }
        }

        const string Usage =
@"cs-build — make a player build of the Unity project, the way Unity's Build Settings does.

  cs-build content [--out DIR] [--pack FILE]
      Write the player data: enabled build scenes + Resources + preloaded assets and
      everything they reference (nothing under an Editor/ folder, no script source).

  cs-build android [--out FILE.apk|FILE.aab] [--debug] [--abi arm64|arm64,x64]
                   [--keystore FILE --alias NAME]   (passwords: CS_KEYSTORE_PASS / CS_KEY_PASS)
      Build a signed Android package. Without a keystore it is signed with the debug key,
      like Unity's development builds. Needs the .NET 'android' workload and an Android SDK
      (cs-build installs the SDK on first use; set ANDROID_HOME to use your own).

  cs-build ios [--out DIR] [--debug] [--unsigned] [--xcode]
      Export an iOS project with the player data inside, then (on a Mac with Xcode)
      build it. Unity builds iOS the same way: export on any machine, build on a Mac.

  common: --project DIR (default: the Unity project above the current folder)";
    }

    public sealed class BuildException : Exception { public BuildException(string m) : base(m) { } }

    public sealed class Options
    {
        readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 1; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
                string key = args[i];
                string? value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : null;
                o._values[key] = value;
            }
            return o;
        }

        public bool Has(string key) => _values.ContainsKey(key);
        public string? Get(string key) => _values.TryGetValue(key, out var v) ? v : null;
    }
}
