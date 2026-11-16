using CosmicShore.Data;
using CosmicShore.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Bullrush - the Rhino's CHARGE game: a joust built on speed. A <b>gore</b> (the Rhino's Charge ability,
    /// <see cref="RhinoGoreBySkimmerEffectSO"/>) is a Strike-class combat hit, landed only while the Rhino rams a rival
    /// faster than the rival is flying AND above the gore speed (150 u/s at Charge 0, three times the cruise, so a gore
    /// needs a Ramp Spool wind-up; 50 u/s at Charge 10). Each gore is one point; the first DOMAIN to the target wins.
    /// See BULLRUSH.md.
    ///
    /// <para><b>Jackstraw's match, unchanged.</b> The match shape (1 round / 1 turn, golf-timed, server winner
    /// detection, the snapshot of the breakdown, the crystals in the heap's clearings, the steering-only hunters) is
    /// <see cref="JackstrawController"/>'s, the arena is Jackstraw's own heap of sticks
    /// (<see cref="SpawnableJackstraw"/>, referenced, not forked), the rule is a <see cref="JackstrawScoringRuleSO"/>
    /// asset with the Rhino's words, and the settings a <see cref="JackstrawSettingsSO"/> asset with the Rhino's
    /// numbers. For a Rhino the heap is a different place than for a Thresher: the plough throws every stick aside at no
    /// cost in speed, so the sticks are cover, not a price, and the Ramp Spool only winds up on a straight line, so the
    /// clearings and the long runs between them are where a gore is built.</para>
    ///
    /// <para>The only thing this class adds is the mode's own opening hint (<see cref="GameToastSituation.BullrushHint"/>),
    /// posted to the local pilot when the countdown ends. Jackstraw's own hint (160) is not in Bullrush's toast config, so
    /// it shows nothing here. NOTHING ABOUT THE RHINO CHANGES (the LOCKED vessel rule). RHINO-ONLY is the card's
    /// Vessels list.</para>
    /// </summary>
    public class BullrushController : JackstrawController
    {
        bool _bullrushHintPosted;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _bullrushHintPosted = false;
        }

        protected override void OnCountdownTimerEnded()
        {
            base.OnCountdownTimerEnded();
            if (_bullrushHintPosted || gameData == null) return;
            _bullrushHintPosted = true;
            var local = gameData.LocalPlayer;
            if (local != null) GameToastAPI.Post(GameToastSituation.BullrushHint, local.Domain);
        }
    }
}
