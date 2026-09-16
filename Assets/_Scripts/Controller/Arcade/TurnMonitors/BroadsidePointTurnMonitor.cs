using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Broadside. All the machinery is
    /// <see cref="CombatPointTurnMonitorBase"/>'s - the NetworkVariable sync, the publish to
    /// <c>GameDataSO.CombatPointTargetCount</c>, and the delegation of the end condition to the
    /// mode's own ScoringRule.
    ///
    /// The one thing this mode owns is WHICH target it races to, and unlike its siblings that is
    /// not a constant: a mixed-fleet point total where the price of a hit depends on the VERB
    /// that landed it (see <see cref="BroadsideScoringRuleSO"/>), scaled by how many pilots a
    /// side actually fields.
    ///
    /// <para><b>The target scales with TEAM SIZE and the reason is the latch.</b>
    /// <c>VesselCombatHitLatch</c> admits one hit per (shooter, victim, class) window, so two
    /// pilots working the same victim BOTH score - a second pilot roughly doubles a domain's
    /// rate. A fixed total would therefore make a 4v4 about a quarter the length of a 1v1. The
    /// target is <c>perPilot x (1 + 0.6 x (teamSize - 1))</c> (100 / 160 / 220 / 280), and the
    /// fraction is deliberately below 1 so a fuller side still finishes sooner - filling your
    /// team is a real advantage rather than a flat trade.</para>
    ///
    /// <para><b>Resolved on the SERVER, RECEIVED by clients.</b> The base class already
    /// replicates whatever this returns through one NetworkVariable, which is what makes a
    /// roster-derived number safe: a client that has not finished building its player list
    /// cannot compute a different target, because it never computes one (the same distinction
    /// <c>CrystalManager.CrystalCountMode.IntensityScaled</c> records). Team size comes from the
    /// live roster rather than from <c>SelectedPlayerCount</c> so an AI backfill, a kick or a
    /// mid-lobby drop is counted as it actually spawned.</para>
    /// </summary>
    public class BroadsidePointTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "BroadsidePointMonitor";

        protected override int ResolvePointTarget()
        {
            int teamSize = ResolveTeamSize();
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetBroadsidePointTarget(teamSize)
                : Mathf.RoundToInt(EndConditionOverridesSO.DefaultBroadsidePointsPerPilot *
                                   (1f + EndConditionOverridesSO.BroadsideExtraPilotFraction *
                                         (Mathf.Max(1, teamSize) - 1)));
        }

        /// <summary>
        /// Pilots per side: the roster divided by the number of domains that actually fielded
        /// someone, rounded. The MEAN rather than the max or the min because the target is one
        /// number every domain races to - a lopsided 2v1 would otherwise either hand the pair a
        /// free win or ask the lone pilot for a total they cannot reach. Falls back to 1, never
        /// 0: a target of 0 is already reached and would end the match on the countdown.
        /// </summary>
        int ResolveTeamSize()
        {
            if (gameData?.Players == null || gameData.Players.Count == 0) return 1;

            int pilots = 0;
            var seen = new bool[GameDataSO.ActiveDomains.Length];
            int domains = 0;

            for (int i = 0; i < gameData.Players.Count; i++)
            {
                var player = gameData.Players[i];
                if (player == null) continue;

                for (int d = 0; d < GameDataSO.ActiveDomains.Length; d++)
                {
                    if (player.Domain != GameDataSO.ActiveDomains[d]) continue;
                    pilots++;
                    if (!seen[d]) { seen[d] = true; domains++; }
                    break;
                }
            }

            if (pilots == 0 || domains == 0) return 1;
            return Mathf.Max(1, Mathf.RoundToInt(pilots / (float)domains));
        }
    }
}
