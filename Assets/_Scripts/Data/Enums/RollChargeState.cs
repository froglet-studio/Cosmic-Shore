namespace CosmicShore.Data
{
    // Always assign static numeric values; Unity serialization drift on enum reordering
    // breaks scene-wired references and SOAP asset references silently.
    //
    // The state of the Sparrow's once-per-press strafing-roll charge, and WHY it last
    // changed (BarrelRollController.OnRollChargeChanged -> SparrowHUDView.SetRollCharge).
    //
    // A boost press arms a roll for a short window (rollArmWindowSeconds, 0.3 s) rather
    // than for the whole boost hold - the boost is indefinite, so an arm that lasted the
    // hold fired on any later full-deflection stick and spun the vessel when the pilot
    // was only turning hard. That window makes EVERY boost press end in a charge change,
    // which is why "no roll available" is two members rather than one: the pip must empty
    // for both, and only a real Spent has anything to announce.
    //
    //   Spent  - consumed by an actual roll. The one state that earns the pip's punch.
    //   Armed  - a fresh boost press opened the window; a full stick deflection now rolls.
    //   Lapsed - the window ran out unfired. Nothing was consumed, so nothing is announced;
    //            the boost keeps running and another roll needs another press.
    public enum RollChargeState
    {
        Spent = 0,
        Armed = 1,
        Lapsed = 2,
    }
}
