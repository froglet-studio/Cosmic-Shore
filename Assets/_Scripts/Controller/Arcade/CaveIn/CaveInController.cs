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
    /// Cave-in - the Meerkat's SPACE game, in Shatterplane's cell of hanging neutral PLATES (read-only: the four
    /// Shatterplane cell configs and their <see cref="SpawnableShatterplane"/> environments). The Meerkat breaks nothing
    /// by flying into a plate: a soliton formed beside one SWALLOWS the prisms around it (<c>MeerkatSwallow</c>) and
    /// fires them out of its way out, and each fired prism that runs into another destroys both, credited to the
    /// soliton's maker. Tunnel into a plate and what comes out the far end caves in the plate behind it. First DOMAIN to
    /// the target of hostile prisms wins, golf-timed, on Rampage's metric (<see cref="ScoringMetric.PrismsDestroyed"/>
    /// through <see cref="CaveInScoringRuleSO"/>, resolved by <see cref="CaveInPrismTurnMonitor"/>). See CAVEIN.md.
    ///
    /// <para><b>Nothing about the Meerkat changes</b> (the LOCKED vessel rule), and nothing about Shatterplane's arena:
    /// this controller neither resizes the nucleus nor lays Shatterplane's crystal field (both are the Brittle Star's
    /// ring's levers); the omni crystals are the scene's CrystalManager's. The mode's levers are the plates (one
    /// layout per intensity) and the AI miners (<see cref="MeerkatArcadeSteer"/>: steering only).</para>
    ///
    /// <para>Structurally <see cref="ShatterplaneController"/> without the field and the nucleus (1 round / 1 turn,
    /// HasEndGame=false, server winner detection, a snapshot SyncFinalScores_ClientRpc carrying the breakdown).
    /// MEERKAT-ONLY is enforced by the arcade card's Vessels list.</para>
    /// </summary>
    public class CaveInController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag CaveInSettings.asset - the AI miners. The prism target lives in EndConditionOverridesSO, resolved " +
                 "by CaveInPrismTurnMonitor.")]
        [SerializeField] CaveInSettingsSO settings;

        [Tooltip("Drag CaveInScoringRule.asset (metric = PrismsDestroyed, Rampage's rule).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Shatterplane cell. Read-only to this controller: its SpawnableShatterplane answers where the " +
                 "plates are.")]
        [SerializeField] Cell arenaCell;

        /// <summary>One plate's face in world space: its centre, its axis and its radius.</summary>
        readonly struct Plate
        {
            public readonly Vector3 Centre, Axis, U, W;
            public readonly float Radius;

            public Plate(Vector3 centre, Vector3 axis, Vector3 u, Vector3 w, float radius)
            {
                Centre = centre;
                Axis = axis;
                U = u;
                W = w;
                Radius = radius;
            }
        }

        readonly List<Plate> _plates = new();
        readonly MeerkatArcadeSteer _steer = new();
        DomainRaceToasts _raceToasts;
        bool _finalResultsSent;
        bool _hintPosted;

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

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            _steer.Disarm();
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
                list[i].HostilePrismsDestroyed = 0;
                list[i].HostileVolumeDestroyed = 0f;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[CaveInController] no settings asset wired; the AI miners are not armed and fly " +
                                   "the platform's own steering.");
                return;
            }
            CollectPlates();
            _steer.Arm(gameData, (int)GameModes.CaveIn, Centre, PickTunnel,
                s => MeerkatArcadeSteer.Rung(settings.aiRetargetSeconds, s.Level));
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
                if (local != null) GameToastAPI.Post(GameToastSituation.CaveInHint, local.Domain);
            }

            if (IsServer && _steer.IsArmed) _steer.Tick();
        }

        // ── The arena, read off its environment ──

        SpawnableShatterplane Environment()
        {
            var env = arenaCell ? arenaCell.GetComponentInChildren<SpawnableShatterplane>(true) : null;
            if (!env) env = FindAnyObjectByType<SpawnableShatterplane>();
            return env;
        }

        readonly List<ShatterplanePlates.Structure> _structureScratch = new();

        /// <summary>Every breakable plate in world space (a stack's plates one by one; the super-shielded screens are
        /// skipped - they never break).</summary>
        void CollectPlates()
        {
            _plates.Clear();
            var env = Environment();
            if (!env) return;
            env.CollectStructures(_structureScratch);
            float front = env.FrontOffset;
            int plates = env.PlatesPerStack;
            float radius = env.PlateWorldRadius;
            var tf = env.transform;
            for (int i = 0; i < _structureScratch.Count; i++)
            {
                var st = _structureScratch[i];
                if (st.Screen) continue;
                var axis = tf.TransformDirection(st.N).normalized;
                var u = tf.TransformDirection(st.U).normalized;
                var w = tf.TransformDirection(st.W).normalized;
                for (int p = 0; p < plates; p++)
                    _plates.Add(new Plate(tf.TransformPoint(st.Centre + st.N * (front - p * env.ShelfGap)), axis, u, w,
                                          radius));
            }
            _structureScratch.Clear();
        }

        /// <summary>
        /// Where a miner goes next. By the rung's chance, a RAID behind the nearest rival (their trail is hostile mass
        /// too). Otherwise an omni crystal within reach, or a fresh point on the face of the plate it is mining
        /// (<see cref="MeerkatArcadeSteer.Seat.StateA"/>), so its solitons form against the plate and swallow it. After
        /// the rung's number of choices it moves on (<see cref="MeerkatArcadeSteer.Seat.StateB"/> counts them): by the
        /// rung's chance to the plate LINED UP behind the last one, the way its fired prisms were going, else to the
        /// nearest other plate. Steering only.
        /// </summary>
        Vector3 PickTunnel(MeerkatArcadeSteer.Seat seat, Transform self)
        {
            int level = seat.Level;
            float error = MeerkatArcadeSteer.Rung(settings.aiAimError, level);
            var rival = _steer.NearestRival(seat, self.position, MeerkatArcadeSteer.Rung(settings.aiRaidRange, level));
            if (rival != null && seat.Rng.NextDouble() < MeerkatArcadeSteer.Rung(settings.aiRaidChance, level))
                return MeerkatArcadeSteer.Jitter(seat,
                    MeerkatArcadeSteer.Led(rival, -MeerkatArcadeSteer.Rung(settings.aiRaidBehindSeconds, level)), error);

            if (MeerkatArcadeSteer.NearestOmniCrystal(self.position) is Vector3 crystal &&
                Vector3.Distance(crystal, self.position) <= settings.aiCrystalReach)
                return crystal;

            if (_plates.Count == 0) return MeerkatArcadeSteer.Jitter(seat, Centre, error);

            int perPlate = Mathf.Max(1, Mathf.RoundToInt(MeerkatArcadeSteer.Rung(settings.aiChoicesPerPlate, level)));
            if (seat.StateA < 0 || seat.StateA >= _plates.Count || seat.StateB >= perPlate)
            {
                bool lineUp = seat.StateA >= 0 && seat.StateA < _plates.Count &&
                              seat.Rng.NextDouble() < MeerkatArcadeSteer.Rung(settings.aiLineUpChance, level);
                seat.StateA = lineUp ? LinedUpBehind(seat.StateA, self.position) : NearestPlate(seat.StateA, self.position);
                seat.StateB = 0;
            }
            seat.StateB++;

            var plate = _plates[seat.StateA];
            float r = plate.Radius * settings.aiFaceSpread * Mathf.Sqrt((float)seat.Rng.NextDouble());
            float a = (float)(seat.Rng.NextDouble() * Mathf.PI * 2.0);
            var onFace = plate.Centre + (plate.U * Mathf.Cos(a) + plate.W * Mathf.Sin(a)) * r;
            return MeerkatArcadeSteer.Jitter(seat, onFace, error);
        }

        /// <summary>The nearest plate to <paramref name="from"/> other than <paramref name="skip"/>.</summary>
        int NearestPlate(int skip, Vector3 from)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _plates.Count; i++)
            {
                if (i == skip && _plates.Count > 1) continue;
                float d = (_plates[i].Centre - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = i;
            }
            return Mathf.Max(0, best);
        }

        /// <summary>The plate within range most nearly straight on from <paramref name="from"/> through plate
        /// <paramref name="last"/> - the way a soliton fired into <paramref name="last"/> sends what it swallows.</summary>
        int LinedUpBehind(int last, Vector3 from)
        {
            var through = _plates[last].Centre - from;
            if (through.sqrMagnitude < 1e-4f) return NearestPlate(last, from);
            through.Normalize();
            int best = -1;
            float bestCos = 0.5f;   // within 60 degrees of straight on, or not lined up at all
            for (int i = 0; i < _plates.Count; i++)
            {
                if (i == last) continue;
                var to = _plates[i].Centre - _plates[last].Centre;
                float d = to.magnitude;
                if (d < 1e-3f || d > settings.aiPlateRange) continue;
                float cos = Vector3.Dot(to / d, through);
                if (cos <= bestCos) continue;
                bestCos = cos;
                best = i;
            }
            return best >= 0 ? best : NearestPlate(last, from);
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
            _steer.Disarm();
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
                    CSDebug.LogError($"[CaveIn] Client could not match RoundStats for '{sName}'. " +
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

            _steer.Disarm();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.HostilePrismsDestroyed = 0;
                s.HostileVolumeDestroyed = 0f;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
