using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The launcher's two secrets (the GitHub token and the Anthropic API key), kept in the OS's
    /// credential store instead of launcher.json: Windows Credential Manager, the macOS Keychain
    /// (<c>security</c>), or the Secret Service on Linux (<c>secret-tool</c>). A secret never goes on
    /// a command line: it is written to the tool's standard input. Where no store answers (a Linux
    /// box without a keyring), it falls back to <c>secrets.json</c> beside the settings, readable by
    /// its owner only.
    ///
    /// A launcher pointed at its own data folder (AMOEBIUS_DATA_DIR: a second Amoebius, the tests)
    /// uses its own entries, so it never reads or overwrites the everyday launcher's.
    /// </summary>
    public static class SecretStore
    {
        public const string GitHubToken = "github-token";
        public const string AnthropicApiKey = "anthropic-api-key";
        public const string NetKey = "online-server-key";

        /// <summary>Where the secrets live on this machine, for SETTINGS to say.</summary>
        public static string Where => OperatingSystem.IsWindows() ? "Windows Credential Manager"
            : OperatingSystem.IsMacOS() ? "the macOS Keychain"
            : SecretTool ? "the system keyring (Secret Service)" : "secrets.json (readable by you only)";

        static string Service
        {
            get
            {
                var own = Environment.GetEnvironmentVariable("AMOEBIUS_DATA_DIR");
                if (string.IsNullOrEmpty(own)) return "Amoebius";
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(own))))[..12];
                return "Amoebius-" + hash.ToLowerInvariant();
            }
        }

        /// <summary>The stored value, or null.</summary>
        public static string? Get(string name)
        {
            try
            {
                if (OperatingSystem.IsWindows()) return WinCred.Read(Service + "/" + name);
                if (OperatingSystem.IsMacOS())
                {
                    var (code, output) = Run("security", new[] { "find-generic-password", "-s", Service, "-a", name, "-w" }, null);
                    return code == 0 ? Empty(output.TrimEnd('\n', '\r')) : null;
                }
                if (SecretTool)
                {
                    var (code, output) = Run("secret-tool", new[] { "lookup", "service", Service, "account", name }, null);
                    // Not found, or no keyring answered (no D-Bus session): what Set fell back to, if anything.
                    return code == 0 ? Empty(output.TrimEnd('\n', '\r')) : FileGet(name);
                }
                return FileGet(name);
            }
            catch (Exception) { return null; }
        }

        /// <summary>Stores <paramref name="value"/>, or removes the entry when it is empty. False when nothing could store it.</summary>
        public static bool Set(string name, string? value)
        {
            value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            try
            {
                if (OperatingSystem.IsWindows()) return value == null ? WinCred.Delete(Service + "/" + name) : WinCred.Write(Service + "/" + name, value);
                if (OperatingSystem.IsMacOS())
                {
                    if (value == null) { Run("security", new[] { "delete-generic-password", "-s", Service, "-a", name }, null); return true; }
                    // `security -i` reads its command from stdin, so the secret is not in any process's arguments.
                    var (code, _) = Run("security", new[] { "-i" },
                        $"add-generic-password -U -s {Quote(Service)} -a {Quote(name)} -w {Quote(value)}\n");
                    return code == 0;
                }
                if (SecretTool)
                {
                    int code = value == null
                        ? Run("secret-tool", new[] { "clear", "service", Service, "account", name }, null).Code
                        : Run("secret-tool", new[] { "store", "--label", Service + " " + name, "service", Service, "account", name }, value).Code;
                    if (code == 0 || value == null) { FileSet(name, null); return true; }
                }
                return FileSet(name, value);
            }
            catch (Exception) { return false; }
        }

        static string? Empty(string s) => s.Length == 0 ? null : s;

        static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        static bool? _secretTool;
        static bool SecretTool => _secretTool ??= OperatingSystem.IsLinux() && OnPath("secret-tool");

        static bool OnPath(string exe)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                if (File.Exists(Path.Combine(dir, exe))) return true;
            return false;
        }

        static (int Code, string Output) Run(string exe, string[] args, string? stdin)
        {
            var psi = new ProcessStartInfo(exe)
            {
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            if (stdin != null) p.StandardInput.Write(stdin);
            p.StandardInput.Close();
            var output = p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            if (!p.WaitForExit(15000)) { try { p.Kill(); } catch (InvalidOperationException) { } return (-1, ""); }
            return (p.ExitCode, output);
        }

        // ── the fallback file ──────────────────────────────────────────────

        static string FilePath => Path.Combine(LauncherSettings.DataDir, "secrets.json");

        static Dictionary<string, string> FileRead()
        {
            try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath)) ?? new() : new(); }
            catch (Exception) { return new(); }
        }

        static string? FileGet(string name) => FileRead().TryGetValue(name, out var v) ? Empty(v) : null;

        static bool FileSet(string name, string? value)
        {
            var all = FileRead();
            if (value == null && !all.Remove(name)) return true;
            if (value != null) all[name] = value;
            if (all.Count == 0) { if (File.Exists(FilePath)) File.Delete(FilePath); return true; }
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, "");
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.WriteAllText(tmp, JsonSerializer.Serialize(all));
            File.Move(tmp, FilePath, overwrite: true);
            return true;
        }

        // ── Windows Credential Manager (advapi32) ──────────────────────────

        static class WinCred
        {
            const int CredTypeGeneric = 1;
            const int CredPersistLocalMachine = 2;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            struct Credential
            {
                public int Flags;
                public int Type;
                public string TargetName;
                public string? Comment;
                public long LastWritten;
                public int CredentialBlobSize;
                public IntPtr CredentialBlob;
                public int Persist;
                public int AttributeCount;
                public IntPtr Attributes;
                public string? TargetAlias;
                public string? UserName;
            }

            [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
            static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

            [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
            static extern bool CredWrite(ref Credential credential, int flags);

            [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
            static extern bool CredDelete(string target, int type, int flags);

            [DllImport("advapi32.dll")]
            static extern void CredFree(IntPtr buffer);

            public static string? Read(string target)
            {
                if (!CredRead(target, CredTypeGeneric, 0, out var ptr)) return null;
                try
                {
                    var c = Marshal.PtrToStructure<Credential>(ptr);
                    if (c.CredentialBlobSize == 0) return null;
                    var bytes = new byte[c.CredentialBlobSize];
                    Marshal.Copy(c.CredentialBlob, bytes, 0, bytes.Length);
                    return Encoding.UTF8.GetString(bytes);
                }
                finally { CredFree(ptr); }
            }

            public static bool Write(string target, string value)
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                var blob = Marshal.AllocHGlobal(bytes.Length);
                try
                {
                    Marshal.Copy(bytes, 0, blob, bytes.Length);
                    var c = new Credential
                    {
                        Type = CredTypeGeneric, TargetName = target, CredentialBlobSize = bytes.Length,
                        CredentialBlob = blob, Persist = CredPersistLocalMachine, UserName = Environment.UserName,
                    };
                    return CredWrite(ref c, 0);
                }
                finally
                {
                    Array.Clear(bytes);
                    Marshal.FreeHGlobal(blob);
                }
            }

            public static bool Delete(string target) => CredDelete(target, CredTypeGeneric, 0) || Marshal.GetLastWin32Error() == 1168; // ERROR_NOT_FOUND
        }
    }
}
