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
    /// Shatterplane - the Brittle Star's SPACE game: wide, thin round PLATES of neutral prisms hang in the cell
    /// (<see cref="SpawnableShatterplane"/>, the cell's own environment). Square up to a plate and the thrown ring's
    /// flat blast erases it whole; a few degrees off, it cuts one band across it; the guns chew what the ring misses.
    /// First DOMAIN to break the per-intensity target of hostile prisms wins, golf-timed, on Rampage's metric
    /// (<see cref="ScoringMetric.PrismsDestroyed"/> through <see cref="ShatterplaneScoringRuleSO"/>, resolved by
    /// <see cref="ShatterplanePrismTurnMonitor"/>). See SHATTERPLANE.md.
    ///
    /// <para><b>It changes nothing about the Brittle Star.</b> The mode's levers are the arena, the crystal field and
    /// the AI. The arena's NUCLEUS is one of them: the ring's blast is as wide as the nucleus of the cell it blows in,
    /// so the controller sizes this cell's nucleus to <see cref="ShatterplaneSettingsSO.nucleusRadius"/> (decision D2,
    /// a little bigger than a standard cell's) and clears <see cref="Cell.NucleusIsControlZone"/> (play geometry, not a
    /// claim - Wrecking Ball's rule), on every peer, so every peer's ring reaches as far.</para>
    ///
    /// <para><b>The crystal field is laid on every peer</b> at the arena's crystal sites
    /// (<see cref="SpawnableShatterplane.CollectCrystalSites"/>: on each plate's axis, in front of it), each site's
    /// element drawn from <see cref="ShatterplaneSettingsSO.fieldSeed"/>, and each collected crystal grows back in its
    /// own place after <see cref="ShatterplaneSettingsSO.fieldRegrowSeconds"/>. A crystal is a spare ring for the
    /// Brittle Star (every crystal list on its impactor container), and a pilot who takes one is already lined up
    /// square to the plate behind it. Collection is per-peer, as in Flatline, which is tolerable because a crystal
    /// scores nothing.</para>
    ///
    /// <para><b>Per intensity</b> (<see cref="ShatterplaneSettingsSO.fieldElementByIntensity"/>,
    /// <see cref="ShatterplaneSettingsSO.fieldPerTeamByIntensity"/>): I3 lays SPACE crystals only, one per team present at
    /// every site, side by side across the plate's axis, each tinted to its team and collectable by it alone
    /// (<see cref="ElementalCrystalImpactor.RestrictTo"/>, the opt-in gate Harvest's I3 added) - so the race to a plate is never a race to deny another team its
    /// ring. The AI forages only crystals its own team may take. I4's live elemental fauna are the CELL's (its spawn
    /// profile), never this controller's.</para>
    ///
    /// <para>Structurally <see cref="FlatlineController"/> for the field and <see cref="SiroccoController"/> for the
    /// prism race (1 round / 1 turn, HasEndGame=false, server winner detection, a snapshot SyncFinalScores_ClientRpc
    /// carrying the breakdown). BRITTLE-STAR-ONLY is enforced by the arcade card's Vessels list.</para>
    /// </summary>
    public class ShatterplaneController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag ShatterplaneSettings.asset - the nucleus, the crystal field and the AI. The prism target lives in " +
                 "EndConditionOverridesSO (per intensity), resolved by ShatterplanePrismTurnMonitor.")]
        [SerializeField] ShatterplaneSettingsSO settings;

        [Tooltip("Drag ShatterplaneScoringRule.asset (metric = PrismsDestroyed, Rampage's rule).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Shatterplane cell. Its nucleus is sized to the settings' nucleus radius (the ring's reach); its " +
                 "SpawnableShatterplane answers where the plates and the crystals are.")]
        [SerializeField] Cell arenaCell;

        sealed class FieldSlot
        {
            public Vector3 Position;
            public Element Element;
            /// <summary>The team this crystal belongs to (only it can collect it), or Blue for anyone's.</summary>
            public Domains Owner = Domains.Blue;
            public Crystal Crystal;
            public ElementalCrystalImpactor Impactor;
            public float RegrowAt = -1f;
        }

        readonly List<FieldSlot> _field = new();
        readonly List<ShatterplaneSquarer.PlateFace> _plates = new();
        readonly ShatterplaneSquarer _squarer = new();
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

            ApplyNucleus();

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            _squarer.Disarm();
            ClearField();
            base.OnNetworkDespawn();
        }

        /// <summary>Every peer: the arena's nucleus is the ring's reach. A radius asked for before the cell builds its
        /// nucleus is honoured when it does (<see cref="Cell.SetNucleusWorldRadius"/>).</summary>
        void ApplyNucleus()
        {
            if (!arenaCell || settings == null) return;
            arenaCell.SetNucleusWorldRadius(settings.nucleusRadius);
            arenaCell.NucleusIsControlZone = false;
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
            // The field is LOCAL, laid identically on every peer, so this runs before the server gate.
            ClearField();
            LayField();

            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[ShatterplaneController] no settings asset wired; the AI squarers are not armed " +
                                   "and fly the platform's own steering.");
                return;
            }
            CollectPlates();
            _squarer.Arm(gameData, settings, Centre, _plates, PlateRadius(), NearestLiveCrystal);
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
                if (local != null) GameToastAPI.Post(GameToastSituation.ShatterplaneHint, local.Domain);
            }

            if (settings != null && Time.time >= _nextFieldCheck)
            {
                _nextFieldCheck = Time.time + settings.progressSampleSeconds;
                RegrowField();
            }

            if (IsServer && _squarer.IsArmed) _squarer.Tick();
        }

        // ── The arena, read off its environment ──

        SpawnableShatterplane Environment()
        {
            var env = arenaCell ? arenaCell.GetComponentInChildren<SpawnableShatterplane>(true) : null;
            if (!env) env = FindAnyObjectByType<SpawnableShatterplane>();
            return env;
        }

        float PlateRadius()
        {
            var env = Environment();
            return env ? env.PlateWorldRadius : 0f;
        }

        readonly List<ShatterplanePlates.Structure> _structureScratch = new();

        /// <summary>Every plate in world space, front plate first within a stack (the face a square ring is thrown at;
        /// once it is gone the one behind it is the next face).</summary>
        void CollectPlates()
        {
            _plates.Clear();
            var env = Environment();
            if (!env) return;
            env.CollectStructures(_structureScratch);
            float front = env.FrontOffset;
            int plates = env.PlatesPerStack;
            var tf = env.transform;
            for (int i = 0; i < _structureScratch.Count; i++)
            {
                var st = _structureScratch[i];
                var axis = tf.TransformDirection(st.N).normalized;
                for (int p = 0; p < plates; p++)
                    _plates.Add(new ShatterplaneSquarer.PlateFace(
                        tf.TransformPoint(st.Centre + st.N * (front - p * env.ShelfGap)), axis));
            }
            _structureScratch.Clear();
        }

        readonly List<Vector3> _siteScratch = new();
        readonly List<Vector3> _axisScratch = new();
        readonly List<Domains> _teamScratch = new();

        /// <summary>The crystal sites in world space, read off the arena's own environment, with each site's plate axis
        /// in <see cref="_axisScratch"/>. Empty when the cell has no SpawnableShatterplane, and then there is no field.</summary>
        List<Vector3> CollectSites()
        {
            _siteScratch.Clear();
            _axisScratch.Clear();
            var env = Environment();
            if (!env) return _siteScratch;
            env.CollectCrystalSites(_siteScratch, _axisScratch);
            for (int i = 0; i < _siteScratch.Count; i++)
            {
                _siteScratch[i] = env.transform.TransformPoint(_siteScratch[i]);
                if (i < _axisScratch.Count) _axisScratch[i] = env.transform.TransformDirection(_axisScratch[i]).normalized;
            }
            return _siteScratch;
        }

        /// <summary>The teams present in this match, in enum order, so every peer lists them alike (Blue is nobody's).</summary>
        List<Domains> TeamsPresent()
        {
            _teamScratch.Clear();
            var players = gameData != null ? gameData.Players : null;
            if (players == null) return _teamScratch;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p.Domain == Domains.Blue || _teamScratch.Contains(p.Domain)) continue;
                _teamScratch.Add(p.Domain);
            }
            _teamScratch.Sort();
            return _teamScratch;
        }

        int Intensity => gameData != null && gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1;

        // ── The crystal field (every peer) ──

        void LayField()
        {
            if (settings == null) return;
            if (ElementalCrystalSetSO.Load() == null) return;   // no elemental set in this project state: no field

            var sites = CollectSites();
            var rng = new System.Random(settings.fieldSeed);
            int intensity = Intensity;
            var fixedElement = settings.FieldElementFor(intensity);
            // Per team (I3): one crystal per team present at every site, side by side across the plate's axis, each
            // collectable only by its team - so a team that reaches a plate first cannot take the others' rings.
            var teams = settings.FieldPerTeamFor(intensity) ? TeamsPresent() : null;
            int perSite = teams is { Count: > 0 } ? teams.Count : 1;
            for (int i = 0; i < sites.Count; i++)
            {
                Vector3 across = Vector3.zero;
                if (perSite > 1 && i < _axisScratch.Count)
                {
                    ShatterplanePlates.Basis(_axisScratch[i], out var u, out _);
                    across = u;
                }
                for (int t = 0; t < perSite; t++)
                {
                    var element = fixedElement != Element.None ? fixedElement : ElementalCrystalSetSO.RandomElementFrom(rng);
                    var slot = new FieldSlot
                    {
                        Position = sites[i] + across * ((t - (perSite - 1) * 0.5f) * settings.teamCrystalSpacing),
                        Element = element,
                        Owner = teams is { Count: > 0 } ? teams[t] : Domains.Blue,
                    };
                    _field.Add(slot);
                    Mint(slot);
                }
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
            // A team's crystal: tinted to the team and skimmed up by it alone (the opt-in gate Harvest's I3 added).
            if (slot.Owner != Domains.Blue)
            {
                impactor.RestrictTo(slot.Owner);
                crystal.ApplyDomainPreview(slot.Owner);
            }
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

        /// <summary>The nearest crystal of the field still there for <paramref name="team"/> to take, on this machine (the
        /// AI's re-arm target): anyone's crystals and the team's own, never another team's.</summary>
        Vector3? NearestLiveCrystal(Vector3 from, Domains team)
        {
            Vector3? best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _field.Count; i++)
            {
                var slot = _field[i];
                if (!IsLive(slot)) continue;
                if (slot.Owner != Domains.Blue && slot.Owner != team) continue;
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
                .OrderByDescending(s => s.HostilePrismsDestroyed)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            _squarer.Disarm();
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
                    CSDebug.LogError($"[Shatterplane] Client could not match RoundStats for '{sName}'. " +
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

            _squarer.Disarm();
            ClearField();

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
