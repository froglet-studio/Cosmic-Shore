using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// Builds the iOS .ipa on GitHub's free macOS runner (.github/workflows/prisma-ios.yml):
    /// starts the run for a branch, follows it step by step, downloads the artifact and unzips the
    /// .ipa. Apple only compiles iPhone apps on macOS; this is how a Windows PC gets one without a Mac.
    /// The .ipa is unsigned: Sideloadly (or AltStore) signs it with the user's Apple ID.
    /// </summary>
    public sealed class GitHubActions
    {
        public const string Workflow = "prisma-ios.yml";
        const string RequestFile = "Port/ios-build-request.txt";
        static readonly string[] SharedBranches = { "bleeding-edge", "development", "main" };

        readonly string _owner, _repo, _token;
        readonly HttpClient _http;

        public string? RunUrl { get; private set; }

        public GitHubActions(string remoteUrl, string token)
        {
            var m = Regex.Match(remoteUrl, @"github\.com[/:]([^/]+)/([^/.]+?)(\.git)?/?$");
            if (!m.Success) throw new InvalidOperationException("The repository is not on github.com: " + remoteUrl);
            _owner = m.Groups[1].Value; _repo = m.Groups[2].Value; _token = token;
            _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(10) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Prisma/0.1");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        }

        string Api(string path) => $"https://api.github.com/repos/{_owner}/{_repo}/{path}";

        async Task<HttpResponseMessage> Send(HttpMethod method, string url, object? body, CancellationToken ct)
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            return await _http.SendAsync(req, ct);
        }

        async Task<JsonDocument> Get(string url, CancellationToken ct)
        {
            var r = await Send(HttpMethod.Get, url, null, ct);
            if (!r.IsSuccessStatusCode) throw new Exception($"GitHub answered {(int)r.StatusCode} for {url}");
            return JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
        }

        /// <summary>
        /// Starts the build and returns the run id. Prefers workflow_dispatch (needs the workflow on
        /// the default branch and an Actions-write token); falls back to committing the request file
        /// on a feature branch, which is the workflow's push trigger.
        /// </summary>
        public async Task<long> Start(string branch, bool debug, LogBuffer log, CancellationToken ct)
        {
            var since = DateTime.UtcNow.AddSeconds(-5);
            var repo = await Get($"https://api.github.com/repos/{_owner}/{_repo}", ct);
            string def = repo.RootElement.GetProperty("default_branch").GetString()!;

            var r = await Send(HttpMethod.Post, Api($"actions/workflows/{Workflow}/dispatches"),
                new { @ref = def, inputs = new { @ref = branch, configuration = debug ? "Debug" : "Release" } }, ct);
            string evt = "workflow_dispatch";
            if (r.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
            {
                // Not on the default branch yet: trigger it from the branch itself.
                if (SharedBranches.Contains(branch) || branch.StartsWith("build/"))
                    throw new Exception($"The iOS workflow is not on {def} yet, and {branch} is a shared branch. Pick your feature branch, or merge the port first.");
                log.Add(LogKind.Info, $"Workflow not on {def} yet - starting it from {branch} instead.");
                await TouchRequestFile(branch, ct);
                evt = "push";
            }
            else if (r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                throw new Exception("GitHub refused to start the build. The token needs Actions: read & write (fine-grained) or the 'workflow' scope. Set it in SETTINGS > Source.");
            else if (!r.IsSuccessStatusCode)
                throw new Exception($"GitHub answered {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync(ct)}");

            // The API does not return the run it started; find it by time (and branch for a push).
            for (int i = 0; i < 40; i++)
            {
                await Task.Delay(3000, ct);
                var q = $"actions/workflows/{Workflow}/runs?event={evt}&per_page=10" + (evt == "push" ? "&branch=" + Uri.EscapeDataString(branch) : "");
                using var runs = await Get(Api(q), ct);
                foreach (var run in runs.RootElement.GetProperty("workflow_runs").EnumerateArray())
                {
                    if (run.GetProperty("created_at").GetDateTime().ToUniversalTime() < since) continue;
                    RunUrl = run.GetProperty("html_url").GetString();
                    return run.GetProperty("id").GetInt64();
                }
            }
            throw new Exception("The build was requested but no run appeared on GitHub within two minutes.");
        }

        async Task TouchRequestFile(string branch, CancellationToken ct)
        {
            string? sha = null;
            var cur = await Send(HttpMethod.Get, Api($"contents/{RequestFile}?ref={Uri.EscapeDataString(branch)}"), null, ct);
            if (cur.IsSuccessStatusCode)
                using (var doc = JsonDocument.Parse(await cur.Content.ReadAsStringAsync(ct)))
                    sha = doc.RootElement.GetProperty("sha").GetString();
            var text = $"Requested by Prisma at {DateTime.UtcNow:O}\n";
            var put = await Send(HttpMethod.Put, Api("contents/" + RequestFile), new
            {
                message = "ci(ios): request a Prisma .ipa build",
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes(text)),
                branch,
                sha,
            }, ct);
            if (!put.IsSuccessStatusCode)
                throw new Exception($"Could not start the build on {branch} ({(int)put.StatusCode}). The token needs Contents: read & write for that, or merge the workflow to the default branch.");
        }

        public sealed record RunState(string Status, string? Conclusion, string Step, int Done, int Total);

        public async Task<RunState> Poll(long runId, CancellationToken ct)
        {
            using var run = await Get(Api($"actions/runs/{runId}"), ct);
            string status = run.RootElement.GetProperty("status").GetString()!;
            string? conclusion = run.RootElement.GetProperty("conclusion").ValueKind == JsonValueKind.String
                ? run.RootElement.GetProperty("conclusion").GetString() : null;
            string step = status == "queued" ? "Waiting for a Mac runner" : "Starting";
            int done = 0, total = 0;
            using var jobs = await Get(Api($"actions/runs/{runId}/jobs"), ct);
            foreach (var job in jobs.RootElement.GetProperty("jobs").EnumerateArray())
            {
                if (!job.TryGetProperty("steps", out var steps)) continue;
                foreach (var s in steps.EnumerateArray())
                {
                    total++;
                    var st = s.GetProperty("status").GetString();
                    if (st == "completed") done++;
                    else if (st == "in_progress") step = s.GetProperty("name").GetString() ?? step;
                }
            }
            return new RunState(status, conclusion, step, done, total);
        }

        /// <summary>Downloads the run's .ipa artifact and unzips it into <paramref name="outDir"/>.</summary>
        public async Task<string> DownloadIpa(long runId, string outDir, CancellationToken ct)
        {
            using var arts = await Get(Api($"actions/runs/{runId}/artifacts"), ct);
            var art = arts.RootElement.GetProperty("artifacts").EnumerateArray()
                .FirstOrDefault(a => a.GetProperty("name").GetString()!.Contains("ipa"));
            if (art.ValueKind != JsonValueKind.Object) throw new Exception("The run finished but published no .ipa artifact.");
            var r = await Send(HttpMethod.Get, art.GetProperty("archive_download_url").GetString()!, null, ct);
            if (r.StatusCode == HttpStatusCode.Found || r.StatusCode == HttpStatusCode.Redirect)
            {
                // The archive lives on a signed storage URL; it must be fetched WITHOUT the GitHub token.
                using var plain = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
                r = await plain.GetAsync(r.Headers.Location, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            if (!r.IsSuccessStatusCode) throw new Exception($"Downloading the artifact failed ({(int)r.StatusCode}).");
            Directory.CreateDirectory(outDir);
            var zip = Path.Combine(outDir, "artifact.zip");
            await using (var f = File.Create(zip)) await r.Content.CopyToAsync(f, ct);
            string? ipa = null;
            using (var z = ZipFile.OpenRead(zip))
                foreach (var e in z.Entries.Where(e => e.Name.EndsWith(".ipa", StringComparison.OrdinalIgnoreCase)))
                {
                    ipa = Path.Combine(outDir, e.Name);
                    e.ExtractToFile(ipa, overwrite: true);
                }
            File.Delete(zip);
            return ipa ?? throw new Exception("The artifact held no .ipa.");
        }
    }
}
