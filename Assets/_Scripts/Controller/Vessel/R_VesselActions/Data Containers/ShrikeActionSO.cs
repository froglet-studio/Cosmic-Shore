using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The Shrike's controls (values are serialized on the control assets — never renumber; 0-2 were the
    /// first port's direct Lance / Sling / Paint, retired when the right trigger became the mode's).</summary>
    public enum ShrikeControl
    {
        Mass = 3,
        Trigger = 4,
        Zoom = 5,
        SpaceMode = 6,
        ChargeMode = 7,
        TimeMode = 8,
    }

    /// <summary>
    /// One of the Shrike's controls (design: <c>R_VesselActions/SHRIKE.md</c> §3; Garrett, 2026-10-15). Stateless and
    /// shared: every press goes straight to the hull's own <see cref="ShrikeExecutor"/>, which owns all state.
    ///
    /// <para><b>Trigger</b> (RT) does the selected mode's action and is held: Space fires the lance while held, Charge
    /// paints while held, Time slings on the press. <b>Zoom</b> (LT) is held. The three MODE controls and <b>Mass</b> act
    /// on the press and have no release (vessel rule 37: <c>StopAction</c> fires on every release, and the AI's
    /// Start → Stop cycle would otherwise undo them).</para>
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
                case ShrikeControl.Trigger: shrike.BeginTrigger(); break;
                case ShrikeControl.Zoom: shrike.BeginZoom(); break;
                case ShrikeControl.Mass: shrike.PressMass(); break;
                case ShrikeControl.SpaceMode: shrike.SelectMode(ShrikeExecutor.Mode.Space); break;
                case ShrikeControl.ChargeMode: shrike.SelectMode(ShrikeExecutor.Mode.Charge); break;
                case ShrikeControl.TimeMode: shrike.SelectMode(ShrikeExecutor.Mode.Time); break;
            }
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            var shrike = executors ? executors.Get<ShrikeExecutor>() : null;
            if (!shrike) return;
            switch (control)
            {
                case ShrikeControl.Trigger: shrike.EndTrigger(); break;
                case ShrikeControl.Zoom: shrike.EndZoom(); break;
            }
        }
    }
}
