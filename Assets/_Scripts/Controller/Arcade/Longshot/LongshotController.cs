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
    /// Longshot - the Serpent's SPACE game: a timed shoot. Lances of neutral prisms - single-file lines of cubes, bent into
    /// zig-zag runs at the top rungs - hang round the core (laid by this controller on every peer,
    /// <see cref="LongshotField"/>). The Serpent's sniper round is one hitscan cone that breaks EVERY hostile prism inside
    /// it, neutral ones included (SERPENT_SNIPER_SCOPE.md), on a 12 s recovery: fired along a lance it takes the whole run,
    /// fired across it a prism or two. A rival's straight trail is a lance of their colour. The score is the prisms a
    /// domain broke (<see cref="ScoringMetric.PrismsDestroyed"/>: hostile mass only, so a Serpent's own trail never
    /// counts) when the clock runs out.
    ///
    /// <para><b>The platform keeps the score.</b> Every prism the round destroys is credited to its shooter by the
    /// platform's destruction path, so this controller only lays the field, zeroes the counters, feeds the lead toast, arms
    /// the AI and snapshots the whistle.</para>
    ///
    /// <para><b>The arena is a bare nucleus cell, with no food web</b> (Tapestry's reasoning). Omni crystals respawn
    /// inside the nucleus.</para>
    ///
    /// <para><b>The AI presses only the scope and the round</b> (<see cref="LongshotRifleman"/>). Nothing about the
    /// Serpent changes (the LOCKED vessel rule).</para>
    /// </summary>
    public class LongshotController : MultiplayerDomainGamesController, LongshotRifleman.IFieldView
    {
        const string FieldOwnerId = "LongshotField";

        [Header("Config")]
        [Tooltip("Drag LongshotSettings.asset - lead feedback and the AI rifleman's policy. The field is LongshotField; " +
                 "the round length lives in EndConditionOverridesSO.")]
        [SerializeField] LongshotSettingsSO settings;

        [Tooltip("Drag LongshotScoringRule.asset (metric = PrismsDestroyed, timed, highest wins).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The scene's cell. Read-only to this controller: its centre places the lances.")]
        [SerializeField] Cell arenaCell;

        [Tooltip("The environment prism every lance prism is laid from (SpawnablePrism.prefab).")]
        [SerializeField] Prism lancePrefab;

        [Tooltip("The nucleus radius (u) the lances keep clear of. Authored to the cell's nucleus by the generator.")]
        [SerializeField, Min(0f)] float nucleusRadius = 392f;

        [Tooltip("The membrane radius (u) the lances keep inside. Authored to the cell's membrane by the generator.")]
        [SerializeField, Min(1f)] float membraneRadius = 1200f;

        readonly List<LongshotField.Piece> _pieces = new();
        readonly List<LongshotField.Segment> _segments = new();
        List<Prism>[] _segmentPrisms = System.Array.Empty<List<Prism>>();
        int[] _segmentStanding = System.Array.Empty<int>();
        Transform _fieldRoot;

        readonly LongshotRifleman _riflemen = new();

        bool _finalResultsSent;
        bool _hintPosted;
        Coroutine _progressRoutine;
        Domains _leaderDomain = Domains.Blue;
        float _lastLeadAnnounce = float.NegativeInfinity;

        protected override bool UseGolfRules => false;
        protected override bool UseSceneReloadForReplay => true;
        protected override bool HasEndGame => false;

        Vector3 Centre => arenaCell ? arenaCell.transform.position : Vector3.zero;
        int Intensity => gameData != null && gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1;

        // ── What the rifleman reads (LongshotRifleman.IFieldView) ────────────

        int LongshotRifleman.IFieldView.SegmentCount => _segments.Count;
        Vector3 LongshotRifleman.IFieldView.SegmentStart(int segment) => Centre + _segments[segment].Start;
        Vector3 LongshotRifleman.IFieldView.SegmentEnd(int segment) => Centre + _segments[segment].End;
        int LongshotRifleman.IFieldView.Standing(int segment) =>
            segment >= 0 && segment < _segmentStanding.Length ? _segmentStanding[segment] : 0;

        int LongshotRifleman.IFieldView.StandingInCone(int segment, Vector3 apex, Vector3 dir, float range,
                                                        float halfAngleDegrees, float minRadius)
        {
            if (segment < 0 || segment >= _segmentPrisms.Length) return 0;
            var list = _segmentPrisms[segment];
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var prism = list[i];
                if (!Standing(prism)) continue;
                if (LongshotField.InCone(prism.transform.position, apex, dir, range, halfAngleDegrees, minRadius)) n++;
            }
            return n;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _hintPosted = false;
            _leaderDomain = Domains.Blue;

            LayField();
            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            _riflemen.Disarm();
            ClearField();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Server-only. Every counter the metric and its breakdown read, zeroed again at the countdown's end
        /// (<see cref="OnCountdownTimerEnded"/>), so the round counts only what was broken in it.
        /// </summary>
        void ZeroCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].HostilePrismsDestroyed = 0;
                list[i].HostileVolumeDestroyed = 0f;
                list[i].FriendlyPrismsDestroyed = 0;
                list[i].FriendlyVolumeDestroyed = 0f;
            }
        }

        // ── The field (every peer) ───────────────────────────────────────────

        void LayField()
        {
            ClearField();
            if (!lancePrefab)
            {
                CSDebug.LogWarning("[Longshot] no lancePrefab on the controller - the field is not laid. " +
                                   "Re-run Tools/Build/author_longshot_assets.py.");
                return;
            }

            var rung = LongshotField.ForIntensity(Intensity);
            LongshotField.Lay(rung, nucleusRadius, membraneRadius, _pieces, _segments);
            _fieldRoot = new GameObject("LongshotField").transform;
            _fieldRoot.position = Centre;
            var trail = new Trail();
            var scale = Vector3.one * LongshotField.PrismSize;

            _segmentPrisms = new List<Prism>[_segments.Count];
            _segmentStanding = new int[_segments.Count];
            for (int s = 0; s < _segments.Count; s++) _segmentPrisms[s] = new List<Prism>(_segments[s].Count);

            int laid = 0;
            for (int i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i];
                var prism = PrismTrailBuilder.LayOne(lancePrefab,
                    new PrismLay(new SpawnPoint(Centre + piece.Position, Quaternion.identity, scale), Domains.Blue,
                                 PrismKind.Plain),
                    _fieldRoot, trail, FieldOwnerId, watchForReveal: true);
                if (!prism) continue;
                _segmentPrisms[piece.Segment].Add(prism);
                _segmentStanding[piece.Segment]++;
                laid++;
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Longshot] intensity {Intensity}: {rung.LanceCount} lances in {_segments.Count} runs, {laid} prisms.");
        }

        /// <summary>Still a standing lance prism: alive, not re-issued by the pool, and still no team's.</summary>
        static bool Standing(Prism prism) =>
            prism && !prism.destroyed && prism.Domain == Domains.Blue && prism.ownerID == FieldOwnerId;

        void ClearField()
        {
            for (int s = 0; s < _segmentPrisms.Length; s++)
            {
                var list = _segmentPrisms[s];
                for (int i = 0; i < list.Count; i++)
                    if (Standing(list[i])) list[i].Damage(Vector3.zero, Domains.Blue, string.Empty, devastate: true);
                list.Clear();
            }
            _segmentPrisms = System.Array.Empty<List<Prism>>();
            _segmentStanding = System.Array.Empty<int>();
            if (_fieldRoot) Destroy(_fieldRoot.gameObject);
            _fieldRoot = null;
        }

        /// <summary>Counts each run's standing prisms down (only the AI reads it). Swap-remove keeps it O(standing). A
        /// prism a Serpent's wall stole leaves too: it is no longer neutral.</summary>
        void SettleField()
        {
            for (int s = 0; s < _segmentPrisms.Length; s++)
            {
                var list = _segmentPrisms[s];
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (Standing(list[i])) continue;
                    int last = list.Count - 1;
                    list[i] = list[last];
                    list.RemoveAt(last);
                }
                _segmentStanding[s] = list.Count;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded();
            ArmRiflemen();

            StopProgressSampler();
            _progressRoutine = StartCoroutine(ProgressRoutine());
        }

        void Update()
        {
            SettleField();
            if (IsServer && !_finalResultsSent) _riflemen.Tick();
            if (_hintPosted || _finalResultsSent || gameData == null || !gameData.IsTurnRunning) return;
            _hintPosted = true;
            var local = gameData.LocalPlayer;
            if (local != null) GameToastAPI.Post(GameToastSituation.LongshotHint, local.Domain);
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

        // ── AI (server): line up on a run and fire down it ────────────────────

        /// <summary>
        /// Arms every AI Serpent as a <see cref="LongshotRifleman"/>: it lines up along the run worth the most, scopes, and
        /// fires once the round would take its rung's share of the run - or, while its domain trails, goes for the nearest
        /// rival's trail. Difficulty is the seat's AI level, never the intensity. Cleared on the whistle, despawn and replay.
        /// </summary>
        void ArmRiflemen()
        {
            if (settings == null)
            {
                CSDebug.LogWarning("[LongshotController] no settings asset wired; AI riflemen not armed.");
                return;
            }
            _riflemen.Arm(gameData, settings, this, ShouldHuntRival);
        }

        bool ShouldHuntRival(Domains own, int level)
        {
            if (rule == null) return false;

            float deficit = LongshotSettingsSO.Rung(settings.aiRivalTrailDeficitFraction, level);
            if (deficit >= 1f) return false;

            int mine = rule.DomainValue(gameData, own);
            int best = 0;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                if (d == own) continue;
                best = Mathf.Max(best, rule.DomainValue(gameData, d));
            }
            return best > 0 && mine < best * (1f - deficit);
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
                .OrderByDescending(s => s.HostilePrismsDestroyed)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            StopProgressSampler();
            _riflemen.Disarm();
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
            var brokenArray = new int[count];
            var volumeArray = new float[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                brokenArray[i] = statsList[i].HostilePrismsDestroyed;
                volumeArray[i] = statsList[i].HostileVolumeDestroyed;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, brokenArray, volumeArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] broken,
            float[] volumes,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Longshot] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The whistle's snapshot, not the live value: mass can still be destroyed in the
                // frames after the server decided, and the board must show what was decided.
                stat.HostilePrismsDestroyed = broken[i];
                stat.HostileVolumeDestroyed = volumes[i];
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
            _riflemen.Disarm();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.HostilePrismsDestroyed = 0;
                s.HostileVolumeDestroyed = 0f;
                s.FriendlyPrismsDestroyed = 0;
                s.FriendlyVolumeDestroyed = 0f;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
