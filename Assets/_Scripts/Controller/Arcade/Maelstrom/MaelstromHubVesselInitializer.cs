using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spawns every pilot a vessel in the Maelstrom hub, so the round everybody is waiting for is
    /// something they can fly rather than a picture of a world.
    ///
    /// <para>It is the ordinary <see cref="ServerPlayerVesselInitializer"/> chain - the same one
    /// every game scene runs - with two hub-shaped differences and, deliberately, no third:</para>
    ///
    /// <list type="bullet">
    /// <item><b>Autopilot on arrival.</b> A hub vessel flies itself until its pilot taps into the
    /// preview window, exactly as the menu's lava-lamp vessel does. The hub is a waiting room, so
    /// a ship that sat still until someone touched a stick would read as a frozen scene.</item>
    /// <item><b>The upcoming mode's HULL.</b> Fifteen of the sixteen pool modes lock to one vessel,
    /// so the hub hands you that one - the warm-up is in the ship you are about to race in, and
    /// the hull swap is paid for here instead of inside the next scene's connecting screen.</item>
    /// </list>
    ///
    /// <para><b>It does NOT touch domain</b>, and that is the one thing to be careful of when
    /// reading this beside <c>MenuServerPlayerVesselInitializer</c>, which it otherwise resembles.
    /// The menu resets every human to Jade because the lava lamp has no teams; a Maelstrom hub sits
    /// in the middle of a running tournament whose entire scoreboard is per-domain, so resetting a
    /// pilot's domain between rounds would hand their points to a team they are no longer on.</para>
    ///
    /// <para>Hub vessels are ordinary scene vessels (<c>destroyWithScene: true</c>): the round
    /// launch is a Single load, which is exactly what should take them away.</para>
    /// </summary>
    public class MaelstromHubVesselInitializer : ServerPlayerVesselInitializer
    {
        /// <summary>
        /// Nobody is finishing a match here, so a pilot who leaves the hub leaves nothing behind
        /// for the AI to take over. Same reasoning as the menu's.
        /// </summary>
        protected override bool ConvertDepartedPlayersToAI => false;

        /// <summary>
        /// Fly the hull the NEXT round will be played in when that round locks to exactly one, and
        /// otherwise take the pilot's own pick verbatim.
        ///
        /// <para>The base clamps against <c>gameData.AllowedVesselClasses</c>, which is the wrong
        /// answer in a hub for a subtle reason: that list deliberately survives the per-scene
        /// reset (it is pre-launch config the game scene still has to read), so between rounds it
        /// still holds the hull of the round that just FINISHED. Clamping to it would put everyone
        /// in the last mode's ship to warm up for the next one.</para>
        /// </summary>
        /// <remarks>
        /// Unlike the base it deliberately does NOT call <c>Player.ServerForceVesselType</c> to
        /// write the resolved hull back onto <c>NetDefaultVesselType</c>. That variable is the
        /// PLAYER'S standing preference, and the menu reads it verbatim on the way home ("the menu
        /// is where you are allowed to fly anything"); stamping a warm-up hull into it would send
        /// a pilot back to the lava lamp flying whatever the Maelstrom happened to draw last. The
        /// base keeps it honest because a game scene's respawns, HUD and telemetry all read it -
        /// none of which a hub has.
        /// </remarks>
        protected override VesselClassType ResolveSpawnVesselType(Player networkPlayer)
        {
            var pending = MaelstromController.Instance?.PendingGame;
            if (pending != null && pending.Vessels != null && pending.Vessels.Count == 1 && pending.Vessels[0] != null)
                return pending.Vessels[0].Class;

            return networkPlayer.NetDefaultVesselType.Value;
        }

        protected override async UniTask OnPlayerReadyToSpawnAsync(Player player, CancellationToken ct)
        {
            await base.OnPlayerReadyToSpawnAsync(player, ct);
            ActivateAutopilot(player);
        }

        static void ActivateAutopilot(Player player)
        {
            if (player?.Vessel == null)
            {
                CSDebug.LogError("[MaelstromHubVesselInit] Player or Vessel not available after " +
                                 "initialization - the hub vessel will not fly itself.");
                return;
            }

            player.StartPlayer();
            player.Vessel.ToggleAIPilot(true);
            player.InputController?.SetPause(true);
        }
    }
}
