using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat's MODE: since 2026-11-02 there is one, <b>Time</b> - the triggers fly the orbit slingshot
    /// (<see cref="StoatSlingExecutor"/>, <c>R_VesselActions/STOAT_DIPOLE.md</c> §1b).
    ///
    /// <para><b>Removed 2026-11-02</b> (Yash: "I only want to keep the second one"): Mass mode (the field dipole, its predicted
    /// path, the thick trail and Armoured Wake) and Gate mode (the warp gate), and the B switch that stepped between the three.
    /// What they were and how to bring them back: <c>Docs/Studios/STOAT_FIELD_TRAJECTORY_PARKED.md</c>.</para>
    ///
    /// <para>Kept as the one place the Stoat's executors, its HUD and the flight recorder ask which mode the triggers fly, so
    /// a mode can return here without touching them. <see cref="Mode"/>'s numbers are recorded in flights; never renumber.</para>
    /// </summary>
    public sealed class StoatModeExecutor : ShipActionExecutorBase
    {
        public enum Mode { Mass = 0, Time = 1 }

        [Header("Config")]
        [Tooltip("The Stoat's mode asset (no rows of its own since Mass and Gate mode were removed).")]
        [SerializeField] StoatModeActionSO config;

        /// <summary>The mode the triggers fly: always Time.</summary>
        public Mode Current => Mode.Time;
        public bool IsTimeMode => true;
        public bool IsMassMode => false;
        /// <summary>True while the triggers fly the orbit slingshot - always.</summary>
        public bool UsesOrbit => true;
        /// <summary>The HUD's Mass card: empty (Time mode).</summary>
        public float MassMode01 => 0f;
        public StoatModeActionSO Config => config;

        public override void Initialize(IVesselStatus shipStatus) { }
    }
}
