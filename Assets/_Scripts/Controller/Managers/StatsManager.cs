using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;


namespace CosmicShore.Gameplay
{
    [System.Serializable]
    public struct CellStats
    {
        public int LifeFormsInCell;
        public int LiveBlockCount;
        public CellPhase Phase;
        public Domains DominantDomain;
    }

    [Serializable]
    public struct CrystalStats
    {
        public string PlayerName;
        public Element Element;
        public float Value;
    }

    [Serializable]
    public struct PrismStats
    {
        public string OwnName;
        public float Volume;
        public string AttackerName;

        /// <summary>
        /// The DOMAIN the destroyed prism was wearing. Needed because ENVIRONMENT mass (flora,
        /// fauna bodies, laid cell structure) has no roster entry, so the owner-name comparison
        /// that classifies a trail says nothing about it - without this, every prism in the world
        /// counted as hostile to every player including the one whose own colour it wore.
        /// <see cref="Domains.Blue"/> is the neutral sentinel and stays hostile to everyone.
        /// </summary>
        public Domains OwnDomain;
    }

    /// <summary>
    /// One landed vessel-vs-vessel hit: who fired it, who wore it, and with what.
    /// <see cref="VictimName"/> is carried for diagnostics and for the toast feed - the score
    /// only ever moves for the SHOOTER, and only <see cref="StatsManager"/> decides that.
    /// </summary>
    [Serializable]
    public struct CombatHitStats
    {
        public string ShooterName;
        public string VictimName;
        public CombatHitClass HitClass;
    }

    [Serializable]
    public struct AbilityStats
    {
        public string PlayerName;
        public InputEvents ControlType;
        public float Duration;
    }

    public class StatsManager : MonoBehaviour
    {
        [SerializeField]
        GameDataSO gameData;

        [SerializeField]
        CellRuntimeDataSO cellData;

        [SerializeField]
        NetcodeHooks _netcodeHooks;

        bool _allowRecord = true;

        void OnEnable()
        {
            if (_netcodeHooks != null)
                _netcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;

            // Subscribed in code rather than through a scene-wired EventListener, like
            // SceneLoader's SOAP subscriptions: this manager already holds the cell runtime
            // SO that owns the channel, so there is nothing per-scene to forget.
            if (cellData != null)
                cellData.OnFaunaKilled.OnRaised += LifeformKilled;

            // Same reasoning, one level up: the combat-hit channel hangs off GameDataSO (the
            // one asset every scene's StatsManager already carries) rather than a serialized
            // field here, so gunnery records in EVERY scene with nothing per-scene to wire.
            // No null guard on the CHANNEL itself, per the SOAP fail-loud policy: an unwired
            // Event_CombatHitStats must throw here rather than silently un-score every mode.
            if (gameData != null)
                gameData.OnCombatHitLanded.OnRaised += CombatHitLanded;
        }

        void OnDisable()
        {
            if (_netcodeHooks != null)
                _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;

            if (cellData != null)
                cellData.OnFaunaKilled.OnRaised -= LifeformKilled;

            if (gameData != null)
                gameData.OnCombatHitLanded.OnRaised -= CombatHitLanded;
        }

        void OnNetworkSpawn()
        {
            if (_netcodeHooks.IsServer)
            {
                _allowRecord = true;
                return;
            }

            _allowRecord = false;
        }

        public void LifeformCreated(int cellID)
        {
            if (!_allowRecord || cellData == null) return;

            var cellStatsList = cellData.CellStatsList;

            if (!cellStatsList.ContainsKey(cellID))
                cellStatsList[cellID] = new CellStats();

            var cs = cellStatsList[cellID];
            cs.LifeFormsInCell++;
            cellStatsList[cellID] = cs;
        }

        public void LifeformDestroyed(int cellID)
        {
            if (!_allowRecord || cellData == null) return;

            var cellStatsList = cellData.CellStatsList;

            if (!cellStatsList.ContainsKey(cellID))
                cellStatsList[cellID] = new CellStats();

            var cs = cellStatsList[cellID];
            cs.LifeFormsInCell--;
            cellStatsList[cellID] = cs;
        }

        /// <summary>
        /// A fauna died to an attributed force - credit the killer. Raised on
        /// <see cref="CellRuntimeDataSO.OnFaunaKilled"/> by the sealed <c>Fauna.Die</c>, which
        /// only publishes PLAYER-attributed deaths (starvation and predation carry engine
        /// attribution and are filtered there). The roster lookup is the second filter and the
        /// authoritative one: a name that is not a player in this game simply credits nobody,
        /// so an AI-vs-AI kill, a worm devouring a tadpole, or a stray attribution string can
        /// never move a scoreboard.
        ///
        /// UNLIKE every other writer here this one has a CLIENT branch, and it has to. Every
        /// other stat originates from something that exists identically on every peer - a
        /// prism sits at the same place on the server, so the server's own physics sees a
        /// client's ram and records it. Fauna do not: they have no NetworkObject and every peer
        /// simulates its own swarm (Docs/ECOSYSTEM.md §7 caveat 4), so a creature a client just
        /// shot may not exist on the server at all. Recording server-only would mean only the
        /// host could ever score a kill.
        ///
        /// So a client forwards its OWN kill through its OWN Player object
        /// (<see cref="Player.ReportFaunaKill_ServerRpc"/>), the same owner-detects →
        /// server-records round-trip <c>NetworkVesselImpactor</c> uses for jousts. Identity
        /// comes from RPC ownership, so a client cannot credit anyone but itself.
        /// </summary>
        public void LifeformKilled(string killerName)
        {
            if (string.IsNullOrEmpty(killerName)) return;

            if (_allowRecord)
            {
                // Server: credit directly. Covers the host's own kills and every AI's (AI
                // players are server-owned, so their creatures ARE the server's simulation).
                if (gameData.TryGetRoundStats(killerName, out IRoundStats killerStats))
                    killerStats.LifeformsKilled++;
                return;
            }

            // Client: forward only this machine's own kill, and only through the Player it owns.
            var local = gameData.LocalPlayer;
            if (local is Player netPlayer && local.IsLocalUser && local.Name == killerName)
                netPlayer.ReportFaunaKill_ServerRpc();
        }

        /// <summary>
        /// A vessel landed a shot on an opposing vessel - credit the SHOOTER. Raised on
        /// <see cref="GameDataSO.OnCombatHitLanded"/> by the two combat-hit impact effects,
        /// already deduplicated per (shooter, victim, class) by <c>VesselCombatHitLatch</c>.
        ///
        /// LIKE the fauna path and UNLIKE every prism stat, this one has a CLIENT branch, and
        /// for the same underlying reason: <b>projectiles are not networked</b>. A bullet or a
        /// skyburst is a pooled local object spawned by whichever machine's gun fired it - it
        /// has no NetworkObject and no RPCs - so a shot a client just landed does not exist on
        /// the server at all. Recorded server-only, only the host could ever score in a
        /// dogfight.
        ///
        /// So the machine that SIMULATED the shot reports it. Ownership decides who that is:
        /// the server records directly (covering the host's own guns and every AI's, since AI
        /// players are server-owned), and a client forwards ONLY its own shot through the
        /// Player object it owns. If an AI's gun happens to fire on a client too, that client
        /// sees the name mismatch and drops it - the server's copy is the one that counts.
        ///
        /// IDENTITY COMES FROM RPC OWNERSHIP, NOT FROM THE NAME STRING: the server credits the
        /// RoundStats of the Player object <see cref="Player.ReportCombatHit_ServerRpc"/>
        /// arrived on, so a client can only ever credit itself.
        /// </summary>
        public void CombatHitLanded(CombatHitStats hit)
        {
            if (string.IsNullOrEmpty(hit.ShooterName)) return;

            if (_allowRecord)
            {
                if (gameData.TryGetRoundStats(hit.ShooterName, out IRoundStats shooterStats))
                    CombatHitScoring.Credit(shooterStats, hit.HitClass, gameData.ScoringRule);
                return;
            }

            var local = gameData.LocalPlayer;
            if (local is Player netPlayer && local.IsLocalUser && local.Name == hit.ShooterName)
                netPlayer.ReportCombatHit_ServerRpc((int)hit.HitClass);
        }

        public void CrystalCollected(CrystalStats crystalStats)
        {
            if (!_allowRecord) return;

            var playerName = crystalStats.PlayerName;
            if (!gameData.TryGetRoundStats(playerName, out IRoundStats stats))
                return;
            stats.CrystalsCollected++;

            switch (crystalStats.Element)
            {
                case Element.Omni:
                    UpdateStatForPlayer(playerName, s =>
                    {
                        s.OmniCrystalsCollected++;
                    });
                    break;

                case Element.Charge:
                    UpdateStatForPlayer(playerName, s =>
                    {
                        s.ElementalCrystalsCollected++;
                        s.ChargeCrystalValue += crystalStats.Value;
                    });
                    break;

                case Element.Mass:
                    UpdateStatForPlayer(playerName, s =>
                    {
                        s.ElementalCrystalsCollected++;
                        s.MassCrystalValue += crystalStats.Value;
                    });
                    break;

                case Element.Space:
                    UpdateStatForPlayer(playerName, s =>
                    {
                        s.ElementalCrystalsCollected++;
                        s.SpaceCrystalValue += crystalStats.Value;
                    });
                    break;

                case Element.Time:
                    UpdateStatForPlayer(playerName, s =>
                    {
                        s.ElementalCrystalsCollected++;
                        s.TimeCrystalValue += crystalStats.Value;
                    });
                    break;
                case Element.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void ExecuteSkimmerShipCollision(string skimmerPlayerName)
        {
            if (!_allowRecord) return;

            if (!gameData.TryGetRoundStats(skimmerPlayerName, out var roundStats))
                return;
            roundStats.SkimmerShipCollisions++;
        }

        public void ExecuteJoustCollision(string joustPlayerName)
        {
            if (!_allowRecord) return;

            if (!gameData.TryGetRoundStats(joustPlayerName, out var roundStats))
            {
                CSDebug.LogWarning($"[StatsManager] ExecuteJoustCollision: no RoundStats for '{joustPlayerName}'. " +
                    $"Available: [{string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}]");
                return;
            }

            roundStats.JoustCollisions++;
            CSDebug.Log($"[StatsManager] JoustCollision recorded for '{joustPlayerName}': {roundStats.JoustCollisions}");
        }

        public void PrismCreated(PrismStats prismStats)
        {
            if (!_allowRecord) return;

            if (!gameData.TryGetRoundStats(prismStats.OwnName, out var roundStats))
                return;

            roundStats.BlocksCreated++;
            roundStats.PrismsRemaining++;
            roundStats.VolumeCreated += prismStats.Volume;
            roundStats.VolumeRemaining += prismStats.Volume;
        }

        /// <summary>
        /// Is a destroyed ENVIRONMENT prism (one with no roster entry - flora, fauna bodies, laid
        /// cell structure) friendly to the player who destroyed it?
        ///
        /// Friendly iff it wore that player's own colour. <see cref="Domains.Blue"/> is the
        /// platform's "no team" sentinel, so neutral mass is hostile to everyone and always
        /// scores. This is the same rule trails already followed ("your own team's mass is worth
        /// nothing"), applied to the rest of the world: before it, every prism in the arena
        /// counted as hostile to every player, including the third of a forest wearing their own
        /// colour, which made domain irrelevant to target choice.
        /// </summary>
        public static bool IsFriendlyEnvironmentPrism(Domains attackerDomain, Domains prismDomain) =>
            prismDomain != Domains.Blue && prismDomain == attackerDomain;

        public void PrismDestroyed(PrismStats prismStats)
        {
            var attackingPlayerName = prismStats.AttackerName;
            var victimPlayerName = prismStats.OwnName;

            var hasVictim = gameData.TryGetRoundStats(victimPlayerName, out IRoundStats victimPlayerStats);

            if (!_allowRecord)
            {
                // CLIENT. Only ENVIRONMENT mass is forwarded - see ForwardEnvironmentKill.
                if (!hasVictim) ForwardEnvironmentKill(prismStats);
                return;
            }

            var hasAttacker = gameData.TryGetRoundStats(attackingPlayerName, out IRoundStats attackerPlayerStats);

            if (hasAttacker && (hasVictim || OwnsAttacker(attackingPlayerName)))
            {
                CreditPrismDestruction(
                    attackerPlayerStats, prismStats.Volume,
                    isFriendly: attackingPlayerName == victimPlayerName
                                || (hasVictim && attackerPlayerStats.Domain == victimPlayerStats.Domain)
                                || (!hasVictim && IsFriendlyEnvironmentPrism(
                                        attackerPlayerStats.Domain, prismStats.OwnDomain)));
            }

            if (!hasVictim) return;
            victimPlayerStats.PrismsRemaining--;
            victimPlayerStats.VolumeRemaining -= prismStats.Volume;
        }

        /// <summary>
        /// The one place a prism kill moves an attacker's counters, shared by the server's direct
        /// path and the client round-trip (<see cref="Player.ReportEnvironmentPrismDestroyed_ServerRpc"/>)
        /// so the two can never drift apart.
        /// </summary>
        internal static void CreditPrismDestruction(IRoundStats attacker, float volume, bool isFriendly)
        {
            attacker.BlocksDestroyed++;
            attacker.TotalVolumeDestroyed += volume;

            if (isFriendly)
            {
                attacker.FriendlyPrismsDestroyed++;
                attacker.FriendlyVolumeDestroyed += volume;
            }
            else
            {
                attacker.HostilePrismsDestroyed++;
                attacker.HostileVolumeDestroyed += volume;
            }
        }

        /// <summary>
        /// Does THIS machine simulate the named player? On the server that is the host's own
        /// player plus every AI (both server-owned NetworkObjects); a remote client's player is
        /// simulated on that client.
        ///
        /// Used to decide who credits ENVIRONMENT mass. A rostered prism (a trail) exists at the
        /// same place on every peer, so the server's own physics observes a client's ram and
        /// records it - that path is unchanged. Environment mass does NOT: flora and fauna are
        /// spawned per-peer from local Random rolls and the populations diverge (see
        /// <c>CellNetworkSync</c>'s class doc, Docs/ECOSYSTEM.md §7 caveat 4), so the server's
        /// copy of a cactus is somewhere else entirely. If the server credited environment kills
        /// it saw a remote player make, a client would score for destroying trees it never flew
        /// near while the ones it actually shredded scored nothing - and it would double-count
        /// against the client's own report. So each machine credits only the players it simulates.
        /// </summary>
        bool OwnsAttacker(string attackerName)
        {
            var players = gameData.Players;
            if (players == null) return true;

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p.Name != attackerName) continue;
                return p is not Player netPlayer || !netPlayer.IsSpawned || netPlayer.IsOwner;
            }

            // Unknown attacker (environment-on-environment, a lifeform husk): nobody else will
            // report it, so the server is the right place to account for it.
            return true;
        }

        /// <summary>
        /// CLIENT: forward this machine's OWN environment-mass kill to the server, the same
        /// owner-detects → server-records round-trip <see cref="LifeformKilled"/> and
        /// <see cref="CombatHitLanded"/> use, and for the same underlying reason - the thing that
        /// was destroyed does not exist on the server at the position it was destroyed at.
        ///
        /// Only the local player's own kills are forwarded: every peer simulates every vessel, so
        /// without the name check each client would report the same kill for everyone it can see.
        /// Identity still comes from RPC ownership, not the name - the server credits the Player
        /// object the RPC arrived on.
        /// </summary>
        void ForwardEnvironmentKill(PrismStats prismStats)
        {
            if (string.IsNullOrEmpty(prismStats.AttackerName)) return;

            var local = gameData.LocalPlayer;
            if (local is Player netPlayer && local.IsLocalUser && local.Name == prismStats.AttackerName)
                netPlayer.ReportEnvironmentPrismDestroyed_ServerRpc(prismStats.Volume, (int)prismStats.OwnDomain);
        }

        public void PrismRestored(PrismStats prismStats)
        {
            if (!_allowRecord) return;

            var restoringPlayerName = prismStats.OwnName;

            if (!gameData.TryGetRoundStats(restoringPlayerName, out IRoundStats roundStats))
                return;

            roundStats.BlocksRestored++;
            roundStats.PrismsRemaining++;
            roundStats.VolumeRestored += prismStats.Volume;
            roundStats.VolumeRemaining += prismStats.Volume;
        }

        public void PrismVolumeModified(PrismStats prismStats)
        {
            if (!_allowRecord) return;

            var ownerPlayerName = prismStats.OwnName;

            if (!gameData.TryGetRoundStats(ownerPlayerName, out IRoundStats roundStats))
                return;

            roundStats.VolumeCreated += prismStats.Volume;
            roundStats.VolumeRemaining += prismStats.Volume;
        }

        public void PrismStolen(PrismStats prismStats)
        {
            if (!_allowRecord) return;

            var stealingPlayerName = prismStats.OwnName;
            if (!gameData.TryGetRoundStats(stealingPlayerName, out IRoundStats stealingPlayerStats))
                return;

            stealingPlayerStats.PrismStolen++;
            stealingPlayerStats.PrismsRemaining++;
            stealingPlayerStats.VolumeStolen += prismStats.Volume;
            stealingPlayerStats.VolumeRemaining += prismStats.Volume;

            var victimPlayerName = prismStats.AttackerName;
            if (!gameData.TryGetRoundStats(victimPlayerName, out IRoundStats victimPlayerStats))
                return;

            victimPlayerStats.PrismsRemaining--;
            victimPlayerStats.VolumeRemaining -= prismStats.Volume;
        }

        public void RegisterAbilityExecuted(AbilityStats abilityStats)
        {
            if (!_allowRecord) return;

            if (!gameData.TryGetRoundStats(abilityStats.PlayerName, out IRoundStats playerStats))
                return;

            switch (abilityStats.ControlType)
            {
                case InputEvents.FullSpeedStraightAction:
                    playerStats.FullSpeedStraightAbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.RightStickAction:
                    playerStats.RightStickAbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.LeftStickAction:
                    playerStats.LeftStickAbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.FlipAction:
                    playerStats.FlipAbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.Button1Action:
                    playerStats.Button1AbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.Button2Action:
                    playerStats.Button2AbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.Button3Action:
                    playerStats.Button3AbilityActiveTime += abilityStats.Duration;
                    break;
            }
        }

        void UpdateStatForPlayer(string playerName, Action<IRoundStats> updateAction)
        {
            if (!gameData.TryGetRoundStats(playerName, out var roundStats))
                return;

            updateAction(roundStats);
        }
    }
}
