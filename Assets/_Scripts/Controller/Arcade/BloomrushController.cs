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
    /// Bloomrush — the **Manta-only** party game, and the vessel's accessibility thesis as a
    /// mode: nobody has to learn a button. The whole kit fires off flying, touching and
    /// picking things up — skim the cactus reef to arm bombs, graze wildlife and rival Mantas
    /// to plant them (silently; one bomb per target, so tagging is DENIAL), then reach a
    /// crystal before the fuses burn down and Kabloom the whole board at once.
    ///
    /// <b>120-second timed round</b> (the scene's <c>NetworkTimeBasedTurnMonitor</c> is the
    /// only end condition), scored on <see cref="ScoringMetric.VolumeDestroyed"/> — hostile
    /// prism VOLUME, domain-summed — with FUSES BEATEN as the tiebreaker
    /// (<see cref="BloomrushScoringRuleSO"/>). "Beat the fuse" needs no scoring special case:
    /// a crystal-cashed bloom is authored bigger than a fuse fizzle, so cashing in pays more
    /// volume by construction.
    ///
    /// <b>The arena is Rampage's cactus forest, reused verbatim</b> (referenced, never
    /// forked — the Bends precedent: same vessel economy wants the same place). Its intensity
    /// ladder is already Bloomrush's spec: the reef is identical at all four levels while the
    /// crystals thin and the wildlife climbs. The one Bloomrush-owned intensity dial is the
    /// FUSE — 30/25/20/20 seconds by intensity, pushed through
    /// <see cref="MantaBombRules.FuseSecondsOverride"/> on every machine at countdown end
    /// (after the config sync gate, so a client can never plant on intensity-1 fuses in an
    /// intensity-4 match).
    ///
    /// <b>TEAM mode is the co-op hook</b>: bombs are per-pilot but the score pools per domain,
    /// and any teammate's crystal cashes their own board — a weaker player contributes by
    /// tagging while a stronger one runs crystals. A per-player winner is deliberately not
    /// offered (four seats, three domains — the Wildlife Liberation revert).
    ///
    /// Structural clone of <see cref="SalvoController"/>: 1 round / 1 turn, server-
    /// authoritative winner in OnTurnEndedCustom at time-out, final scores by snapshot
    /// ClientRpc. Points mode (no golf): everyone carries their own bloomed volume.
    /// </summary>
    public class BloomrushController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag BloomrushScoringRule.asset — metric VolumeDestroyed, fuses-beaten " +
                 "tiebreak, timed (IsObjectiveReached always false).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The cactus-forest cell (Rampage's, reused). Supplies the centre the " +
                 "elemental-crystal scatter is measured from.")]
        [SerializeField] Cell arenaCell;

        [Header("Fuse ladder (seconds, by intensity 1-4)")]
        [Tooltip("Fuse length pushed onto every Manta's bomb bay per intensity. The spec's " +
                 "ladder: 30 / 25 / 20 / 20.")]
        [SerializeField] float[] fuseSecondsByIntensity = { 30f, 25f, 20f, 20f };

        [Header("Elemental crystal pickups")]
        [Tooltip("Elemental crystals scattered through the reef — pure element progression " +
                 "(Charge grows the bay, Space the blooms, Time the soar, Mass the trail). " +
                 "They do NOT trigger Kabloom; only omni crystals do. 0 disables.")]
        [SerializeField, Min(0)] int elementalCrystalCount = 16;

        [Tooltip("Radius of the scatter shell. Keep it inside the cactus belt (the Rampage " +
                 "forest plants at ~0.76-0.94 of the membrane) so they land among the reefs.")]
        [SerializeField, Min(1f)] float crystalScatterRadius = 850f;

        [Tooltip("Deterministic scatter seed — every peer lays the same crystals.")]
        [SerializeField] int crystalScatterSeed = 45;

        readonly System.Collections.Generic.List<Crystal> _spawnedCrystals = new();

        bool _finalResultsSent;

        // Points mode: highest volume wins. (Every earlier sibling is golf-timed; Bloomrush is
        // the family's first timed highest-score round.)
        protected override bool UseGolfRules => false;
        protected override bool UseSceneReloadForReplay => true;

        // End-game runs through OnTurnEndedCustom → SyncFinalScores_ClientRpc; suppress the
        // base turn→round→game duplicate.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;

            if (IsServer) ZeroBloomCounters();
        }

        public override void OnNetworkDespawn()
        {
            MantaBombRules.FuseSecondsOverride = null;
            ClearElementalCrystals();
            base.OnNetworkDespawn();
        }

        void ZeroBloomCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].HostileVolumeDestroyed = 0f;
                list[i].HostilePrismsDestroyed = 0;
                list[i].FusesBeaten = 0;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            // Peer-local work BEFORE the server gate (the Salvo shape): the fuse ladder and
            // the deterministic crystal scatter must land on every machine — bombs are local
            // objects, and a client that never reached these lines would plant default fuses
            // in an intensity-4 match. SelectedIntensity is safe to read here: the countdown
            // only starts after the config ClientRpc has set it.
            int intensity = Mathf.Clamp(gameData.SelectedIntensity, 1,
                Mathf.Max(1, fuseSecondsByIntensity.Length));
            if (fuseSecondsByIntensity.Length > 0)
                MantaBombRules.FuseSecondsOverride = fuseSecondsByIntensity[intensity - 1];

            ClearElementalCrystals();
            SpawnElementalCrystals();

            if (!IsServer) return;

            // The last moment before anyone can score — a late joiner is on the roster by now.
            ZeroBloomCounters();

            base.OnCountdownTimerEnded();
        }

        // ── Elemental crystal pickups (the Salvo/Dog Fight recipe) ───────────

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

        // ── Server-authoritative game end at time-out ────────────────────────

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;
            if (rule == null) return;

            // Time ran out — the rule ranks the domains (volume, then fuses beaten).
            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            var winnerRep = gameData.RoundStatsList
                .Where(s => s != null && s.Domain == winningDomain)
                .OrderByDescending(s => s.HostileVolumeDestroyed)
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
            var volumeArray = new float[count];
            var fusesArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                volumeArray[i] = statsList[i].HostileVolumeDestroyed;
                fusesArray[i] = statsList[i].FusesBeaten;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, volumeArray, fusesArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            float[] volumes,
            int[] fusesBeaten,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Bloomrush] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.HostileVolumeDestroyed = volumes[i];
                stat.FusesBeaten = fusesBeaten[i];
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
                s.HostileVolumeDestroyed = 0f;
                s.HostilePrismsDestroyed = 0;
                s.FusesBeaten = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
