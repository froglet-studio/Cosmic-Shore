using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CosmicShore.Build
{
    /// <summary>
    /// Build Settings → Android → Build: player data, then the phone player compiled and packaged
    /// as a signed APK (or an AAB for the Play Store) named and versioned from Player Settings —
    /// package name (applicationIdentifier.Android), product name, bundleVersion and
    /// AndroidBundleVersionCode. Without a keystore the package is signed with the debug key, as
    /// Unity signs a development build.
    /// </summary>
    public sealed class AndroidBuild
    {
        readonly string _project;
        readonly Options _o;

        public AndroidBuild(string project, Options options) { _project = project; _o = options; }

        public int Run()
        {
            var builder = new PlayerDataBuilder(_project);
            bool aab = (_o.Get("--out") ?? "").EndsWith(".aab", StringComparison.OrdinalIgnoreCase) || _o.Has("--aab");
            string product = builder.ProductName();
            string outFile = Path.GetFullPath(_o.Get("--out") ?? Path.Combine(_project, "Builds", "Android", Safe(product) + (aab ? ".aab" : ".apk")));
            string work = Path.Combine(_project, "Builds", "Android", "_work");
            string id = _o.Get("--id") ?? builder.ApplicationIdentifier("Android");
            string config = _o.Has("--debug") ? "Debug" : "Release";

            Console.WriteLine($"Android build: {product} {builder.BundleVersion()} ({builder.AndroidVersionCode()}) — {id}, {config}");
            Toolchain.EnsureWorkload("android");
            var (sdk, jdk) = AndroidSdk.Ensure(_o.Get("--sdk"), _o.Get("--jdk"));

            var report = Toolchain.StagePlayerData(builder, Path.Combine(work, "PlayerData"), Path.Combine(work, "pak"));

            var fmod = Path.Combine(_project, "Assets", "Plugins", "FMOD", "platforms");
            bool fmodReal = Toolchain.IsRealFile(Path.Combine(fmod, "android", "lib", "arm64-v8a", "libfmodstudio.so"))
                         && Toolchain.IsRealFile(Path.Combine(fmod, "android", "lib", "fmod.jar"));
            if (!fmodReal) Console.WriteLine("warning: FMOD's Android runtime is a Git LFS pointer in this clone — the build will be silent (run `git lfs pull`).");

            var abis = (_o.Get("--abi") ?? "arm64").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(a => a switch { "arm64" or "arm64-v8a" => "android-arm64", "x64" or "x86_64" => "android-x64", "arm" or "armeabi-v7a" => "android-arm", _ => a })
                .ToList();
            // `build` is what packages and signs an Android app (publish -o fights the project references).
            var mobile = Toolchain.MobileProject(_project);
            var publish = Path.Combine(Path.GetDirectoryName(mobile)!, "bin", config, "net10.0-android");
            if (Directory.Exists(publish))
                foreach (var old in Directory.EnumerateFiles(publish, "*.a?b").Concat(Directory.EnumerateFiles(publish, "*.apk"))) File.Delete(old);

            var args = new List<string>
            {
                "build", mobile, "-f", "net10.0-android", "-c", config,
                "-p:CsAbis=" + string.Join("%3B", abis),   // not RuntimeIdentifiers: a global one would reach the class libraries too
                "-p:AndroidPackageFormat=" + (aab ? "aab" : "apk"),
                "-p:ApplicationId=" + id,
                "-p:ApplicationTitle=" + product,
                "-p:ApplicationDisplayVersion=" + builder.BundleVersion(),
                "-p:ApplicationVersion=" + builder.AndroidVersionCode(),
                "-p:CsDataPak=" + Path.Combine(work, "pak"),
                "-p:AndroidSdkDirectory=" + sdk,
                "-p:JavaSdkDirectory=" + jdk,
            };
            if (fmodReal) args.Add("-p:CsFmodRoot=" + fmod);
            if (builder.DefaultIcon() is { } icon) args.Add("-p:CsAppIcon=" + icon);
            var keystore = _o.Get("--keystore");
            if (keystore != null)
            {
                args.Add("-p:AndroidKeyStore=true");
                args.Add("-p:AndroidSigningKeyStore=" + Path.GetFullPath(keystore));
                args.Add("-p:AndroidSigningKeyAlias=" + (_o.Get("--alias") ?? throw new BuildException("--keystore needs --alias")));
                args.Add("-p:AndroidSigningStorePass=env:CS_KEYSTORE_PASS");
                args.Add("-p:AndroidSigningKeyPass=env:" + (Environment.GetEnvironmentVariable("CS_KEY_PASS") != null ? "CS_KEY_PASS" : "CS_KEYSTORE_PASS"));
                if (Environment.GetEnvironmentVariable("CS_KEYSTORE_PASS") == null) throw new BuildException("set CS_KEYSTORE_PASS (and CS_KEY_PASS if the key's password differs)");
            }
            if (Toolchain.Run(Toolchain.Dotnet(), args) != 0) throw new BuildException("the Android toolchain failed (see above)");

            var produced = Directory.EnumerateFiles(publish, aab ? "*-Signed.aab" : "*-Signed.apk").FirstOrDefault()
                ?? Directory.EnumerateFiles(publish, aab ? "*.aab" : "*.apk").FirstOrDefault()
                ?? throw new BuildException("the toolchain produced no package");
            Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
            File.Copy(produced, outFile, overwrite: true);
            Console.WriteLine();
            Console.WriteLine($"built {outFile} ({new FileInfo(outFile).Length / (1024 * 1024)} MB) — player data {report.ContentHash}, {report.Assets} assets");
            Console.WriteLine(aab ? "upload it to the Play Console (an AAB is not installable directly)."
                                  : "install it on a device with USB debugging: adb install -r \"" + outFile + "\"");
            return 0;
        }

        internal static string Safe(string name) => string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
    }

    /// <summary>Finds (or installs once, as Unity Hub's Android module does) the Android SDK and a JDK.</summary>
    public static class AndroidSdk
    {
        public static (string Sdk, string Jdk) Ensure(string? sdk, string? jdk)
        {
            sdk ??= FirstExisting(Environment.GetEnvironmentVariable("ANDROID_HOME"), Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
                Path.Combine(Home(), "Android", "Sdk"), Path.Combine(Home(), "Library", "Android", "sdk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk"),
                "/opt/android-sdk", Path.Combine(ToolsDir(), "android-sdk"));
            jdk ??= FirstExisting(Environment.GetEnvironmentVariable("JAVA_HOME"), Path.Combine(ToolsDir(), "jdk"), "/usr/lib/jvm/java-21-openjdk-amd64", "/usr/lib/jvm/java-17-openjdk-amd64");
            bool sdkOk = sdk != null && Directory.Exists(Path.Combine(sdk, "platforms")) && Directory.Exists(Path.Combine(sdk, "build-tools"));
            if (sdkOk && jdk != null) return (sdk!, jdk);

            // The workload's own installer fetches the SDK packages (and a JDK) the build needs.
            sdk = sdkOk ? sdk! : Path.Combine(ToolsDir(), "android-sdk");
            jdk ??= Path.Combine(ToolsDir(), "jdk");
            Console.WriteLine($"installing the Android SDK into {sdk} (one time)...");
            var probe = Path.Combine(ToolsDir(), "sdk-probe");
            Directory.CreateDirectory(probe);
            File.WriteAllText(Path.Combine(probe, "Probe.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0-android</TargetFramework><SupportedOSPlatformVersion>28</SupportedOSPlatformVersion></PropertyGroup></Project>");
            var env = new Dictionary<string, string> { ["USER"] = Environment.UserName.Length > 0 ? Environment.UserName : "builder" };
            int code = Toolchain.Run(Toolchain.Dotnet(), new[]
            {
                "build", Path.Combine(probe, "Probe.csproj"), "-t:InstallAndroidDependencies", "-f", "net10.0-android",
                "-p:AndroidSdkDirectory=" + sdk, "-p:JavaSdkDirectory=" + jdk, "-p:AcceptAndroidSDKLicenses=True",
            }, env: env);
            if (code != 0) throw new BuildException("installing the Android SDK failed; install Android Studio (or set ANDROID_HOME and JAVA_HOME) and build again");
            return (sdk, jdk);
        }

        static string Home() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        static string ToolsDir() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CosmicShore", "build-tools");

        static string? FirstExisting(params string?[] dirs) => dirs.FirstOrDefault(d => !string.IsNullOrEmpty(d) && Directory.Exists(d));
    }
}
