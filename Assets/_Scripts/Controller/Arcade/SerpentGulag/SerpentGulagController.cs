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
    /// Serpent Gulag - the Serpent's CHARGE game: a SNIPER DUEL at range. Pilots spawn far apart in a 3,600 u
    /// cell; a field of elemental crystals floats in the middle of it. A SNIPE - a Sniper Shot that strips a rival
    /// pilot - is one point, and the first DOMAIN to the target wins (golf-timed). On the Serpent, Charge is the
    /// rifle's recovery (12 s at rest, 5.4 s at Charge 10), so the field is where a sniper reloads faster: in the
    /// open, in every rival's sights. Intensity 1 is open space; 2-4 add sniper nests and barred cell blocks
    /// (<see cref="SpawnableSerpentGulag"/>, the cell's own environment). See SERPENTGULAG.md.
    ///
    /// <para>Structurally a sibling of <see cref="DustupController"/> (1 round / 1 turn, HasEndGame=false, server
    /// winner detection, a snapshot SyncFinalScores_ClientRpc carrying the breakdown, the hit latch cleared per
    /// match). It changes nothing about the Serpent: the rifle already reports a snipe as a Debuff-class combat
    /// hit, and <see cref="SerpentGulagScoringRuleSO"/> is the rule that pays for it.</para>
    ///
    /// <para><b>The crystal field is laid on every peer</b> from <see cref="SerpentGulagSettingsSO.fieldSeed"/>
    /// (the Dog Fight recipe: the elemental set's prefabs provisioned with a collector), and each collected crystal
    /// grows back in its own place after <see cref="SerpentGulagSettingsSO.fieldRegrowSeconds"/>. Collection is
    /// per-peer, as it is in Dog Fight, which is tolerable because a crystal scores nothing; it only feeds an
    /// element.</para>
    ///
    /// SERPENT-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class SerpentGulagController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag SerpentGulagSettings.asset - the crystal field and the AI marksmen. The snipe target lives " +
                 "in EndConditionOverridesSO, resolved by SerpentGulagSnipeTurnMonitor; the snipe VALUE on the rule.")]
        [SerializeField] SerpentGulagSettingsSO settings;

        [Tooltip("Drag SerpentGulagScoringRule.asset (metric = CombatPoints, a snipe = Debuff).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Gulag cell. Read-only to this controller: its position is the arena CENTRE, where the crystal " +
                 "field floats.")]
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
        readonly SerpentGulagMarksman _marksman = new();
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
            // otherwise inherit a claimed window and eat the first snipe.
            VesselCombatHitLatch.Clear();

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            _marksman.Disarm();
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
                list[i].DebuffHitsLanded = 0;
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
                CSDebug.LogWarning("[SerpentGulagController] no settings asset wired; the AI marksmen are not armed " +
                                   "and fly the platform's own steering.");
                return;
            }
            _marksman.Arm(gameData, settings, Centre, NearestLiveCrystal);
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
                if (local != null) GameToastAPI.Post(GameToastSituation.SerpentGulagScopeHint, local.Domain);
            }

            if (settings != null && Time.time >= _nextFieldCheck)
            {
                _nextFieldCheck = Time.time + settings.progressSampleSeconds;
                RegrowField();
            }

            if (IsServer && _marksman.IsArmed) _marksman.Tick();
        }

        // ── The crystal field (every peer) ──

        void LayField()
        {
            if (settings == null || settings.fieldCrystalCount <= 0) return;
            if (ElementalCrystalSetSO.Load() == null) return;   // no elemental set in this project state: no field

            var rng = new System.Random(settings.fieldSeed);
            Vector3 centre = Centre;
            for (int i = 0; i < settings.fieldCrystalCount; i++)
            {
                var element = ElementalCrystalSetSO.RandomElementFrom(rng);
                // Equal-volume scatter through the sphere (cube root of a uniform draw), so the field is evenly
                // filled rather than bunched at the middle.
                float r = settings.fieldRadius * Mathf.Pow((float)rng.NextDouble(), 1f / 3f);
                float theta = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float y = (float)(rng.NextDouble() * 2.0 - 1.0);
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var slot = new FieldSlot
                {
                    Position = centre + new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta)) * r,
                    Element = element,
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
            _marksman.Disarm();
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
            var snipeArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                snipeArray[i] = statsList[i].DebuffHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray, snipeArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] snipes,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[SerpentGulag] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // snipes as 0.
                stat.DebuffHitsLanded = snipes[i];
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

            _marksman.Disarm();
            ClearField();
            VesselCombatHitLatch.Clear();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.CombatPoints = 0;
                s.DebuffHitsLanded = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
