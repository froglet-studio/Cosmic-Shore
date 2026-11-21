// GameLoop/Time are process-global (one player loop per process, same as the original
// engine), so scene-model tests must not run concurrently.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
