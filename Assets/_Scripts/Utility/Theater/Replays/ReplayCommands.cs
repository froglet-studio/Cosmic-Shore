using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// What a viewer can do to a replay that is playing - the RECEIVER of every
    /// <see cref="IReplayCommand"/>. <see cref="ReplayPlayer"/> is the one real implementation; a test
    /// drives a fake.
    /// </summary>
    public interface IReplayTransport
    {
        bool IsPlaying { get; }
        bool IsPaused { get; }

        /// <summary>Seconds into the replay, from its first sample.</summary>
        float Position { get; }

        float Duration { get; }

        /// <summary>Playback rate: 1 is real time.</summary>
        float Speed { get; set; }

        TheaterShot Shot { get; }

        /// <summary>The pilot the subject shots are watching.</summary>
        string FollowName { get; }

        void TogglePause();

        /// <summary>Jump to a point, in seconds from the start. Clamped to the replay.</summary>
        void Seek(float seconds);

        /// <summary>Pick a shot by its position in <see cref="TheaterShot"/>; the current one re-centres.</summary>
        void SetShot(int index);

        void CycleShot(int step);
        void CycleFollow(int step);

        /// <summary>Stop watching: the stage comes down and the screen goes back to what it was.</summary>
        void Stop();
    }

    /// <summary>
    /// One thing a viewer asked a replay to do. Every input reaches the replay as one of these -
    /// a HUD button, a pad button and a key all build the same command - so what each control does
    /// is written once, and is tested without a scene (<c>ReplayCommandTests</c>).
    ///
    /// <para>These are VIEWER commands. The match itself is recorded as outcomes (poses), never
    /// as inputs - see <c>Docs/REPLAYS.md</c> for why this game cannot replay inputs.</para>
    /// </summary>
    public interface IReplayCommand
    {
        void Execute(IReplayTransport transport);
    }

    /// <summary>The replay commands. Parameterless ones are shared instances; none holds state.</summary>
    public static class ReplayCommands
    {
        public static readonly IReplayCommand TogglePause = new TogglePauseCommand();
        public static readonly IReplayCommand Stop = new StopCommand();

        public static IReplayCommand Nudge(float seconds) => new NudgeCommand(seconds);
        public static IReplayCommand SeekFraction(float fraction) => new SeekFractionCommand(fraction);
        public static IReplayCommand ScaleSpeed(float factor, float min, float max) => new ScaleSpeedCommand(factor, min, max);
        public static IReplayCommand SetShot(int index) => new SetShotCommand(index);
        public static IReplayCommand CycleShot(int step) => new CycleShotCommand(step);
        public static IReplayCommand CycleFollow(int step) => new CycleFollowCommand(step);

        sealed class TogglePauseCommand : IReplayCommand
        {
            public void Execute(IReplayTransport transport) => transport.TogglePause();
        }

        sealed class StopCommand : IReplayCommand
        {
            public void Execute(IReplayTransport transport) => transport.Stop();
        }

        sealed class NudgeCommand : IReplayCommand
        {
            readonly float _seconds;
            public NudgeCommand(float seconds) => _seconds = seconds;
            public void Execute(IReplayTransport transport) => transport.Seek(transport.Position + _seconds);
        }

        sealed class SeekFractionCommand : IReplayCommand
        {
            readonly float _fraction;
            public SeekFractionCommand(float fraction) => _fraction = Mathf.Clamp01(fraction);
            public void Execute(IReplayTransport transport) => transport.Seek(_fraction * transport.Duration);
        }

        sealed class ScaleSpeedCommand : IReplayCommand
        {
            readonly float _factor, _min, _max;

            public ScaleSpeedCommand(float factor, float min, float max)
            {
                _factor = factor;
                _min = Mathf.Min(min, max);
                _max = Mathf.Max(min, max);
            }

            public void Execute(IReplayTransport transport) =>
                transport.Speed = Mathf.Clamp(transport.Speed * _factor, _min, _max);
        }

        sealed class SetShotCommand : IReplayCommand
        {
            readonly int _index;
            public SetShotCommand(int index) => _index = index;
            public void Execute(IReplayTransport transport) => transport.SetShot(_index);
        }

        sealed class CycleShotCommand : IReplayCommand
        {
            readonly int _step;
            public CycleShotCommand(int step) => _step = step;
            public void Execute(IReplayTransport transport) => transport.CycleShot(_step);
        }

        sealed class CycleFollowCommand : IReplayCommand
        {
            readonly int _step;
            public CycleFollowCommand(int step) => _step = step;
            public void Execute(IReplayTransport transport) => transport.CycleFollow(_step);
        }
    }
}
