using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Watches every action a vessel runs, for the match replay (<c>Docs/REPLAYS.md</c> §6.3): the replay
    /// re-runs the same actions on the real vessels at the same moments, so a Sparrow's guns fire, a boost
    /// burns and a trail drifts as they did in the match.
    /// </summary>
    public interface IVesselActionObserver
    {
        /// <summary>An input's actions ran (<paramref name="down"/>) or were released, resolved against <paramref name="device"/>.</summary>
        void OnInputAction(IVesselStatus vessel, InputEvents input, byte device, bool down);

        /// <summary>An action an autopilot started or stopped directly, without an input (<see cref="VesselActionTape.Start"/>).</summary>
        void OnDirectAction(IVesselStatus vessel, ShipActionSO action, bool start);
    }

    /// <summary>
    /// The one place a vessel's actions are reported from. Two roads run an action, and both pass here:
    /// <list type="bullet">
    /// <item>an INPUT, through <see cref="R_VesselActionHandler"/> (every human press, and the autopilots that
    /// press inputs), reported with the input and the device its actions were resolved against;</item>
    /// <item>an autopilot starting a <see cref="ShipActionSO"/> itself (the AI ability coroutine, the boost
    /// policies, a crew's triggers), which calls <see cref="Start"/> / <see cref="Stop"/> here instead of the
    /// action directly, so the replay sees it too.</item>
    /// </list>
    /// An action another action dispatches (the Sparrow's and Grizzly's mode-switching fire) is not reported:
    /// re-running the outer action re-runs it. With no <see cref="Observer"/> each report costs one null check.
    /// </summary>
    public static class VesselActionTape
    {
        /// <summary>Who is recording - the match replay's recorder, or nobody.</summary>
        public static IVesselActionObserver Observer;

        /// <summary>Start <paramref name="action"/> on a vessel the way an autopilot does, and report it.</summary>
        public static void Start(ShipActionSO action, ActionExecutorRegistry executors, IVesselStatus vessel)
        {
            if (!action) return;
            action.StartAction(executors, vessel);
            Observer?.OnDirectAction(vessel, action, true);
        }

        /// <summary>Stop <paramref name="action"/> the way an autopilot does, and report it.</summary>
        public static void Stop(ShipActionSO action, ActionExecutorRegistry executors, IVesselStatus vessel)
        {
            if (!action) return;
            action.StopAction(executors, vessel);
            Observer?.OnDirectAction(vessel, action, false);
        }
    }
}
