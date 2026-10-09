#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Prisma
{
    /// <summary>
    /// Finds inactive branches on GitHub and deletes the ones the user ticks, saving each as tag
    /// <c>archive/&lt;branch&gt;</c> first so it can be restored. Prisma's BRANCHES page drives it.
    ///
    /// The rules are the repo's (<c>Tools/BranchJanitor/policy.json</c>, also used by the Branch cleanup
    /// workflow and branch_cleanup.py); <see cref="Policy.Defaults"/> mirrors them for a workspace that
    /// does not carry the file yet. A branch is LOCKED, and never deleted here, when it is a trunk or
    /// pipeline branch, has an open pull request, or has a commit newer than <see cref="Policy.InactiveDays"/>.
    /// A LARGE branch (that many commits in no trunk or more) is deletable only when the caller allows it.
    /// </summary>
    public sealed class BranchCleanup
    {
        public enum Group { Merged = 0, Small = 1, Medium = 2, Large = 3, Active = 4, Locked = 5 }

        public sealed class Policy
        {
            public string[] Trunks { get; set; } = { "bleeding-edge", "master" };
            public string[] NeverDelete { get; set; } = { "master", "main", "bleeding-edge", "development", "Ys-bleeding-edge" };
            public string[] NeverDeletePatterns { get; set; } = { "^build/", "^release/", "^archive/" };
            public int InactiveDays { get; set; } = 30;
            public int LargeCommitThreshold { get; set; } = 11;
            public bool LockOpenPullRequests { get; set; } = true;

            public static Policy Defaults => new();

            /// <summary>The repo's policy.json when the workspace has it, else the defaults.</summary>
            public static Policy Load(string? workspaceDir)
            {
                try
                {
                    var path = workspaceDir == null ? null : Path.Combine(workspaceDir, "Tools", "BranchJanitor", "policy.json");
                    if (path != null && File.Exists(path))
                        return JsonSerializer.Deserialize<Policy>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Defaults;
                }
                catch (Exception) { /* a malformed file falls back to the safe defaults */ }
                return Defaults;
            }
        }

        public sealed class Branch
        {
            public string Name { get; set; } = "";
            public string Sha { get; set; } = "";
            public DateTime Date { get; set; }
            public string Author { get; set; } = "";
            public string Message { get; set; } = "";
            public int? OpenPr { get; set; }
            /// <summary>Commits on the branch that are in no trunk; null when it shares history with none.</summary>
            public int? Unique { get; set; }
            public bool Measured { get; set; }
            public Group Group { get; set; }
            /// <summary>Why the branch cannot be deleted; null when it can.</summary>
            public string? Lock { get; set; }
            /// <summary>What happened when deletion was attempted.</summary>
            public string? Result { get; set; }
        }

        /// <summary>Sorts one branch into its group and says whether, and why, it is locked.</summary>
        public static (Group group, string? lockReason) Classify(Branch b, Policy p, DateTime nowUtc, bool allowLarge)
        {
            if (p.NeverDelete.Contains(b.Name) || p.NeverDeletePatterns.Any(r => Regex.IsMatch(b.Name, r)))
                return (Group.Locked, "trunk / pipeline branch");
            if (p.LockOpenPullRequests && b.OpenPr != null)
                return (Group.Locked, $"open PR #{b.OpenPr}");
            int age = (int)(nowUtc - b.Date).TotalDays;
            if (age < p.InactiveDays) return (Group.Active, $"commit {age} days ago");
            if (!b.Measured) return (Group.Large, "not measured yet");
            if (b.Unique == null) return (Group.Large, "history could not be measured");
            if (b.Unique == 0) return (Group.Merged, null);
            if (b.Unique <= 3) return (Group.Small, null);
            if (b.Unique < p.LargeCommitThreshold) return (Group.Medium, null);
            return (Group.Large, allowLarge ? null : $"large: {b.Unique} unique commits");
        }

        readonly string _owner, _repo, _token;
        readonly HttpClient _http;

        public BranchCleanup(string remoteUrl, string token, HttpMessageHandler? handler = null)
        {
            var m = Regex.Match(remoteUrl, @"github\.com[/:]([^/]+)/([^/.]+?)(\.git)?/?$");
            if (!m.Success) throw new InvalidOperationException("The repository is not on github.com: " + remoteUrl);
            _owner = m.Groups[1].Value; _repo = m.Groups[2].Value; _token = token;
            _http = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromMinutes(2) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Prisma/0.1");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }

        public string Repository => _owner + "/" + _repo;

        async Task<(HttpStatusCode code, JsonDocument? body)> Send(HttpMethod method, string url, object? body, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(method, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var r = await _http.SendAsync(req, ct);
            var text = await r.Content.ReadAsStringAsync(ct);
            JsonDocument? doc = null;
            if (text.Length > 0) try { doc = JsonDocument.Parse(text); } catch (JsonException) { }
            return (r.StatusCode, doc);
        }

        async Task<JsonElement> GraphQl(string query, CancellationToken ct)
        {
            var (code, doc) = await Send(HttpMethod.Post, "https://api.github.com/graphql",
                new { query, variables = new { o = _owner, r = _repo } }, ct);
            if (code == HttpStatusCode.Unauthorized) throw new Exception("GitHub refused the sign-in (401). Paste a token in SETTINGS > Source.");
            if (doc == null || !doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                throw new Exception($"GitHub answered {(int)code}: {Trim(doc?.RootElement.ToString() ?? "", 200)}");
            return data.Clone(); // per-alias errors (unrelated histories) leave that alias null
        }

        /// <summary>Every branch with its tip commit and any open pull request.</summary>
        public async Task<List<Branch>> List(CancellationToken ct)
        {
            var list = new List<Branch>();
            string? cursor = null;
            do
            {
                var after = cursor == null ? "" : $",after:{JsonSerializer.Serialize(cursor)}";
                var data = await GraphQl("query($o:String!,$r:String!){repository(owner:$o,name:$r){refs(refPrefix:\"refs/heads/\",first:100" + after +
                    "){pageInfo{hasNextPage endCursor} nodes{name target{... on Commit{oid committedDate messageHeadline author{name}}} associatedPullRequests(states:OPEN,first:1){nodes{number}}}}}}", ct);
                var refs = data.GetProperty("repository").GetProperty("refs");
                foreach (var n in refs.GetProperty("nodes").EnumerateArray())
                {
                    var t = n.GetProperty("target");
                    var prs = n.GetProperty("associatedPullRequests").GetProperty("nodes");
                    list.Add(new Branch
                    {
                        Name = n.GetProperty("name").GetString()!,
                        Sha = t.TryGetProperty("oid", out var o) ? o.GetString()! : "",
                        Date = t.TryGetProperty("committedDate", out var d) ? d.GetDateTime().ToUniversalTime() : DateTime.UtcNow,
                        Message = t.TryGetProperty("messageHeadline", out var mh) ? mh.GetString() ?? "" : "",
                        Author = t.TryGetProperty("author", out var au) && au.ValueKind == JsonValueKind.Object && au.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "",
                        OpenPr = prs.GetArrayLength() > 0 ? prs[0].GetProperty("number").GetInt32() : null,
                    });
                }
                var page = refs.GetProperty("pageInfo");
                cursor = page.GetProperty("hasNextPage").GetBoolean() ? page.GetProperty("endCursor").GetString() : null;
            } while (cursor != null);
            return list;
        }

        /// <summary>Counts each branch's commits that are in no trunk (30 branches per query).</summary>
        public async Task Measure(IList<Branch> branches, Policy p, Action<int>? progress, CancellationToken ct)
        {
            for (int i = 0; i < branches.Count; i += 30)
            {
                var chunk = branches.Skip(i).Take(30).ToList();
                var sb = new StringBuilder("query($o:String!,$r:String!){repository(owner:$o,name:$r){");
                for (int ti = 0; ti < p.Trunks.Length; ti++)
                {
                    sb.Append($"t{ti}: ref(qualifiedName:{JsonSerializer.Serialize("refs/heads/" + p.Trunks[ti])}){{");
                    for (int bi = 0; bi < chunk.Count; bi++)
                        sb.Append($"c{bi}: compare(headRef:{JsonSerializer.Serialize("refs/heads/" + chunk[bi].Name)}){{aheadBy}} ");
                    sb.Append('}');
                }
                sb.Append("}}");
                var repo = (await GraphQl(sb.ToString(), ct)).GetProperty("repository");
                for (int bi = 0; bi < chunk.Count; bi++)
                {
                    var vals = new List<int>();
                    for (int ti = 0; ti < p.Trunks.Length; ti++)
                        if (repo.TryGetProperty($"t{ti}", out var t) && t.ValueKind == JsonValueKind.Object &&
                            t.TryGetProperty($"c{bi}", out var c) && c.ValueKind == JsonValueKind.Object)
                            vals.Add(c.GetProperty("aheadBy").GetInt32());
                    chunk[bi].Unique = vals.Count > 0 ? vals.Min() : null;
                    chunk[bi].Measured = true;
                }
                progress?.Invoke(Math.Min(i + 30, branches.Count));
            }
        }

        /// <summary>
        /// Saves tag archive/&lt;branch&gt; at the tip it was listed with, then deletes the branch. Refuses
        /// when the branch moved since it was listed, and keeps the branch when the tag could not be saved.
        /// </summary>
        public async Task<bool> ArchiveAndDelete(Branch b, CancellationToken ct)
        {
            var path = string.Join("/", b.Name.Split('/').Select(Uri.EscapeDataString));
            var (code, cur) = await Send(HttpMethod.Get, $"https://api.github.com/repos/{_owner}/{_repo}/git/ref/heads/{path}", null, ct);
            if (code == HttpStatusCode.NotFound) { b.Result = "already gone"; return false; }
            var sha = cur?.RootElement.TryGetProperty("object", out var obj) == true ? obj.GetProperty("sha").GetString() : null;
            if (sha != b.Sha) { b.Result = "skipped: new commits since it was listed"; return false; }

            (code, var tag) = await Send(HttpMethod.Post, $"https://api.github.com/repos/{_owner}/{_repo}/git/refs",
                new { @ref = "refs/tags/archive/" + b.Name, sha = b.Sha }, ct);
            bool tagExists = code == HttpStatusCode.UnprocessableEntity && (tag?.RootElement.ToString() ?? "").Contains("already exists");
            if (code != HttpStatusCode.Created && !tagExists)
            {
                b.Result = $"kept: could not save the archive tag ({(int)code})";
                return false;
            }
            (code, var del) = await Send(HttpMethod.Delete, $"https://api.github.com/repos/{_owner}/{_repo}/git/refs/heads/{path}", null, ct);
            if (code != HttpStatusCode.NoContent)
            {
                b.Result = $"failed to delete ({(int)code}: {Trim(del?.RootElement.ToString() ?? "", 80)})";
                return false;
            }
            b.Result = "deleted, saved as archive/" + b.Name;
            return true;
        }

        static string Trim(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "...";
    }
}
