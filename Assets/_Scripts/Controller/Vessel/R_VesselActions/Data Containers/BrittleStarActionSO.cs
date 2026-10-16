using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The Brittle Star's six controls (values are serialized on the control assets — never renumber).</summary>
    public enum BrittleStarControl
    {
        SpeedMode = 0,
        CreationMode = 1,
        DestructionMode = 2,
        Fire = 3,
        RingDetonation = 4,
        ChargeMode = 5,
    }

    /// <summary>
    /// One of the Brittle Star's six controls (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §3). Stateless and shared:
    /// every press goes straight to the hull's own <see cref="BrittleStarExecutor"/>, which owns all state.
    ///
    /// <para><b>The four MODE controls are press-and-forget</b> — a switch has no release semantics (vessel rule 37:
    /// <c>StopAction</c> fires on every release, and the AI's Start → wait → Stop cycle would otherwise undo every
    /// switch it made). <b>Fire</b> is held: it shoots (or stiffens the cords) from press to release, its depth read analog by the executor. <b>Ring Detonation</b> fires on the press.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "BrittleStarAction", menuName = "ScriptableObjects/Vessel Actions/Brittle Star Control")]
    public sealed class BrittleStarActionSO : ShipActionSO
    {
        [Tooltip("Which of the Brittle Star's controls this asset is.")]
        [SerializeField] BrittleStarControl control;

        public BrittleStarControl Kind => control;

        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            var star = executors ? executors.Get<BrittleStarExecutor>() : null;
            if (!star) return;
            switch (control)
            {
                case BrittleStarControl.SpeedMode: star.SelectMode(BrittleStarExecutor.Mode.Speed); break;
                case BrittleStarControl.CreationMode: star.SelectMode(BrittleStarExecutor.Mode.Creation); break;
                case BrittleStarControl.DestructionMode: star.SelectMode(BrittleStarExecutor.Mode.Destruction); break;
                case BrittleStarControl.ChargeMode: star.SelectMode(BrittleStarExecutor.Mode.Charge); break;
                case BrittleStarControl.Fire: star.BeginFire(); break;
                case BrittleStarControl.RingDetonation: star.DetonateRing(); break;
            }
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            if (control != BrittleStarControl.Fire) return;   // a switch and a detonation have no release
            var star = executors ? executors.Get<BrittleStarExecutor>() : null;
            if (star) star.EndFire();
        }
    }
}
