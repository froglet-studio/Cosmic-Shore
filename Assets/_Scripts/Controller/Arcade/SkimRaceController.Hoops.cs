using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Skim Race's course as the Squirrel's Time game (<see cref="SkimRaceHoops"/>, <c>SKIMRACE.md</c> §0): the hoops the
    /// server stands on the rail, the Time crystals (I3+) and live wires (I4) every peer lays from the authored anchors,
    /// the race beats and the per-intensity hint as toasts, and two FMOD slots for the local pilot's rings. The vessel is
    /// untouched: skimming, Live Wire and the boost are its shipped kit.
    /// </summary>
    public partial class SkimRaceController : ICrystalAnchorPlacement
    {
        [Header("Live wires (SkimRaceHoops, I4)")]
        [Tooltip("The prism every live-wire segment is laid from (the environment prism, SpawnablePrism), laid as a DANGER " +
                 "prism. Authored by Tools/Build/author_skimrace_assets.py. Unset = no wires, and a warning.")]
        [SerializeField] Prism liveWirePrefab;

        [Tooltip("Seconds after a wire segment was knocked away before it is laid again, once no hull is near it.")]
        [SerializeField, Min(0.5f)] float liveWireRestandSeconds = 3f;

        [Tooltip("Seconds a taken Time crystal (I3+) stays gone before it grows back in its place (Redline's number).")]
        [SerializeField, Min(0.5f)] float timeCrystalRegrowSeconds = 5f;

        [Header("Toasts")]
        [Tooltip("Crystals from the target at which the leading domain's home stretch is announced (0 = never).")]
        [SerializeField, Min(0)] int homeStretchRings = 3;

        [Header("Audio (FMOD) - leave empty for silence")]
        [Tooltip("Played for the LOCAL pilot each time they thread one of their rings (their crystal count rises). " +
                 "Ships empty: audio authors the event.")]
        [SerializeField] FMODUnity.EventReference ringThreadedEvent;

        [Tooltip("Played for the LOCAL pilot once, when their domain is one ring from the finish. Ships empty.")]
        [SerializeField] FMODUnity.EventReference lastRingEvent;

        const string WireOwnerId = "SkimRaceLiveWire";

        sealed class WireSegment
        {
            public SkimRaceHoops.Piece Piece;
            public Prism Prism;
            public float GoneAt = -1f;
        }

        sealed class TimeCrystalSlot
        {
            public Vector3 Position;
            public Crystal Crystal;
            public ElementalCrystalImpactor Impactor;
            public float RegrowAt = -1f;
        }

        SkimRaceHoops.Ribbon _ribbon;
        int _ribbonIntensity = -1;
        SpawnableWaypointTrack _rail;
        readonly List<WireSegment> _wires = new();
        readonly List<TimeCrystalSlot> _timeCrystals = new();
        readonly List<SkimRaceHoops.Piece> _pieceScratch = new();
        Transform _wireRoot;
        Trail _wireTrail;
        float _nextCourseCheck;

        DomainRaceToasts _raceToasts;
        float _beatsTurnStart = float.NaN;
        bool _hintPosted;
        int _localCrystalsSeen = -1;
        bool _lastRingPlayed;

        // ── The rail ─────────────────────────────────────────────────────────

        /// <summary>The authored rail for this match's intensity, sampled once (null when the scene has no waypoint track).</summary>
        SkimRaceHoops.Ribbon Ribbon()
        {
            int intensity = Intensity;
            if (_ribbon != null && _ribbonIntensity == intensity) return _ribbon;
            if (!_rail) _rail = FindAnyObjectByType<SpawnableWaypointTrack>(FindObjectsInactive.Include);
            if (!_rail || !_rail.TryGetRail(intensity, out var positions, out var ups, out bool spline)) return null;
            _ribbon = SkimRaceHoops.Ribbon.Build(positions, ups, spline);
            _ribbonIntensity = intensity;
            return _ribbon.Count > 0 ? _ribbon : null;
        }

        // ── The hoops (server: CrystalManager asks on every spawn against an anchor) ─────

        /// <summary>
        /// Stand the crystal's ring as a hoop on the rail (<see cref="SkimRaceHoops.Place"/>): the anchor's foot moved up
        /// to 30 u along the rail, across it, and onto a random face. Server only; the position replicates as before.
        /// False (the platform's shell) when the crystal is not a race ring or there is no rail.
        /// </summary>
        public bool TryPlaceAroundAnchor(Vector3 anchor, Crystal crystalPrefab, out Vector3 position)
        {
            position = anchor;
            if (!crystalPrefab || !crystalPrefab.TryGetComponent(out CrystalRaceRing ring)) return false;
            var ribbon = Ribbon();
            if (ribbon == null) return false;
            int intensity = Intensity;
            var hoop = SkimRaceHoops.Place(ribbon, anchor, intensity, ring.MouthRadiusAt(intensity),
                Random.value, Random.value, Random.value);
            position = hoop.Centre;
            return true;
        }

        // ── Laying (every peer, from the authored anchors) ───────────────────

        /// <summary>Lay the Time crystals and the live wires this intensity asks for. Called once the track is laid.</summary>
        void LayCoursePieces()
        {
            ClearCoursePieces();
            var ribbon = Ribbon();
            var manager = GetComponent<CrystalManager>();
            if (ribbon == null || !manager || !manager.TryGetAnchors(out var anchors)) return;
            int intensity = Intensity;

            SkimRaceHoops.PlaceWires(ribbon, anchors, intensity, _pieceScratch);
            if (_pieceScratch.Count > 0 && !liveWirePrefab)
                CSDebug.LogWarning("[SkimRace] no live-wire prefab on the controller - intensity 4 has no live wires.");
            else if (_pieceScratch.Count > 0)
            {
                _wireRoot = new GameObject("SkimRaceLiveWires").transform;
                _wireTrail = new Trail();
                for (int i = 0; i < _pieceScratch.Count; i++)
                {
                    var seg = new WireSegment { Piece = _pieceScratch[i] };
                    seg.Prism = LayWire(seg.Piece, forReveal: true);
                    _wires.Add(seg);
                }
            }

            SkimRaceHoops.PlaceTimeCrystals(ribbon, anchors, intensity, _pieceScratch);
            if (_pieceScratch.Count > 0 && ElementalCrystalSetSO.Load() == null)
                CSDebug.LogWarning("[SkimRace] no ElementalCrystalSet in Resources - the course has no Time crystals.");
            else
                for (int i = 0; i < _pieceScratch.Count; i++)
                {
                    var slot = new TimeCrystalSlot { Position = _pieceScratch[i].Position };
                    _timeCrystals.Add(slot);
                    Mint(slot);
                }

            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[SkimRace] COURSE intensity {intensity}: hoops {(SkimRaceHoops.StandsOnFace(intensity) ? "stand on a face" : "hang on the skim line")}, " +
                $"{_timeCrystals.Count} Time crystals, {_wires.Count} live-wire prisms, start Time {SkimRaceHoops.StartingTimeLevel(intensity)}.");
        }

        Prism LayWire(in SkimRaceHoops.Piece piece, bool forReveal)
        {
            var rotation = Quaternion.LookRotation(piece.Forward, piece.Up);
            var prism = PrismTrailBuilder.LayOne(liveWirePrefab,
                new PrismLay(new SpawnPoint(piece.Position, rotation, SkimRaceHoops.WirePrismScale), Domains.Blue, PrismKind.Danger),
                _wireRoot, _wireTrail, WireOwnerId, watchForReveal: forReveal);
            if (prism && !forReveal) prism.CompletesAsLiveRibbon = true;
            return prism;
        }

        /// <summary>Still a segment of its wire: alive, and not re-issued to somebody else by the pool.</summary>
        static bool Holds(Prism prism) => prism && !prism.destroyed && prism.ownerID == WireOwnerId;

        void Mint(TimeCrystalSlot slot)
        {
            var set = ElementalCrystalSetSO.Load();
            var prefab = set != null ? set.GetPrefab(Element.Time) : null;
            if (prefab == null) return;
            var crystal = Instantiate(prefab, slot.Position, Quaternion.identity);
            // Sized BEFORE Start(): Crystal stamps crystalValue from lossyScale.
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

        static bool IsLive(TimeCrystalSlot slot) => slot.Crystal && slot.Impactor && !slot.Impactor.HasBeenCollected;

        void ClearCoursePieces()
        {
            for (int i = 0; i < _wires.Count; i++)
                if (Holds(_wires[i].Prism) && !EnvironmentPrismPool.TryRelease(_wires[i].Prism))
                    Destroy(_wires[i].Prism.gameObject);
            _wires.Clear();
            if (_wireRoot && _wireRoot.childCount == 0) Destroy(_wireRoot.gameObject);
            _wireRoot = null;
            for (int i = 0; i < _timeCrystals.Count; i++)
                if (_timeCrystals[i].Crystal) Destroy(_timeCrystals[i].Crystal.gameObject);
            _timeCrystals.Clear();
        }

        // ── Every frame (every peer) ─────────────────────────────────────────

        void Update()
        {
            TickBeats();
            if (Time.time < _nextCourseCheck) return;
            _nextCourseCheck = Time.time + 0.5f;
            TickWires();
            TickTimeCrystals();
        }

        void TickWires()
        {
            float now = Time.time;
            for (int i = 0; i < _wires.Count; i++)
            {
                var seg = _wires[i];
                if (Holds(seg.Prism) && (seg.Prism.transform.position - seg.Piece.Position).sqrMagnitude <= 1f)
                {
                    seg.GoneAt = -1f;
                    continue;
                }
                if (HullNear(seg.Piece.Position)) { seg.GoneAt = now; continue; }
                if (seg.GoneAt < 0f) { seg.GoneAt = now; continue; }
                if (now - seg.GoneAt < liveWireRestandSeconds) continue;
                if (Holds(seg.Prism))
                {
                    seg.Prism.transform.SetPositionAndRotation(seg.Piece.Position, Quaternion.LookRotation(seg.Piece.Forward, seg.Piece.Up));
                    seg.Prism.NotifyPositionChanged();
                }
                else seg.Prism = LayWire(seg.Piece, forReveal: false);
                seg.GoneAt = -1f;
            }
        }

        void TickTimeCrystals()
        {
            float now = Time.time;
            for (int i = 0; i < _timeCrystals.Count; i++)
            {
                var slot = _timeCrystals[i];
                if (IsLive(slot)) continue;
                if (slot.RegrowAt < 0f) { slot.RegrowAt = now + timeCrystalRegrowSeconds; continue; }
                if (now < slot.RegrowAt || slot.Crystal) continue;   // one still flying to its collector is left to finish
                Mint(slot);
            }
        }

        /// <summary>Is any hull near enough that laying a wire segment now would lay it on them?</summary>
        bool HullNear(Vector3 point)
        {
            const float reach = 60f;
            var players = gameData ? gameData.Players : null;
            if (players == null) return false;
            for (int i = 0; i < players.Count; i++)
            {
                var tf = players[i]?.Vessel?.Transform;
                if (tf && (tf.position - point).sqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        // ── Toasts and the local pilot's sounds ──────────────────────────────

        void TickBeats()
        {
            if (!rule || gameData == null) return;
            if (!Mathf.Approximately(gameData.TurnStartTime, _beatsTurnStart))
            {
                _beatsTurnStart = gameData.TurnStartTime;
                _hintPosted = false;
                _localCrystalsSeen = -1;
                _lastRingPlayed = false;
            }
            if (_raceEnded || !gameData.IsTurnRunning) return;

            int target = rule.TargetFor(gameData);
            int perLap = _rail ? _rail.CrystalsPerLap(Intensity) : 0;
            int finalLapAt = perLap > 0 && target >= perLap * 2 ? target - perLap : 0;
            // Halfway, home stretch and final lap (129, 131, 132): a local poll over the replicated crystal counts. The
            // lead change (130) is not authored - Skim Race's own leader toast (21) already says it.
            _raceToasts ??= new DomainRaceToasts(rule);
            _raceToasts.Tick(gameData, homeStretchRings, finalLapAt);

            var local = gameData.LocalPlayer;
            if (local == null) return;
            if (!_hintPosted)
            {
                _hintPosted = true;
                // One line per rung, each naming the one new thing it asks (310-313, authored in GameToastConfig_SkimRace).
                GameToastAPI.Post(HintFor(Intensity), local.Domain);
            }

            int mine = local.RoundStats?.CrystalsCollected ?? 0;
            if (_localCrystalsSeen >= 0 && mine > _localCrystalsSeen && !ringThreadedEvent.IsNull)
                CosmicShore.Core.AudioSystem.Instance.PlaySFXEvent(ringThreadedEvent);
            _localCrystalsSeen = mine;

            if (!_lastRingPlayed && target > 1 && rule.DomainValue(gameData, local.Domain) == target - 1)
            {
                _lastRingPlayed = true;
                if (!lastRingEvent.IsNull) CosmicShore.Core.AudioSystem.Instance.PlaySFXEvent(lastRingEvent);
            }
        }

        /// <summary>The hint toast for a rung: 310 + (intensity - 1).</summary>
        public static GameToastSituation HintFor(int intensity) =>
            (GameToastSituation)((int)GameToastSituation.SkimRaceHintRail + SkimRaceHoops.Clamp(intensity));
    }
}
