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
    /// Palisade - the Swordfish's MASS game: a timed wall war. The Swordfish's Mass ability is <b>Afterimage</b>: for
    /// five seconds after the blade LEAVES the energized state it leaves prism copies of itself where it swings, and
    /// Mass scales their volume (x1 -> x2.5); at Mass 5 they are laid as danger prisms. The score is the volume a
    /// domain has STANDING at the whistle (<see cref="ScoringMetric.VolumeRemaining"/>, a live stock), so the round
    /// asks one thing: where do you swing your walls into place, and whose walls do you cut down?
    ///
    /// <para><b>The sword builds and the sword raids.</b> The blade cuts any rival prism it reaches (two strokes for a
    /// shielded one), so a raid runs the bill through the opposing walls - and an energized raid ends in afterimages
    /// laid right where it was, which is the mode's swing: the raid that cuts their wall raises yours. At Mass 5 the
    /// new walls are danger, so a rival ramming through to cut them pays for it.</para>
    ///
    /// <para><b>Tapestry's shape, reused</b> (<see cref="TapestryController"/>): the bare Barren cell (no food web - a
    /// grazer would eat the trailing team's mass), a peer-local crystal scatter for the elements (Mass crystals are
    /// bigger walls), the timed monitor as the scene's only end, the server's lead sampler, the snapshot. It differs
    /// in what the Swordfish has: no mode press for the AI to drive (its sword hand is the vessel's own
    /// <see cref="SwordfishSwordAutopilot"/>, which energizes in a crowd), the shared lead-change toast
    /// (<see cref="GameToastSituation.DomainRaceLeadChanged"/>), and a once-a-match hint. A game mode never changes the
    /// vessel (LOCKED): its levers are the arena, the crystals and the AI.</para>
    ///
    /// SWORDFISH-ONLY is enforced by the arcade card's Vessels list. See <c>Arcade/PALISADE.md</c>.
    /// </summary>
    public class PalisadeController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag PalisadeSettings.asset - crystals, lead feedback and the AI's build/raid policy. The round " +
                 "length lives in EndConditionOverridesSO.")]
        [SerializeField] PalisadeSettingsSO settings;

        [Tooltip("Drag PalisadeScoringRule.asset (metric = VolumeRemaining, timed, highest wins).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Barren cell. Read-only to this controller: its centre anchors the crystal scatter and the AI's " +
                 "building orbit, and its targeting grids answer 'where is the densest opposing mass' for a raid.")]
        [SerializeField] Cell arenaCell;

        readonly List<Crystal> _spawnedCrystals = new();
        readonly Dictionary<IPlayer, bool> _aiRaiding = new();

        bool _finalResultsSent;
        bool _hintPosted;
        Coroutine _progressRoutine;
        Domains _leaderDomain = Domains.Blue;
        float _lastLeadAnnounce = float.NegativeInfinity;
        TimeBasedTurnMonitor _clock;

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
            DisarmPilots();
            ClearElementalCrystals();
            base.OnNetworkDespawn();
        }

        void Update()
        {
            // The hint, once, to the local pilot, when the turn first runs (every peer posts its own).
            if (_hintPosted || gameData == null || !gameData.IsTurnRunning) return;
            _hintPosted = true;
            var local = gameData.LocalPlayer;
            if (local != null) GameToastAPI.Post(GameToastSituation.PalisadeHint, local.Domain);
        }

        /// <summary>Server-only. Every counter the metric and its breakdown read, so the round counts only its own.</summary>
        void ZeroCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].VolumeRemaining = 0f;
                list[i].PrismsRemaining = 0;
                list[i].VolumeStolen = 0f;
                list[i].PrismStolen = 0;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            // Peer-local BEFORE the server gate: the scatter is local and deterministic, so every machine lays it.
            ClearElementalCrystals();
            SpawnElementalCrystals();

            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded();
            ArmPilots();

            StopProgressSampler();
            _progressRoutine = StartCoroutine(ProgressRoutine());
        }

        // ── Lead feedback (server samples, every peer gets the toast) ─────────

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
            GameToastAPI.Post(GameToastSituation.DomainRaceLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), volume.ToString());
        }

        // ── Elemental crystal pickups (Tapestry's recipe) ────────────────────

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

                // Equal-volume scatter through the ball (cube root of a uniform draw).
                double u = rng.NextDouble();
                float r = settings.crystalScatterRadius * Mathf.Pow((float)u, 1f / 3f);
                float theta = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float y = (float)(rng.NextDouble() * 2.0 - 1.0);
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var pos = centre + new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta)) * r;

                var crystal = Instantiate(prefab, pos, Quaternion.identity);
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

        // ── AI (server): steering only; the sword is the vessel's own hand ────

        /// <summary>
        /// Every AI Swordfish decides, on a slow clock (<see cref="PalisadeSettingsSO.aiDecisionSeconds"/>), whether to
        /// BUILD or RAID. Steering only, through <see cref="AIPilot.SetExternalTargetProvider"/>; the sword is
        /// <see cref="SwordfishSwordAutopilot"/> on the prefab, which swings at hostile prisms in reach and energizes in
        /// a crowd of them, so the afterimages follow every raid on their own.
        /// <list type="bullet">
        /// <item><b>Build</b> while its domain is not trailing the best rival by more than
        /// <see cref="PalisadeSettingsSO.aiRaidDeficitFraction"/>: a slow orbit of the cell (its own great circle, so
        /// two builders do not lay one arc twice), detouring for any elemental crystal within reach.</item>
        /// <item><b>Raid</b> while it trails, and in the last <see cref="PalisadeSettingsSO.aiLateRaidSeconds"/>
        /// regardless: straight at the densest opposing mass (<see cref="Cell.GetExplosionTarget"/>).</item>
        /// </list>
        /// Cleared on the whistle, despawn and replay.
        /// </summary>
        void ArmPilots()
        {
            // The providers read settings every server frame; without it they would throw inside AIPilot.Update.
            if (settings == null)
            {
                CSDebug.LogWarning("[PalisadeController] no settings asset wired; AI hooks not armed.");
                return;
            }

            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;
            _aiRaiding.Clear();

            int index = 0;
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                float nextDecision = 0f;
                var axis = Quaternion.AngleAxis(index++ * 67f, Vector3.up)
                           * Quaternion.AngleAxis(35f, Vector3.right) * Vector3.up;
                var cross = Vector3.Cross(axis, Vector3.forward);
                var start = cross.sqrMagnitude > 1e-4f ? cross.normalized : Vector3.right;

                pilot.SetExternalTargetProvider(() =>
                {
                    var selfTf = captured.Vessel?.Transform;
                    if (selfTf == null || _finalResultsSent) return centre;

                    if (Time.time >= nextDecision)
                    {
                        nextDecision = Time.time + settings.aiDecisionSeconds;
                        _aiRaiding[captured] = ShouldRaid(captured.Domain);
                    }

                    if (_aiRaiding.TryGetValue(captured, out var raid) && raid && arenaCell)
                        return arenaCell.GetExplosionTarget(captured.Domain);

                    if (TryNearestCrystal(selfTf.position, out var crystalPos)) return crystalPos;

                    float radius = settings.crystalScatterRadius * settings.aiBuildOrbitFraction;
                    float angle = (Time.time - gameData.TurnStartTime) * settings.aiBuildOrbitDegreesPerSecond;
                    return centre + Quaternion.AngleAxis(angle, axis) * start * radius;
                });
            }
        }

        bool ShouldRaid(Domains own)
        {
            if (rule == null) return false;

            float remaining = TurnMonitorRemaining();
            if (remaining >= 0f && remaining <= settings.aiLateRaidSeconds) return true;

            int mine = rule.DomainValue(gameData, own);
            int best = 0;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                if (d == own) continue;
                best = Mathf.Max(best, rule.DomainValue(gameData, d));
            }
            return best > 0 && mine < best * (1f - settings.aiRaidDeficitFraction);
        }

        /// <summary>Seconds left on the round clock, or -1 when this scene has no timed monitor.</summary>
        float TurnMonitorRemaining()
        {
            // One monitor per gameplay scene: resolved once, re-resolved only if it dies, asked on the slow clock.
            if (!_clock) _clock = FindAnyObjectByType<TimeBasedTurnMonitor>();
            return _clock ? _clock.TimeRemaining : -1f;
        }

        bool TryNearestCrystal(Vector3 from, out Vector3 position)
        {
            position = default;
            float bestSqr = settings.aiCrystalReach * settings.aiCrystalReach;
            bool found = false;
            for (int i = 0; i < _spawnedCrystals.Count; i++)
            {
                var c = _spawnedCrystals[i];
                if (!c || !c.gameObject.activeInHierarchy) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                position = c.transform.position;
                found = true;
            }
            return found;
        }

        void DisarmPilots()
        {
            _aiRaiding.Clear();
            var players = gameData != null ? gameData.Players : null;
            if (players == null) return;
            foreach (var p in players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                p.Vessel?.VesselStatus?.AIPilot?.ClearExternalTargetProvider();
            }
        }

        // ── Server-authoritative game end at time-out ────────────────────────

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
            DisarmPilots();
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
                    CSDebug.LogError($"[Palisade] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The whistle's snapshot, not the live value: a wall can still be cut in the frames after the server
                // decided, and the board must show what was decided.
                stat.VolumeRemaining = volumes[i];
                stat.PrismsRemaining = prisms[i];
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
            _hintPosted = false;
            _leaderDomain = Domains.Blue;
            _lastLeadAnnounce = float.NegativeInfinity;

            StopProgressSampler();
            DisarmPilots();
            ClearElementalCrystals();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.VolumeRemaining = 0f;
                s.PrismsRemaining = 0;
                s.VolumeStolen = 0f;
                s.PrismStolen = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
