using CosmicShore.Data;
using CosmicShore.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Landslide - the Rhino's SPACE game: a demolition race through Cleave's four arenas (the panes, the swell, the
    /// cage, the twistbands; referenced, not forked). The Rhino's hull destroys nothing, so every prism it scores is a
    /// <b>strike</b>: the plough throws a prism sideways, and a thrown prism that runs into another destroys both, scored
    /// to the Rhino's pilot (<see cref="PrismMotion.Annihilate"/>). Cleave's arenas are corduroys of ribs packed a
    /// prism apart, so nearly every throw is a strike. SPACE is the plough's <b>Blade</b>, its width (14 u at rest to
    /// 40 u at Space 10): a wider blade catches more ribs per pass, and every rib it catches is a strike. First domain
    /// to the hostile-prism target wins. See LANDSLIDE.md.
    ///
    /// <para><b>Cleave's match, unchanged.</b> The match shape (1 round / 1 turn, golf-timed, server winner detection,
    /// the milestone toasts 50-52, which Landslide's toast config words for itself) and the AI cage-breakers
    /// (steering only: stations outside the arena so every transit ploughs through it) are
    /// <see cref="CleaveController"/>'s - written for the Rhino before the Swordfish split took the sword. The rule is a
    /// <see cref="CleaveScoringRuleSO"/> asset; the target is Landslide's own
    /// (<see cref="LandslidePrismTurnMonitor"/>).</para>
    ///
    /// <para>The only thing this class adds is the opening hint (<see cref="GameToastSituation.LandslideHint"/>), posted
    /// to the local pilot when the countdown ends. NOTHING ABOUT THE RHINO CHANGES (the LOCKED vessel rule).
    /// RHINO-ONLY is the card's Vessels list.</para>
    /// </summary>
    public class LandslideController : CleaveController
    {
        bool _hintPosted;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _hintPosted = false;
        }

        protected override void OnCountdownTimerEnded()
        {
            base.OnCountdownTimerEnded();
            if (_hintPosted || gameData == null) return;
            _hintPosted = true;
            var local = gameData.LocalPlayer;
            if (local != null) GameToastAPI.Post(GameToastSituation.LandslideHint, local.Domain);
        }
    }
}
