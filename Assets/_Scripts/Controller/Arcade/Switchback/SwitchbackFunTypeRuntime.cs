using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The per-peer, per-match half of Switchback's fun types (SWITCHBACK.md "Fun types"):
    /// <list type="bullet">
    /// <item><b>Plan 2</b> (Live Features, and Combined): the charge ribbons' skim and burst, the I4 danger clusters'
    /// sweep, and the shortcut rings beside hairpins (drawn here, threaded through
    /// <see cref="SwitchbackController"/>'s <c>ThreadsAlternate</c>).</item>
    /// <item><b>Plan 3</b> (Par &amp; Medals, and Combined): the par toast at the start, a split toast every
    /// <see cref="SplitEvery"/> gates, the medal and personal best at the finish.</item>
    /// <item><b>Blast Run</b> (<see cref="SwitchbackBlastRun"/>): the snap-trap jaws and sweepers ride the same moving-danger
    /// sweep on their own clocks; the I4 hunter packs' prisms follow the pack state the server replicates
    /// (<see cref="BlastRunPackState"/>), and a downed hunter is toasted and counted.</item>
    /// <item><b>Every fun type</b>: one <c>[Switchback] RUN</c> log line at the finish - fun type, intensity, seed,
    /// time, gates, skims, prisms destroyed, medal - so a test session can be read back from the log.</item>
    /// </list>
    /// Everything here watches the LOCAL pilot only, and nothing here is replicated: the course and its layout are
    /// the same on every peer, the danger sweep runs off the server clock every peer shares, and the scoring is
    /// still the gate race's own (<see cref="GateRaceController"/>).
    /// </summary>
    public sealed class SwitchbackFunTypeRuntime : MonoBehaviour
    {
        public const int SplitEvery = 5;

        /// <summary>A prism laid at most this far from where the layout put it is that prism.</summary>
        const float PrismMatchDistance = 0.75f;
        /// <summary>How long to keep looking for moving prisms that the budgeted lay has not produced yet.</summary>
        const float PrismScanTimeout = 30f;
        const float PrismScanInterval = 0.5f;

        GameDataSO _gameData;
        SwitchbackFunType _funType;
        int _intensity;
        int _seed;
        readonly List<RaceGate> _course = new(32);
        Vector3 _centre;
        System.Func<int> _raceLength;

        SwitchbackFeatureLayout _layout;
        SwitchbackRibbonSkim[] _skims;
        int _skimsDone;

        Transform _prismContainer;
        Prism[] _moving;            // per layout prism index; null until matched (and for non-moving ones)
        bool[] _lost;               // per layout prism index: matched once, since destroyed, pooled or re-parented
        int _movingExpected, _movingMatched;
        readonly List<(int first, int count)> _tracked = new();   // the moving clusters' and the packs' prism ranges

        // Blast Run hunters: the server's replicated state, and where this peer draws each pack.
        NetworkList<BlastRunPackState> _packState;
        Vector3[] _packShown;
        int[] _packDownSeen;
        int _huntersDown;
        float _nextScan, _scanDeadline;

        readonly List<RaceGateRing> _shortcutRings = new();
        int _litShortcut = -1;

        float _par;
        int _lastSplitGate;
        bool _announcedPar, _finished;
        int _blocksAtStart = -1;

        /// <summary>Hand over the match. <paramref name="layout"/> is null for a fun type without live features.</summary>
        public void Init(GameDataSO gameData, SwitchbackFunType funType, int intensity, int seed,
                         IReadOnlyList<RaceGate> course, Vector3 centre, System.Func<int> raceLength,
                         SwitchbackFeatureLayout layout, Transform prismContainer)
        {
            _gameData = gameData;
            _funType = funType;
            _intensity = intensity;
            _seed = seed;
            _course.Clear();
            for (int i = 0; i < course.Count; i++) _course.Add(course[i]);
            _centre = centre;
            _raceLength = raceLength;
            _layout = layout;
            _prismContainer = prismContainer;
            _par = SwitchbackPar.ParSeconds(_course, _centre);

            if (_layout != null)
            {
                _skims = new SwitchbackRibbonSkim[_layout.Ribbons.Count];
                _moving = new Prism[_layout.Prisms.Count];
                _lost = new bool[_layout.Prisms.Count];
                foreach (var c in _layout.MovingClusters) _tracked.Add((c.FirstPrism, c.Count));
                foreach (var pk in _layout.Packs) _tracked.Add((pk.FirstPrism, pk.Members * pk.PerMember));
                foreach (var r in _tracked) _movingExpected += r.count;
                _packShown = new Vector3[_layout.Packs.Count];
                _packDownSeen = new int[_layout.Packs.Count];
                for (int i = 0; i < _layout.Packs.Count; i++) _packShown[i] = _layout.Packs[i].Den;
                _scanDeadline = Time.time + PrismScanTimeout;
                BuildShortcutRings();
            }
        }

        /// <summary>Blast Run I4: the server's pack state, which this peer's pack prisms follow.</summary>
        public void AttachPacks(NetworkList<BlastRunPackState> packState) => _packState = packState;

        /// <summary>
        /// How many of hunter <paramref name="member"/> of pack <paramref name="pack"/>'s prisms still stand here.
        /// <paramref name="judged"/> is false until every one of them has been found once (or the scan gave up), so a
        /// hunter whose prisms are still being laid is never called down.
        /// </summary>
        public int AlivePackPrisms(int pack, int member, out bool judged)
        {
            judged = false;
            if (_layout == null || pack < 0 || pack >= _layout.Packs.Count) return 0;
            var pk = _layout.Packs[pack];
            int first = pk.FirstPrism + member * pk.PerMember, alive = 0;
            bool all = true;
            for (int j = 0; j < pk.PerMember; j++)
            {
                int k = first + j;
                if (Live(k)) alive++;
                else if (!_lost[k]) all = false;   // never found yet
            }
            judged = all || Time.time >= _scanDeadline;
            return alive;
        }

        /// <summary>The tracked prism at <paramref name="k"/> is still the one laid there: not destroyed (gone, or flagged
        /// <see cref="Prism.destroyed"/>) and not handed to another parent (a pool re-using it). Anything else is dropped for good - never moved again.</summary>
        bool Live(int k)
        {
            var prism = _moving[k];
            if (prism == null) return false;
            if (!prism || prism.destroyed || !_prismContainer || !prism.transform.IsChildOf(_prismContainer))
            {
                _moving[k] = null;
                _lost[k] = true;
                return false;
            }
            return true;
        }

        void BuildShortcutRings()
        {
            var theme = _gameData ? _gameData.ThemeManagerData : null;
            var root = new GameObject("SwitchbackShortcuts").transform;
            root.SetParent(transform, false);
            for (int i = 0; i < _layout.Shortcuts.Count; i++)
            {
                var sc = _layout.Shortcuts[i];
                var go = new GameObject($"Shortcut_{sc.SkipsGate + 1:00}");
                go.transform.SetParent(root, false);
                var ring = go.AddComponent<RaceGateRing>();
                ring.Build(sc.SkipsGate, sc.Ring, theme, 0.6f);
                _shortcutRings.Add(ring);
            }
        }

        /// <summary>
        /// Did <paramref name="prev"/> to <paramref name="cur"/> thread the shortcut that stands in for the gate at
        /// race index <paramref name="raceIndex"/>? Asked by the controller for every pilot this peer simulates.
        /// </summary>
        public bool ThreadsShortcut(int raceIndex, Vector3 prev, Vector3 cur)
        {
            if (_layout == null) return false;
            for (int i = 0; i < _layout.Shortcuts.Count; i++)
                if (_layout.Shortcuts[i].SkipsGate == raceIndex && _shortcutRings[i] && _shortcutRings[i].CrossedMouth(prev, cur))
                    return true;
            return false;
        }

        void Update()
        {
            if (_gameData == null || _course.Count == 0) return;
            TickMovingDanger();
            TickPacks();

            var player = _gameData.LocalPlayer;
            var stats = player?.RoundStats;
            var vessel = player?.Vessel;
            if (stats == null) return;
            int threaded = stats.SwitchesThreaded;

            if (_blocksAtStart < 0 && _gameData.TurnStartTime > 0f) _blocksAtStart = stats.BlocksDestroyed;
            if (!_announcedPar && _gameData.TurnStartTime > 0f) AnnouncePar();

            TickShortcutHighlight(threaded);
            if (vessel != null && !(vessel is Object uo && !uo) && vessel.Transform)
                TickRibbons(vessel, vessel.Transform.position);
            TickSplits(threaded);
            TickFinish(stats, threaded);
        }

        // ── Plan 2 ─────────────────────────────────────────────────────

        void TickRibbons(IVessel vessel, Vector3 pos)
        {
            if (_skims == null) return;
            for (int i = 0; i < _skims.Length; i++)
            {
                if (!_skims[i].Step(_layout.Ribbons[i], pos, Time.time)) continue;
                _skimsDone++;
                var transformer = vessel.VesselStatus?.VesselTransformer;
                if (transformer) transformer.ModifyThrottle(SwitchbackLiveFeatures.BurstThrottle, SwitchbackLiveFeatures.BurstSeconds);
                ToastNotificationAPI.Show("CLEAN SKIM - BURST!", ToastKind.Success);
            }
        }

        void TickShortcutHighlight(int threaded)
        {
            int lit = -1;
            if (_layout != null)
                for (int i = 0; i < _layout.Shortcuts.Count; i++)
                    if (_layout.Shortcuts[i].SkipsGate == threaded) { lit = i; break; }
            if (lit == _litShortcut) return;
            if (_litShortcut >= 0 && _shortcutRings[_litShortcut]) _shortcutRings[_litShortcut].SetIsNextForLocalPilot(false);
            if (lit >= 0 && _shortcutRings[lit]) _shortcutRings[lit].SetIsNextForLocalPilot(true);
            _litShortcut = lit;
        }

        /// <summary>The danger sweep, off the server clock so every peer's prisms stand in the same place.</summary>
        void TickMovingDanger()
        {
            if (_layout == null || _tracked.Count == 0) return;
            if (_movingMatched < _movingExpected && Time.time >= _nextScan && Time.time < _scanDeadline) ScanForMovingPrisms();

            var nm = NetworkManager.Singleton;
            float t = nm != null && nm.IsListening ? nm.ServerTime.TimeAsFloat : Time.time;
            foreach (var cluster in _layout.MovingClusters)
            {
                Vector3 d = cluster.DisplacementAt(t);
                for (int j = 0; j < cluster.Count; j++)
                {
                    int k = cluster.FirstPrism + j;
                    if (!Live(k)) continue;
                    var prism = _moving[k];
                    prism.transform.position = _layout.Prisms[k].Position + d;
                    prism.NotifyPositionChanged();
                }
            }
        }

        /// <summary>The prisms are laid ASYNC and in budgeted batches: match what exists now to the layout by laid
        /// position, and come back until every moving prism is found (or the scan times out - those stay put).</summary>
        void ScanForMovingPrisms()
        {
            _nextScan = Time.time + PrismScanInterval;
            if (!_prismContainer) return;
            var found = _prismContainer.GetComponentsInChildren<Prism>(true);
            float sq = PrismMatchDistance * PrismMatchDistance;
            foreach (var range in _tracked)
                for (int j = 0; j < range.count; j++)
                {
                    int k = range.first + j;
                    if (_moving[k] || _lost[k]) continue;
                    Vector3 laid = _layout.Prisms[k].Position;
                    for (int f = 0; f < found.Length; f++)
                    {
                        if (!found[f] || (found[f].transform.position - laid).sqrMagnitude > sq) continue;
                        _moving[k] = found[f];
                        _movingMatched++;
                        break;
                    }
                }
            if (_movingMatched >= _movingExpected || Time.time + PrismScanInterval >= _scanDeadline)
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                    $"[Switchback] Moving danger: {_movingMatched}/{_movingExpected} prisms found.");
        }

        // ── Blast Run hunters ─────────────────────────────────────────

        /// <summary>Every peer: draw each pack where the server says it is (lerped between the server's 10 Hz writes),
        /// carrying every standing member's prisms by the pack's offset from its den; a down member stays where it
        /// died. A newly downed member is toasted once.</summary>
        void TickPacks()
        {
            if (_layout == null || _layout.Packs.Count == 0 || _packState == null) return;
            int n = Mathf.Min(_layout.Packs.Count, _packState.Count);
            for (int p = 0; p < n; p++)
            {
                var pk = _layout.Packs[p];
                var state = _packState[p];
                int newlyDown = state.DownMask & ~_packDownSeen[p];
                if (newlyDown != 0)
                {
                    for (int m = 0; m < pk.Members; m++)
                        if ((newlyDown & (1 << m)) != 0)
                        {
                            _huntersDown++;
                            ToastNotificationAPI.Show($"HUNTER DOWN ({_huntersDown}/{HunterCount})", ToastKind.Success);
                            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Switchback] BLAST RUN hunter down: pack {p} member {m}.");
                        }
                    _packDownSeen[p] |= newlyDown;
                }

                _packShown[p] = BlastRunHunters.Follow(_packShown[p], state.Position, Time.deltaTime);
                Vector3 offset = _packShown[p] - pk.Den;
                for (int m = 0; m < pk.Members; m++)
                {
                    if ((state.DownMask & (1 << m)) != 0) continue;
                    int first = pk.FirstPrism + m * pk.PerMember;
                    for (int j = 0; j < pk.PerMember; j++)
                    {
                        int k = first + j;
                        if (!Live(k)) continue;
                        var prism = _moving[k];
                        prism.transform.position = _layout.Prisms[k].Position + offset;
                        prism.NotifyPositionChanged();
                    }
                }
            }
        }

        int HunterCount
        {
            get
            {
                int c = 0;
                if (_layout != null) foreach (var pk in _layout.Packs) c += pk.Members;
                return c;
            }
        }

        // ── Plan 3 ─────────────────────────────────────────────────────

        void AnnouncePar()
        {
            _announcedPar = true;
            if (!SwitchbackFunTypes.HasParMedals(_funType)) return;
            string best = PlayerPrefs.HasKey(PbKey) ? $" - BEST {SwitchbackPar.FormatTime(PlayerPrefs.GetFloat(PbKey))}" : "";
            ToastNotificationAPI.Show($"PAR {SwitchbackPar.FormatTime(_par)}{best}", ToastKind.Goal);
        }

        void TickSplits(int threaded)
        {
            if (!SwitchbackFunTypes.HasParMedals(_funType) || _gameData.TurnStartTime <= 0f) return;
            if (threaded <= _lastSplitGate || threaded >= _course.Count) { _lastSplitGate = Mathf.Max(_lastSplitGate, threaded); return; }
            if (threaded / SplitEvery > _lastSplitGate / SplitEvery)
            {
                float elapsed = Time.time - _gameData.TurnStartTime;
                float par = SwitchbackPar.ParAtGate(_course, _centre, threaded - 1);
                ToastNotificationAPI.Show($"GATE {threaded} - {SwitchbackPar.FormatDelta(elapsed, par)} vs par",
                    elapsed <= par ? ToastKind.Success : ToastKind.None);
            }
            _lastSplitGate = threaded;
        }

        void TickFinish(IRoundStats stats, int threaded)
        {
            int length = _raceLength != null ? _raceLength() : _course.Count;
            if (_finished || length <= 0 || threaded < length || _gameData.TurnStartTime <= 0f) return;
            _finished = true;

            float time = Time.time - _gameData.TurnStartTime;
            var medal = SwitchbackPar.MedalFor(time, _par);
            float previous = PlayerPrefs.GetFloat(PbKey, 0f);
            bool best = previous <= 0f || time < previous;
            if (best)
            {
                PlayerPrefs.SetFloat(PbKey, time);
                PlayerPrefs.Save();
            }

            if (SwitchbackFunTypes.HasParMedals(_funType))
            {
                string medalText = medal == SwitchbackMedal.None ? "NO MEDAL" : medal.ToString().ToUpperInvariant();
                ToastNotificationAPI.Show(
                    $"{medalText} - {SwitchbackPar.FormatTime(time)} (par {SwitchbackPar.FormatTime(_par)})" +
                    (best ? " - NEW BEST" : $" - best {SwitchbackPar.FormatTime(previous)}"),
                    medal == SwitchbackMedal.None ? ToastKind.None : ToastKind.Goal);
            }

            int hits = _blocksAtStart >= 0 ? stats.BlocksDestroyed - _blocksAtStart : -1;
            CSDebug.Log($"[Switchback] RUN funType={_funType} intensity={_intensity} seed={_seed} " +
                        $"time={time:F2}s par={_par:F2}s gates={threaded}/{length} skims={_skimsDone}/{(_skims?.Length ?? 0)} " +
                        $"prismsDestroyed={hits} medal={medal} pb={(best ? time : previous):F2}s{(best ? " (new)" : "")}" +
                        (SwitchbackFunTypes.HasBlastRun(_funType) && _layout != null
                            ? $" blastRun stations={_layout.Stations.Count} keys={BlastRunKeyCrystals.Count} huntersDown={_huntersDown}/{HunterCount}"
                            : ""));
        }

        string PbKey => SwitchbackFunTypes.PersonalBestKey(_funType, _intensity);
    }
}
