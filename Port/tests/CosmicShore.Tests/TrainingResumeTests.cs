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

            int generation = TrainingRunResume.Prepare(_dir, 2, out var checkpoints, out var refusal);

            Assert.Equal(8, generation);
            Assert.Null(refusal);
            Assert.Equal(new[] { Path.Combine(_dir, "ckpt_w0.json"), Path.Combine(_dir, "ckpt_w1.json") }, checkpoints);
            Assert.True(File.Exists(Path.Combine(_dir, "g00007_w0.txt")), "a completed generation's results stay");
            Assert.False(File.Exists(Path.Combine(_dir, "g00008_w1.txt")), "the in-flight generation is re-flown, so its stale slice goes");
            Assert.False(File.Exists(Path.Combine(_dir, "done")), "the failed marker is cleared");
        }

        [Fact]
        public void Refuses_when_a_worker_has_no_checkpoint()
        {
            Checkpoint(0, 3);
            Assert.Equal(-1, TrainingRunResume.Prepare(_dir, 2, out var checkpoints, out var refusal));
            Assert.Null(checkpoints);
            Assert.Contains("worker 1 has no checkpoint", refusal);
        }

        [Fact]
        public void Refuses_a_finished_run_and_disagreeing_checkpoints()
        {
            Checkpoint(0, 5); Checkpoint(1, 5);
            File.WriteAllText(Path.Combine(_dir, "done"), "10");
            Assert.Equal(-1, TrainingRunResume.Prepare(_dir, 2, out _, out var refusal));
            Assert.Contains("already done", refusal);
            File.Delete(Path.Combine(_dir, "done"));

            Checkpoint(1, 6);
            Assert.Equal(-1, TrainingRunResume.Prepare(_dir, 2, out _, out refusal));
            Assert.Contains("disagree", refusal);
        }

        [Fact]
        public void Reads_the_generation_and_result_file_names()
        {
            Assert.Equal(7, TrainingRunResume.CheckpointGeneration("{\"Population\": { \"generation\" : 7 }}"));
            Assert.Equal(-1, TrainingRunResume.CheckpointGeneration("{}"));
            Assert.Equal(8, TrainingRunResume.ResultGeneration("g00008_w3.txt"));
            Assert.Equal(-1, TrainingRunResume.ResultGeneration("meta.txt"));
            Assert.Equal(-1, TrainingRunResume.ResultGeneration("ckpt_w0.json"));
        }
    }
}
