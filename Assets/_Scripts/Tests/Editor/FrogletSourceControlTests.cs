using System.Collections.Generic;
using System.Linq;
using CosmicShore.Editor.Froglet;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The parsers behind FrogletTools > Build > Source Control, held to the exact text git and
    /// Tools/Build/push_checks.py print. The rules about what may be pushed are tested in python
    /// (push_checks.py --self-test); these hold the window's reading of them.
    /// </summary>
    public class FrogletSourceControlTests
    {
        [Test]
        public void Status_ReadsCodesPathsAndRenames()
        {
            // git status --porcelain=v1 -z: a rename's source follows as its own NUL-separated field.
            const string z = " M Assets/A.prefab\0R  Assets/New Name.cs\0Assets/Old.cs\0?? Assets/B.png\0 D Docs/x.md\0UU Assets/C.unity\0";
            var s = FrogletSourceControl.ParseStatus(z);
            Assert.AreEqual(5, s.Count);
            Assert.AreEqual("Assets/A.prefab", s[0].Path);
            Assert.AreEqual('~', s[0].Kind);
            Assert.AreEqual("Assets/New Name.cs", s[1].Path);
            Assert.AreEqual("Assets/Old.cs", s[1].OriginalPath);
            Assert.IsTrue(s[1].IsRenamed);
            Assert.IsTrue(s[2].IsUntracked);
            Assert.AreEqual('+', s[2].Kind);
            Assert.IsTrue(s[3].IsDeleted);
            Assert.IsTrue(s[4].IsConflicted);
            Assert.IsFalse(s[4].IsDeleted);
            Assert.AreEqual('!', s[4].Kind);
        }

        [Test]
        public void Status_EmptyIsNoChanges() => Assert.AreEqual(0, FrogletSourceControl.ParseStatus(string.Empty).Count);

        [Test]
        public void Metas_FoldIntoTheirAssetButStandAloneWhenTheAssetDidNotChange()
        {
            var s = FrogletSourceControl.ParseStatus("?? Assets/A.cs\0?? Assets/A.cs.meta\0 M Assets/B.mat.meta\0 M Assets/C.mat\0");
            var rows = FrogletSourceControl.GroupWithMetas(s);
            Assert.AreEqual(3, rows.Count);
            var a = rows.Single(r => r.Path == "Assets/A.cs");
            Assert.AreEqual("Assets/A.cs.meta", a.Meta.Path);
            Assert.IsNotNull(rows.SingleOrDefault(r => r.Path == "Assets/B.mat.meta"), "a meta whose asset did not change keeps its own row");
            CollectionAssert.AreEquivalent(new[] { "Assets/A.cs", "Assets/A.cs.meta", "Assets/B.mat.meta", "Assets/C.mat" },
                                           FrogletSourceControl.PathsOf(rows));
        }

        [Test]
        public void Rename_CommitsBothPaths()
        {
            var rows = FrogletSourceControl.GroupWithMetas(FrogletSourceControl.ParseStatus("R  Assets/New.cs\0Assets/Old.cs\0"));
            CollectionAssert.AreEquivalent(new[] { "Assets/New.cs", "Assets/Old.cs" }, FrogletSourceControl.PathsOf(rows));
        }

        [Test]
        public void Counts_ReadRevListOutput()
        {
            Assert.IsTrue(FrogletSourceControl.TryParseCounts("3\t12\n", out var l, out var r));
            Assert.AreEqual(3, l);
            Assert.AreEqual(12, r);
            Assert.IsFalse(FrogletSourceControl.TryParseCounts("fatal: no upstream", out _, out _));
            Assert.IsFalse(FrogletSourceControl.TryParseCounts("", out _, out _));
        }

        [Test]
        public void Branches_LocalRemoteAndNoOriginHead()
        {
            const string refs = "refs/heads/charles/thresher\t2026-11-06\tThresher\n" +
                                "refs/remotes/origin/HEAD\t2026-11-06\tx\n" +
                                "refs/remotes/origin/bleeding-edge\t2026-11-06\tMerge pull request #1769\n";
            var b = FrogletSourceControl.ParseBranches(refs);
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual("charles/thresher", b[0].Name);
            Assert.IsFalse(b[0].IsRemote);
            Assert.AreEqual("bleeding-edge", b[1].Name);
            Assert.IsTrue(b[1].IsRemote);
            Assert.AreEqual("Merge pull request #1769", b[1].Subject);
        }

        [Test]
        public void Log_MarksUnpushedCommits()
        {
            const char us = '\u001f';
            var log = $"aaa{us}aa{us}Charles{us}2026-11-06{us}Thresher: sub: colon\nbbb{us}bb{us}Yash{us}2026-11-05{us}Older\n";
            var c = FrogletSourceControl.ParseLog(log, new HashSet<string> { "aaa" });
            Assert.AreEqual(2, c.Count);
            Assert.IsTrue(c[0].Unpushed);
            Assert.AreEqual("Thresher: sub: colon", c[0].Subject);
            Assert.IsFalse(c[1].Unpushed);
        }

        [TestCase("charles/thresher-audio", true)]
        [TestCase("bleeding-edge", false)]
        [TestCase("main", false)]
        [TestCase("has space", false)]
        [TestCase("a..b", false)]
        [TestCase("ends/", false)]
        [TestCase("x.lock", false)]
        [TestCase("", false)]
        public void BranchNames(string name, bool valid)
            => Assert.AreEqual(valid, FrogletSourceControl.IsValidBranchName(name, out _), name);

        [Test]
        public void PullRequestPage_FromEitherGitHubRemoteForm()
        {
            Assert.AreEqual("https://github.com/froglet-studio/Cosmic-Shore/compare/bleeding-edge...charles/thresher?expand=1",
                FrogletSourceControl.CompareUrl("https://github.com/froglet-studio/Cosmic-Shore.git", "charles/thresher"));
            Assert.AreEqual("https://github.com/froglet-studio/Cosmic-Shore/compare/bleeding-edge...charles/thresher?expand=1",
                FrogletSourceControl.CompareUrl("git@github.com:froglet-studio/Cosmic-Shore.git\n", "charles/thresher"));
            Assert.IsNull(FrogletSourceControl.CompareUrl("https://gitlab.com/a/b.git", "x"));
            Assert.IsNull(FrogletSourceControl.CompareUrl(null, "x"));
        }

        [Test]
        public void PushChecksReport_ReadsWhatThePythonPrints()
        {
            // Shape of `python3 Tools/Build/push_checks.py --json` (fields renamed on either side break here).
            const string json = "{\"branch\": \"charles/thresher\", \"base\": \"origin/bleeding-edge\", " +
                                "\"behind\": {\"merges\": 35, \"commits\": 120}, \"checked\": 563, \"unpushedPaths\": 0, " +
                                "\"findings\": [" +
                                "{\"id\": \"junk\", \"severity\": \"warn\", \"title\": \"95 file(s)\", \"detail\": \"a\\nb\", " +
                                "\"paths\": [\"Assets/.DS_Store\", \"steam_appid.txt\"], \"fix\": \"discard\"}," +
                                "{\"id\": \"protected-branch\", \"severity\": \"block\", \"title\": \"You are on main\", \"detail\": \"\", " +
                                "\"paths\": [], \"fix\": null}]}";
            var r = JsonUtility.FromJson<ScCheckReport>(json);
            Assert.AreEqual("charles/thresher", r.branch);
            Assert.AreEqual("origin/bleeding-edge", r.@base);
            Assert.AreEqual(35, r.behind.merges);
            Assert.AreEqual(563, r.@checked);
            Assert.AreEqual(2, r.findings.Length);
            Assert.AreEqual("discard", r.findings[0].fix);
            CollectionAssert.AreEqual(new[] { "Assets/.DS_Store", "steam_appid.txt" }, r.findings[0].paths);
            Assert.IsTrue(r.findings[1].IsBlock);
            Assert.IsTrue(string.IsNullOrEmpty(r.findings[1].fix));
        }
    }
}
