using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The Vessel Studio's <c>sample</c> (the hub's "Ask the studio agent") in Amoebius: one question to the Claude Code
    /// CLI Amoebius already runs for its AGENT page (same sign-in, model and API key settings), read-only (no edits, no
    /// shell, no web), its text streamed back as it arrives (/vessel-studio D33).
    /// </summary>
    public static class StudioAsk
    {
        public const int MaxPromptChars = 40_000;
        public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(4);

        /// <summary>Tools an Ask never gets: it answers a question, it changes nothing.</summary>
        public static readonly string[] Denied = { "Bash", "Edit", "Write", "MultiEdit", "NotebookEdit", "WebFetch", "WebSearch", "Task" };

        /// <summary>A failure the page shows by its code (as the artifact's sample rejects).</summary>
        public sealed class AskException : Exception
        {
            public string Code { get; }
            public AskException(string code, string message) : base(message) { Code = code; }
        }

        /// <summary>Runs one question; <paramref name="onText"/> gets the whole answer so far each time it grows. Returns the answer.</summary>
        public static async Task<string> Run(string? cli, string prompt, string workDir, string? model, string? apiKey, Action<string> onText, CancellationToken ct)
        {
            if (cli == null || !File.Exists(cli)) throw new AskException("unavailable", "Asking needs Claude Code in Amoebius: install it and sign in on the AGENT page.");
            if (string.IsNullOrWhiteSpace(prompt)) throw new AskException("invalid_argument", "empty question");
            if (prompt.Length > MaxPromptChars) throw new AskException("invalid_argument", "question too long");
            var psi = new ProcessStartInfo(cli)
            {
                WorkingDirectory = Directory.Exists(workDir) ? workDir : Environment.CurrentDirectory,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            foreach (var a in new[] { "-p", "--output-format", "stream-json", "--verbose", "--permission-mode", "plan", "--max-turns", "3" })
                psi.ArgumentList.Add(a);
            if (!string.IsNullOrWhiteSpace(model) && model.Trim() != "default") { psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(model.Trim()); }
            psi.ArgumentList.Add("--disallowedTools");
            foreach (var d in Denied) psi.ArgumentList.Add(d);
            if (!string.IsNullOrWhiteSpace(apiKey)) psi.Environment["ANTHROPIC_API_KEY"] = apiKey.Trim();

            using var p = Process.Start(psi) ?? throw new AskException("unavailable", "claude did not start");
            await p.StandardInput.WriteAsync(prompt);
            p.StandardInput.Close();
            var err = p.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            using var reg = timeout.Token.Register(() => { try { p.Kill(entireProcessTree: true); } catch { } });
            var answer = new StringBuilder();
            string? error = null, code = null;
            string? line;
            while ((line = await p.StandardOutput.ReadLineAsync()) != null)
            {
                var (text, e, c) = ParseLine(line);
                if (text != null)
                {
                    if (answer.Length > 0) answer.Append("\n\n");
                    answer.Append(text.Trim());
                    onText(answer.ToString());
                }
                if (e != null) { error = e; code = c; }
            }
            await p.WaitForExitAsync(CancellationToken.None);
            if (timeout.IsCancellationRequested) throw new AskException(ct.IsCancellationRequested ? "cancelled" : "deadline_exceeded", "the answer took too long");
            if (error != null) throw new AskException(code ?? "error", error);
            if (p.ExitCode != 0 && answer.Length == 0)
            {
                var e = (await err).Trim();
                throw new AskException("error", e.Length > 0 ? e.Split('\n')[0] : $"claude exited with {p.ExitCode}");
            }
            return answer.ToString();
        }

        /// <summary>One stream-json line: an assistant text block, or the run's error (with a code the page knows).</summary>
        public static (string? text, string? error, string? code) ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] != '{') return (null, null, null);
            try
            {
                using var d = JsonDocument.Parse(line);
                var r = d.RootElement;
                string type = r.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
                if (type == "assistant" && r.TryGetProperty("message", out var m) && m.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var b in content.EnumerateArray())
                        if (b.TryGetProperty("type", out var bt) && bt.GetString() == "text" && b.TryGetProperty("text", out var tx)) sb.Append(tx.GetString());
                    return (sb.Length > 0 ? sb.ToString() : null, null, null);
                }
                if (type == "result" && r.TryGetProperty("is_error", out var ie) && ie.ValueKind == JsonValueKind.True)
                {
                    string msg = r.TryGetProperty("result", out var res) ? res.ToString() : "the answer did not arrive";
                    string code = msg.Contains("limit", StringComparison.OrdinalIgnoreCase) ? "rate_limited" : "error";
                    return (null, msg, code);
                }
            }
            catch (JsonException) { }
            return (null, null, null);
        }
    }
}
