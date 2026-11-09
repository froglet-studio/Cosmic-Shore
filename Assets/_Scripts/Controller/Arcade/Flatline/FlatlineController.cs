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
    /// Flatline - the Brittle Star's CHARGE game: thin PANES of prisms crossing the cell (<see cref="SpawnableFlatline"/>,
    /// the cell's own environment). A pane stops a round, the cords cut straight through it, and a ring thrown face-on
    /// into it bursts flat along it. Only hull-on-hull hits score, at the fleet price: a round 1, a cord or thrown-saber
    /// Strike 8 (<see cref="FlatlineScoringRuleSO"/>); the ring blast scores nothing. First DOMAIN to the target wins
    /// (golf-timed); the target is per intensity (<see cref="FlatlinePointTurnMonitor"/>). See FLATLINE.md.
    ///
    /// <para>Structurally a sibling of <see cref="SerpentGulagController"/> (1 round / 1 turn, HasEndGame=false, server
    /// winner detection, a snapshot SyncFinalScores_ClientRpc carrying the breakdown, the hit latch cleared per match).
    /// It changes nothing about the Brittle Star.</para>
    ///
    /// <para><b>The crystal field is laid on every peer</b> at the arena's seam sites
    /// (<see cref="SpawnableFlatline.CollectCrystalSites"/>), each site's element drawn from
    /// <see cref="FlatlineSettingsSO.fieldSeed"/>, and each collected crystal grows back in its own place after
    /// <see cref="FlatlineSettingsSO.fieldRegrowSeconds"/>. A crystal is a spare ring for the Brittle Star (every crystal
    /// list on its impactor container), so the seams are where the ring is re-armed: the most exposed places in the
    /// cell. Collection is per-peer, as in Dog Fight and Serpent Gulag, which is tolerable because a crystal scores
    /// nothing.</para>
    ///
    /// BRITTLE-STAR-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class FlatlineController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag FlatlineSettings.asset - the crystal field's regrowth and the AI hunters. The point target " +
                 "lives in EndConditionOverridesSO (per intensity), resolved by FlatlinePointTurnMonitor; the point " +
                 "VALUES on the rule.")]
        [SerializeField] FlatlineSettingsSO settings;

        [Tooltip("Drag FlatlineScoringRule.asset (metric = CombatPoints, a round 1, a Strike 8, the ring 0).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Flatline cell. Read-only to this controller: its position is the arena CENTRE, and its " +
                 "SpawnableFlatline answers where the seams carry crystals.")]
        [SerializeField] Cell arenaCell;

        sealed class FieldSlot
        {
            public Vector3 Position;
            public Element Element;
            public Crystal Crystal;
            public ElementalCrystalImpactor Impactor;
            public float RegrowAt = -1f;
        }

        readonly List<FieldSlot> _field = new();
        readonly FlatlineHunter _hunter = new();
        DomainRaceToasts _raceToasts;
        bool _finalResultsSent;
        bool _hintPosted;
        float _nextFieldCheck;

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

            // The hit latch is static and Time.time keeps running across a scene load, so a fast rematch could
            // otherwise inherit a claimed window and eat the first strike.
            VesselCombatHitLatch.Clear();

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            _hunter.Disarm();
            ClearField();
            base.OnNetworkDespawn();
        }

        /// <summary>Server-only: the setters push through server-write NetworkVariables.</summary>
        void ZeroCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].CombatPoints = 0;
                list[i].BulletHitsLanded = 0;
                list[i].StrikeHitsLanded = 0;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            // The field is LOCAL, laid identically on every peer, so this runs before the server gate.
            ClearField();
            LayField();

            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[FlatlineController] no settings asset wired; the AI hunters are not armed " +
                                   "and fly the platform's own steering.");
                return;
            }
            _hunter.Arm(gameData, settings, Centre, NearestLiveCrystal);
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
                if (local != null) GameToastAPI.Post(GameToastSituation.FlatlineHint, local.Domain);
            }

            if (settings != null && Time.time >= _nextFieldCheck)
            {
                _nextFieldCheck = Time.time + settings.progressSampleSeconds;
                RegrowField();
            }

            if (IsServer && _hunter.IsArmed) _hunter.Tick();
        }

        readonly List<Vector3> _siteScratch = new();

        /// <summary>The crystal sites in world space, read off the arena's own environment (the seams where its panes
        /// cross). Empty when the cell has no SpawnableFlatline, and then there is no field.</summary>
        List<Vector3> CollectSites()
        {
            _siteScratch.Clear();
            var env = arenaCell ? arenaCell.GetComponentInChildren<SpawnableFlatline>(true) : null;
            if (!env) env = FindAnyObjectByType<SpawnableFlatline>();
            if (!env) return _siteScratch;
            env.CollectCrystalSites(_siteScratch);
            for (int i = 0; i < _siteScratch.Count; i++)
                _siteScratch[i] = env.transform.TransformPoint(_siteScratch[i]);
            return _siteScratch;
        }

        // ── The crystal field (every peer) ──

        void LayField()
        {
            if (settings == null) return;
            if (ElementalCrystalSetSO.Load() == null) return;   // no elemental set in this project state: no field

            var sites = CollectSites();
            var rng = new System.Random(settings.fieldSeed);
            for (int i = 0; i < sites.Count; i++)
            {
                var slot = new FieldSlot
                {
                    Position = sites[i],
                    Element = ElementalCrystalSetSO.RandomElementFrom(rng),
                };
                _field.Add(slot);
                Mint(slot);
            }
        }

        void Mint(FieldSlot slot)
        {
            var set = ElementalCrystalSetSO.Load();
            var prefab = set != null ? set.GetPrefab(slot.Element) : null;
            if (prefab == null) return;

            var crystal = Instantiate(prefab, slot.Position, Quaternion.identity);
            // One size, one petal, like every elemental crystal. Sized BEFORE Start(): Crystal stamps
            // crystalValue from lossyScale.
            crystal.transform.localScale = Vector3.one * ElementalCrystalSetSO.CrystalWorldScale;
            crystal.enabled = true;
            crystal.gameObject.SetActive(true);

            var impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
            impactor.Crystal = crystal;
            if (set.CollectionEffects is { Length: > 0 })
                impactor.SetCollectionEffects(set.CollectionEffects);
            crystal.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);

            slot.Crystal = crystal;
            slot.Impactor = impactor;
            slot.RegrowAt = -1f;
        }

        static bool IsLive(FieldSlot slot) => slot.Crystal && slot.Impactor && !slot.Impactor.HasBeenCollected;

        void RegrowField()
        {
            float now = Time.time;
            for (int i = 0; i < _field.Count; i++)
            {
                var slot = _field[i];
                if (IsLive(slot)) continue;
                if (slot.RegrowAt < 0f) { slot.RegrowAt = now + settings.fieldRegrowSeconds; continue; }
                if (now < slot.RegrowAt) continue;
                // A collected crystal destroys itself at the end of its capture; one still flying is left to finish.
                if (slot.Crystal) continue;
                Mint(slot);
            }
        }

        /// <summary>The nearest crystal of the field still there to take, on this machine (the AI's forage target).</summary>
        Vector3? NearestLiveCrystal(Vector3 from)
        {
            Vector3? best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _field.Count; i++)
            {
                var slot = _field[i];
                if (!IsLive(slot)) continue;
                float d = (slot.Position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = slot.Position;
            }
            return best;
        }

        void ClearField()
        {
            for (int i = 0; i < _field.Count; i++)
                if (_field[i].Crystal) Destroy(_field[i].Crystal.gameObject);
            _field.Clear();
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
                .OrderByDescending(s => s.CombatPoints)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            _hunter.Disarm();
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
            var pointsArray = new int[count];
            var roundArray = new int[count];
            var strikeArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                roundArray[i] = statsList[i].BulletHitsLanded;
                strikeArray[i] = statsList[i].StrikeHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray, roundArray, strikeArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] rounds,
            int[] strikes,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Flatline] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // strikes and rounds as 0.
                stat.BulletHitsLanded = rounds[i];
                stat.StrikeHitsLanded = strikes[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            _finalResultsSent = true;
            ClearField();

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

            _hunter.Disarm();
            ClearField();
            VesselCombatHitLatch.Clear();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.CombatPoints = 0;
                s.BulletHitsLanded = 0;
                s.StrikeHitsLanded = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
