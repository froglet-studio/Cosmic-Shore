using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The EDITOR page's engine: <c>cs-asset</c> built from the workspace's own source (so it reads
    /// the checkout it shows, with that branch's scripts), run per request with JSON on stdout.
    /// It is built once per Prisma session (incrementally, and again on REBUILD); every call after
    /// that is a short process run.
    /// </summary>
    public sealed class EditorTool
    {
        readonly Toolchain _tools;
        readonly Workspace _ws;
        readonly LogBuffer _log;
        readonly SemaphoreSlim _build = new(1, 1);
        bool _built;

        public EditorTool(Toolchain tools, Workspace ws, LogBuffer log) { _tools = tools; _ws = ws; _log = log; }

        public bool Building { get; private set; }
        public string? Error { get; private set; }

        string Project => Path.Combine(_ws.Dir, "Port", "src", "CosmicShore.AssetTool", "CosmicShore.AssetTool.csproj");
        string Exe => Path.Combine(_ws.Dir, "Port", "src", "CosmicShore.AssetTool", "bin", "Debug", "net10.0", OperatingSystem.IsWindows() ? "cs-asset.exe" : "cs-asset");

        /// <summary>Whether this branch's cs-asset has the editor commands at all (older branches do not).</summary>
        public bool Supported => File.Exists(Path.Combine(_ws.Dir, "Port", "src", "CosmicShore.AssetTool", "EditorData.cs"));

        /// <summary>Forget the build (and any error) so the next call rebuilds: REBUILD, after a pull or a script edit.</summary>
        public void Invalidate() { _built = false; Error = null; }

        /// <summary>Builds cs-asset from the workspace (the game's scripts with it) unless this session already did.</summary>
        public async Task<bool> Ensure()
        {
            if (_built && File.Exists(Exe)) return true;
            await _build.WaitAsync();
            try
            {
                if (_built && File.Exists(Exe)) return true;
                Error = null;
                if (!_ws.Exists) { Error = "No workspace yet: press START on PLAY once."; return false; }
                if (_tools.Dotnet == null) { Error = ".NET is not set up yet: press START on PLAY once."; return false; }
                if (!Supported) { Error = "This branch's Prisma has no editor commands yet (pick a branch that has Port/src/CosmicShore.AssetTool/EditorData.cs)."; return false; }
                Building = true;
                _log.Add(LogKind.Info, "---- Build the editor tools (cs-asset) ----");
                var r = await ProcessRunner.Run(_tools.Dotnet, new[] { "build", Project, "-c", "Debug", "-nologo", "-v:minimal", "-clp:NoSummary" },
                    _ws.Dir, _log, CancellationToken.None, _tools.DotnetEnv());
                _built = r.ExitCode == 0 && File.Exists(Exe);
                if (!_built) Error = "Building cs-asset failed - see CONSOLE.";
                return _built;
            }
            finally { Building = false; _build.Release(); }
        }

        /// <summary>Runs one command; returns its JSON, or null with <see cref="Error"/> set.</summary>
        public async Task<JsonDocument?> Json(params string[] args)
        {
            var (ok, stdout, stderr) = await Run(args);
            if (!ok) { Error = FirstLine(stderr) ?? "cs-asset failed"; return null; }
            try { return JsonDocument.Parse(stdout); }
            catch (JsonException e) { Error = "cs-asset answered something that is not JSON: " + e.Message; return null; }
        }

        /// <summary>Runs one command and returns its exit, stdout and stderr (for set, whose output is a diff).</summary>
        public async Task<(bool ok, string stdout, string stderr)> Run(params string[] args)
        {
            if (!await Ensure()) return (false, "", Error ?? "");
            var r = await ProcessRunner.Run(Exe, args, _ws.Dir, null, CancellationToken.None, _tools.DotnetEnv(), quiet: true);
            return (r.ExitCode == 0, r.StdOut, r.StdErr);
        }

        static string? FirstLine(string s) => s.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);

        /// <summary>A YAML scalar cs-asset's set takes as text: plain when it is safe, single-quoted otherwise.</summary>
        public static string YamlScalar(string text)
        {
            if (text.Length == 0) return "''";
            // Unity writes multi-line text double-quoted with escapes; so do we.
            if (text.Contains('\n')) return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
            bool plain = !text.Any(c => c is ':' or '#' or '\'' or '"' or '{' or '}' or '[' or ']' or ',' or '&' or '*' or '!' or '|' or '>' or '%' or '@' or '`' or '\n')
                         && !char.IsWhiteSpace(text[0]) && !char.IsWhiteSpace(text[^1]) && text[0] != '-' && text[0] != '?';
            return plain ? text : "'" + text.Replace("'", "''") + "'";
        }

        public string FullPath(string projectRelative) => Path.Combine(_ws.Dir, projectRelative.Replace('/', Path.DirectorySeparatorChar));
    }
}
