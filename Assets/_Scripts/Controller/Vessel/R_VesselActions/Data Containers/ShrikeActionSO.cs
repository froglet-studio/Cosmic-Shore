using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The Shrike's four controls (values are serialized on the control assets — never renumber).</summary>
    public enum ShrikeControl
    {
        Lance = 0,
        Sling = 1,
        Paint = 2,
        Dock = 3,
    }

    /// <summary>
    /// One of the Shrike's four controls (design: <c>R_VesselActions/SHRIKE.md</c> §3). Stateless and shared: every press
    /// goes straight to the hull's own <see cref="ShrikeExecutor"/>, which owns all state.
    ///
    /// <para><b>Lance</b> (RT) and <b>Paint</b> (LT) are held: the lance fires whenever its cooldown is up, the array
    /// paints while the trigger is down. <b>Sling</b> (B) and <b>Dock</b> (A) act on the press and have no release
    /// (vessel rule 37: <c>StopAction</c> fires on every release, and the AI's Start → Stop cycle would otherwise undo
    /// them).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ShrikeAction", menuName = "ScriptableObjects/Vessel Actions/Shrike Control")]
    public sealed class ShrikeActionSO : ShipActionSO
    {
        [Tooltip("Which of the Shrike's controls this asset is.")]
        [SerializeField] ShrikeControl control;

        public ShrikeControl Kind => control;

        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            var shrike = executors ? executors.Get<ShrikeExecutor>() : null;
            if (!shrike) return;
            switch (control)
            {
                case ShrikeControl.Lance: shrike.BeginLance(); break;
                case ShrikeControl.Sling: shrike.PressSling(); break;
                case ShrikeControl.Paint: shrike.BeginPaint(); break;
                case ShrikeControl.Dock: shrike.PressDock(); break;
            }
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            var shrike = executors ? executors.Get<ShrikeExecutor>() : null;
            if (!shrike) return;
            switch (control)
            {
                case ShrikeControl.Lance: shrike.EndLance(); break;
                case ShrikeControl.Paint: shrike.EndPaint(); break;
            }
        }
    }
}
