// FrictionCrystalTurnMonitor.cs
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Friction's crystal target scales per intensity level (10/20/30/50 per the design doc)
    /// rather than being derived from track waypoints like SkimRace. The table is the mode's
    /// AUTO-CALC - the answer when FrogletTools > Game Modes > End Game Conditions leaves
    /// Friction's crystal count at 0 - and is overridable the same way every crystal mode's
    /// waypoint calc is: a number authored there wins outright, for every intensity.
    ///
    /// Nothing here touches the resolved count directly. The base resolves it in StartMonitor
    /// (tool > this auto-calc), publishes it through the NetworkVariable to
    /// GameDataSO.CrystalTargetCount, and the mode's ScoringRule reads it from there - so the
    /// number the goal row counts to, the number that ends the turn and the number the
    /// scoreboard's "crystals left" is measured against are one value.
    /// </summary>
    public class FrictionCrystalTurnMonitor : NetworkCrystalCollisionTurnMonitor
    {
        [Tooltip("Crystal target per intensity (index 0 = intensity 1). Read only while the End " +
                 "Game Conditions tool has Friction's crystal count at 0 (auto).")]
        [SerializeField]
        int[] crystalTargetByIntensity = { 10, 20, 30, 50 };

        protected override int ComputeAutoCalcCount()
        {
            if (crystalTargetByIntensity == null || crystalTargetByIntensity.Length == 0)
                return base.ComputeAutoCalcCount();

            int intensity = Mathf.Clamp(gameData.SelectedIntensity.Value, 1, crystalTargetByIntensity.Length);
            return Mathf.Max(1, crystalTargetByIntensity[intensity - 1]);
        }
    }
}
