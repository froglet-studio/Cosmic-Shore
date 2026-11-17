using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spearline - the Swordfish-only lap race, and the Swordfish's TIME card (SPEARLINE.md). The course is Headlong's
    /// circuit (this airframe's ramp and turn) with Spearline's own mouths, and the gentlest rings carry SEALS: panes of
    /// prisms in the mouth (<see cref="SpearlineSeals"/>). A sealed ring counts only when the pilot crosses it through a
    /// hole of at least <see cref="SpearlineSealSettings.NeedPrisms"/> prisms, and the hull alone punches a smaller one,
    /// so every seal asks for a swing of the bill INTO the pane on the way through. From I2 a seal can be armoured: a
    /// shielded sector that only cracks, so the pilot reads where the plain face is, crosses it and swings into it.
    ///
    /// <para><b>What the mode adds:</b> the rings, the seals (every peer laying the same prisms from the broadcast
    /// rings), their regrowth, the worn seal (each closed pass lowers that pilot's need there, so nobody is trapped), the
    /// AI's read and its sword coach (<c>SpearlineController.Coach.cs</c>), and the local feedback
    /// (<c>SpearlineController.Feedback.cs</c>). <b>What it does not:</b> nothing about the vessel. The AI's strokes go
    /// through its shipped sword hand (<see cref="SwordfishSwordAutopilot"/>), which writes the trigger mirrors.</para>
    /// </summary>
    public partial class SpearlineController : GateRaceController
    {
        [Header("AI nose lead")]
        [Tooltip("Each AI's nose lead by hull (AIPilot.NoseLeadGain), set as it is armed. The Rhino's 2.5 on Headlong's " +
                 "circuit: the same airframe. The lead may put the command at most 10 degrees past the aim (Thornline's cap): at " +
                 "the default 40 the AI locked into an 11 u circle hundreds of units from its ring for up to 185 s, which the " +
                 "orbit break never sees (the bearing to the ring barely turns), and timed out at I3-I4 (race judge, 2026-11-17).")]
        [SerializeField] AINoseLeadRow[] aiNoseLead = { new AINoseLeadRow(VesselClassType.Swordfish, 2.5f, 10f) };

        protected override AINoseLeadRow[] AINoseLeadTable => aiNoseLead;

        [Header("Spearline")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE length, so the circuit " +
                 "is laid with target/laps rings.")]
        [SerializeField, Min(1)] int laps = SpearlineCourseSource.DefaultLaps;

        [Tooltip("The prism every seal is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_spearline_assets.py. Unset = no seals, and a warning.")]
        [SerializeField] Prism sealPrefab;

        [Tooltip("Seal prisms grown back a frame, at most.")]
        [SerializeField, Min(1)] int sealRegrowPerFrame = 40;

        [Tooltip("A seal grows back only while no hull is within this distance (u) of its ring's centre, past the mouth.")]
        [SerializeField, Min(0f)] float sealClearance = 60f;

        [Header("AI read (the coach)")]
        [Tooltip("The chance, per seal and lap, that an AI does not read the seal and crosses its centre with a right-hand " +
                 "stroke instead, by its seat's difficulty: Easy, Medium, Hard, Legendary.")]
        [SerializeField] float[] aiMissReadByDifficulty = { 0.3f, 0.15f, 0.05f, 0f };

        [Tooltip("How far (u) an AI's crossing lands from the one it read, at most, by difficulty: Easy, Medium, Hard, " +
                 "Legendary. A fixed draw per seat, seal and lap.")]
        [SerializeField] float[] aiLaneErrorByDifficulty = { 10f, 7f, 4f, 2f };

        [Tooltip("The AI holds its stroke from this long (s) before it reaches the seal's plane: the bill is out well " +
                 "before the pane (holding early costs nothing).")]
        [SerializeField, Min(0.05f)] float aiStrokeLeadSeconds = 0.35f;

        [Tooltip("...and until it is this far (u) past the plane.")]
        [SerializeField, Min(0f)] float aiStrokeHoldPast = 30f;

        [Tooltip("Inside this distance (u) of the seal the AI's read is frozen, so its line stops moving under it; " +
                 "outside it the read is refreshed a few times a second from the seal as it stands.")]
        [SerializeField, Min(0f)] float aiPlanFreezeDistance = 150f;

        [Tooltip("Inside this distance (u) of a sealed ring the coach owns the AI's triggers (holding them at rest until " +
                 "the stroke), so the hand never stands the bill up to energize in front of a pane.")]
        [SerializeField, Min(0f)] float aiCoachOwnsTriggersWithin = 500f;

        [Header("Feedback (local, in the world)")]
        [Tooltip("The plain face of the local pilot's next ARMOURED seal is drawn this much lifted (0..1), so the face " +
                 "that cuts reads against the shields. A look only.")]
        [SerializeField, Range(0f, 1f)] float plainFaceLift = 0.35f;

        [Tooltip("...once the pilot is within this distance (u) of it.")]
        [SerializeField, Min(0f)] float plainFaceShowDistance = 900f;

        [Tooltip("When the local pilot threads a seal on the first pass, the prisms round the hole flash this much " +
                 "lifted and fade back over holeFlashSeconds.")]
        [SerializeField, Range(0f, 1f)] float holeFlashLift = 0.9f;

        [SerializeField, Min(0f)] float holeFlashSeconds = 0.6f;

        [Tooltip("A run of seals threaded on the first pass is called at these lengths (SpearlineCleanStreak).")]
        [SerializeField] int[] cleanStreakCalls = { 3, 6, 12 };

        [Header("Audio (FMOD) - every slot ships EMPTY; an empty slot plays nothing")]
        [Tooltip("The local pilot threaded a seal through a hole.")]
        [SerializeField] FMODUnity.EventReference sealOpenedEvent;

        [Tooltip("The local pilot crossed a sealed ring that held (a closed pass: come round).")]
        [SerializeField] FMODUnity.EventReference sealHeldEvent;

        [Tooltip("A run of first-pass seals was called.")]
        [SerializeField] FMODUnity.EventReference cleanStreakEvent;

        const string SealOwnerId = "Spearline";

        protected override string ModeName => "Spearline";

        protected override RaceCourseSource CreateCourseSource() => new SpearlineCourseSource { Laps = laps };

        protected override bool AICommitFromApproachPoint => true;

        // Every seal prism, in ring order: where it stands, its ring, the live prism, when it went, and armour.
        readonly List<Vector3> _sealPositions = new();
        readonly List<int> _sealRing = new();
        readonly List<Prism> _sealPrisms = new();
        readonly List<float> _goneAt = new();
        readonly List<bool> _sealArmour = new();
        readonly Dictionary<int, Seal> _sealOf = new();
        readonly List<Vector3> _scratch = new();
        readonly List<int> _hole = new();
        // Seal-local scratch for the hole rule and the AI's read, sized to the largest seal when the seals are laid.
        bool[] _bufGone = System.Array.Empty<bool>(), _bufBreaks = System.Array.Empty<bool>(),
               _bufCut = System.Array.Empty<bool>(), _bufInHole = System.Array.Empty<bool>(),
               _bufCutNow = System.Array.Empty<bool>();
        readonly List<int> _readHole = new();
        // Closed passes a pilot has made at a seal since last threading it (the worn seal), on the peer that judges them.
        readonly Dictionary<(IPlayer, int), int> _closedPasses = new();
        Transform _sealRoot;
        int _regrowCursor;
        bool _hintPosted;

        /// <summary>One sealed ring: its slice of the seal lists, its hex neighbours and its armour.</summary>
        sealed class Seal
        {
            public int Start, Count;
            public int[][] Adjacency;
            public bool Armoured;
            public float ArmourAngle;
        }

        SpearlineSealSettings Settings => SpearlineSeals.ForIntensity(Intensity);

        public override void OnNetworkDespawn()
        {
            ClearSeals();
            base.OnNetworkDespawn();
        }

        // ── The seals (every peer, from the broadcast rings) ─────────────

        protected override void OnCourseRaised()
        {
            ClearSeals();
            ResetFeedback();
            _hintPosted = false;
            if (!sealPrefab)
            {
                CSDebug.LogWarning("[Spearline] no seal prefab on the controller - the seals are not laid.");
                return;
            }
            var s = Settings;
            var sealedRings = SpearlineSeals.SealedRings(_course, s);
            _sealRoot = new GameObject("SpearlineSeals").transform;
            int ordinal = 0;
            for (int ring = 0; ring < _course.Count; ring++)
            {
                if (!sealedRings[ring]) continue;
                var gate = _course[ring];
                _scratch.Clear();
                SpearlineSeals.Lay(gate, gate.Radius, _scratch);
                var seal = new Seal
                {
                    Start = _sealPositions.Count,
                    Count = _scratch.Count,
                    Armoured = SpearlineSeals.IsArmoured(ordinal++, s),
                    ArmourAngle = SpearlineSeals.ArmourAngle(ring, gate.Position),
                };
                SpearlineSeals.Basis(gate.Axis, out var u, out var v);
                for (int k = 0; k < _scratch.Count; k++)
                {
                    Vector3 d = _scratch[k] - gate.Position;
                    _sealPositions.Add(_scratch[k]);
                    _sealRing.Add(ring);
                    _sealArmour.Add(seal.Armoured && SpearlineSeals.InArmour(Vector3.Dot(d, u), Vector3.Dot(d, v),
                                                                            seal.ArmourAngle, s.ArmourDegrees));
                    _sealPrisms.Add(null);
                    _goneAt.Add(-1f);
                    _sealPrisms[_sealPrisms.Count - 1] = LaySealPrism(_sealPrisms.Count - 1, forReveal: true);
                }
                seal.Adjacency = SpearlineSeals.Adjacency(_sealPositions, seal.Start, seal.Count);
                _sealOf[ring] = seal;
                if (seal.Count > _bufGone.Length)
                {
                    _bufGone = new bool[seal.Count];
                    _bufBreaks = new bool[seal.Count];
                    _bufCut = new bool[seal.Count];
                    _bufInHole = new bool[seal.Count];
                    _bufCutNow = new bool[seal.Count];
                }
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Spearline] intensity {Intensity}: {_course.Count} rings, {_sealOf.Count} sealed, {_sealPrisms.Count} seal prisms.");
        }

        Prism LaySealPrism(int i, bool forReveal)
        {
            int ring = _sealRing[i];
            var rotation = Quaternion.LookRotation(_course[ring].Axis);
            var scale = new Vector3(SpearlineSeals.PrismSize, SpearlineSeals.PrismSize, SpearlineSeals.PrismDepth);
            return PrismTrailBuilder.LayOne(sealPrefab,
                new PrismLay(new SpawnPoint(_sealPositions[i], rotation, scale), Domains.Blue,
                             _sealArmour[i] ? PrismKind.Shielded : PrismKind.Plain),
                _sealRoot, new Trail(), SealOwnerId, admitAuthoredScale: true, watchForReveal: forReveal);
        }

        static bool Holds(Prism prism) => prism && !prism.destroyed && prism.ownerID == SealOwnerId;

        static bool ShieldUp(Prism prism) => prism && prism.prismProperties != null && prism.prismProperties.IsShielded;

        /// <summary>Seal prism <paramref name="i"/> is gone (destroyed, or no longer ours).</summary>
        bool Gone(int i) => !Holds(_sealPrisms[i]);

        /// <summary>Seal prism <paramref name="i"/> breaks under a hull as it crosses: plain, or armour already cracked.</summary>
        bool BreaksUnderHull(int i) => !_sealArmour[i] || !ShieldUp(_sealPrisms[i]);

        /// <summary>Seal prism <paramref name="i"/> stands as laid: there, and its armour (if any) uncracked.</summary>
        bool Intact(int i) => Holds(_sealPrisms[i]) && (!_sealArmour[i] || ShieldUp(_sealPrisms[i]));

        /// <summary>
        /// A sealed ring threads only through a hole: the ring's own mouth, AND a hole of at least the pilot's need round
        /// the crossing (<see cref="SpearlineSeals.HoleSize"/>). The pilot is the one whose hull made this segment
        /// (<see cref="PilotAt"/>); a closed pass wears that pilot's need there (the worn seal). The probe's segment
        /// belongs to no pilot and is judged at the full need.
        /// </summary>
        protected override bool ThreadsGate(int ringIndex, Vector3 prev, Vector3 cur)
        {
            if (!base.ThreadsGate(ringIndex, prev, cur)) return false;
            if (!_sealOf.TryGetValue(ringIndex, out var seal) || seal.Count == 0) return true;
            var gate = _course[ringIndex];
            // The base said this segment crossed the mouth; judge where it met the plane (at cur if it ends on it).
            SpearlineSeals.PlaneCrossing(gate, prev, cur, out var crossing);

            var pilot = PilotAt(cur);
            int passes = pilot != null && _closedPasses.TryGetValue((pilot, ringIndex), out int n) ? n : 0;
            int need = SpearlineSeals.NeedAfter(Settings, passes);
            FillSealState(seal);
            int hole = SpearlineSeals.HoleSize(crossing, gate.Axis, _sealPositions, seal.Start, seal.Adjacency,
                                               _bufGone, _bufBreaks, _bufInHole, _hole);
            bool opens = hole >= need;
            if (pilot != null)
            {
                if (opens) _closedPasses.Remove((pilot, ringIndex));
                else _closedPasses[(pilot, ringIndex)] = passes + 1;
                if (pilot == gameData.LocalPlayer) OnLocalSealCrossed(ringIndex, seal, opens, passes == 0);
            }
            return opens;
        }

        /// <summary>The seal's prisms as they stand now, into the scratch: gone, breaks under a hull, cut by a stroke.</summary>
        void FillSealState(Seal seal)
        {
            for (int k = 0; k < seal.Count; k++)
            {
                int i = seal.Start + k;
                bool gone = Gone(i);
                bool breaks = BreaksUnderHull(i);
                _bufGone[k] = gone;
                _bufBreaks[k] = gone || breaks;
                _bufCut[k] = !gone && breaks;
            }
        }

        /// <summary>The pilot whose hull is at <paramref name="position"/> this frame: the detection loop passes each
        /// hull's own <c>Transform.position</c> as the segment's end, so an exact match names it. Null for the probe.</summary>
        IPlayer PilotAt(Vector3 position)
        {
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var tf = players[i]?.Vessel?.Transform;
                if (tf && tf.position == position) return players[i];
            }
            return null;
        }

        /// <summary>Every peer, every frame: the hint once the turn runs, the local feedback, and a cut seal prism (or a
        /// cracked shield) grows back <see cref="SpearlineSealSettings.RegrowSeconds"/> after the field has passed its
        /// ring. The dead prism is released to its pool first: Spearline owns its seal prisms' exit.</summary>
        void LateUpdate()
        {
            if (!_hintPosted && gameData != null && gameData.IsTurnRunning)
            {
                _hintPosted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(GameToastSituation.SpearlineHint, local.Domain);
            }

            TickFeedback();

            if (_sealPrisms.Count == 0 || !sealPrefab) return;
            float now = Time.time;
            float regrow = Settings.RegrowSeconds;
            int budget = sealRegrowPerFrame;
            for (int n = 0; n < _sealPrisms.Count && budget > 0; n++)
            {
                int i = (_regrowCursor + n) % _sealPrisms.Count;
                if (Intact(i)) { _goneAt[i] = -1f; continue; }
                if (_goneAt[i] < 0f) { _goneAt[i] = now; continue; }
                int ring = _sealRing[i];
                // The clock runs from when the field passed: while anyone is at the ring or flying for it, it restarts.
                if (FieldAtRing(ring)) { _goneAt[i] = now; continue; }
                if (now - _goneAt[i] < regrow) continue;
                if (_sealPrisms[i] && _sealPrisms[i].ownerID == SealOwnerId) Release(_sealPrisms[i]);
                _sealPrisms[i] = LaySealPrism(i, forReveal: false);
                _goneAt[i] = -1f;
                OnSealPrismRegrown(i);
                budget--;
            }
            _regrowCursor = (_regrowCursor + 1) % _sealPrisms.Count;
        }

        /// <summary>Is any pilot flying for this ring now, or any hull at it? Then its seal does not grow back yet
        /// (growing it on a hull, or in front of a pilot mid-approach, would close a hole they were promised).</summary>
        bool FieldAtRing(int ring)
        {
            var gate = _course[ring];
            float reach = gate.Radius + sealClearance;
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                var tf = p?.Vessel?.Transform;
                if (!tf) continue;
                if ((tf.position - gate.Position).sqrMagnitude < reach * reach) return true;
                int threaded = p.RoundStats?.SwitchesThreaded ?? 0;
                if (threaded < RaceLength && RingIndexFor(threaded) == ring) return true;
            }
            return false;
        }

        /// <summary>Return a seal prism, standing or dead, to its pool (or destroy it if it did not come from one).</summary>
        static void Release(Prism prism)
        {
            if (prism && !EnvironmentPrismPool.TryRelease(prism)) Destroy(prism.gameObject);
        }

        void ClearSeals()
        {
            for (int i = 0; i < _sealPrisms.Count; i++)
                if (_sealPrisms[i] && _sealPrisms[i].ownerID == SealOwnerId) Release(_sealPrisms[i]);
            _sealPrisms.Clear();
            _sealPositions.Clear();
            _sealRing.Clear();
            _sealArmour.Clear();
            _goneAt.Clear();
            _sealOf.Clear();
            _closedPasses.Clear();
            _regrowCursor = 0;
            if (_sealRoot) Destroy(_sealRoot.gameObject);
            _sealRoot = null;
        }
    }
}
