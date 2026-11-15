using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Honeycomb - the Honeyguide's MASS game: a timed building war scored on the prism volume a domain has STANDING in
    /// its own colour at the whistle (<see cref="ScoringMetric.VolumeRemaining"/> through
    /// <see cref="HoneycombScoringRuleSO"/>, ended by <see cref="HoneycombTimeTurnMonitor"/>). See HONEYCOMB.md.
    ///
    /// <para><b>The hive is the Mass verb.</b> An omni crystal the Honeyguide takes - the cell's own, which the arrow
    /// points at - becomes a HIVE: a comb of real prisms behind the hull, every one super-shielded while hive mode
    /// lasts, the cells of your colour yours to keep, while the Ratel's board turns into the hive puzzle that pays out
    /// prisms and charges. A human's hull also plants a Mass hive in the cell every 45 s (sooner with each Mass level,
    /// a second at once with Hive Chain at Mass 5). Between hives the hull's own trail is mass too, and the slips the
    /// hive's charges buy lay it fast. Rivals take it back with their pecks - a live stock, so the round is timed.</para>
    ///
    /// <para><b>It changes nothing about the Honeyguide</b> (the LOCKED vessel rule). The arena is the bare Barren cell,
    /// referenced read-only (Tapestry's), with no food web on purpose: herbivores graze whatever the controlling colour
    /// does not own, which eats the TRAILING team's mass - an anti-comeback current in a mode whose economy is contested
    /// mass. The mode's lever is the elemental crystal scatter (<see cref="HoneycombSettingsSO.elementalCrystalCountByIntensity"/>,
    /// Tapestry's peer-local recipe): the cell grows no lifeforms, so these are the only element progression, and a Mass
    /// crystal is a sooner, wider hive.</para>
    ///
    /// <para><b>The AI is the platform's.</b> An AI Honeyguide's pilot already chases the cell's omni crystal, which is
    /// the hive, and its crew already pecks rival prisms ahead, so the narrowest hook is none (Rampage's rule: crystal
    /// seeking IS the loop). An AI crew plants no Mass hive (<c>HoneyguideCrew.StepMassHive</c>), so humans get hives
    /// an AI does not: a known asymmetry, recorded in HONEYCOMB.md, and a vessel question rather than a mode's.</para>
    ///
    /// Structurally <see cref="TapestryController"/> and <see cref="GlasshouseController"/> (timed, points mode,
    /// UseGolfRules = false, the time monitor as the scene's only monitor, a snapshot ClientRpc carrying the breakdown).
    /// HONEYGUIDE-ONLY is enforced by the arcade card's Vessels list.
    /// </summary>
    public class HoneycombController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag HoneycombSettings.asset - the crystal scatter and the lead feedback. The round length lives in " +
                 "EndConditionOverridesSO.")]
        [SerializeField] HoneycombSettingsSO settings;

        [Tooltip("Drag HoneycombScoringRule.asset (metric = VolumeRemaining, timed, highest wins).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Barren cell. Read-only to this controller: its centre anchors the crystal scatter.")]
        [SerializeField] Cell arenaCell;

        readonly List<Crystal> _spawnedCrystals = new();

        bool _finalResultsSent;
        bool _hintPosted;
        Coroutine _progressRoutine;
        Domains _leaderDomain = Domains.Blue;
        float _lastLeadAnnounce = float.NegativeInfinity;

        protected override bool UseGolfRules => false;
        protected override bool UseSceneReloadForReplay => true;
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _hintPosted = false;
            _leaderDomain = Domains.Blue;

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            ClearElementalCrystals();
            base.OnNetworkDespawn();
        }

        /// <summary>Server-only. Every counter the metric and its breakdown read, zeroed again at the countdown's end so
        /// the round counts only what was built in it.</summary>
        void ZeroCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].VolumeRemaining = 0f;
                list[i].PrismsRemaining = 0;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            // Peer-local BEFORE the server gate (Tapestry's shape): the crystal scatter is local and deterministic, so
            // every machine must lay it.
            ClearElementalCrystals();
            SpawnElementalCrystals();

            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            StopProgressSampler();
            _progressRoutine = StartCoroutine(ProgressRoutine());
        }

        void Update()
        {
            if (_finalResultsSent || _hintPosted || gameData == null || !gameData.IsTurnRunning) return;
            _hintPosted = true;
            var local = gameData.LocalPlayer;
            if (local != null) GameToastAPI.Post(GameToastSituation.HoneycombHint, local.Domain);
        }

        // ── Lead feedback (server samples, every peer gets the toast) ──

        void StopProgressSampler()
        {
            if (_progressRoutine == null) return;
            StopCoroutine(_progressRoutine);
            _progressRoutine = null;
        }

        IEnumerator ProgressRoutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, settings ? settings.progressSampleSeconds : 1f));
            while (!_finalResultsSent)
            {
                SampleLead();
                yield return wait;
            }
            _progressRoutine = null;
        }

        void SampleLead()
        {
            if (!IsServer || rule == null || settings == null) return;
            if (Time.time - gameData.TurnStartTime < settings.leadAnnounceAfterSeconds) return;

            var leader = rule.ResolveWinner(gameData);
            if (leader == Domains.Blue || leader == _leaderDomain) return;

            int volume = rule.DomainValue(gameData, leader);
            if (volume <= 0) return;

            bool first = _leaderDomain == Domains.Blue;
            _leaderDomain = leader;
            if (first) return;   // the first leader after the opening is not a CHANGE
            if (Time.time - _lastLeadAnnounce < settings.leadAnnounceMinGapSeconds) return;

            _lastLeadAnnounce = Time.time;
            AnnounceLeadChanged_ClientRpc((int)leader, volume);
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, int volume)
        {
            // The shared lead-change situation (130): a timed mode has no target, so the controller samples it.
            GameToastAPI.Post(GameToastSituation.DomainRaceLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), volume.ToString());
        }

        // ── Elemental crystal pickups (the Tapestry / Bloomrush recipe) ──

        void SpawnElementalCrystals()
        {
            // SelectedIntensity is safe here: the countdown only starts after the config ClientRpc has set it.
            int count = settings != null ? settings.CrystalCountFor(gameData.SelectedIntensity) : 0;
            if (count <= 0) return;

            var set = ElementalCrystalSetSO.Load();
            if (set == null) return;

            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;
            var rng = new System.Random(settings.crystalScatterSeed);

            for (int i = 0; i < count; i++)
            {
                var element = ElementalCrystalSetSO.RandomElementFrom(rng);
                var prefab = set.GetPrefab(element);
                if (prefab == null) continue;

                // Equal-volume scatter through the shell (cube root of a uniform draw).
                double u = rng.NextDouble();
                float r = settings.crystalScatterRadius * Mathf.Pow((float)u, 1f / 3f);
                float theta = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float y = (float)(rng.NextDouble() * 2.0 - 1.0);
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var pos = centre + new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta)) * r;

                var crystal = Instantiate(prefab, pos, Quaternion.identity);
                // One size, one petal, like every elemental crystal (ElementalCrystalSetSO.CrystalWorldScale).
                crystal.transform.localScale = Vector3.one * ElementalCrystalSetSO.CrystalWorldScale;
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

        // ── Server-authoritative game end at time-out ──

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
                .OrderByDescending(s => s.VolumeRemaining)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            StopProgressSampler();
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var volumeArray = new float[count];
            var prismArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                volumeArray[i] = statsList[i].VolumeRemaining;
                prismArray[i] = statsList[i].PrismsRemaining;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, volumeArray, prismArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            float[] volumes,
            int[] prisms,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Honeycomb] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // standing mass as whatever its last sample said.
                stat.VolumeRemaining = volumes[i];
                stat.PrismsRemaining = prisms[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;
            _finalResultsSent = true;
            ClearElementalCrystals();

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // ── Replay ──

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            _hintPosted = false;
            _leaderDomain = Domains.Blue;
            _lastLeadAnnounce = float.NegativeInfinity;

            StopProgressSampler();
            ClearElementalCrystals();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.VolumeRemaining = 0f;
                s.PrismsRemaining = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
