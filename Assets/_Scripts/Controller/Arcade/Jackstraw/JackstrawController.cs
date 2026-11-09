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
    /// Jackstraw - the Thresher's CHARGE game: a brawl in a 0G heap of pick-up sticks
    /// (<see cref="SpawnableJackstraw"/>, the cell's own environment). A STRIKE - the Thresher's ball or chain
    /// reaching a rival hull - is one point, and the first DOMAIN to the target wins (golf-timed). The sticks are
    /// cover and a price: every one the ball breaks costs it speed, so the clean line to a rival is the hot one, and
    /// the heap thins as the match goes on. See JACKSTRAW.md.
    ///
    /// <para>Structurally a sibling of <see cref="SerpentHoodController"/> (1 round / 1 turn, HasEndGame=false,
    /// server winner detection, a snapshot SyncFinalScores_ClientRpc carrying the breakdown, the hit latch cleared per
    /// match). It changes nothing about the Thresher: every hull contact is already a Strike-class combat hit, and
    /// <see cref="JackstrawScoringRuleSO"/> is the rule that pays for it.</para>
    ///
    /// <para><b>The crystals float in the heap's clearings</b>, laid on every peer from
    /// <see cref="JackstrawSettingsSO.crystalSeed"/> (Serpent Gulag's field recipe), and each collected one grows back
    /// in its own place. A silenced pilot cannot score (Docs/ElementalAbilitySystem/SILENCE.md), so the clearings are
    /// where a struck pilot runs: out of the sticks' cover.</para>
    ///
    /// THRESHER-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class JackstrawController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag JackstrawSettings.asset - the crystals and the AI hunters. The strike target lives in " +
                 "EndConditionOverridesSO, resolved by JackstrawStrikeTurnMonitor; the strike VALUE on the rule.")]
        [SerializeField] JackstrawSettingsSO settings;

        [Tooltip("Drag JackstrawScoringRule.asset (metric = CombatPoints, a strike = Strike).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The heap's cell. Read-only to this controller: its position is the arena CENTRE, and its " +
                 "SpawnableJackstraw says where the clearings are.")]
        [SerializeField] Cell arenaCell;

        sealed class CrystalSlot
        {
            public Vector3 Position;
            public Element Element;
            public Crystal Crystal;
            public ElementalCrystalImpactor Impactor;
            public float RegrowAt = -1f;
        }

        readonly List<CrystalSlot> _crystals = new();
        readonly List<SpawnableJackstraw.Clearing> _clearings = new();
        readonly JackstrawHunter _hunter = new();
        DomainRaceToasts _raceToasts;
        bool _finalResultsSent;
        bool _hintPosted;
        float _nextCrystalCheck;

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
            ClearCrystals();
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
                list[i].StrikeHitsLanded = 0;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            // The crystals are LOCAL, laid identically on every peer, so this runs before the server gate.
            ClearCrystals();
            LayCrystals();

            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[JackstrawController] no settings asset wired; the AI hunters are not armed and " +
                                   "fly the platform's own steering.");
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
                if (local != null) GameToastAPI.Post(GameToastSituation.JackstrawHint, local.Domain);
            }

            if (settings != null && Time.time >= _nextCrystalCheck)
            {
                _nextCrystalCheck = Time.time + settings.progressSampleSeconds;
                RegrowCrystals();
            }

            if (IsServer && _hunter.IsArmed) _hunter.Tick();
        }

        /// <summary>Every clearing in world space, read off the arena's own environment.</summary>
        List<SpawnableJackstraw.Clearing> CollectClearings()
        {
            _clearings.Clear();
            var env = arenaCell ? arenaCell.GetComponentInChildren<SpawnableJackstraw>(true) : null;
            if (!env) env = FindAnyObjectByType<SpawnableJackstraw>();
            if (!env) return _clearings;
            env.CollectClearings(_clearings);
            for (int i = 0; i < _clearings.Count; i++)
            {
                var c = _clearings[i];
                _clearings[i] = new SpawnableJackstraw.Clearing(env.transform.TransformPoint(c.Centre),
                                                                c.Radius * env.transform.lossyScale.x);
            }
            return _clearings;
        }

        // ── The crystals in the clearings (every peer) ──

        void LayCrystals()
        {
            if (settings == null || settings.crystalsPerClearing <= 0) return;
            if (ElementalCrystalSetSO.Load() == null) return;   // no elemental set in this project state: none

            var clearings = CollectClearings();
            var rng = new System.Random(settings.crystalSeed);
            for (int c = 0; c < clearings.Count; c++)
            {
                var clearing = clearings[c];
                var centre = clearing.Centre;
                float reach = clearing.Radius * settings.clearingFill;
                for (int i = 0; i < settings.crystalsPerClearing; i++)
                {
                    var element = ElementalCrystalSetSO.RandomElementFrom(rng);
                    // Equal-volume scatter through the clearing's inner ball (cube root of a uniform draw).
                    float r = reach * Mathf.Pow((float)rng.NextDouble(), 1f / 3f);
                    float theta = (float)(rng.NextDouble() * Mathf.PI * 2f);
                    float y = (float)(rng.NextDouble() * 2.0 - 1.0);
                    float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                    var slot = new CrystalSlot
                    {
                        Position = centre + new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta)) * r,
                        Element = element,
                    };
                    _crystals.Add(slot);
                    Mint(slot);
                }
            }
        }

        void Mint(CrystalSlot slot)
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

        static bool IsLive(CrystalSlot slot) => slot.Crystal && slot.Impactor && !slot.Impactor.HasBeenCollected;

        void RegrowCrystals()
        {
            float now = Time.time;
            for (int i = 0; i < _crystals.Count; i++)
            {
                var slot = _crystals[i];
                if (IsLive(slot)) continue;
                if (slot.RegrowAt < 0f) { slot.RegrowAt = now + settings.crystalRegrowSeconds; continue; }
                if (now < slot.RegrowAt) continue;
                // A collected crystal destroys itself at the end of its capture; one still flying is left to finish.
                if (slot.Crystal) continue;
                Mint(slot);
            }
        }

        /// <summary>The nearest crystal still there to take, on this machine (the AI's refuel target).</summary>
        Vector3? NearestLiveCrystal(Vector3 from)
        {
            Vector3? best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _crystals.Count; i++)
            {
                var slot = _crystals[i];
                if (!IsLive(slot)) continue;
                float d = (slot.Position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = slot.Position;
            }
            return best;
        }

        void ClearCrystals()
        {
            for (int i = 0; i < _crystals.Count; i++)
                if (_crystals[i].Crystal) Destroy(_crystals[i].Crystal.gameObject);
            _crystals.Clear();
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
            var strikeArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                strikeArray[i] = statsList[i].StrikeHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray, strikeArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
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
                    CSDebug.LogError($"[Jackstraw] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // strikes as 0.
                stat.StrikeHitsLanded = strikes[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            _finalResultsSent = true;
            ClearCrystals();

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
            ClearCrystals();
            VesselCombatHitLatch.Clear();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.CombatPoints = 0;
                s.StrikeHitsLanded = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
