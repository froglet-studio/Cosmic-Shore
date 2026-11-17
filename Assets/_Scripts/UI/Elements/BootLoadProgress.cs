using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The boot splash's 0..1 progress (Docs/MENU_REDESIGN_B.md sections 176-177.2): the sign-in steps the boot
    /// already announces, then the menu world's build. The twin of <see cref="ArenaLoadProgress"/>, which does the
    /// same for the loading screen into a game.
    ///
    /// <para><b>Every value is a measurement.</b> A boot step has no inside to measure, so it counts when it is
    /// over: the status that starts a step (<see cref="Report"/>) is the news that every step before it finished,
    /// and the bar moves to that step's start. The world build is measured as it runs (<see cref="Tick"/>): prisms
    /// laid over prisms queued, then prisms settled over the most that were ever growing. Between measurements the
    /// bar HOLDS. Nothing here reads a clock.</para>
    ///
    /// <para><b>The shares</b> (the bar's four steps end at <see cref="StepEnds"/>):</para>
    /// <list type="bullet">
    /// <item>Connecting, 0 to 8%: over when sign-in starts.</item>
    /// <item>Signing in, 8 to 22%: over when the profile starts loading.</item>
    /// <item>Loading profile, 22 to 36%: over when the lobby is being joined.</item>
    /// <item>Creating session, 36 to 100%: joining the lobby (36), creating the session (41), host ready (46),
    /// then the world: laying 50 to 85%, growing 85 to 100%.</item>
    /// </list>
    /// <para>The world owns half the bar because it is the longest wait of a boot (the menu's boot world is
    /// thousands of prisms) and the only part with a real fraction to show.</para>
    ///
    /// <para><b>A load that is not a boot</b> (the splash as the scene-transition overlay on a return to the
    /// menu) reports no step, so the world build is all there is to measure and it spans the whole bar:
    /// laying 0 to 70%, growing 70 to 100%.</para>
    ///
    /// <para><b>Monotonic by construction.</b> Statuses arrive out of order and more than once (the profile can
    /// load before or after the lobby line; a retry starts again at "Signing in"), and the lay counter drops to
    /// 0 whenever a batch starts. The model never lowers its output: a report or a tick that would show less
    /// than what is shown is not shown. It ends at exactly 1 when a world build that was seen is over.</para>
    /// </summary>
    public class BootLoadProgress
    {
        /// <summary>Where the bar's four steps end: Connecting, Signing in, Loading profile, Creating session.</summary>
        public static readonly float[] StepEnds = { 0.08f, 0.22f, 0.36f, 1f };

        public const float ConnectingFloor = 0f;
        public const float SigningInFloor = 0.08f;
        public const float LoadingProfileFloor = 0.22f;
        public const float JoiningLobbyFloor = 0.36f;
        public const float CreatingSessionFloor = 0.41f;
        public const float HostReadyFloor = 0.46f;

        /// <summary>On a boot the world build starts here. Off a boot it starts at 0.</summary>
        public const float WorldFloor = 0.50f;

        /// <summary>Laying's part of the world's share; growing is the rest.</summary>
        public const float LayPartOfWorld = 0.70f;

        float _value;
        int _growPeak;
        bool _boot;
        bool _worldSeen;

        /// <summary>The bar's value. Never decreases within a load.</summary>
        public float Value => _value;

        /// <summary>True once the load is over: the bar reads exactly 1.</summary>
        public bool Done => _value >= 1f;

        /// <summary>True once a boot step was reported in this load: the bar is counting a boot, not a return.</summary>
        public bool IsBoot => _boot;

        /// <summary>True while a boot's bar is still filling: a new load must not wipe it.</summary>
        public bool BootInProgress => _boot && !Done;

        /// <summary>Start of a load. The ONLY place the value may go down.</summary>
        public void Reset()
        {
            _value = 0f;
            _growPeak = 0;
            _boot = false;
            _worldSeen = false;
        }

        /// <summary>Where the bar stands when <paramref name="step"/> starts: every step before it is done.</summary>
        public static float FloorOf(BootStep step) => step switch
        {
            BootStep.Connecting => ConnectingFloor,
            BootStep.SigningIn => SigningInFloor,
            BootStep.LoadingProfile => LoadingProfileFloor,
            BootStep.JoiningLobby => JoiningLobbyFloor,
            BootStep.CreatingSession => CreatingSessionFloor,
            BootStep.HostReady => HostReadyFloor,
            _ => 0f,
        };

        /// <summary>A boot status started <paramref name="step"/>. <see cref="BootStep.None"/> changes nothing.</summary>
        public float Report(BootStep step)
        {
            if (step == BootStep.None) return _value;
            _boot = true;
            return Raise(FloorOf(step));
        }

        /// <summary>
        /// One frame of the world build. <paramref name="covering"/> is the load veil over a world that is not
        /// ready (the boot world's cover): while it is up the build is not over, whatever the counters read in a
        /// gap between batches. Once a build was seen and nothing is covering, laying or growing, the load is done.
        /// </summary>
        public float Tick(bool covering, bool laying, float layProgress, int growRemaining)
        {
            if (covering || laying || growRemaining > 0) _worldSeen = true;

            if (laying)
                return Raise(World(LayPartOfWorld * Mathf.Clamp01(layProgress)));

            if (growRemaining > 0)
            {
                // The most still growing at any point of this load is the total: the count only falls from its peak.
                if (growRemaining > _growPeak) _growPeak = growRemaining;
                float settled = 1f - (float)growRemaining / _growPeak;
                return Raise(World(LayPartOfWorld + (1f - LayPartOfWorld) * Mathf.Clamp01(settled)));
            }

            if (_worldSeen && !covering) return Raise(1f);

            // Nothing measurable: hold.
            return _value;
        }

        // A 0..1 fraction of the world build, on the bar.
        float World(float fraction)
        {
            float floor = _boot ? WorldFloor : 0f;
            return floor + (1f - floor) * fraction;
        }

        float Raise(float target)
        {
            if (target > _value) _value = Mathf.Min(1f, target);
            return _value;
        }
    }
}
