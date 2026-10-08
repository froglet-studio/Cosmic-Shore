using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Prisma.Bisect;

namespace CosmicShore.Tests
{
    /// <summary>
    /// prisma_bisect (ROADMAP C1b): how a candidate is judged, and reading the answer back from a
    /// real <c>git bisect run</c> limited to Port/ - a planted regression three commits back, a
    /// commit outside Port/ that must never be tested, and a candidate that cannot be judged.
    /// </summary>
    public class BisectTests : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "bisect-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(_root, true);
            }
            catch { }
        }

        [Fact]
        public void Signature_is_a_substring_or_a_regex()
        {
            Assert.True(BisectVerdict.Matches("NullReference", "NullReferenceException: at Cell.Update"));
            Assert.False(BisectVerdict.Matches("nullreference", "NullReferenceException"));
            Assert.True(BisectVerdict.Matches("/null ?reference/", "NullReferenceException"));
            Assert.True(BisectVerdict.Matches("/^Prism.*pool$/", "PrismFactory: empty pool"));
            Assert.False(BisectVerdict.Matches("/^pool/", "PrismFactory: empty pool"));
        }

        [Fact]
        public void Smoke_verdicts()
        {
            Assert.Equal(BisectVerdict.Skip, BisectVerdict.Smoke(buildFailed: true, pass: false, Array.Empty<string>(), "x"));
            Assert.Equal(BisectVerdict.Good, BisectVerdict.Smoke(false, true, Array.Empty<string>(), ""));
            Assert.Equal(BisectVerdict.Bad, BisectVerdict.Smoke(false, false, new[] { "anything" }, ""));
            // With a signature, only the regression's own error counts: older noise is not this bug.
            Assert.Equal(BisectVerdict.Good, BisectVerdict.Smoke(false, false, new[] { "an unrelated error" }, "bisect-plant"));
            Assert.Equal(BisectVerdict.Bad, BisectVerdict.Smoke(false, false, new[] { "x", "boom: bisect-plant at boot" }, "bisect-plant"));
        }

        [Fact]
        public void Parity_verdicts()
        {
            Assert.Equal(BisectVerdict.Skip, BisectVerdict.Parity(buildFailed: true, capable: true, 0));
            Assert.Equal(BisectVerdict.Skip, BisectVerdict.Parity(false, capable: false, 0));
            Assert.Equal(BisectVerdict.Good, BisectVerdict.Parity(false, true, 0));
            Assert.Equal(BisectVerdict.Bad, BisectVerdict.Parity(false, true, 2));
        }

        static string Git(string cwd, string args)
        {
            var psi = new ProcessStartInfo("git", args) { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.Environment["GIT_AUTHOR_NAME"] = psi.Environment["GIT_COMMITTER_NAME"] = "t";
            psi.Environment["GIT_AUTHOR_EMAIL"] = psi.Environment["GIT_COMMITTER_EMAIL"] = "t@t";
            using var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEndAsync();
            var e = p.StandardError.ReadToEndAsync();
            p.WaitForExit();
            return o.Result + e.Result;
        }

        void Commit(string path, string text, string message)
        {
            var f = Path.Combine(_root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(f)!);
            File.WriteAllText(f, text);
            Git(_root, "add -A");
            Git(_root, $"commit -q -m \"{message}\"");
        }

        [Fact]
        public void Reads_a_real_bisect_limited_to_Port()
        {
            Directory.CreateDirectory(_root);
            Git(_root, "init -q");
            Git(_root, "config core.autocrlf false");
            Commit("Port/a.txt", "0", "base");
            for (int i = 1; i <= 9; i++)
            {
                if (i == 6) Commit("Port/plant.txt", "regression", "plant");                  // HEAD~3
                else if (i == 8) Commit("Assets/readme.txt", "not the engine", "outside Port");
                else if (i == 4) Commit("Port/unjudgeable.txt", "x", "cannot be judged");
                else Commit($"Port/f{i}.txt", i.ToString(), $"c{i}");
            }
            string plant = Git(_root, "rev-parse HEAD~3").Trim(), outside = Git(_root, "rev-parse HEAD~1").Trim();
            Assert.Contains("plant", Git(_root, "log -1 --format=%s " + plant));

            // The step: skip the unjudgeable commit, bad where the plant exists, good otherwise.
            File.WriteAllText(Path.Combine(_root, "..", Path.GetFileName(_root) + "-step.sh"),
                "test -f Port/unjudgeable.txt && test ! -f Port/f5.txt && exit 125\ntest -f Port/plant.txt && exit 1\nexit 0\n");
            Git(_root, "bisect start HEAD HEAD~9 -- Port/");
            var run = Git(_root, $"bisect run sh ../{Path.GetFileName(_root)}-step.sh");
            var log = Git(_root, "bisect log");
            Git(_root, "bisect reset");
            File.Delete(Path.Combine(_root, "..", Path.GetFileName(_root) + "-step.sh"));

            var o = BisectOutcome.Parse(run, log);
            Assert.Equal(plant, o.FirstBad);
            Assert.Equal("plant", o.FirstBadSubject);
            Assert.InRange(o.Steps.Count, 1, 14);
            Assert.DoesNotContain(o.Steps, s => outside.StartsWith(s.Commit));
            Assert.All(o.Steps, s => Assert.Contains(s.Verdict, new[] { "good", "bad", "skip" }));
            Assert.Empty(o.Candidates);
        }

        [Fact]
        public void Only_skipped_commits_left_names_the_candidates()
        {
            const string a = "1111111111111111111111111111111111111111", b = "2222222222222222222222222222222222222222";
            var run = $"There are only 'skip'ped commits left to test.\nThe first bad commit could be any of:\n{a}\n{b}\nWe cannot bisect more!\n";
            var log = $"# bad: [{b}] x\ngit bisect start 'b' 'g'\n# skip: [{a}] one\ngit bisect skip {a}\n";
            var o = BisectOutcome.Parse(run, log);
            Assert.Null(o.FirstBad);
            Assert.Equal(new[] { a, b }, o.Candidates);
            Assert.Single(o.Skipped);
        }
    }
}
