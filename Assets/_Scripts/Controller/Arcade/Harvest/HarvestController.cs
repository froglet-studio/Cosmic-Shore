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
    /// Harvest - the Thresher's SPACE game: a race to destroy hostile prisms in a field of SHEAVES
    /// (<see cref="SpawnableHarvest"/>, the cell's own environment). A sheaf is a core of blocks with stalks standing
    /// out of it: mow the stalks with the chain to bank energy in the ball, then whip the ball into the core, where the
    /// bank goes off as one blast (THRESHER.md §2-2a). First DOMAIN to the target wins, golf-timed. See HARVEST.md.
    ///
    /// <para>Structurally <see cref="RampageController"/> (1 round / 1 turn, HasEndGame=false, server winner detection
    /// in OnTurnEndedCustom, a snapshot SyncFinalScores_ClientRpc carrying the breakdown). The destruction stat
    /// increments itself (StatsManager.PrismDestroyed), so no per-event listener is needed. The omni crystals are the
    /// cell's own (its CrystalManager, in the field's clear centre): a crystal is the Thresher's 12 s windmill.</para>
    ///
    /// <para><b>Team Space crystals</b> (I3, <see cref="HarvestSettingsSO.teamCrystalsPerTeamByIntensity"/>): for each
    /// team in the match the controller floats Space elemental crystals in that team's colour, at open points of the
    /// field (<see cref="SpawnableHarvest.CollectOpenPoints"/>), laid identically on every peer, which only that team
    /// can take (<see cref="ElementalCrystalImpactor.RestrictTo"/>) and which grow back after a while. A Space level
    /// is the Thresher's own scaling (a bigger blast, x4 at rest Space up to x8 at full): the environment hands it out,
    /// the kit is unchanged. The element is <see cref="HarvestSettingsSO.teamCrystalElement"/>.
    /// The I4 creatures are the cell's own (its spawn profile), not this controller's.</para>
    ///
    /// <para><b>Nothing about the Thresher changes</b> (the LOCKED vessel rule): the mode's levers are the sheaves,
    /// the crystals and the AI. Every Thresher spawns in Mass Mode, where the ball steals and never destroys; the hint
    /// toast tells a human to press A, and the AI (<see cref="HarvestReaper"/>) presses it once itself.</para>
    ///
    /// THRESHER-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class HarvestController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag HarvestSettings.asset - the AI reapers. The prism target lives in EndConditionOverridesSO, " +
                 "resolved by HarvestPrismTurnMonitor; the results wording on the rule.")]
        [SerializeField] HarvestSettingsSO settings;

        [Tooltip("Drag HarvestScoringRule.asset (metric = PrismsDestroyed, golf).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The field's cell. Read-only to this controller: its position is the arena CENTRE, and its " +
                 "SpawnableHarvest says where the sheaves are.")]
        [SerializeField] Cell arenaCell;

        sealed class CrystalSlot
        {
            public Vector3 Position;
            public Domains Domain;
            public Crystal Crystal;
            public ElementalCrystalImpactor Impactor;
            public float RegrowAt = -1f;
        }

        readonly HarvestReaper _reaper = new();
        readonly List<SpawnableHarvest.Sheaf> _sheaves = new();
        readonly List<CrystalSlot> _teamCrystals = new();
        readonly List<Vector3> _points = new();
        float _nextCrystalCheck;
        DomainRaceToasts _raceToasts;
        bool _finalResultsSent;
        bool _hintPosted;

        // Golf: winners carry their finish time, losers a remaining-prisms sentinel (RampageScoringRuleSO).
        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;
        protected override bool HasEndGame => false;

        Vector3 Centre => arenaCell ? arenaCell.transform.position : Vector3.zero;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _hintPosted = false;
        }

        public override void OnNetworkDespawn()
        {
            _reaper.Disarm();
            ClearTeamCrystals();
            base.OnNetworkDespawn();
        }

        protected override void OnCountdownTimerEnded()
        {
            // The team crystals are LOCAL, laid identically on every peer, so this runs before the server gate.
            ClearTeamCrystals();
            LayTeamCrystals();

            if (!IsServer) return;

            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[HarvestController] no settings asset wired; the AI reapers are not armed, stay in " +
                                   "Mass Mode and fly the platform's own steering.");
                return;
            }
            _reaper.Arm(gameData, settings, Centre, CollectSheaves(), NearestTeamCrystal);
        }

        void Update()
        {
            if (_finalResultsSent) return;

            if (rule != null)
            {
                // Quarter, halfway and lead change as toasts: a local poll over the replicated score (128-130).
                _raceToasts ??= new DomainRaceToasts(rule);
                _raceToasts.Tick(gameData);
            }

            if (gameData == null || !gameData.IsTurnRunning) return;

            if (!_hintPosted)
            {
                _hintPosted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(GameToastSituation.HarvestHint, local.Domain);
            }

            if (settings != null && _teamCrystals.Count > 0 && Time.time >= _nextCrystalCheck)
            {
                _nextCrystalCheck = Time.time + settings.crystalCheckSeconds;
                RegrowTeamCrystals();
            }

            if (IsServer && _reaper.IsArmed) _reaper.Tick();
        }

        /// <summary>Every sheaf in world space, read off the arena's own environment.</summary>
        List<SpawnableHarvest.Sheaf> CollectSheaves()
        {
            _sheaves.Clear();
            var env = arenaCell ? arenaCell.GetComponentInChildren<SpawnableHarvest>(true) : null;
            if (!env) env = FindAnyObjectByType<SpawnableHarvest>();
            if (!env) return _sheaves;
            env.CollectSheaves(_sheaves);
            float scale = env.transform.lossyScale.x;
            for (int i = 0; i < _sheaves.Count; i++)
            {
                var s = _sheaves[i];
                _sheaves[i] = new SpawnableHarvest.Sheaf(env.transform.TransformPoint(s.Centre),
                                                         env.transform.TransformDirection(s.Axis).normalized,
                                                         s.CoreRadius * scale, s.Reach * scale, s.Domain);
            }
            return _sheaves;
        }

        SpawnableHarvest Environment()
        {
            var env = arenaCell ? arenaCell.GetComponentInChildren<SpawnableHarvest>(true) : null;
            return env ? env : FindAnyObjectByType<SpawnableHarvest>();
        }

        // ── The team Space crystals (every peer) ──

        /// <summary>The teams in the match, in enum order (Jade, Ruby, Gold), so every peer lays the same crystals.</summary>
        List<Domains> TeamsPresent()
        {
            var teams = new List<Domains>();
            var players = gameData != null ? gameData.Players : null;
            if (players == null) return teams;
            for (int i = 0; i < players.Count; i++)
            {
                var d = players[i]?.Domain ?? Domains.Blue;
                if (d == Domains.Blue || teams.Contains(d)) continue;
                teams.Add(d);
            }
            teams.Sort();
            return teams;
        }

        void LayTeamCrystals()
        {
            if (settings == null || gameData == null) return;
            int intensity = gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1;
            int perTeam = settings.TeamCrystalsPerTeam(intensity);
            if (perTeam <= 0 || ElementalCrystalSetSO.Load() == null) return;
            var env = Environment();
            if (!env) return;

            foreach (var team in TeamsPresent())
            {
                _points.Clear();
                // One stream per team (its enum value), so a team's places do not move when another team joins.
                env.CollectOpenPoints((int)team, perTeam, settings.teamCrystalClearance, _points);
                for (int i = 0; i < _points.Count; i++)
                {
                    var slot = new CrystalSlot { Position = env.transform.TransformPoint(_points[i]), Domain = team };
                    _teamCrystals.Add(slot);
                    MintTeamCrystal(slot);
                }
            }
        }

        void MintTeamCrystal(CrystalSlot slot)
        {
            var set = ElementalCrystalSetSO.Load();
            var prefab = set != null ? set.GetPrefab(settings.teamCrystalElement) : null;
            if (prefab == null) return;

            var crystal = Instantiate(prefab, slot.Position, Quaternion.identity);
            // One size, one petal, like every elemental crystal. Sized BEFORE Start(): Crystal stamps crystalValue from
            // lossyScale.
            crystal.transform.localScale = Vector3.one * ElementalCrystalSetSO.CrystalWorldScale;
            crystal.enabled = true;
            crystal.gameObject.SetActive(true);
            // Its team's colour, and only its team may take it.
            crystal.ApplyDomainPreview(slot.Domain);

            var impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
            impactor.Crystal = crystal;
            impactor.RestrictTo(slot.Domain);
            if (set.CollectionEffects is { Length: > 0 })
                impactor.SetCollectionEffects(set.CollectionEffects);
            crystal.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);

            slot.Crystal = crystal;
            slot.Impactor = impactor;
            slot.RegrowAt = -1f;
        }

        static bool IsLive(CrystalSlot slot) => slot.Crystal && slot.Impactor && !slot.Impactor.HasBeenCollected;

        void RegrowTeamCrystals()
        {
            float now = Time.time;
            for (int i = 0; i < _teamCrystals.Count; i++)
            {
                var slot = _teamCrystals[i];
                if (IsLive(slot)) continue;
                if (slot.RegrowAt < 0f) { slot.RegrowAt = now + settings.teamCrystalRegrowSeconds; continue; }
                if (now < slot.RegrowAt) continue;
                // A collected crystal destroys itself at the end of its capture; one still flying is left to finish.
                if (slot.Crystal) continue;
                MintTeamCrystal(slot);
            }
        }

        /// <summary>The nearest live crystal of <paramref name="team"/>'s on this machine (the AI's team-crystal target).</summary>
        Vector3? NearestTeamCrystal(Vector3 from, Domains team)
        {
            Vector3? best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _teamCrystals.Count; i++)
            {
                var slot = _teamCrystals[i];
                if (slot.Domain != team || !IsLive(slot)) continue;
                float d = (slot.Position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = slot.Position;
            }
            return best;
        }

        void ClearTeamCrystals()
        {
            for (int i = 0; i < _teamCrystals.Count; i++)
                if (_teamCrystals[i].Crystal) Destroy(_teamCrystals[i].Crystal.gameObject);
            _teamCrystals.Clear();
        }

        // ── Server-authoritative game end ──

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
            _reaper.Disarm();
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
                    CSDebug.LogError($"[Harvest] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // prisms as 0.
                stat.HostilePrismsDestroyed = prismsDestroyed[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            _finalResultsSent = true;
            ClearTeamCrystals();

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
            _raceToasts = null;
            _reaper.Disarm();
            ClearTeamCrystals();

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
