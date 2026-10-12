#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Prisma
{
    /// <summary>
    /// Where Blender and Maya are installed, the way Unity's ModelImporter looks for them, so
    /// cs-asset (which converts .blend/.ma/.mb files through them) and Prisma's SETTINGS >
    /// TOOLCHAIN agree. <c>PRISMA_BLENDER</c> / <c>PRISMA_MAYAPY</c> name the executable outright
    /// (Prisma sets them from SETTINGS); otherwise PATH, then the usual install folders.
    /// </summary>
    public static class DccLocator
    {
        /// <summary>The Blender executable, or null.</summary>
        public static string? FindBlender() => Find("PRISMA_BLENDER", new[] { "blender" }, BlenderInstalls());

        /// <summary>Maya's Python (mayapy): PRISMA_MAYAPY, MAYA_LOCATION/bin, then PATH and the usual install folders.</summary>
        public static string? FindMayaPy()
        {
            var loc = Environment.GetEnvironmentVariable("MAYA_LOCATION");
            var fromLocation = string.IsNullOrEmpty(loc) ? Array.Empty<string>()
                : new[] { Path.Combine(loc, "bin", OperatingSystem.IsWindows() ? "mayapy.exe" : "mayapy") };
            return Find("PRISMA_MAYAPY", new[] { "mayapy" }, fromLocation.Concat(MayaInstalls()));
        }

        /// <summary>"4.2.1" from <c>blender --version</c>'s first line; null when it does not answer in time.</summary>
        public static string? BlenderVersion(string exe)
        {
            try
            {
                var psi = new ProcessStartInfo(exe) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                psi.ArgumentList.Add("--version");
                using var p = Process.Start(psi)!;
                var first = p.StandardOutput.ReadLine();
                if (!p.WaitForExit(15000)) { try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
                return first != null && first.StartsWith("Blender ", StringComparison.Ordinal) ? first["Blender ".Length..].Trim() : first?.Trim();
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or InvalidOperationException) { return null; }
        }

        /// <summary>Maya's version from its install folder ("Maya2025" -> "2025"); mayapy takes too long to start to ask it.</summary>
        public static string? MayaVersion(string mayapy)
        {
            for (var d = Path.GetDirectoryName(mayapy); d != null; d = Path.GetDirectoryName(d))
            {
                var name = Path.GetFileName(d);
                if (name.StartsWith("maya", StringComparison.OrdinalIgnoreCase) && name.Length > 4 && char.IsDigit(name[4])) return name[4..];
            }
            return null;
        }

        static string? Find(string envVar, string[] names, IEnumerable<string> installs)
        {
            var env = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                foreach (var n in names)
                    foreach (var candidate in OperatingSystem.IsWindows() ? new[] { n + ".exe" } : new[] { n })
                    {
                        var p = Path.Combine(dir, candidate);
                        if (File.Exists(p)) return p;
                    }
            return installs.FirstOrDefault(File.Exists);
        }

        /// <summary>Newest version first: the folders hold a version in their name.</summary>
        static IEnumerable<string> Versions(string parent, string pattern) =>
            Directory.Exists(parent)
                ? Directory.GetDirectories(parent, pattern).OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                : Enumerable.Empty<string>();

        static IEnumerable<string> BlenderInstalls()
        {
            if (OperatingSystem.IsWindows())
            {
                foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    foreach (var d in Versions(Path.Combine(root, "Blender Foundation"), "Blender*")) yield return Path.Combine(d, "blender.exe");
                }
                // Steam's Blender.
                yield return @"C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe";
            }
            else if (OperatingSystem.IsMacOS()) yield return "/Applications/Blender.app/Contents/MacOS/Blender";
            else
            {
                yield return "/usr/bin/blender";
                yield return "/snap/bin/blender";
            }
        }

        static IEnumerable<string> MayaInstalls()
        {
            if (OperatingSystem.IsWindows())
            {
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk");
                foreach (var d in Versions(root, "Maya*")) yield return Path.Combine(d, "bin", "mayapy.exe");
            }
            else if (OperatingSystem.IsMacOS())
                foreach (var d in Versions("/Applications/Autodesk", "maya*")) yield return Path.Combine(d, "Maya.app", "Contents", "bin", "mayapy");
            else
                foreach (var d in Versions("/usr/autodesk", "maya*")) yield return Path.Combine(d, "bin", "mayapy");
        }
    }
}
