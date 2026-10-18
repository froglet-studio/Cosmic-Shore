namespace CosmicShore.UI
{
    /// <summary>
    /// The Rhino's HUD view. Everything it shows lives on the fleet's four-card ability row
    /// (<see cref="VesselHUDView.abilityIcons"/>, drawn by the ability lockup), driven by
    /// <see cref="RhinoVesselHUDController"/> through the base view's card pushes.
    ///
    /// <para>It used to carry a pre-lockup STATUS CLUSTER beside the row - a skimmer-size ring, a
    /// "slowed" counter, a slow-line flash, a debuff timer, laser and crystal indicators - wired
    /// through six fields here and three GameObjects the Rhino prefab added to this HUD. While the
    /// Rhino bound no ability icon the lockup cleared all of it; binding its first icon (2026-10-06)
    /// switched the lockup to sparing whatever a HUD-root component still references, so the whole
    /// cluster came back in the row's own corner (Docs/ABILITY_LOCKUP.md, vessel skill rule 38).
    /// Two of its channels had no producer at all and the other two counted every Rhino's blasts
    /// rather than this one's. The fields, the GameObjects and the channels are deleted
    /// (2026-10-16), so there is nothing left for the lockup to spare.</para>
    /// </summary>
    public class RhinoVesselHUDView : VesselHUDView
    {
        public override void Initialize() { }
    }
}
