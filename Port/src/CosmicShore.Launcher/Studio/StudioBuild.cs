using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The Vessel Studio build, in Amoebius: the same output as <c>.claude/skills/vessel-studio/build_artifact.py</c>
    /// (index.html and every studio page from studios.json, studios.json, sync.js, the catalog's shared files and
    /// build.json), assembled from one git ref of the studio checkout. Amoebius serves exactly this folder
    /// (<see cref="StudioServer"/>), so OPEN IN AMOEBIUS shows the bytes claude.ai and the live mirror show, never the
    /// raw repo pages over file:// (/vessel-studio D33). No Python needed to look at a studio.
    ///
    /// <para>Kept byte-identical to the Python build: <c>StudioBuildTests</c> builds the same ref both ways and compares
    /// every file (build.json by field), and <c>parity_gate.py</c> checks the served pages.</para>
    /// </summary>
    public static class StudioBuild
    {
        public const string Dir = "Docs/Studios/VesselStudio";
        public const string Tag = "<script src=\"sync.js\"></script>";
        public const string Repo = "froglet-studio/cosmic-shore";
        public const string DefaultArtifact = "https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa";

        /// <summary>What a build shows: the branch, the commit, the last studio commit and the pages it wrote.</summary>
        public sealed record Info(string OutDir, string Branch, string Sha, string PathSha, string Subject, string CommittedAt, IReadOnlyList<string> Pages);

        /// <summary>The Sync panel tag before the LAST &lt;/body&gt; (or at the end); never twice (build_artifact.inject).</summary>
        public static string Inject(string html)
        {
            if (html.Contains("src=\"sync.js\"", StringComparison.Ordinal)) return html;
            int i = html.ToLowerInvariant().LastIndexOf("</body>", StringComparison.Ordinal);
            return i >= 0 ? html[..i] + Tag + html[i..] : html + Tag;
        }

        /// <summary>build_artifact.branch_name: origin/x, refs/remotes/origin/x and refs/heads/x are all branch x.</summary>
        public static string BranchName(string @ref)
        {
            foreach (var p in new[] { "refs/remotes/origin/", "origin/", "refs/heads/" })
                if (@ref.StartsWith(p, StringComparison.Ordinal)) return @ref[p.Length..];
            return @ref;
        }

        /// <summary>
        /// The ref to build for a checkout: its branch name when HEAD is on one (so build.json names the branch, as
        /// <c>build_artifact.py --ref &lt;branch&gt;</c> would), else the commit.
        /// </summary>
        public static string CheckoutRef(string git, string root)
        {
            var b = TryGit(git, root, "rev-parse", "--abbrev-ref", "HEAD")?.Trim();
            if (!string.IsNullOrEmpty(b) && b != "HEAD") return b;
            return TryGit(git, root, "rev-parse", "HEAD")?.Trim() ?? "HEAD";
        }

        /// <summary>Builds <paramref name="ref"/> of the checkout at <paramref name="root"/> into <paramref name="outDir"/> (emptied first).</summary>
        public static Info Build(string git, string root, string @ref, string outDir, string? artifact = null, string? session = null)
        {
            var catText = Show(git, root, @ref, "studios.json") ?? throw new InvalidOperationException($"{@ref} has no {Dir}/studios.json");
            using var cat = JsonDocument.Parse(catText);
            var pages = new List<string> { "index.html" };
            if (cat.RootElement.TryGetProperty("studios", out var st) && st.ValueKind == JsonValueKind.Array)
                foreach (var s in st.EnumerateArray())
                    if (s.TryGetProperty("file", out var f) && f.ValueKind == JsonValueKind.String && f.GetString() is { Length: > 0 } file) pages.Add(file);
            var shared = new List<string>();
            if (cat.RootElement.TryGetProperty("shared", out var sh) && sh.ValueKind == JsonValueKind.Array)
                foreach (var s in sh.EnumerateArray())
                    if (s.ValueKind == JsonValueKind.String && s.GetString() is { Length: > 0 } file) shared.Add(file);
            foreach (var f in pages.Concat(shared))
                if (!SafeName(f)) throw new InvalidOperationException($"studios.json names a file outside the studio folder: {f}");

            if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
            Directory.CreateDirectory(outDir);
            foreach (var f in pages)
                Write(outDir, f, Inject(Show(git, root, @ref, f) ?? throw new InvalidOperationException($"{@ref} has no {Dir}/{f}")));
            Write(outDir, "studios.json", catText);
            // sync.js and shared files: from the ref, else from this checkout's working tree (as the Python build does)
            foreach (var f in new[] { "sync.js" }.Concat(shared))
            {
                var txt = Show(git, root, @ref, f);
                if (txt == null)
                {
                    var local = Path.Combine(root, Dir, f);
                    txt = File.Exists(local) ? Text(File.ReadAllBytes(local)) : throw new InvalidOperationException($"no {Dir}/{f} in {@ref} or the checkout");
                }
                Write(outDir, f, txt);
            }
            string head = Git(git, root, "rev-parse", @ref).Trim();
            string pathSha = Git(git, root, "log", "-1", "--format=%H", @ref, "--", Dir).Trim();
            string subject = Git(git, root, "log", "-1", "--format=%s", pathSha).Trim();
            string committedAt = Git(git, root, "log", "-1", "--format=%cI", pathSha).Trim();
            var info = new Info(outDir, BranchName(@ref), head, pathSha, subject, committedAt, pages);
            Write(outDir, "build.json", BuildJson(info, artifact ?? DefaultArtifact, session));
            return info;
        }

        /// <summary>build.json exactly as Python's <c>json.dump(info, indent=2)</c> writes it (ASCII, \uXXXX escapes).</summary>
        public static string BuildJson(Info i, string artifact, string? session)
        {
            var sb = new StringBuilder("{\n");
            void Field(string k, string? v, bool last = false) =>
                sb.Append("  ").Append(PyString(k)).Append(": ").Append(v == null ? "null" : PyString(v)).Append(last ? "\n" : ",\n");
            Field("repo", Repo); Field("branch", i.Branch); Field("sha", i.Sha); Field("pathSha", i.PathSha);
            Field("subject", i.Subject); Field("committedAt", i.CommittedAt); Field("publishedAt", null);
            Field("artifact", artifact); Field("session", session, last: true);
            return sb.Append('}').ToString();
        }

        /// <summary>A JSON string as Python's json module writes it with ensure_ascii (lowercase hex, \u for non-ASCII).</summary>
        public static string PyString(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20 || c > 0x7e) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>One file name inside the studio folder: no folders, no "..", nothing a URL or a shell would read twice.</summary>
        public static bool SafeName(string f) =>
            f.Length is > 0 and <= 80 && !f.StartsWith('.') && f.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') && !f.Contains("..");

        static void Write(string dir, string name, string text) => File.WriteAllText(Path.Combine(dir, name), text, new UTF8Encoding(false));

        /// <summary>UTF-8 text with Python's universal newlines (what <c>git show</c> through <c>text=True</c> gives the build).</summary>
        static string Text(byte[] b)
        {
            var s = new UTF8Encoding(false).GetString(b);
            return s.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        static string? Show(string git, string root, string @ref, string file)
        {
            var r = Run(git, root, "show", $"{@ref}:{Dir}/{file}");
            return r.code == 0 ? Text(r.stdout) : null;
        }

        static string Git(string git, string root, params string[] args)
        {
            var r = Run(git, root, args);
            if (r.code != 0) throw new InvalidOperationException($"git {string.Join(' ', args)}: {r.stderr.Trim()}");
            return Text(r.stdout);
        }

        static string? TryGit(string git, string root, params string[] args)
        {
            try { var r = Run(git, root, args); return r.code == 0 ? Text(r.stdout) : null; } catch { return null; }
        }

        static (int code, byte[] stdout, string stderr) Run(string git, string root, params string[] args)
        {
            var psi = new ProcessStartInfo(git)
            {
                WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("git did not start");
            var err = p.StandardError.ReadToEndAsync();
            using var ms = new MemoryStream();
            p.StandardOutput.BaseStream.CopyTo(ms);
            p.WaitForExit();
            return (p.ExitCode, ms.ToArray(), err.Result);
        }
    }
}
