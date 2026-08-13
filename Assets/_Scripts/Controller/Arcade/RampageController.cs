using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rampage - the **Dolphin-only** demolition race, and the destructive analog of Crystal
    /// Capture ("Scurry"): every domain races to destroy the prism target first (hostile
    /// prisms only - another domain's mass; shattering your own trail is worthless).
    /// Structural clone of <see cref="MultiplayerCrystalCaptureController"/>: 1 round / 1 turn,
    /// server-authoritative winner detection in OnTurnEndedCustom, final scores replicated by
    /// snapshot ClientRpc. The destruction stat itself auto-increments via
    /// StatsManager.PrismDestroyed (SOAP block-destroyed channel), so no per-event listener is
    /// needed here.
    ///
    /// <para><b>Why the Dolphin, and why ONE crystal.</b> The Dolphin's damage verb is not its
    /// hull, it is the conic jaw blast - and that blast is armed by SKIMMING (150 skims fills
    /// the Energy meter) and fired by touching a CRYSTAL, which spends the whole meter in one
    /// shot (<c>DOLPHIN_ENERGY_ECONOMY.md</c> §1). So the vessel already contains the loop this
    /// mode wants: graze the forest to charge, then cash the charge on the one crystal in the
    /// arena. The arena carries exactly one (<c>fixedCrystalCount: 1</c>), which turns the
    /// vessel's private economy into a contested object - including the denial play of taking
    /// it empty to move it away from a fully-charged rival. Nothing here scripts that; it falls
    /// out of the crystal being singular and the meter being spent on contact.</para>
    ///
    /// <para>Vessel restriction is NOT enforced here - it is the platform's two-place clamp
    /// (<c>GameDataSO.SyncFromArcadeGame</c> for the machine that pressed Start,
    /// <c>ServerPlayerVesselInitializer.ResolveSpawnVesselType</c> server-side at spawn), fed by
    /// the single entry in <c>ArcadeGameRampage.Vessels</c>. See
    /// <see cref="DogFightController"/> / RIBCAGE.md for why the server clamp is the one that
    /// matters in multiplayer.</para>
    /// </summary>
    public class RampageController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag RampageScoringRule.asset - the per-mode scoring strategy (winner, scores, results).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("AI")]
        [Tooltip("The arena cell whose density grids the AI hunts while it is banking skim " +
                 "energy - the densest region of mass HOSTILE to its domain (the same query " +
                 "aggression-1 fauna use).")]
        [SerializeField] Cell arenaCell;
        [Tooltip("Seconds between AI target refreshes. FindDensestRegion runs a Burst job, " +
                 "so pilots sample on this cadence and fly at the cached point between samples.")]
        [SerializeField, Min(0.25f)] float aiRetargetSeconds = 1.5f;
        [Tooltip("Normalized Dolphin Energy (resource slot 0) at which an AI stops grazing the " +
                 "forest and breaks for the crystal. Below it the blast is too narrow to be " +
                 "worth spending the meter on; at 1 the AI would never fire.")]
        [SerializeField, Range(0.05f, 1f)] float aiCrystalRunEnergy = 0.6f;

        // Dolphin resource slot 0 = Energy (slot 1 = Boost). See DOLPHIN_ENERGY_ECONOMY.md §1.
        const int DolphinEnergyResourceIndex = 0;

        private bool _finalResultsSent;

        // Golf: winners carry their finish time, losers a DnfThreshold+remaining sentinel
        // (see RampageScoringRuleSO.AssignScores) - lower is better, like HexRace.
        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // Rampage handles end-game through OnTurnEndedCustom (server-side winner detection) →
        // SyncFinalScores_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
        // Suppress the base controller's turn→round→game flow so we don't get a duplicate
        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
        }

        // ── AI: graze to charge, then run the crystal (server) ────────────

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
            ArmDolphinHunters();
        }

        /// <summary>
        /// Gives every AI pilot the same two-phase loop the mode asks of a human Dolphin:
        ///
        /// <list type="bullet">
        ///   <item><b>Charging</b> (Energy &lt; <see cref="aiCrystalRunEnergy"/>): fly at the
        ///   densest region of mass HOSTILE to its domain - <see cref="Cell.GetExplosionTarget"/>,
        ///   the same density-grid query aggression-1 fauna use (no physics queries, no parallel
        ///   spatial store - see Docs/SPATIAL_INDEX.md). Grazing that cluster banks skim energy,
        ///   and the prisms it clips on the way through score outright.</item>
        ///   <item><b>Cashing</b> (Energy at or above the threshold): break for the crystal and
        ///   spend the meter as a wide jaw blast.</item>
        /// </list>
        ///
        /// A single-phase mass hunter is what shipped when this mode was Rhino-flavoured, and it
        /// is actively wrong for a Dolphin: the AI would bank a full meter and never fire it,
        /// because nothing but a crystal discharges the blast. Equally, the AIPilot DEFAULT
        /// (crystal seeking with no external provider) is wrong on its own - the AI would sprint
        /// to every crystal at zero charge, dumping an empty meter on arrival and never lighting
        /// up the forest. The race lives in the alternation, so the AI has to run it too.
        ///
        /// The energy read is per-frame and free (a float off the pilot's own ResourceSystem);
        /// only the Burst density query is sampled on <see cref="aiRetargetSeconds"/>.
        /// Mirrors Astro League's ArmStrikers pattern.
        /// </summary>
        void ArmDolphinHunters()
        {
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                Vector3 cached = captured.Vessel.Transform.position;
                float nextSample = 0f;
                pilot.SetExternalTargetProvider(() =>
                {
                    if (IsChargedForBlast(captured) && TryGetContestedCrystal(out var crystalPos))
                        return crystalPos;

                    if (arenaCell != null && Time.time >= nextSample)
                    {
                        nextSample = Time.time + aiRetargetSeconds;
                        cached = arenaCell.GetExplosionTarget(captured.Domain);
                    }
                    return cached;
                });
            }
        }

        /// <summary>
        /// True once this AI has banked enough Energy that spending it on the crystal is worth
        /// the trip. Reads the live resource rather than counting skims, so it stays correct
        /// through every drain the economy applies (a ram halves the meter, a crystal empties it).
        /// </summary>
        bool IsChargedForBlast(IPlayer player)
        {
            var resources = player.Vessel?.VesselStatus?.ResourceSystem?.Resources;
            if (resources == null || resources.Count <= DolphinEnergyResourceIndex) return false;

            var energy = resources[DolphinEnergyResourceIndex];
            if (energy == null || energy.MaxAmount <= 0f) return false;

            return energy.CurrentAmount / energy.MaxAmount >= aiCrystalRunEnergy;
        }

        /// <summary>
        /// The arena's contested crystal. Iterates the in-memory <see cref="Crystal.Active"/>
        /// registry (never a FindObjectsByType scene scan) and skips one already exploding, so
        /// a pilot doesn't keep steering at a prize another Dolphin just took. No domain filter:
        /// Rampage spawns its single crystal neutral (<c>spawnCrystalWithPlayerDomain: 0</c>) and
        /// every pilot may collect it - that is the whole contest.
        /// </summary>
        bool TryGetContestedCrystal(out Vector3 position)
        {
            position = default;

            var crystals = Crystal.Active;
            for (int i = 0; i < crystals.Count; i++)
            {
                var crystal = crystals[i];
                if (crystal == null || crystal.IsExploding) continue;

                position = crystal.transform.position;
                return true;
            }
            return false;
        }

        // ── Server-authoritative game end ─────────────────────────────────

        /// <summary>
        /// Server-side winner detection, mirroring the Crystal Capture pattern.
        /// Called from SyncTurnEnd_ClientRpc BEFORE ExecuteServerTurnEnd → SetupNewRound,
        /// so _finalResultsSent is set in time to suppress the Ready button.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;

            // Winning domain (highest destruction sum, Jade→Ruby→Gold tie-break) delegated to
            // the rule; representative winner-name = best individual contributor on that domain
            // (legacy display field - victory/defeat attribution uses WinnerDomain).
            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            var winnerRep = gameData.RoundStatsList
                .Where(s => s.Domain == winningDomain)
                .OrderByDescending(s => s.HostilePrismsDestroyed)
                .FirstOrDefault();
            if (winnerRep == null) return;

            // Winners score the match time (Time.time - TurnStartTime, the server's turn
            // clock); losers the remaining-prisms sentinel. The rule owns the encoding;
            // the snapshot RPC replicates the final Scores so clients rebuild identically.
            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the game just ended.
        /// HasEndGame=false causes ExecuteServerRoundEnd to call SetupNewRound instead of
        /// ExecuteServerGameEnd - this override prevents the Ready button from appearing.
        /// </summary>
        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        // ── Score sync ───────────────────────────────────────────────────

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var prismsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                prismsArray[i] = statsList[i].HostilePrismsDestroyed;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, prismsArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] prismsDestroyed,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Rampage] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.HostilePrismsDestroyed = prismsDestroyed[i];
            }

            // Authoritative winner - written to gameData, consumed by EndGameControllers
            // OnWinnerCalculated (below) is the "results ready" signal.
            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // ── Replay ───────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;

            foreach (var s in gameData.RoundStatsList)
            {
                s.HostilePrismsDestroyed = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
