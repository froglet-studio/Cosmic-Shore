using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Salvo - the **Sparrow-only** demolition race, and Dog Fight's inverse in the same
    /// Boneyard: there the wreckage is cover and shooting it is worthless; here tearing it
    /// apart IS the score. Every domain races to destroy the hostile-prism target first
    /// (<see cref="ScoringMetric.PrismsDestroyed"/>, the Rampage/PeelTheCage metric - the
    /// destruction stat auto-increments via StatsManager's block-destroyed channel plus the
    /// environment-prism client RPC, so no per-event listener is needed here).
    ///
    /// Structural clone of <see cref="RampageController"/>: 1 round / 1 turn,
    /// server-authoritative winner detection in OnTurnEndedCustom, final scores replicated by
    /// snapshot ClientRpc, golf-timed (winners carry their finish time).
    ///
    /// <para><b>The loop: crystals are the missile economy - but they are no longer the ONLY
    /// refuel.</b> The Sparrow's guns are free but chip one prism at a time; the skyburst levels
    /// whole structures but costs half the missile tank (<c>SkyBurstGunAction.ammoCost 0.5</c>
    /// against a max of 1). The arena is stocked with crystals
    /// (<c>CrystalCountMode.PlayerCountPlusExtra</c> + 5, the Scurry abundance rather than
    /// Rampage's scarcity) and the match is a rhythm of crystal run → double salvo → crystal run.
    ///
    /// <para><b>⚠ CHANGED UNDER THIS MODE'S FEET (2026-09).</b> This doc used to say "the tank
    /// does not regenerate - the ONLY refuel is an omni crystal
    /// (<c>SparrowVesselChangeResourceByCrystalEffect</c>)". That asset is DELETED. The Sparrow's
    /// missiles now reload by DESTROYING HOSTILE PRISMS
    /// (<c>VesselRearmOnPrismDestruction</c>, 0.02 per prism = 25 prisms per rocket), and the omni
    /// crystal instead grants an 8-second elemental-debuff ward. This mode's premise is therefore
    /// softened rather than broken: a Sparrow tearing up the Boneyard is now self-funding, so the
    /// crystal line is an ACCELERANT rather than the sole tap, and the tension between "shoot the
    /// wreckage" and "run the crystals" is weaker than when this mode shipped. The wingman reload
    /// below is untouched and is still the reason to play it together. If the mode wants its
    /// original scarcity back, the lever is <c>ammoPerPrism</c> on the Sparrow (0 restores
    /// crystal-only refuelling exactly), not a change here.</para></para>
    ///
    /// <para><b>The reason to play it together: the WINGMAN RELOAD.</b> A collected omni
    /// crystal reloads the missile bays of EVERY pilot on the collector's domain, not just the
    /// collector (<see cref="HandleOmniCrystalCollected"/> →
    /// <see cref="RefuelDomainMissiles_ClientRpc"/>). One pilot can fly the crystal line while
    /// a wingman camps the densest wreckage and fires every reload the runner buys - a real
    /// division of labour on top of the domain-pooled score, not just parallel solo play.
    /// The collector's own machine is covered by the same RPC - the platform crystal effect that
    /// used to refill them as well is gone (see the ⚠ note above), so this RPC is now the ONLY
    /// thing a crystal does for a missile tank. Its set-to-full is idempotent, so it remains safe
    /// to arrive more than once.</para>
    ///
    /// <para>Ammo is deliberately LOCAL state: each machine simulates its own vessel's firing
    /// (projectiles are local objects - see DOGFIGHT.md "Multiplayer"), so a broadcast
    /// set-to-full on every peer is exactly as authoritative as the ammo system itself. The
    /// refuel signal originates server-side because omni collection resolves server-only
    /// (<c>OmniCrystalImpactor.AcceptImpactee</c> early-outs on network clients).</para>
    ///
    /// <para><b>The AI is the platform default, deliberately</b> - the same reasoning as
    /// Rampage: an AI pilot already seeks the nearest collectible cell item (here: the omni
    /// crystals, i.e. its own ammo line) and its Sparrow already fires FullAuto and SkyBurst
    /// on their own cooldowns at whatever the wreck field puts in front of it. Salvo is NOT in
    /// <c>ServerPlayerVesselInitializerWithAI</c>'s seek-players set - hunting pilots is Dog
    /// Fight's game, not this one's.</para>
    ///
    /// <para>Vessel restriction is NOT enforced here - it is the platform's clamp layers
    /// (<c>GameDataSO.SyncFromArcadeGame</c>, <c>ServerPlayerVesselInitializer
    /// .ResolveSpawnVesselType</c>, and the AI clamp), fed by the single entry in
    /// <c>ArcadeGameSalvo.Vessels</c>.</para>
    /// </summary>
    public class SalvoController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag SalvoScoringRule.asset - the per-mode scoring strategy (winner, scores, " +
                 "results). Metric = PrismsDestroyed, golf-timed like Rampage.")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Boneyard cell. Read-only to this controller: it supplies the arena centre " +
                 "the elemental-crystal scatter is measured from. The wreckage itself comes from " +
                 "the cell's config for the selected intensity (CellTypeChoiceOptions.IntensityWise).")]
        [SerializeField] Cell arenaCell;

        [Header("Wingman reload")]
        [Tooltip("Raised (server-side) by the omni crystal on every collection, carrying the " +
                 "collector's name. Wire EventOnCrystalCollected - the same channel the crystal " +
                 "prefab and StatsManager already share. Fail-loud: no null guard by policy.")]
        [SerializeField] ScriptableEventCrystalStats onOmniCrystalCollected;

        [Tooltip("Index of the Sparrow's missile tank in its ResourceSystem (Missiles = 0 on the " +
                 "shipped prefab - the same index SkyBurstGunAction spends). The refuel sets this " +
                 "resource to its own MaxAmount on every domain-mate.")]
        [SerializeField, Min(0)] int missileResourceIndex = 0;

        [Header("Elemental crystal pickups")]
        [Tooltip("How many ELEMENTAL crystals are scattered through the arena, on top of the " +
                 "omni crystals. Pure elemental progression (Mass stretches the Sparrow's fired " +
                 "prisms) - they do NOT refuel missiles; only omni crystals do. 0 disables.")]
        [SerializeField, Min(0)] int elementalCrystalCount = 14;

        [Tooltip("Radius of the shell the elemental crystals scatter through. Keep it inside the " +
                 "arena (the Boneyard is 520 at every intensity) so they land among the wreckage.")]
        [SerializeField, Min(1f)] float crystalScatterRadius = 400f;

        [Tooltip("Seed for the elemental scatter. Placement is DETERMINISTIC from this plus the " +
                 "count, so every peer lays the same crystals in the same places without a " +
                 "network message.")]
        [SerializeField] int crystalScatterSeed = 42;

        readonly System.Collections.Generic.List<Crystal> _spawnedCrystals = new();

        bool _finalResultsSent;

        // Golf: winners carry their finish time, losers a DnfThreshold+remaining sentinel
        // (see RampageScoringRuleSO.AssignScores) - lower is better, like SkimRace.
        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // End-game runs through OnTurnEndedCustom (server-side winner detection) →
        // SyncFinalScores_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
        // Suppress the base turn→round→game flow so there is no duplicate.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;

            // Every peer subscribes, but the raise only ever happens where collection resolves -
            // the server (OmniCrystalImpactor early-outs on network clients) - and the handler
            // re-guards on IsServer anyway, so a client subscription is inert by construction.
            onOmniCrystalCollected.OnRaised += HandleOmniCrystalCollected;

            // Belt-and-braces against the PeelTheCage regression where players started a match on a
            // non-zero score: RoundStats lives on the PERSISTENT Player object, and a stat that
            // survives a scene load is worth zeroing twice rather than never. The authoritative
            // reset is ServerPlayerVesselInitializer.PrepareForNewScene.
            if (IsServer) ZeroDestructionCounters();
        }

        public override void OnNetworkDespawn()
        {
            onOmniCrystalCollected.OnRaised -= HandleOmniCrystalCollected;
            ClearElementalCrystals();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Zeroes the scored stat on every roster entry. Server-only: the setter pushes through
        /// a server-write NetworkVariable and replication clears every client's mirror.
        /// </summary>
        void ZeroDestructionCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].HostilePrismsDestroyed = 0;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            // Elemental crystals are LOCAL objects laid identically on every peer from a fixed
            // seed, so this runs BEFORE the server gate - a client that never reached the lines
            // below would otherwise fly an arena with no pickups in it.
            ClearElementalCrystals();
            SpawnElementalCrystals();

            if (!IsServer) return;

            // The last moment before anyone can score - a late joiner is on the roster by now.
            ZeroDestructionCounters();

            base.OnCountdownTimerEnded();
        }

        // ── The wingman reload ───────────────────────────────────────────────

        /// <summary>
        /// Server-side: an omni crystal was collected. Resolve the collector's domain off the
        /// roster and broadcast the domain-wide missile reload. A blast-consumed crystal
        /// (Scarab forge) carries an empty name and refuels nobody - there is no pilot to
        /// credit a reload to.
        /// </summary>
        void HandleOmniCrystalCollected(CrystalStats stats)
        {
            if (!IsServer || _finalResultsSent) return;
            if (string.IsNullOrEmpty(stats.PlayerName)) return;

            if (!gameData.TryGetRoundStats(stats.PlayerName, out IRoundStats roundStats)) return;
            var domain = roundStats.Domain;
            if (domain == Domains.Blue) return;

            RefuelDomainMissiles_ClientRpc((int)domain);
        }

        /// <summary>
        /// Sets the missile tank full on every vessel of the given domain, on every peer.
        /// Ammo is local state (each machine simulates its own vessel's firing), so the
        /// broadcast IS the mechanism, not a mirror of one: the write that matters is the one
        /// that lands on each vessel's OWNER machine, and the same write on its replicas is a
        /// harmless idempotent set. Runs on the host too - Netcode ClientRpcs execute on the
        /// host's client half, which covers the server's own pilot and every AI.
        /// </summary>
        [ClientRpc]
        void RefuelDomainMissiles_ClientRpc(int domain)
        {
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p.Domain != (Domains)domain) continue;

                var resources = p.Vessel?.VesselStatus?.ResourceSystem;
                if (resources == null) continue;
                if (missileResourceIndex >= resources.Resources.Count) continue;

                resources.SetResourceAmount(
                    missileResourceIndex, resources.Resources[missileResourceIndex].MaxAmount);
            }
        }

        // ── Elemental crystal pickups (same recipe as Dog Fight) ─────────────

        /// <summary>
        /// Scatters ELEMENTAL crystals through the arena, alongside the scene's omni crystals -
        /// two different pickups: omni = the missile economy (and the wingman reload), elemental
        /// = element levels (Mass stretches the Sparrow's fired prisms). Deterministic from the
        /// seed so every peer lays the same crystals with no network message; collection is
        /// per-peer, tolerable only because these score nothing (DOGFIGHT.md's standing caveat).
        /// Runtime provisioning mirrors Microscene.MintElementalCrystal - the standalone
        /// elemental prefabs deliberately carry no collection components.
        /// </summary>
        void SpawnElementalCrystals()
        {
            if (elementalCrystalCount <= 0) return;

            var set = ElementalCrystalSetSO.Load();
            if (set == null) return;

            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;
            var rng = new System.Random(crystalScatterSeed);

            for (int i = 0; i < elementalCrystalCount; i++)
            {
                var element = ElementalCrystalSetSO.RandomElementFrom(rng);
                var prefab = set.GetPrefab(element);
                if (prefab == null) continue;

                // Equal-volume scatter through the shell (cube root of a uniform draw).
                double u = rng.NextDouble();
                float r = crystalScatterRadius * Mathf.Pow((float)u, 1f / 3f);
                float theta = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float y = (float)(rng.NextDouble() * 2.0 - 1.0);
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var pos = centre + new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta)) * r;

                var crystal = Instantiate(prefab, pos, Quaternion.identity);
                crystal.transform.localScale *= (float)(rng.NextDouble() * 0.7 + 0.5);
                crystal.enabled = true;
                crystal.gameObject.SetActive(true);

                var impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
                impactor.Crystal = crystal;
                if (set.CollectionEffects is { Length: > 0 })
                    impactor.SetCollectionEffects(set.CollectionEffects);
                crystal.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);

                _spawnedCrystals.Add(crystal);
            }
        }

        void ClearElementalCrystals()
        {
            for (int i = 0; i < _spawnedCrystals.Count; i++)
                if (_spawnedCrystals[i]) Destroy(_spawnedCrystals[i].gameObject);
            _spawnedCrystals.Clear();
        }

        // ── Server-authoritative game end (the Rampage shape) ────────────────

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;
            if (rule == null) return;

            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            var winnerRep = gameData.RoundStatsList
                .Where(s => s != null && s.Domain == winningDomain)
                .OrderByDescending(s => s.HostilePrismsDestroyed)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the race just ended - prevents the Ready
        /// button from reappearing (HasEndGame=false routes round end back through here).
        /// </summary>
        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        // ── Score sync ───────────────────────────────────────────────────────

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
                    CSDebug.LogError($"[Salvo] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.HostilePrismsDestroyed = prismsDestroyed[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // ── Replay ───────────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            ClearElementalCrystals();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.HostilePrismsDestroyed = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
