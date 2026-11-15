using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Riposte - the Swordfish's CHARGE game (RIPOSTE.md): a sword duel in Jackstraw's 0G heap of pick-up sticks (the
    /// same four cells, <see cref="SpawnableJackstraw"/>). A STRIKE - the Swordfish's sword reaching a rival hull - is one
    /// point, and the first DOMAIN to the target wins (golf-timed).
    ///
    /// <para><b>Why the heap, for this hull.</b> A sword hit lands only while the Swordfish is FASTER than its victim
    /// (<c>VesselCombatHitBySword</c>, BROADSIDE.md section 3), and the Swordfish's speed is its ramp, which winds up only on a
    /// straight line. The sticks break straight lines. The blade cuts a plain stick and the hull rams one for free, so a
    /// pilot can cut a lane to wind up in; the shielded knots take two strokes, the red tips debuff a hull that rams
    /// them, and the I4 ironwood binds a blade that is not energized (Shield Breaker, Space 5, pops it). Charge is the
    /// Swordfish's PvP card: x1 -> x2.5 petals a hit, and at Charge 5 Sword Steal pockets them.</para>
    ///
    /// <para><b>Everything else is Jackstraw's, reused:</b> the crystals in the clearings (a silenced pilot refuels
    /// there), the AI hunters (<see cref="JackstrawHunter"/>, steering only; the Swordfish's own sword hand swings), the
    /// server winner, the snapshot and the replay. Riposte authors its own settings asset (the hunters' sword-length
    /// miss radii), rule words, target and hint. SWORDFISH-ONLY is enforced by the card's Vessels list.</para>
    /// </summary>
    public class RiposteController : JackstrawController
    {
        protected override GameToastSituation HintSituation => GameToastSituation.RiposteHint;
    }
}
