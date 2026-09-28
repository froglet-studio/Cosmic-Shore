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
    /// Tapestry - the Butterfly's MASS game: a timed painting war. The Butterfly is the hull that
    /// makes the 2D prismscape, and on it MASS is the WIDTH of the wake - Mass mode lays keys 5x
    /// the narrow line at Mass 0 and 20x at Mass 15. So the score is the mass a domain has
    /// STANDING when the clock runs out (<see cref="ScoringMetric.VolumeRemaining"/>), and the
    /// right trigger is the whole decision: <b>Mass mode</b> paints (wide wake, dust off),
    /// <b>Dust mode</b> raids (narrow line, and the dust capsule below the hull destroys, shrinks or
    /// STEALS one in three of each rival prism it touches - a steal moves the volume straight onto
    /// your score). You cannot paint wide and raid at once.
    ///
    /// <para><b>The arena is the bare Barren cell, and there is deliberately no food web</b> - the
    /// Hijack reasoning. In a cell with fauna the herbivores graze whatever the controlling colour
    /// does not own, and the leader's colour is by definition the most abundant, so a swarm would
    /// preferentially eat whatever the TRAILING team just painted - an anti-comeback current in a
    /// mode whose whole economy is contested mass. Here the only forces that remove mass are the
    /// pilots' own dust, which is the point.</para>
    ///
    /// <para><b>Element progression comes from a crystal scatter</b> (Bloomrush's recipe, peer-local
    /// and deterministic): the cell grows no lifeforms, so no hearts drop, and a Mass crystal is a
    /// wider brush. The Butterfly's Mass-5 upgrade (Gilded Wake) makes Mass mode lay SHIELDED keys,
    /// which the dust can only shed, not take - the late-game defence.</para>
    ///
    /// Structurally Bloomrush's sibling (timed, points mode, UseGolfRules = false, a
    /// <see cref="TapestryTimeTurnMonitor"/> as the scene's only monitor). BUTTERFLY-ONLY is
    /// enforced by the arcade card's Vessels list.
    /// </summary>
    public class TapestryController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag TapestrySettings.asset - crystals, lead feedback and the AI's paint/raid " +
                 "policy. The round length lives in EndConditionOverridesSO.")]
        [SerializeField] TapestrySettingsSO settings;

        [Tooltip("Drag TapestryScoringRule.asset (metric = VolumeRemaining, timed, highest wins).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Barren cell. Read-only to this controller: its centre anchors the crystal " +
                 "scatter and the AI's painting orbit, and its targeting grids answer 'where is " +
                 "the densest opposing mass' for a raiding AI.")]
        [SerializeField] Cell arenaCell;

        readonly List<Crystal> _spawnedCrystals = new();
        readonly ButterflyAutopilotModeDriver _modes = new();
        readonly Dictionary<IPlayer, bool> _aiRaiding = new();

        bool _finalResultsSent;
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
            _leaderDomain = Domains.Blue;

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            DisarmPainters();
            ClearElementalCrystals();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Server-only. Every counter the metric and its breakdown read. The live stock is zeroed
        /// at the whistle's opposite end too (<see cref="OnCountdownTimerEnded"/>), so the round
        /// counts only what was painted in it.
        /// </summary>
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
            // Peer-local BEFORE the server gate (Bloomrush's shape): the crystal scatter is local
            // and deterministic, so every machine must lay it.
            ClearElementalCrystals();
            SpawnElementalCrystals();

            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded();
            ArmPainters();

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
            GameToastAPI.Post(GameToastSituation.TapestryLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), volume.ToString());
        }

        // ── Elemental crystal pickups (the Bloomrush / Salvo / Dog Fight recipe) ──

        void SpawnElementalCrystals()
        {
            // SelectedIntensity is safe here: the countdown only starts after the config ClientRpc
            // has set it (Bloomrush reads its fuse ladder at the same point for the same reason).
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

        // ── AI (server): paint while ahead, raid while behind ──────────────────

        /// <summary>
        /// Every AI Butterfly decides, on a slow clock (<see cref="TapestrySettingsSO.aiDecisionSeconds"/>),
        /// whether to PAINT or RAID:
        /// <list type="bullet">
        /// <item><b>Paint</b> (Mass mode) while its domain is not trailing the best rival by more
        /// than <see cref="TapestrySettingsSO.aiRaidDeficitFraction"/>: it flies a slow orbit of the
        /// cell - a broad arc of wide keys - detouring for any elemental crystal within reach.</item>
        /// <item><b>Raid</b> (Dust mode) while it trails, and in the last
        /// <see cref="TapestrySettingsSO.aiLateRaidSeconds"/> regardless: it flies ABOVE the
        /// densest opposing mass (<see cref="Cell.GetExplosionTarget"/>) so the mass passes
        /// through the dust hanging below it.</item>
        /// </list>
        /// Steering is <see cref="AIPilot.SetExternalTargetProvider"/>; the mode is the replicated
        /// press (<see cref="ButterflyAutopilotModeDriver"/>). Both are cleared on the whistle,
        /// despawn and replay.
        /// </summary>
        void ArmPainters()
        {
            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;
            _modes.RetrySeconds = settings ? settings.aiModeRetrySeconds : 1f;
            _aiRaiding.Clear();

            int index = 0;
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                float nextDecision = 0f;
                // Each bot orbits on its own great circle, so two painters do not lay one arc twice.
                var axis = Quaternion.AngleAxis(index++ * 67f, Vector3.up)
                           * Quaternion.AngleAxis(35f, Vector3.right) * Vector3.up;
                var start = Vector3.Cross(axis, Vector3.forward).sqrMagnitude > 1e-4f
                    ? Vector3.Cross(axis, Vector3.forward).normalized
                    : Vector3.right;

                pilot.SetExternalTargetProvider(() =>
                {
                    var selfTf = captured.Vessel?.Transform;
                    if (selfTf == null) return centre;
                    if (_finalResultsSent) return centre;

                    if (Time.time >= nextDecision)
                    {
                        nextDecision = Time.time + settings.aiDecisionSeconds;
                        _aiRaiding[captured] = ShouldRaid(captured.Domain);
                    }
                    bool raid = _aiRaiding.TryGetValue(captured, out var r) && r;
                    _modes.Drive(captured, wantDust: raid);

                    Vector3 selfPos = selfTf.position;

                    if (raid && arenaCell)
                        return arenaCell.GetExplosionTarget(captured.Domain) + selfTf.up * settings.aiDustHover;

                    if (TryNearestCrystal(selfPos, out var crystalPos)) return crystalPos;

                    float radius = settings.crystalScatterRadius * settings.aiPaintOrbitFraction;
                    float angle = (Time.time - gameData.TurnStartTime) * settings.aiPaintOrbitDegreesPerSecond;
                    return centre + Quaternion.AngleAxis(angle, axis) * start * radius;
                });
            }
        }

        bool ShouldRaid(Domains own)
        {
            if (rule == null) return false;

            var monitor = TurnMonitorRemaining();
            if (monitor >= 0f && monitor <= settings.aiLateRaidSeconds) return true;

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
            // Resolved once and cached - there is exactly one monitor per gameplay scene - and
            // re-resolved only if it dies. Asked on the AI's slow decision clock, never per frame.
            if (!_clock) _clock = FindAnyObjectByType<TimeBasedTurnMonitor>();
            return _clock ? _clock.TimeRemaining : -1f;
        }

        TimeBasedTurnMonitor _clock;

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

        void DisarmPainters()
        {
            _modes.ReleaseAll();
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
            DisarmPainters();
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
            var stolenArray = new float[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                volumeArray[i] = statsList[i].VolumeRemaining;
                stolenArray[i] = statsList[i].VolumeStolen;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, volumeArray, stolenArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            float[] volumes,
            float[] stolen,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Tapestry] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The whistle's snapshot, not the live value: mass can still be destroyed in the
                // frames after the server decided, and the board must show what was decided.
                stat.VolumeRemaining = volumes[i];
                stat.VolumeStolen = stolen[i];
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
            _leaderDomain = Domains.Blue;
            _lastLeadAnnounce = float.NegativeInfinity;

            StopProgressSampler();
            DisarmPainters();
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
