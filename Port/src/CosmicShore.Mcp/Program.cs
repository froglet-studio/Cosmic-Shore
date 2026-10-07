using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace CosmicShore.Mcp
{
    /// <summary>
    /// prisma-mcp - Prisma as an MCP server (stdio, JSON-RPC 2.0, one message per line).
    /// Gives an agent the engine's own verbs: build and test the port, check it leaves Unity
    /// alone, and start / look at / drive / stop a running player through its control port.
    ///
    ///   claude mcp add prisma -- dotnet run --project Port/src/CosmicShore.Mcp
    ///
    /// stdout is the protocol and nothing else; every diagnostic goes to stderr.
    /// </summary>
    public static class Program
    {
        static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
        static Tools s_tools = null!;

        public static async Task<int> Main(string[] args)
        {
            var repo = Repo.Find(args.SkipWhile(a => a != "--repo").Skip(1).FirstOrDefault());
            if (repo == null) { Console.Error.WriteLine("prisma-mcp: no Cosmic Shore checkout found (run it inside one, or pass --repo DIR)"); return 1; }
            // One prisma_bisect candidate (git bisect run calls this): the exit code is the verdict.
            int step = Array.IndexOf(args, "--bisect-step");
            if (step >= 0 && step + 1 < args.Length) return await Tools.BisectStep(repo, args[step + 1]);
            s_tools = new Tools(repo);
            // One tool from a shell (CI): prisma-mcp --call engine_smoke '{"scene":"Menu_Main"}'.
            // Prints the tool's text; exits 1 when the tool reports a failure.
            int call = Array.IndexOf(args, "--call");
            if (call >= 0 && call + 1 < args.Length)
            {
                var a = call + 2 < args.Length && args[call + 2].StartsWith("{") ? JsonNode.Parse(args[call + 2])!.AsObject() : new JsonObject();
                try
                {
                    var content = await s_tools.Call(args[call + 1], a);
                    var text = string.Join("\n", content.Select(c => c?["text"]?.ToString()).Where(t => t != null));
                    Console.WriteLine(text);
                    return text.StartsWith("FAIL") || text.StartsWith("build FAILED") || text.StartsWith("tests FAILED") ? 1 : 0;
                }
                catch (ToolException e) { Console.WriteLine(e.Message); return 1; }
                finally { s_tools.Dispose(); }
            }
            Console.Error.WriteLine($"prisma-mcp: serving {repo}");
            var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            using var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            try
            {
                string? line;
                while ((line = await stdin.ReadLineAsync()) != null)
                {
                    if (line.Length == 0) continue;
                    JsonNode? reply;
                    try { reply = await Handle(JsonNode.Parse(line)!.AsObject()); }
                    catch (JsonException e) { reply = Error(null, -32700, "parse error: " + e.Message); }
                    if (reply != null) stdout.WriteLine(reply.ToJsonString(Json));
                }
            }
            finally { s_tools.Dispose(); }
            return 0;
        }

        static async Task<JsonNode?> Handle(JsonObject msg)
        {
            var id = msg["id"]?.DeepClone();
            string method = msg["method"]?.GetValue<string>() ?? "";
            var p = msg["params"] as JsonObject ?? new JsonObject();
            if (id == null) return null; // a notification (initialized, cancelled): nothing to answer
            switch (method)
            {
                case "initialize":
                    return Result(id, new JsonObject
                    {
                        ["protocolVersion"] = p["protocolVersion"]?.GetValue<string>() ?? "2025-06-18",
                        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                        ["serverInfo"] = new JsonObject { ["name"] = "prisma", ["version"] = "0.1.0" },
                        ["instructions"] = Tools.Instructions,
                    });
                case "ping":
                    return Result(id, new JsonObject());
                case "tools/list":
                    return Result(id, new JsonObject { ["tools"] = Tools.List() });
                case "tools/call":
                {
                    string name = p["name"]?.GetValue<string>() ?? "";
                    var a = p["arguments"] as JsonObject ?? new JsonObject();
                    JsonArray content;
                    bool isError = false;
                    try { content = await s_tools.Call(name, a); }
                    catch (Exception e)
                    {
                        isError = true;
                        content = new JsonArray(Tools.Text(e is ToolException ? e.Message : e.ToString()));
                    }
                    return Result(id, new JsonObject { ["content"] = content, ["isError"] = isError });
                }
                default:
                    return Error(id, -32601, "method not found: " + method);
            }
        }

        static JsonObject Result(JsonNode id, JsonNode result) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

        static JsonObject Error(JsonNode? id, int code, string message) =>
            new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };
    }

    public sealed class ToolException : Exception { public ToolException(string m) : base(m) { } }

    static class Repo
    {
        /// <summary>The checkout: --repo, COSMIC_SHORE_REPO, or the first folder above the working directory (or this tool) holding Port/src.</summary>
        public static string? Find(string? given)
        {
            foreach (var start in new[] { given, Environment.GetEnvironmentVariable("COSMIC_SHORE_REPO"), Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                if (string.IsNullOrEmpty(start)) continue;
                for (var d = new DirectoryInfo(Path.GetFullPath(start)); d != null; d = d.Parent)
                    if (Directory.Exists(Path.Combine(d.FullName, "Port", "src")) && Directory.Exists(Path.Combine(d.FullName, "Assets")))
                        return d.FullName;
            }
            return null;
        }
    }
}
