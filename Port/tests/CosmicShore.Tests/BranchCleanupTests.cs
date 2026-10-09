using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Prisma;
using Xunit;

namespace CosmicShore.Tests;

/// <summary>
/// The BRANCHES page: which branches may be deleted (the repo's branch policy), how many commits a
/// branch holds that no trunk has, and that a deletion always saves tag archive/&lt;branch&gt; first,
/// never deletes a branch that moved since it was listed, and keeps the branch when the tag fails.
/// </summary>
public class BranchCleanupTests
{
    static readonly DateTime Now = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
    static readonly BranchCleanup.Policy P = BranchCleanup.Policy.Defaults;

    static BranchCleanup.Branch B(string name, int? unique, int daysOld = 90, int? pr = null) =>
        new() { Name = name, Unique = unique, Measured = true, Date = Now.AddDays(-daysOld), OpenPr = pr, Sha = "sha-" + name };

    [Theory]
    [InlineData("master", 0, BranchCleanup.Group.Locked)]
    [InlineData("development", 0, BranchCleanup.Group.Locked)]
    [InlineData("build/android", 0, BranchCleanup.Group.Locked)]
    [InlineData("archive/old", 0, BranchCleanup.Group.Locked)]
    [InlineData("claude/merged", 0, BranchCleanup.Group.Merged)]
    [InlineData("claude/small", 3, BranchCleanup.Group.Small)]
    [InlineData("claude/medium", 10, BranchCleanup.Group.Medium)]
    [InlineData("claude/large", 11, BranchCleanup.Group.Large)]
    public void Groups_follow_the_policy(string name, int unique, BranchCleanup.Group expected) =>
        Assert.Equal(expected, BranchCleanup.Classify(B(name, unique), P, Now, false).group);

    [Fact]
    public void Locked_branches_say_why_and_large_needs_an_explicit_allow()
    {
        Assert.Equal("open PR #7", BranchCleanup.Classify(B("x", 1, pr: 7), P, Now, true).lockReason);
        Assert.Equal(BranchCleanup.Group.Active, BranchCleanup.Classify(B("x", 0, daysOld: 3), P, Now, true).group);
        Assert.NotNull(BranchCleanup.Classify(B("x", 40), P, Now, false).lockReason);
        Assert.Null(BranchCleanup.Classify(B("x", 40), P, Now, true).lockReason);
        // A branch that shares no history with any trunk is treated as LARGE.
        Assert.Equal(BranchCleanup.Group.Large, BranchCleanup.Classify(B("x", null), P, Now, false).group);
        Assert.Null(BranchCleanup.Classify(B("merged", 0), P, Now, false).lockReason);
    }

    /// <summary>A fake api.github.com: answers GraphQL listing/compare queries and the git refs endpoints.</summary>
    sealed class FakeGitHub : HttpMessageHandler
    {
        public readonly Dictionary<string, (string sha, int?[] ahead)> Branches = new();
        public readonly List<string> Calls = new();
        public HttpStatusCode TagStatus = HttpStatusCode.Created;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            string body = req.Content == null ? "" : await req.Content.ReadAsStringAsync(ct);
            var path = req.RequestUri!.AbsolutePath;
            Calls.Add(req.Method + " " + path);
            if (path == "/graphql")
            {
                var q = JsonDocument.Parse(body).RootElement.GetProperty("query").GetString()!;
                if (q.Contains("refs(refPrefix"))
                {
                    var nodes = Branches.Select(kv => new
                    {
                        name = kv.Key,
                        target = new { oid = kv.Value.sha, committedDate = "2026-07-01T00:00:00Z", messageHeadline = "m", author = new { name = "A" } },
                        associatedPullRequests = new { nodes = Array.Empty<object>() },
                    });
                    return Json(new { data = new { repository = new { refs = new { pageInfo = new { hasNextPage = false, endCursor = (string?)null }, nodes } } } });
                }
                var repo = new Dictionary<string, object?>();
                foreach (Match t in Regex.Matches(q, @"(t\d+): ref\(qualifiedName:""refs/heads/[^""]+""\)\{(.*?)\}\s*(?=t\d+:|\}\})"))
                {
                    int ti = int.Parse(t.Groups[1].Value[1..]);
                    var compares = new Dictionary<string, object?>();
                    foreach (Match c in Regex.Matches(t.Groups[2].Value, @"(c\d+): compare\(headRef:""refs/heads/([^""]+)""\)"))
                    {
                        var ahead = Branches[c.Groups[2].Value].ahead[ti];
                        compares[c.Groups[1].Value] = ahead == null ? null : new { aheadBy = ahead };
                    }
                    repo[t.Groups[1].Value] = compares;
                }
                return Json(new { data = new { repository = repo } });
            }
            var refGet = Regex.Match(path, "/git/ref/heads/(.+)$");
            if (req.Method == HttpMethod.Get && refGet.Success)
            {
                var name = Uri.UnescapeDataString(refGet.Groups[1].Value);
                return Branches.TryGetValue(name, out var b) ? Json(new { @object = new { sha = b.sha } }) : new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            if (req.Method == HttpMethod.Post && path.EndsWith("/git/refs")) return new HttpResponseMessage(TagStatus) { Content = new StringContent("{}") };
            if (req.Method == HttpMethod.Delete) return new HttpResponseMessage(HttpStatusCode.NoContent);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        static HttpResponseMessage Json(object o) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json") };
    }

    [Fact]
    public async Task Lists_measures_against_both_trunks_and_takes_the_smaller_count()
    {
        var gh = new FakeGitHub();
        gh.Branches["old/merged"] = ("s1", new int?[] { 0, 3 });
        gh.Branches["old/history"] = ("s2", new int?[] { null, 2 });   // no common history with bleeding-edge
        gh.Branches["lost"] = ("s3", new int?[] { null, null });
        var c = new BranchCleanup("https://github.com/froglet-studio/Cosmic-Shore.git", "t", gh);
        var list = await c.List(CancellationToken.None);
        await c.Measure(list, P, null, CancellationToken.None);
        Assert.Equal(0, list.Single(b => b.Name == "old/merged").Unique);
        Assert.Equal(2, list.Single(b => b.Name == "old/history").Unique);
        Assert.Null(list.Single(b => b.Name == "lost").Unique);
        Assert.Equal("froglet-studio/Cosmic-Shore", c.Repository);
    }

    [Fact]
    public async Task Delete_saves_the_archive_tag_first_and_refuses_a_moved_branch()
    {
        var gh = new FakeGitHub();
        gh.Branches["claude/a/b"] = ("tip", new int?[] { 0, 0 });
        var c = new BranchCleanup("https://github.com/froglet-studio/Cosmic-Shore", "t", gh);

        var moved = new BranchCleanup.Branch { Name = "claude/a/b", Sha = "older" };
        Assert.False(await c.ArchiveAndDelete(moved, CancellationToken.None));
        Assert.DoesNotContain(gh.Calls, x => x.StartsWith("DELETE"));

        var b = new BranchCleanup.Branch { Name = "claude/a/b", Sha = "tip" };
        Assert.True(await c.ArchiveAndDelete(b, CancellationToken.None));
        int tag = gh.Calls.FindIndex(x => x.StartsWith("POST") && x.EndsWith("/git/refs"));
        int del = gh.Calls.FindIndex(x => x.StartsWith("DELETE"));
        Assert.True(tag >= 0 && del > tag, string.Join(", ", gh.Calls));
        Assert.EndsWith("/git/refs/heads/claude/a/b", gh.Calls[del]);
        Assert.StartsWith("deleted", b.Result);
    }

    [Fact]
    public async Task A_failed_archive_tag_keeps_the_branch()
    {
        var gh = new FakeGitHub { TagStatus = HttpStatusCode.Forbidden };
        gh.Branches["x"] = ("tip", new int?[] { 0, 0 });
        var c = new BranchCleanup("https://github.com/froglet-studio/Cosmic-Shore", "t", gh);
        var b = new BranchCleanup.Branch { Name = "x", Sha = "tip" };
        Assert.False(await c.ArchiveAndDelete(b, CancellationToken.None));
        Assert.DoesNotContain(gh.Calls, x => x.StartsWith("DELETE"));
        Assert.StartsWith("kept", b.Result);
    }

    [Fact]
    public void Policy_reads_the_repo_file_and_falls_back_to_defaults()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "brpolicy-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "Tools", "BranchJanitor"));
        try
        {
            Assert.Equal(30, BranchCleanup.Policy.Load(dir).InactiveDays);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "Tools", "BranchJanitor", "policy.json"), "{\"inactiveDays\": 45, \"largeCommitThreshold\": 20}");
            var p = BranchCleanup.Policy.Load(dir);
            Assert.Equal(45, p.InactiveDays);
            Assert.Equal(20, p.LargeCommitThreshold);
            Assert.Contains("development", p.NeverDelete);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
