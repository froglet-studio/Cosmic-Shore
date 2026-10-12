using Xunit;
using System;
using System.IO;
using Prisma.Training;

namespace CosmicShore.Tests
{
    /// <summary>
    /// --resume-run (Port/docs/AI_TRAINING.md "Long runs"): a killed parallel training run resumes
    /// from the per-generation checkpoints, re-flying the generation that was in flight. Pins what
    /// the supervisor checks before starting the workers.
    /// </summary>
    public class TrainingResumeTests : IDisposable
    {
        readonly string _dir = Path.Combine(Path.GetTempPath(), "resume-tests-" + Guid.NewGuid().ToString("N"));

        public TrainingResumeTests() => Directory.CreateDirectory(_dir);
        public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

        void Checkpoint(int worker, int generation) =>
            File.WriteAllText(Path.Combine(_dir, $"ckpt_w{worker}.json"), $"{{\"ScenarioKey\":\"Squirrel_SkimRace_I4\",\"Population\":{{\"generation\":{generation},\"genomes\":[]}}}}");

        [Fact]
        public void Resumes_at_the_checkpoint_generation_and_drops_the_inflight_results()
        {
            Checkpoint(0, 8); Checkpoint(1, 8);
            File.WriteAllText(Path.Combine(_dir, "g00007_w0.txt"), "merged");
            File.WriteAllText(Path.Combine(_dir, "g00007_w1.txt"), "merged");
            File.WriteAllText(Path.Combine(_dir, "g00008_w1.txt"), "a slice flown before the kill");
            File.WriteAllText(Path.Combine(_dir, "done"), "failed");

            int generation = TrainingRunResume.Prepare(_dir, 2, out var checkpoints, out var refusal, out var note);

            Assert.Equal(8, generation);
            Assert.Null(refusal);
            Assert.Null(note);
            Assert.Equal(new[] { Path.Combine(_dir, "ckpt_w0.json"), Path.Combine(_dir, "ckpt_w1.json") }, checkpoints);
            Assert.True(File.Exists(Path.Combine(_dir, "g00007_w0.txt")), "a completed generation's results stay");
            Assert.False(File.Exists(Path.Combine(_dir, "g00008_w1.txt")), "the in-flight generation is re-flown, so its stale slice goes");
            Assert.False(File.Exists(Path.Combine(_dir, "done")), "the failed marker is cleared");
        }

        /// <summary>
        /// Each worker checkpoints after its OWN merge, when its current match ends, so a kill
        /// between two workers' writes leaves them a generation apart. Every worker holds the
        /// identical population after a merge, so the newest checkpoint stands in for the slot
        /// that is behind, and the run resumes at the newest generation.
        /// </summary>
        [Fact]
        public void Checkpoints_a_generation_apart_resume_from_the_newest()
        {
            Checkpoint(0, 9); Checkpoint(1, 8);
            File.WriteAllText(Path.Combine(_dir, "g00008_w0.txt"), "merged by worker 0");
            File.WriteAllText(Path.Combine(_dir, "g00008_w1.txt"), "merged by worker 0");
            File.WriteAllText(Path.Combine(_dir, "g00009_w0.txt"), "a slice flown before the kill");
            File.WriteAllText(Path.Combine(_dir, "done"), "failed");

            int generation = TrainingRunResume.Prepare(_dir, 2, out var checkpoints, out var refusal, out var note);

            Assert.Equal(9, generation);
            Assert.Null(refusal);
            Assert.Equal(new[] { Path.Combine(_dir, "ckpt_w0.json"), Path.Combine(_dir, "ckpt_w1.json") }, checkpoints);
            Assert.Equal(File.ReadAllText(Path.Combine(_dir, "ckpt_w0.json")), File.ReadAllText(Path.Combine(_dir, "ckpt_w1.json")));
            Assert.Equal(9, TrainingRunResume.CheckpointGeneration(File.ReadAllText(Path.Combine(_dir, "ckpt_w1.json"))));
            Assert.False(File.Exists(Path.Combine(_dir, "ckpt_w1.json.tmp")), "the copy is written atomically");
            Assert.True(File.Exists(Path.Combine(_dir, "g00008_w1.txt")), "the generation the newest checkpoint merged stays");
            Assert.False(File.Exists(Path.Combine(_dir, "g00009_w0.txt")), "the resumed generation is re-flown");
            Assert.Contains("worker(s) 1 had not checkpointed generation 9", note);
            Assert.DoesNotContain("progress row", note);
            Assert.False(File.Exists(Path.Combine(_dir, "done")), "the failed marker is cleared");
        }

        /// <summary>Worker 0 behind: the note says the previous generation's progress row may be missing.</summary>
        [Fact]
        public void Worker_zero_behind_is_named_with_the_rows_only_it_writes()
        {
            Checkpoint(0, 4); Checkpoint(1, 5); Checkpoint(2, 5);

            Assert.Equal(5, TrainingRunResume.Prepare(_dir, 3, out var checkpoints, out var refusal, out var note));

            Assert.Null(refusal);
            Assert.NotNull(checkpoints);
            Assert.Equal(5, TrainingRunResume.CheckpointGeneration(File.ReadAllText(Path.Combine(_dir, "ckpt_w0.json"))));
            Assert.Contains("worker(s) 0 had not checkpointed generation 5", note);
            Assert.Contains("every worker resumes from ckpt_w1.json", note);
            Assert.Contains("generation 4's progress row", note);
        }

        /// <summary>A kill in the first generation: one worker has checkpointed, the other never has.</summary>
        [Fact]
        public void A_worker_with_no_checkpoint_yet_takes_the_newest()
        {
            Checkpoint(0, 1);

            Assert.Equal(1, TrainingRunResume.Prepare(_dir, 2, out var checkpoints, out var refusal, out var note));

            Assert.Null(refusal);
            Assert.Equal(new[] { Path.Combine(_dir, "ckpt_w0.json"), Path.Combine(_dir, "ckpt_w1.json") }, checkpoints);
            Assert.Equal(File.ReadAllText(Path.Combine(_dir, "ckpt_w0.json")), File.ReadAllText(Path.Combine(_dir, "ckpt_w1.json")));
            Assert.Contains("worker(s) 1", note);
        }

        [Fact]
        public void Refuses_when_no_worker_has_a_checkpoint()
        {
            File.WriteAllText(Path.Combine(_dir, "g00000_w0.txt"), "a slice flown before the kill");
            File.WriteAllText(Path.Combine(_dir, "done"), "failed");

            Assert.Equal(-1, TrainingRunResume.Prepare(_dir, 2, out var checkpoints, out var refusal, out _));

            Assert.Null(checkpoints);
            Assert.Contains("no worker checkpoint", refusal);
            Assert.True(File.Exists(Path.Combine(_dir, "done")), "a refused resume leaves the run as it found it");
            Assert.True(File.Exists(Path.Combine(_dir, "g00000_w0.txt")));
        }

        [Fact]
        public void Refuses_a_finished_run_a_missing_generation_and_a_worker_count_below_the_run()
        {
            Checkpoint(0, 5); Checkpoint(1, 5);
            File.WriteAllText(Path.Combine(_dir, "done"), "10");
            Assert.Equal(-1, TrainingRunResume.Prepare(_dir, 2, out _, out var refusal, out _));
            Assert.Contains("already done", refusal);
            File.Delete(Path.Combine(_dir, "done"));

            Checkpoint(2, 5);
            Assert.Equal(-1, TrainingRunResume.Prepare(_dir, 2, out _, out refusal, out _));
            Assert.Contains("worker 2's checkpoint but --workers is 2", refusal);
            File.Delete(Path.Combine(_dir, "ckpt_w2.json"));

            File.WriteAllText(Path.Combine(_dir, "ckpt_w1.json"), "{\"Population\":{}}");
            Assert.Equal(-1, TrainingRunResume.Prepare(_dir, 2, out _, out refusal, out _));
            Assert.Contains("carries no generation", refusal);
        }

        [Fact]
        public void Reads_the_generation_and_result_file_names()
        {
            Assert.Equal(7, TrainingRunResume.CheckpointGeneration("{\"Population\": { \"generation\" : 7 }}"));
            Assert.Equal(-1, TrainingRunResume.CheckpointGeneration("{}"));
            Assert.Equal(8, TrainingRunResume.ResultGeneration("g00008_w3.txt"));
            Assert.Equal(-1, TrainingRunResume.ResultGeneration("meta.txt"));
            Assert.Equal(-1, TrainingRunResume.ResultGeneration("ckpt_w0.json"));
            Assert.Equal(3, TrainingRunResume.CheckpointSlot("ckpt_w3.json"));
            Assert.Equal(-1, TrainingRunResume.CheckpointSlot("ckpt_w.json"));
            Assert.Equal(-1, TrainingRunResume.CheckpointSlot("ckpt_wx.json"));
            Assert.Equal(-1, TrainingRunResume.CheckpointSlot("g00008_w3.txt"));
        }
    }
}
