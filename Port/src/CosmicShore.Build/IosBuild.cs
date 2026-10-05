using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CosmicShore.Build
{
    /// <summary>
    /// Build Settings → iOS → Build. Three outcomes, picked by where it runs:
    /// <list type="bullet">
    /// <item><b>Not a Mac</b>: exports an <b>Xcode project</b> (<c>CosmicShore.xcodeproj</c> + the player
    /// data), like Unity's iOS export. Open it in Xcode on a Mac, pick a team, Run or Archive.</item>
    /// <item><b>Mac, <c>--unsigned</c></b>: compiles <c>&lt;product&gt;.ipa</c> with no signature — the
    /// GitHub-runner path. Sign and install it on Windows with Sideloadly and a free Apple ID.</item>
    /// <item><b>Mac</b>: compiles and signs with the Mac's own identities: CS_IOS_CODESIGN_KEY
    /// ("Apple Development: …") and CS_IOS_PROVISIONING_PROFILE; unset, Xcode's automatic choice.</item>
    /// </list>
    /// Bundle id, name, version and build number come from Player Settings
    /// (applicationIdentifier.iPhone, productName, bundleVersion, buildNumber.iPhone).
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
            bool unsigned = _o.Has("--unsigned");
            bool xcode = _o.Has("--xcode") || !OperatingSystem.IsMacOS();
            Console.WriteLine($"iOS build: {product} {builder.BundleVersion()} ({builder.IosBuildNumber()}) — {id}, {config}"
                + (xcode ? ", Xcode project" : unsigned ? ", unsigned .ipa" : ", signed .ipa"));

            string playerData = Path.Combine(outDir, "PlayerData");
            var report = Toolchain.StagePlayerData(builder, playerData, pakDir: null);
            var fmod = Path.Combine(_project, "Assets", "Plugins", "FMOD", "platforms");
            bool fmodReal = Toolchain.IsRealFile(Path.Combine(fmod, "ios", "lib", "libfmodstudiounityplugin.a"));
            if (!fmodReal) Console.WriteLine("warning: FMOD's iOS runtime is a Git LFS pointer in this clone — the build will be silent (run `git lfs pull`).");

            if (xcode)
            {
                var proj = XcodeProject.Write(outDir, new XcodeProject.Settings(
                    ProductName: "CosmicShore", DisplayName: product, BundleId: id,
                    Version: builder.BundleVersion(), Build: builder.IosBuildNumber(),
                    MinimumOs: "15.0", RepoPath: _project));
                Console.WriteLine();
                Console.WriteLine($"exported {proj} — player data {report.ContentHash}, {report.Assets} assets.");
                Console.WriteLine("On a Mac: open it in Xcode, pick your Team under Signing & Capabilities, then Run or Product > Archive.");
                Console.WriteLine("No Mac: build an unsigned .ipa on GitHub's free Mac runner instead (launcher BUILD page, or Port/docs/IOS.md).");
                return 0;
            }

            var bin = Path.Combine(outDir, "bin");
            var args = new List<string>
            {
                "build", Toolchain.MobileProject(_project), "-f", "net10.0-ios", "-c", config,
                // Only the iOS target: on a Mac the project also lists Android, whose workload a CI runner lacks.
                "-p:CsPlatform=ios",
                "-p:RuntimeIdentifier=ios-arm64",
                "-p:ApplicationId=" + id,
                "-p:ApplicationTitle=" + product,
                "-p:ApplicationDisplayVersion=" + builder.BundleVersion(),
                "-p:ApplicationVersion=" + builder.IosBuildNumber(),
                "-p:CsPlayerData=" + playerData,
                "-p:OutputPath=" + bin + Path.DirectorySeparatorChar,
            };
            if (fmodReal) args.Add("-p:CsFmodRoot=" + fmod);
            if (unsigned)
            {
                // No identity on a CI runner: build the device .app without a signature. Sideloadly
                // re-signs the whole bundle (nested frameworks included) with the user's Apple ID.
                args.Add("-p:EnableCodeSigning=false");
                args.Add("-p:_RequireCodeSigning=false");
                args.Add("-p:CodesignRequireProvisioningProfile=false");
            }
            else AddSigning(args);

            Toolchain.EnsureWorkload("ios");
            if (Toolchain.Run(Toolchain.Dotnet(), args) != 0) throw new BuildException("the iOS toolchain failed (see above)");

            var app = Directory.EnumerateDirectories(bin, "*.app", SearchOption.AllDirectories)
                .OrderByDescending(Directory.GetLastWriteTimeUtc).FirstOrDefault()
                ?? throw new BuildException($"no .app was produced under {bin}");
            var ipa = Path.Combine(outDir, product.Replace(' ', '-') + (unsigned ? "-unsigned" : "") + ".ipa");
            PackageIpa(app, ipa);
            Console.WriteLine(unsigned
                ? $"built {ipa} — unsigned: install it with Sideloadly (Windows) or AltStore, which sign it with your Apple ID."
                : $"built {ipa} — install it with Xcode (Window > Devices) or Apple Configurator.");
            return 0;
        }

        /// <summary>An .ipa is a zip with Payload/&lt;App&gt;.app at its root. Uses ditto on a Mac so symlinks inside frameworks survive.</summary>
        static void PackageIpa(string app, string ipa)
        {
            var stage = Path.Combine(Path.GetTempPath(), "cs-ipa-" + Guid.NewGuid().ToString("N"));
            var payload = Path.Combine(stage, "Payload");
            Directory.CreateDirectory(payload);
            try
            {
                if (File.Exists(ipa)) File.Delete(ipa);
                if (Toolchain.Run("ditto", new[] { app, Path.Combine(payload, Path.GetFileName(app)) }) != 0)
                    throw new BuildException("copying the .app failed");
                if (Toolchain.Run("ditto", new[] { "-c", "-k", "--sequesterRsrc", "--keepParent", payload, ipa }) != 0)
                    throw new BuildException("zipping the .ipa failed");
            }
            finally { try { Directory.Delete(stage, true); } catch { } }
        }

        static void AddSigning(List<string> args)
        {
            var key = Environment.GetEnvironmentVariable("CS_IOS_CODESIGN_KEY");
            var profile = Environment.GetEnvironmentVariable("CS_IOS_PROVISIONING_PROFILE");
            if (!string.IsNullOrEmpty(key)) args.Add("-p:CodesignKey=" + key);
            if (!string.IsNullOrEmpty(profile)) args.Add("-p:CodesignProvision=" + profile);
        }
    }
}
