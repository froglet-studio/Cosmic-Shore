using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One of the Thresher's two triggers. Both EDGES carry meaning, so — like the Squirrel tube's
    /// <c>StartAction</c>/<c>StopAction</c> → <c>Begin</c>/<c>Commit</c> — this SO only routes the
    /// press and the release to <see cref="ThresherExecutor"/>, which owns all per-vessel state.
    /// The asset is SHARED and STATELESS; which trigger it is comes from <see cref="control"/>.
    ///
    /// <list type="bullet">
    /// <item><see cref="ThresherControl.Winch"/> (right trigger): hold = let chain out to wind up;
    /// release = reel in fast (the crack).</item>
    /// <item><see cref="ThresherControl.Plant"/> (left trigger): hold = plant the ball and swing round
    /// it; release = fly off, the ball yanked after you.</item>
    /// </list>
    ///
    /// Release semantics are REAL for both (rule 37: this is a hold ability, not press-and-forget),
    /// so an AI that presses for <c>Duration: 0</c> gets a zero-length hold — which for the winch
    /// is a no-op and for the plant is a skid that never becomes a pivot. The Thresher authors no AI
    /// abilities for that reason; see THRESHER.md.
    /// </summary>
    [CreateAssetMenu(fileName = "ThresherAction", menuName = "ScriptableObjects/Vessel Actions/Thresher Trigger")]
    public sealed class ThresherActionSO : ShipActionSO
    {
        public enum ThresherControl
        {
            Winch = 0,
            Plant = 1,
        }

        [Tooltip("Winch = right trigger (hold to let chain out, release to reel in and crack). " +
                 "Plant = left trigger (hold to plant the ball and swing round it, release to fly off).")]
        [SerializeField] ThresherControl control = ThresherControl.Winch;

        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus status)
        {
            var thresher = executors?.Get<ThresherExecutor>();
            if (!thresher) return;
            if (control == ThresherControl.Winch) thresher.BeginPayOut();
            else thresher.BeginPlant();
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus status)
        {
            var thresher = executors?.Get<ThresherExecutor>();
            if (!thresher) return;
            if (control == ThresherControl.Winch) thresher.EndPayOut();
            else thresher.EndPlant();
        }
    }
}
