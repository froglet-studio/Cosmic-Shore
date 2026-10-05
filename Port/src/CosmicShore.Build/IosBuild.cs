using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CosmicShore.Build
{
    /// <summary>
    /// Build Settings → iOS → Build, the way Unity does it: write a build folder (the player data
    /// plus a build script) on any machine, then build and sign it with Xcode on a Mac. On a Mac
    /// both steps run here. Bundle id, name, version and build number come from Player Settings
    /// (applicationIdentifier.iPhone, productName, bundleVersion, buildNumber.iPhone).
    ///
    /// Signing uses the Mac's own identities: CS_IOS_CODESIGN_KEY ("Apple Development: …") and
    /// CS_IOS_PROVISIONING_PROFILE (a profile name or UUID); unset, Xcode's automatic choice applies.
    /// </summary>
    public sealed class IosBuild
    {
        readonly string _project;
        readonly Options _o;

        public IosBuild(string project, Options options) { _project = project; _o = options; }

        public int Run()
        {
            var builder = new PlayerDataBuilder(_project);
            string product = builder.ProductName();
            string outDir = Path.GetFullPath(_o.Get("--out") ?? Path.Combine(_project, "Builds", "iOS"));
            string id = _o.Get("--id") ?? builder.ApplicationIdentifier("iPhone");
            string config = _o.Has("--debug") ? "Debug" : "Release";
            Console.WriteLine($"iOS build: {product} {builder.BundleVersion()} ({builder.IosBuildNumber()}) — {id}, {config}");

            var report = Toolchain.StagePlayerData(builder, Path.Combine(outDir, "PlayerData"), pakDir: null);
            var fmod = Path.Combine(_project, "Assets", "Plugins", "FMOD", "platforms");
            bool fmodReal = Toolchain.IsRealFile(Path.Combine(fmod, "ios", "lib", "libfmodstudiounityplugin.a"));
            if (!fmodReal) Console.WriteLine("warning: FMOD's iOS runtime is a Git LFS pointer in this clone — the build will be silent (run `git lfs pull`).");

            var props = new List<string>
            {
                "-p:ApplicationId=" + id,
                "-p:ApplicationTitle=" + product,
                "-p:ApplicationDisplayVersion=" + builder.BundleVersion(),
                "-p:ApplicationVersion=" + builder.IosBuildNumber(),
                "-p:CsPlayerData=" + Path.Combine(outDir, "PlayerData"),
                "-p:RuntimeIdentifier=ios-arm64",
                "-p:ArchiveOnBuild=true",
            };
            if (fmodReal) props.Add("-p:CsFmodRoot=" + fmod);
            var project = Toolchain.MobileProject(_project);
            var publish = Path.Combine(outDir, "publish");
            var args = new List<string> { "publish", project, "-f", "net10.0-ios", "-c", config, "-o", publish };
            args.AddRange(props);
            WriteScript(outDir, args);

            if (!OperatingSystem.IsMacOS())
            {
                Console.WriteLine();
                Console.WriteLine($"exported {outDir} — player data {report.ContentHash}, {report.Assets} assets.");
                Console.WriteLine("iOS apps are built with Apple's toolchain, so the last step runs on a Mac (as Unity's Xcode export does):");
                Console.WriteLine("  on the Mac, in a clone of this branch:  dotnet run --project Port/src/CosmicShore.Build -- ios");
                Console.WriteLine($"  or copy {outDir} over and run its build-ios.sh");
                return 0;
            }

            Toolchain.EnsureWorkload("ios");
            AddSigning(args);
            if (Toolchain.Run(Toolchain.Dotnet(), args) != 0) throw new BuildException("the iOS toolchain failed (see above)");
            var ipa = Directory.EnumerateFiles(publish, "*.ipa").FirstOrDefault();
            Console.WriteLine(ipa != null
                ? $"built {ipa} — install it with Xcode (Window ▸ Devices) or upload it with Transporter."
                : $"built into {publish}");
            return 0;
        }

        static void AddSigning(List<string> args)
        {
            var key = Environment.GetEnvironmentVariable("CS_IOS_CODESIGN_KEY");
            var profile = Environment.GetEnvironmentVariable("CS_IOS_PROVISIONING_PROFILE");
            if (!string.IsNullOrEmpty(key)) args.Add("-p:CodesignKey=" + key);
            if (!string.IsNullOrEmpty(profile)) args.Add("-p:CodesignProvision=" + profile);
        }

        /// <summary>The exported folder's own build step, for a Mac that received the folder rather than running cs-build.</summary>
        static void WriteScript(string outDir, List<string> args)
        {
            Directory.CreateDirectory(outDir);
            string Q(string a) => "'" + a.Replace("'", "'\\''") + "'";
            var lines = new List<string>
            {
                "#!/bin/sh",
                "# Builds and signs the Cosmic Shore iOS app from this exported folder (run on a Mac with Xcode).",
                "# Needs: the .NET SDK with the 'ios' workload (dotnet workload install ios) and a clone of the",
                "# repository at REPO (default: the clone this folder was exported from).",
                "set -e",
                "HERE=\"$(cd \"$(dirname \"$0\")\" && pwd)\"",
                "REPO=\"${REPO:-" + Path.GetFullPath(Path.Combine(Path.GetDirectoryName(args[1])!, "..", "..", "..")) + "}\"",
                "SIGN=\"\"",
                "[ -n \"$CS_IOS_CODESIGN_KEY\" ] && SIGN=\"$SIGN -p:CodesignKey=$CS_IOS_CODESIGN_KEY\"",
                "[ -n \"$CS_IOS_PROVISIONING_PROFILE\" ] && SIGN=\"$SIGN -p:CodesignProvision=$CS_IOS_PROVISIONING_PROFILE\"",
            };
            var portable = args.Select(a => a
                .Replace(Path.Combine(outDir, "PlayerData"), "$HERE/PlayerData")
                .Replace(Path.Combine(outDir, "publish"), "$HERE/publish")).ToList();
            portable[1] = "$REPO/Port/src/CosmicShore.Mobile/CosmicShore.Mobile.csproj";
            lines.Add("dotnet " + string.Join(" ", portable.Select(a => a.Contains('$') ? "\"" + a + "\"" : Q(a))) + " $SIGN");
            var path = Path.Combine(outDir, "build-ios.sh");
            File.WriteAllText(path, string.Join("\n", lines) + "\n");
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, (UnixFileMode)0b111_101_101);
        }
    }
}
