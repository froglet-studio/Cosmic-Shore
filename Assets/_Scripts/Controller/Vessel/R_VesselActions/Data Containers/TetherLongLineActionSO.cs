using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// One trigger of the Tether's LONG tether: hold to fire it at the best distant prism on
    /// that side of the search plane and swing round it; let go to fling off along the tangent.
    ///
    /// The Squirrel tube's shape — <c>StartAction</c> → executor <c>Begin</c>, <c>StopAction</c>
    /// → executor <c>Commit</c> — with both edges meaningful: the press arms the hook (it lands
    /// the moment a target is in reach) and the release is the fling. Bound to
    /// <c>LeftStickAction</c>/<c>RightStickAction</c> on gamepad and
    /// <c>OnlyLeftStickAction</c>/<c>OnlyRightStickAction</c> on touch, exactly where the
    /// Squirrel's triggers sit.
    ///
    /// <b>Shared and stateless</b>: which side this is comes from <see cref="side"/> on the asset;
    /// all state lives on <see cref="TetherExecutor"/>. Press and release both replicate through
    /// <c>R_VesselActionHandler</c>'s ServerRpc → ClientRpc, so they run on every peer.
    /// </summary>
    [CreateAssetMenu(fileName = "TetherLongLineAction", menuName = "ScriptableObjects/Vessel Actions/Tether Long Line")]
    public class TetherLongLineActionSO : ShipActionSO
    {
        [Tooltip("Which side of the search plane this trigger fires to: +1 right, −1 left.")]
        [SerializeField] TetherExecutor.Side side = TetherExecutor.Side.Right;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            var executor = Resolve(execs);
            if (executor) executor.Begin(side);
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            var executor = Resolve(execs);
            if (executor) executor.Commit(side);
        }

        // Implicit bool, not ?. — both are UnityEngine.Objects, and a destroyed one is fake-null.
        static TetherExecutor Resolve(ActionExecutorRegistry execs) => execs ? execs.Get<TetherExecutor>() : null;
    }
}
