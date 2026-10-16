using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The Angler's button controls (values are serialized on the control assets — never renumber). The Sling
    /// painter itself rides the two TRIGGERS, read as analog depth (<see cref="AnglerCommandExecutor"/>), not as presses.</summary>
    public enum AnglerControl
    {
        Lure = 0,
        CycleSquad = 1,
        Recall = 2,
        Dash = 3,
    }

    /// <summary>
    /// One of the Angler's button controls (design: <c>R_VesselActions/ANGLER.md</c> §2). Stateless and shared: every
    /// press goes straight to the hull's own <see cref="AnglerCommandExecutor"/>, which owns all state.
    ///
    /// <para><b>The lure is held</b> (the light is out from press to release). <b>Squad</b> and <b>Recall</b> are
    /// press-and-forget: they have no release semantics (vessel rule 37). So is the <b>Dash</b> (the Time ability).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "AnglerAction", menuName = "ScriptableObjects/Vessel Actions/Angler Control")]
    public sealed class AnglerActionSO : ShipActionSO
    {
        [Tooltip("Which of the Angler's button controls this asset is.")]
        [SerializeField] AnglerControl control;

        public AnglerControl Kind => control;

        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            var angler = executors ? executors.Get<AnglerCommandExecutor>() : null;
            if (!angler) return;
            switch (control)
            {
                case AnglerControl.Lure: angler.BeginLure(); break;
                case AnglerControl.CycleSquad: angler.CycleSquad(); break;
                case AnglerControl.Recall: angler.Recall(); break;
                case AnglerControl.Dash: angler.Dash(); break;
            }
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            if (control != AnglerControl.Lure) return;   // a squad pick, a recall and a dash have no release
            var angler = executors ? executors.Get<AnglerCommandExecutor>() : null;
            if (angler) angler.EndLure();
        }
    }
}
