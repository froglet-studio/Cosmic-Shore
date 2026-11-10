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
    /// Hood - the Serpent's CHARGE game: a SNIPER DUEL across a crystal heart (Shombith, 2026-11-09; it replaced Serpent
    /// Gulag in place, same <see cref="GameModes"/> id 77). Pilots spawn far apart on a 2,200 u sphere; a heart of
    /// elemental crystals floats in the middle; round it, on a sphere, stand the PERCHES - Serpentine's ring shapes bent
    /// into hoods (<see cref="SerpentHoodLayout"/>), every mouth turned to the heart. A sniper sits in a hood's cup and
    /// fires out through its exits. A SNIPE - a Sniper Shot that strips a rival pilot - is one point, and the first
    /// DOMAIN to the target wins (golf-timed). On the Serpent, Charge is the rifle's recovery (12 s at rest, 5.4 s at
    /// Charge 10), so the heart is where a sniper reloads faster: in the open, in every perch's sights. See SERPENTHOOD.md.
    ///
    /// <para><b>The hoods are laid on every peer</b> from the intensity alone (closed form, Serpentine's recipe and its
    /// owner id rule), behind the connecting panel. <b>Break a hood's core</b> (a sniper round, a ram; the vessel is
    /// unchanged, these are ordinary prisms) and the whole hood falls apart, core outward, flushing whoever hid in it;
    /// it grows back <see cref="SerpentHoodSettingsSO.hoodRegrowSeconds"/> later once no hull is inside its reach - the
    /// rule <c>SerpentTimeController.TickShapes</c> runs at Serpentine's rings, here at the perches.</para>
    ///
    /// <para>Structurally a sibling of <see cref="DustupController"/> (1 round / 1 turn, HasEndGame=false, server
    /// winner detection, a snapshot SyncFinalScores_ClientRpc carrying the breakdown, the hit latch cleared per
    /// match). It changes nothing about the Serpent: the rifle already reports a snipe as a Debuff-class combat
    /// hit, and <see cref="SerpentHoodScoringRuleSO"/> is the rule that pays for it.</para>
    ///
    /// <para><b>The crystal heart is laid on every peer</b>, its slots closed form (<see cref="SerpentHoodLayout.FieldSlot"/>)
    /// and its elements from <see cref="SerpentHoodSettingsSO.fieldSeed"/> (the Dog Fight recipe: the elemental set's
    /// prefabs provisioned with a collector); each collected crystal grows back in its own place after
    /// <see cref="SerpentHoodSettingsSO.fieldRegrowSeconds"/>. Collection is per-peer, as in Dog Fight, which is
    /// tolerable because a crystal scores nothing; it only feeds an element.</para>
    ///
    /// SERPENT-ONLY is enforced by the arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public class SerpentHoodController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag SerpentHoodSettings.asset - the crystal heart, the hoods' fall and regrowth, and the AI marksmen. The snipe target lives " +
                 "in EndConditionOverridesSO, resolved by SerpentHoodSnipeTurnMonitor; the snipe VALUE on the rule.")]
        [SerializeField] SerpentHoodSettingsSO settings;

        [Tooltip("Drag SerpentHoodScoringRule.asset (metric = CombatPoints, a snipe = Debuff).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Hood cell. Read-only to this controller: its position is the arena CENTRE, where the crystal " +
                 "heart floats and the hoods stand round.")]
        [SerializeField] Cell arenaCell;

        [Tooltip("The prism every hood is laid from (SpawnablePrism, as Serpentine's shapes are).")]
        [SerializeField] Prism hoodPrismPrefab;

        sealed class FieldSlot
        {
            public Vector3 Position;
            public Element Element;
            public Crystal Crystal;
            public ElementalCrystalImpactor Impactor;
            public float RegrowAt = -1f;
        }

        /// <summary>One hood: where it stands, its prisms in Serpentine's order (core first), and its fall / regrowth.</summary>
        sealed class Hood
        {
            public SerpentHoodLayout.Cover Cover;
            public float Reach;
            public readonly List<SerpentineGate.Part> Parts = new();
            public readonly List<Prism> Prisms = new();
            public Trail Trail;
            public bool Standing;
            public bool Bursting;
            public bool Regrowing;
            public int Cursor;
            public float RegrowAt = -1f;
        }

        /// <summary>The owner id every hood prism carries: a prism still carrying it (alive) is still its hood's.</summary>
        const string HoodOwnerId = "SerpentHood";

        readonly List<FieldSlot> _field = new();
        readonly List<Hood> _hoods = new();
        readonly List<SerpentHoodLayout.Cover> _coverScratch = new();
        Transform _hoodRoot;
        readonly SerpentHoodMarksman _marksman = new();
        DomainRaceToasts _raceToasts;
        bool _finalResultsSent;
        bool _hintPosted;
        float _nextFieldCheck;

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;
        protected override bool HasEndGame => false;

        Vector3 Centre => arenaCell ? arenaCell.transform.position : Vector3.zero;

        int Intensity => gameData != null && gameData.SelectedIntensity != null
            ? Mathf.Clamp(gameData.SelectedIntensity.Value, 1, 4)
            : 1;

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

            // The hoods are LOCAL, laid identically on every peer from the intensity, behind the connecting panel.
            LayHoods();
        }

        public override void OnNetworkDespawn()
        {
            _marksman.Disarm();
            ClearField();
            ClearHoods();
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
                CSDebug.LogWarning("[SerpentHoodController] no settings asset wired; the AI marksmen are not armed " +
                                   "and fly the platform's own steering.");
                return;
            }
            _marksman.Arm(gameData, settings, Centre, NearestLiveCrystal, CollectPerches(), IsStanding);
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
                if (local != null) GameToastAPI.Post(GameToastSituation.SerpentHoodScopeHint, local.Domain);
            }

            if (settings != null && Time.time >= _nextFieldCheck)
            {
                _nextFieldCheck = Time.time + settings.progressSampleSeconds;
                RegrowField();
            }

            if (settings != null) TickHoods();

            if (IsServer && _marksman.IsArmed) _marksman.Tick();
        }

        /// <summary>The perches the AI posts in (shutters are not perches: their cups face no one), with their index into
        /// the hoods.</summary>
        List<SerpentHoodMarksman.Perch> CollectPerches()
        {
            var perches = new List<SerpentHoodMarksman.Perch>();
            for (int i = 0; i < _hoods.Count; i++)
                if (!_hoods[i].Cover.Shutter)
                    perches.Add(new SerpentHoodMarksman.Perch(i, _hoods[i].Cover, _hoods[i].Reach));
            return perches;
        }

        bool IsStanding(int hood) => hood >= 0 && hood < _hoods.Count && _hoods[hood].Standing;

        // ── The hoods (every peer) ──

        /// <summary>
        /// Lay every hood (<see cref="SerpentHoodLayout.Build"/>) from the hood prism, on every peer: the layout is closed
        /// form, so every peer's copy is the same. Behind the connecting panel (watchForReveal), so the arena-ready gate
        /// waits for them.
        /// </summary>
        void LayHoods()
        {
            ClearHoods();
            if (!hoodPrismPrefab) return;
            _hoodRoot = new GameObject("SerpentHoods").transform;
            _coverScratch.Clear();
            int intensity = Intensity;
            SerpentHoodLayout.Covers(Centre, intensity, _coverScratch);
            for (int i = 0; i < _coverScratch.Count; i++)
            {
                var hood = new Hood { Cover = _coverScratch[i], Trail = new Trail() };
                hood.Reach = SerpentHoodLayout.Build(hood.Cover, intensity, hood.Parts);
                for (int k = 0; k < hood.Parts.Count; k++) hood.Prisms.Add(LayPart(hood, k, forReveal: true));
                hood.Standing = true;
                _hoods.Add(hood);
            }
        }

        Prism LayPart(Hood hood, int k, bool forReveal)
        {
            var part = hood.Parts[k];
            var prism = PrismTrailBuilder.LayOne(hoodPrismPrefab,
                new PrismLay(new SpawnPoint(part.Position, part.Rotation, part.Scale), Domains.Blue, part.Kind),
                _hoodRoot, hood.Trail, HoodOwnerId, watchForReveal: forReveal);
            if (prism && !forReveal) prism.CompletesAsLiveRibbon = true;
            return prism;
        }

        /// <summary>Still a prism of its hood: alive, and not taken (a destroyed prism goes back to the pool and is
        /// re-issued to somebody else; a Seed Wall that pulls one in re-owns it).</summary>
        static bool Holds(Prism prism) => prism && !prism.destroyed && prism.ownerID == HoodOwnerId;

        /// <summary>
        /// Every peer, every frame: a standing hood whose core lost a block falls apart, <see cref="SerpentHoodSettingsSO.hoodBurstPerFrame"/>
        /// prisms a frame, core outward (the parts are listed core, weave, shell); a fallen one grows back
        /// <see cref="SerpentHoodSettingsSO.hoodRegrowSeconds"/> later, once no hull is inside its reach,
        /// <see cref="SerpentHoodSettingsSO.hoodRegrowPerFrame"/> prisms a frame. SerpentTimeController.TickShapes' rule.
        /// </summary>
        void TickHoods()
        {
            float now = Time.time;
            for (int h = 0; h < _hoods.Count; h++)
            {
                var hood = _hoods[h];
                if (hood.Standing)
                {
                    for (int k = 0; k < hood.Parts.Count; k++)
                    {
                        if (!hood.Parts[k].Core) break;   // the core is listed first
                        if (Holds(hood.Prisms[k])) continue;
                        hood.Standing = false;
                        hood.Bursting = true;
                        hood.Cursor = 0;
                        break;
                    }
                }

                if (hood.Bursting)
                {
                    int budget = settings.hoodBurstPerFrame;
                    while (budget > 0 && hood.Cursor < hood.Prisms.Count)
                    {
                        var prism = hood.Prisms[hood.Cursor++];
                        if (!Holds(prism)) continue;
                        Vector3 impact = (prism.transform.position - hood.Cover.Centre).normalized * settings.hoodBurstSpeed;
                        if (prism.prismProperties is { IsSuperShielded: true }) prism.DeactivateShields(impact);
                        prism.Damage(impact, Domains.Blue, string.Empty, devastate: true);
                        budget--;
                    }
                    if (hood.Cursor >= hood.Prisms.Count)
                    {
                        hood.Bursting = false;
                        hood.RegrowAt = settings.hoodRegrowSeconds > 0f ? now + settings.hoodRegrowSeconds : -1f;
                    }
                    continue;
                }

                if (!hood.Standing && !hood.Regrowing && hood.RegrowAt >= 0f && now >= hood.RegrowAt && !HullWithin(hood))
                {
                    hood.Regrowing = true;
                    hood.Cursor = 0;
                }

                if (hood.Regrowing)
                {
                    int budget = settings.hoodRegrowPerFrame;
                    while (budget > 0 && hood.Cursor < hood.Parts.Count)
                    {
                        int k = hood.Cursor++;
                        if (Holds(hood.Prisms[k])) continue;
                        hood.Prisms[k] = LayPart(hood, k, forReveal: false);
                        budget--;
                    }
                    if (hood.Cursor >= hood.Parts.Count)
                    {
                        hood.Regrowing = false;
                        hood.RegrowAt = -1f;
                        hood.Standing = true;
                    }
                }
            }
        }

        /// <summary>Is any hull inside this hood's reach (so growing it back now would lay prisms on it)?</summary>
        bool HullWithin(Hood hood)
        {
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var tf = players[i]?.Vessel?.Transform;
                if (tf && (tf.position - hood.Cover.Centre).sqrMagnitude < hood.Reach * hood.Reach) return true;
            }
            return false;
        }

        void ClearHoods()
        {
            for (int h = 0; h < _hoods.Count; h++)
            {
                var prisms = _hoods[h].Prisms;
                for (int i = 0; i < prisms.Count; i++)
                    if (Holds(prisms[i]) && !EnvironmentPrismPool.TryRelease(prisms[i]))
                        Destroy(prisms[i].gameObject);
            }
            _hoods.Clear();
            if (_hoodRoot && _hoodRoot.childCount == 0) Destroy(_hoodRoot.gameObject);
            _hoodRoot = null;
        }

        // ── The crystal heart (every peer) ──

        void LayField()
        {
            if (settings == null || settings.fieldCrystalCount <= 0) return;
            if (ElementalCrystalSetSO.Load() == null) return;   // no elemental set in this project state: no field

            var rng = new System.Random(settings.fieldSeed);
            Vector3 centre = Centre;
            for (int i = 0; i < settings.fieldCrystalCount; i++)
            {
                // The slot is closed form (the drawings and serpent_hood_budget.py agree on it); only the element is drawn.
                var slot = new FieldSlot
                {
                    Position = SerpentHoodLayout.FieldSlot(centre, i, settings.fieldCrystalCount, settings.fieldRadius),
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
                    CSDebug.LogError($"[SerpentHood] Client could not match RoundStats for '{sName}'. " +
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
            LayHoods();

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
