using System.Collections.Generic;
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
    /// Serpent Charge - the Serpent's CHARGE game: a sniper race through Rampage's cactus forest.
    /// On the Serpent, CHARGE is the rifle's RECOVERY - the Sniper Shot waits 12 s at rest and
    /// 5.4 s at Charge 10 - and the round itself is absolute: one hitscan cone, 1.5° wide and 3,000
    /// units long, that destroys EVERYTHING hostile it contains (the round pierces by default) and
    /// strips any rival pilot inside its reach. So the mode is: hold the scope, put the reticle
    /// down the LENGTH of a stand rather than across it, fire, and spend the cooldown collecting
    /// Charge so the next round comes sooner. First DOMAIN to the target wins, on Rampage's metric
    /// with sniped pilots folded in (<see cref="SerpentChargeScoringRuleSO"/>).
    ///
    /// <para><b>The arena is Rampage's, referenced read-only</b> - the four cactus-forest configs
    /// The Bends, Bloomrush and Sirocco reference, and its intensity ladder comes with it (the
    /// forest's DENSITY). A forest is exactly what a piercing round wants: a belt of breakable
    /// stands, a third of them in each colour, where the value of a shot is entirely in its
    /// ANGLE.</para>
    ///
    /// <para><b>Charge crystals are the economy</b> - <see cref="SerpentChargeSettingsSO.chargeCrystalsPerWave"/>
    /// elemental CHARGE crystals, re-laid every <see cref="SerpentChargeSettingsSO.chargeCrystalWaveSeconds"/>.
    /// They are LOCAL objects laid identically on every peer: the server broadcasts only a WAVE
    /// INDEX (<see cref="LayChargeWave_ClientRpc"/>) and each peer derives the layout from
    /// (seed, wave), the Salvo/Dog Fight recipe with the one change a match-long economy needs -
    /// a wave clears and replaces the last one, so per-peer collection state never has to agree
    /// and never accumulates drift. Collection is per-peer for the same reason it is in those
    /// modes; it is tolerable because a crystal scores nothing, it only shortens a cooldown that
    /// is itself owner-authoritative (SniperShotActionExecutor's documented limitation).</para>
    ///
    /// <para><b>The AI is a marksman</b> (<see cref="SerpentMarksmanAI"/>): every AI Serpent holds
    /// its scope, plans the fattest line it can bring to bear, steers down it, and fires through
    /// the replicated press when the rifle is ready and the live line is worth it; from intensity
    /// 2 it hunts rival pilots, and on a long cooldown it detours to Charge crystals. Every one of
    /// those is an intensity ladder on the settings asset - the card's "challenging" is the
    /// player's own dial.</para>
    ///
    /// Structurally <see cref="SiroccoController"/>'s sibling (1 round / 1 turn, HasEndGame=false,
    /// server winner detection, snapshot SyncFinalScores_ClientRpc carrying the score AND both
    /// halves of its breakdown). SERPENT-ONLY is enforced by the arcade card's Vessels list.
    /// </summary>
    public class SerpentChargeController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag SerpentChargeSettings.asset - the Charge-crystal economy and the marksman " +
                 "AI ladders. The prism target lives in EndConditionOverridesSO, resolved by " +
                 "SerpentChargePrismTurnMonitor.")]
        [SerializeField] SerpentChargeSettingsSO settings;

        [Tooltip("Drag SerpentChargeScoringRule.asset (metric = PrismsDestroyed, sniped pilots " +
                 "folded in at PilotSnipePoints).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The cactus-forest cell. Read-only to this controller: its centre anchors the " +
                 "Charge-crystal scatter, and its targeting grids answer 'where is the densest " +
                 "hostile mass' for the marksman AI's line sampling.")]
        [SerializeField] Cell arenaCell;

        readonly SerpentMarksmanAI _marksmen = new();
        readonly List<ElementalCrystalImpactor> _chargeCrystals = new();

        bool _finalResultsSent;
        bool _wavesRunning;
        int _wave;
        float _nextWaveAt;

        // Golf: winners carry their finish time, losers a DnfThreshold+remaining sentinel
        // (see RampageScoringRuleSO.AssignScores) - lower is better.
        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // End-game runs through OnTurnEndedCustom (server-side winner detection) →
        // SyncFinalScores_ClientRpc. Suppress the base turn→round→game flow.
        protected override bool HasEndGame => false;

        // The race beats as toasts (quarter, halfway, lead change, home stretch). A local poll over
        // the replicated folded score, run on every peer - see DomainRaceToasts. Feedback only.
        DomainRaceToasts _raceToasts;

        void Update()
        {
            if (_finalResultsSent || rule == null) return;
            _raceToasts ??= new DomainRaceToasts(rule);
            _raceToasts.Tick(gameData);

            if (!IsServer) return;

            _marksmen.Tick();

            if (_wavesRunning && settings != null && Time.time >= _nextWaveAt)
            {
                _nextWaveAt = Time.time + settings.chargeCrystalWaveSeconds;
                LayChargeWave_ClientRpc(++_wave);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _wavesRunning = false;
            _wave = 0;

            // Belt-and-braces against the Cleave regression (a match starting on a non-zero
            // score): RoundStats lives on the PERSISTENT Player object.
            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopMatchSystems();
            ClearChargeCrystals();
            base.OnNetworkDespawn();
        }

        /// <summary>Server-only: the setters push through server-write NetworkVariables. Both
        /// halves of the folded score, and the raw snipe count the results line reads.</summary>
        void ZeroCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].HostilePrismsDestroyed = 0;
                list[i].CombatPoints = 0;
                list[i].DebuffHitsLanded = 0;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            // The last moment before anyone can score - a late joiner is on the roster by now.
            ZeroCounters();

            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            // Wave 0 now, then one every chargeCrystalWaveSeconds (Update).
            _wave = 0;
            _wavesRunning = settings != null && settings.chargeCrystalsPerWave > 0;
            if (_wavesRunning)
            {
                _nextWaveAt = Time.time + settings.chargeCrystalWaveSeconds;
                LayChargeWave_ClientRpc(_wave);
            }

            ArmMarksmen();
        }

        void StopMatchSystems()
        {
            _wavesRunning = false;
            _marksmen.Disarm();
        }

        // ── AI (server): marksmen ───────────────────────────────────────────

        void ArmMarksmen()
        {
            // The marksman reads settings on every tick; without it the bots would fly the
            // platform's own crystal seeking and never raise a scope. Loud, not silent.
            if (settings == null)
            {
                CSDebug.LogWarning("[SerpentChargeController] no settings asset wired; the marksman AI " +
                                   "is not armed and AI Serpents cannot fire their rifles.");
                return;
            }
            _marksmen.Arm(gameData, settings, rule, arenaCell, () => _chargeCrystals);
        }

        // ── Charge crystals (local objects, laid per wave on every peer) ────

        /// <summary>
        /// Clears the previous wave and lays wave <paramref name="wave"/> on EVERY peer (the host's
        /// client half included, which is where the AI's crystals live). The layout is a pure
        /// function of (seed, wave), so nothing but the index crosses the wire.
        /// </summary>
        [ClientRpc]
        void LayChargeWave_ClientRpc(int wave)
        {
            ClearChargeCrystals();
            if (_finalResultsSent || settings == null || settings.chargeCrystalsPerWave <= 0) return;

            var set = ElementalCrystalSetSO.Load();
            if (set == null) return;
            var prefab = set.GetPrefab(Element.Charge);
            if (prefab == null) return;

            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;
            var rng = new System.Random(unchecked(settings.crystalScatterSeed * 1009 + wave));
            float inner = Mathf.Min(settings.crystalScatterInnerRadius, settings.crystalScatterOuterRadius);
            float outer = settings.crystalScatterOuterRadius;
            float inner3 = inner * inner * inner, outer3 = outer * outer * outer;

            for (int i = 0; i < settings.chargeCrystalsPerWave; i++)
            {
                // Equal-volume scatter through the shell (cube root of a uniform draw over r^3).
                float r = Mathf.Pow(inner3 + (float)rng.NextDouble() * (outer3 - inner3), 1f / 3f);
                float theta = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float y = (float)(rng.NextDouble() * 2.0 - 1.0);
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var pos = centre + new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta)) * r;

                // Runtime provisioning mirrors SalvoController.SpawnElementalCrystals (and
                // Microscene.MintElementalCrystal) - the standalone elemental prefabs deliberately
                // carry no collection components.
                var crystal = Instantiate(prefab, pos, Quaternion.identity);
                crystal.transform.localScale *= (float)(rng.NextDouble() * 0.7 + 0.5);
                crystal.enabled = true;
                crystal.gameObject.SetActive(true);

                var impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
                impactor.Crystal = crystal;
                if (set.CollectionEffects is { Length: > 0 })
                    impactor.SetCollectionEffects(set.CollectionEffects);
                crystal.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);

                _chargeCrystals.Add(impactor);
            }
        }

        void ClearChargeCrystals()
        {
            for (int i = 0; i < _chargeCrystals.Count; i++)
                if (_chargeCrystals[i]) Destroy(_chargeCrystals[i].gameObject);
            _chargeCrystals.Clear();
        }

        // ── Server-authoritative game end (the Rampage shape) ───────────────

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
                .OrderByDescending(s => s.HostilePrismsDestroyed + s.CombatPoints)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            StopMatchSystems();
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        /// <summary>Suppress the base flow's SetupNewRound when the race just ended - prevents the
        /// Ready button from reappearing (HasEndGame=false routes round end back through here).</summary>
        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        // ── Score sync ──────────────────────────────────────────────────────

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var prismsArray = new int[count];
            var combatArray = new int[count];
            var snipesArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                prismsArray[i] = statsList[i].HostilePrismsDestroyed;
                combatArray[i] = statsList[i].CombatPoints;
                snipesArray[i] = statsList[i].DebuffHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, prismsArray, combatArray,
                snipesArray, new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        /// <summary>The final snapshot carries the score AND its breakdown - a client that only
        /// replicated the total would show every loser's prisms and snipes as 0.</summary>
        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] prismsDestroyed,
            int[] combatPoints,
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
                    CSDebug.LogError($"[SerpentCharge] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.HostilePrismsDestroyed = prismsDestroyed[i];
                stat.CombatPoints = combatPoints[i];
                stat.DebuffHitsLanded = snipes[i];
            }

            ClearChargeCrystals();

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // ── Replay ──────────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            _wave = 0;

            StopMatchSystems();
            ClearChargeCrystals();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.HostilePrismsDestroyed = 0;
                s.CombatPoints = 0;
                s.DebuffHitsLanded = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
