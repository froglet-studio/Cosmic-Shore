using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Thresher Time - the Thresher-only target race. A closed loop of gates is cut through the cell and every pilot
    /// flies LAPS of it in order; the first domain whose lead runner clears the last gate of the last lap wins, scored on
    /// finish TIME (golf).
    ///
    /// <para><b>No rings: a target at every gate</b> (Yash, 2026-11-04). Each gate is a NEST
    /// (<see cref="ThresherTimeNest"/>): a core inside a cage that arms itself with the intensity. The rings are still the
    /// course (the objective arrow, the race toasts and the AI steer by them) but they are hidden; a gate opens when its
    /// nest's core breaks, by anything - the ball, a chain cut, a charged blast, a ram, a steal - and the pilot whose
    /// next gate it is clears it by being beside it then, or by flying through its wreck before it regrows
    /// (<see cref="ThresherTimeNest.PassesNear"/>). Shorter regrows and a tighter credit radius make the high intensities
    /// a race to break your own target, not to follow someone else's.</para>
    ///
    /// <para><b>The mode is still a question about slinging.</b> The Thresher cruises at 125 u/s on a fixed 120 deg/s
    /// turn; everything past cruise comes from its plant (orbit the planted ball, wind the chain in, let go). A nest adds
    /// the other half of its kit: plant beside a nest and the orbiting chain shreds the cage and charges the ball, and the
    /// charged ball's next hit is a blast that opens the core. See THRESHERTIME.md.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new ability, no new scoring metric, no change to the vessel (CLAUDE.md,
    /// "A game mode never changes the vessel"). The nests are the mode's environment, laid on every peer from the
    /// broadcast course like Serpentine's gates.</para>
    /// </summary>
    public class ThresherTimeController : GateRaceController
    {
        [Header("Thresher Time circuit")]
        [Tooltip("Laps of the gate set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps gates - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = ThresherTimeCourseSource.DefaultLaps;

        [Header("Targets (ThresherTimeNest)")]
        [Tooltip("Off (the shipped setting since 2026-11-07, Yash: \"I dont wanna break anything in here, just pass through " +
                 "waypoints\"): the rings are the gates - thread one to clear it, your next one lime - and no nests are laid. " +
                 "On: a nest to break stands at every gate and the rings hide (THRESHERTIME.md section 0).")]
        [SerializeField] bool nestsAtGates;
        [Tooltip("The prism every nest is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_thresher_time_assets.py. Unset = the rings stay and no nests are laid, with a warning.")]
        [SerializeField] Prism nestPrefab;
        [Tooltip("On intensity 1-4: how near (u) a pilot must be to an opened nest to clear its gate - beside it when it " +
                 "broke, or flying through its wreck before it regrows.")]
        [SerializeField] float[] creditRadiusByIntensity = { 320f, 280f, 240f, 200f };
        [Tooltip("On intensity 1-4: seconds an opened nest stays down before it grows back (it waits on while any hull " +
                 "is inside its reach). Short at the top: break your own.")]
        [SerializeField] float[] regrowSecondsByIntensity = { 3f, 3f, 2.5f, 2f };
        [Tooltip("Prisms a breaking nest blows apart each frame, core outward.")]
        [SerializeField, Min(1)] int nestBurstPerFrame = 48;
        [Tooltip("Speed (u/s) a breaking nest's prisms fly apart at.")]
        [SerializeField, Min(0f)] float nestBurstSpeed = 40f;
        [Tooltip("Prisms a regrowing nest lays each frame.")]
        [SerializeField, Min(1)] int nestRegrowPerFrame = 40;

        [Tooltip("Seconds the lime ring round YOUR next nest takes to bloom in, and the old one to wither out. Every other " +
                 "ring stays hidden; the lime ring is the gate races' own \"this one is yours next\" mark.")]
        [SerializeField, Min(0.01f)] float nextTargetBloomSeconds = 0.35f;

        [Header("The Belfry (ThresherBelfry)")]
        [Tooltip("On (the shipped setting since 2026-11-07): the clockwork course - every ring swings on a chain, maces " +
                 "swing across the straights from I2, danger shutters slide across every mouth at I4, all on the shared " +
                 "clock. Off: the rings stand still.")]
        [SerializeField] bool belfry;
        [Tooltip("The prism the Belfry's chains, maces and shutters are laid from: the environment prism (SpawnablePrism).")]
        [SerializeField] Prism belfryPrismPrefab;
        [Tooltip("Seconds a broken Belfry prism (a chain link, a mace, a shutter bar) takes to grow back where its part now is.")]
        [SerializeField, Min(0.5f)] float belfryRegrowSeconds = 6f;

        [Header("Rewards")]
        [Tooltip("A crystal sits in the core of every Nth nest (crack it open to take it); 0 = the cell's own crystals.")]
        [SerializeField, Min(0)] int crystalEveryNthTarget = 3;

        protected override string ModeName => "Thresher Time";

        /// <summary>The Thresher's circuit (<see cref="ThresherTimeCourseSource"/>): Headlong's solver,
        /// <see cref="ThresherTimeCourse.ForIntensity"/>'s settings, target/laps gates per lap.</summary>
        protected override RaceCourseSource CreateCourseSource() =>
            new ThresherTimeCourseSource { Laps = laps };

        /// <summary>Owner id no pilot carries: the prisms of the nests.</summary>
        const string NestOwnerId = "ThresherTimeNest";

        /// <summary>One gate's nest: its parts, the prisms laid for them (index for index), and its state.</summary>
        sealed class GateNest
        {
            public int Gate;
            public RaceGate Ring;
            public int Intensity;
            public readonly List<ThresherTimeNest.Part> Parts = new();
            public readonly List<Prism> Prisms = new();
            public Trail Trail;
            public bool Standing;
            public bool Bursting;
            public bool Regrowing;
            public int Cursor;
            public float RegrowAt = -1f;
        }

        readonly List<GateNest> _nests = new();
        Transform _nestRoot;
        CrystalManager _keyManager;
        static readonly ProfilerMarker s_tickMarker = new("ThresherTime.Nests");

        public override void OnNetworkDespawn()
        {
            ClearNests();
            ClearBelfry();
            if (IsServer && _keyManager) _keyManager.ClearCourseKeys();
            _keyManager = null;
            base.OnNetworkDespawn();
        }

        // ── The nests (every peer) ────────────────────────────────────────

        protected override void OnCourseRaised()
        {
            ClearNests();
            ClearBelfry();
            if (belfry) RaiseBelfry();
            if (!nestsAtGates) return;   // the rings are the gates
            if (!nestPrefab)
            {
                CSDebug.LogWarning("[ThresherTime] no nestPrefab - the rings stay and no targets are laid. " +
                                   "Re-run Tools/Build/author_thresher_time_assets.py.");
                return;
            }

            _nestRoot = new GameObject("ThresherTimeNests").transform;
            int intensity = Intensity;
            for (int i = 0; i < _course.Count; i++)
            {
                var nest = new GateNest { Gate = i, Ring = _course[i], Intensity = intensity, Trail = new Trail() };
                ThresherTimeNest.Build(_course[i], i, intensity, nest.Parts);
                for (int k = 0; k < nest.Parts.Count; k++) nest.Prisms.Add(LayPart(nest, k, forReveal: true));
                nest.Standing = true;
                _nests.Add(nest);
                if (i < _rings.Count && _rings[i]) _rings[i].SetVisualHidden(true);
            }

            if (IsServer && crystalEveryNthTarget > 0) PlaceRewardCrystals();
        }

        /// <summary>The server puts a crystal in the core of every Nth nest (the crystal manager's course keys, Switchback's
        /// seam): crack the nest open and fly through its heart to take it. It grows back in place.</summary>
        void PlaceRewardCrystals()
        {
            var keys = new List<Vector3>();
            for (int i = crystalEveryNthTarget - 1; i < _nests.Count; i += crystalEveryNthTarget)
                keys.Add(_nests[i].Ring.Position);
            if (keys.Count == 0) return;
            _keyManager = Object.FindAnyObjectByType<CrystalManager>(FindObjectsInactive.Include);
            if (_keyManager) _keyManager.SetCourseKeys(keys);
            else CSDebug.LogWarning("[ThresherTime] no CrystalManager in the scene - the nests hold no crystals.");
        }

        Prism LayPart(GateNest nest, int k, bool forReveal)
        {
            var part = nest.Parts[k];
            var prism = PrismTrailBuilder.LayOne(nestPrefab,
                new PrismLay(new SpawnPoint(part.Position, part.Rotation, part.Scale), Domains.Blue, part.Kind),
                _nestRoot, nest.Trail, NestOwnerId, watchForReveal: forReveal);
            if (prism && !forReveal) prism.CompletesAsLiveRibbon = true;
            return prism;
        }

        /// <summary>Still a prism of its nest: alive, and not taken (a stolen prism is re-owned, a destroyed one goes back
        /// to the pool and is re-issued to somebody else).</summary>
        static bool Holds(Prism prism) => Holds(prism, NestOwnerId);

        static bool Holds(Prism prism, string owner) => prism && !prism.destroyed && prism.ownerID == owner;

        void ClearNests()
        {
            for (int g = 0; g < _nests.Count; g++)
            {
                var nest = _nests[g];
                for (int k = 0; k < nest.Prisms.Count; k++)
                {
                    var prism = nest.Prisms[k];
                    if (Holds(prism)) prism.Damage(Vector3.zero, Domains.Blue, string.Empty, devastate: true);
                }
            }
            _nests.Clear();
            if (_nestRoot) Destroy(_nestRoot.gameObject);
            _nestRoot = null;
        }

        // A subclass must not declare Update (GateRaceController.Update runs the race); LateUpdate is free.
        void LateUpdate()
        {
            if (_belfry.Parts.Count > 0 || _belfry.Rings.Count > 0)
                using (s_belfryMarker.Auto()) TickBelfry();
            if (_nests.Count == 0) return;
            using (s_tickMarker.Auto()) TickNests();
        }

        // ── The Belfry (every peer, posed off the shared clock) ───────────

        const string BelfryOwnerId = "ThresherTimeBelfry";
        readonly ThresherBelfry.Layout _belfry = new();
        readonly List<Prism> _belfryPrisms = new();
        readonly List<float> _belfryLostAt = new();
        readonly Trail _belfryTrail = new();
        Transform _belfryRoot;
        static readonly ProfilerMarker s_belfryMarker = new("ThresherTime.Belfry");

        /// <summary>The time every peer poses the clockwork at: the server's, so every machine sees the same swing.</summary>
        static float SharedTime
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening ? nm.ServerTime.TimeAsFloat : Time.time;
            }
        }

        void RaiseBelfry()
        {
            ThresherBelfry.Build(_course, Intensity, _belfry);
            if (!belfryPrismPrefab)
            {
                CSDebug.LogWarning("[ThresherTime] no belfryPrismPrefab - the rings swing but no chains, maces or shutters " +
                                   "are laid. Re-run Tools/Build/author_thresher_time_assets.py.");
                return;
            }
            _belfryRoot = new GameObject("ThresherTimeBelfry").transform;
            float t = SharedTime;
            for (int k = 0; k < _belfry.Parts.Count; k++)
            {
                _belfryPrisms.Add(LayBelfryPart(k, t, forReveal: true));
                _belfryLostAt.Add(-1f);
            }
        }

        Prism LayBelfryPart(int k, float t, bool forReveal)
        {
            var part = _belfry.Parts[k];
            ThresherBelfry.PoseAt(_belfry, part, t, out var position, out var rotation);
            var prism = PrismTrailBuilder.LayOne(belfryPrismPrefab,
                new PrismLay(new SpawnPoint(position, rotation, Vector3.one * part.Size), Domains.Blue, part.Kind),
                _belfryRoot, _belfryTrail, BelfryOwnerId, watchForReveal: forReveal);
            if (prism && !forReveal) prism.CompletesAsLiveRibbon = true;
            return prism;
        }

        /// <summary>
        /// Every peer, every frame: move each ring to its pendulum's bob (the ring IS the gate, so its mouth, its lime and
        /// the objective arrow go with it), pose every standing chain link, mace and shutter bar, and grow a broken one
        /// back <see cref="belfryRegrowSeconds"/> later where its part now is.
        /// </summary>
        void TickBelfry()
        {
            float t = SharedTime;
            float now = Time.time;
            for (int i = 0; i < _belfry.Rings.Count && i < _rings.Count; i++)
                if (_rings[i]) _rings[i].transform.position = _belfry.Rings[i].BobAt(t);

            for (int k = 0; k < _belfryPrisms.Count; k++)
            {
                var prism = _belfryPrisms[k];
                if (!Holds(prism, BelfryOwnerId))
                {
                    if (_belfryLostAt[k] < 0f) _belfryLostAt[k] = now;
                    else if (now - _belfryLostAt[k] >= belfryRegrowSeconds)
                    {
                        _belfryPrisms[k] = LayBelfryPart(k, t, forReveal: false);
                        _belfryLostAt[k] = -1f;
                    }
                    continue;
                }
                ThresherBelfry.PoseAt(_belfry, _belfry.Parts[k], t, out var position, out var rotation);
                prism.transform.SetPositionAndRotation(position, rotation);
                prism.NotifyPositionChanged();
            }
        }

        void ClearBelfry()
        {
            for (int k = 0; k < _belfryPrisms.Count; k++)
                if (Holds(_belfryPrisms[k], BelfryOwnerId))
                    _belfryPrisms[k].Damage(Vector3.zero, Domains.Blue, string.Empty, devastate: true);
            _belfryPrisms.Clear();
            _belfryLostAt.Clear();
            _belfry.Clear();
            if (_belfryRoot) Destroy(_belfryRoot.gameObject);
            _belfryRoot = null;
        }

        /// <summary>The AI flies at the ring where it IS: the swinging ring's live pose, so its steering and its miss test
        /// read the same mouth the race counts.</summary>
        protected override RaceGate ChooseAIGate(IPlayer pilot, int threaded, int ringIndex, RaceGate gate) =>
            ringIndex >= 0 && ringIndex < _belfry.Rings.Count ? ThresherBelfry.GateAt(_belfry, ringIndex, SharedTime) : gate;

        /// <summary>
        /// Every peer, every frame: a standing nest whose core lost a block breaks (opening its gate); a breaking one
        /// blows <see cref="nestBurstPerFrame"/> more prisms apart, core outward; an opened one grows back
        /// <see cref="RegrowSeconds"/> later, once no hull is inside its reach.
        /// </summary>
        void TickNests()
        {
            float now = Time.time;
            for (int g = 0; g < _nests.Count; g++)
            {
                var nest = _nests[g];
                if (nest.Standing)
                {
                    for (int k = 0; k < nest.Parts.Count; k++)
                    {
                        if (!nest.Parts[k].Core) break;   // the core is listed first
                        if (Holds(nest.Prisms[k])) continue;
                        nest.Standing = false;
                        nest.Bursting = true;
                        nest.Cursor = 0;
                        break;
                    }
                }

                if (nest.Bursting)
                {
                    int budget = nestBurstPerFrame;
                    while (budget > 0 && nest.Cursor < nest.Prisms.Count)
                    {
                        var prism = nest.Prisms[nest.Cursor++];
                        if (!Holds(prism)) continue;
                        Vector3 impact = (prism.transform.position - nest.Ring.Position).normalized * nestBurstSpeed;
                        prism.Damage(impact, Domains.Blue, string.Empty, devastate: true);
                        budget--;
                    }
                    if (nest.Cursor >= nest.Prisms.Count)
                    {
                        nest.Bursting = false;
                        nest.RegrowAt = now + RegrowSeconds;
                    }
                    continue;
                }

                if (!nest.Standing && !nest.Regrowing && now >= nest.RegrowAt && !HullWithin(nest))
                {
                    nest.Regrowing = true;
                    nest.Cursor = 0;
                }

                if (nest.Regrowing)
                {
                    int budget = nestRegrowPerFrame;
                    while (budget > 0 && nest.Cursor < nest.Parts.Count)
                    {
                        int k = nest.Cursor++;
                        if (Holds(nest.Prisms[k])) continue;
                        nest.Prisms[k] = LayPart(nest, k, forReveal: false);
                        budget--;
                    }
                    if (nest.Cursor >= nest.Parts.Count)
                    {
                        nest.Regrowing = false;
                        nest.RegrowAt = -1f;
                        nest.Standing = true;
                    }
                }
            }
        }

        /// <summary>Is any hull inside this nest's reach (so growing it back now would lay prisms on it)?</summary>
        bool HullWithin(GateNest nest)
        {
            float reach = ThresherTimeNest.OuterReach(nest.Intensity) + 10f;
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var tf = players[i]?.Vessel?.Transform;
                if (tf && (tf.position - nest.Ring.Position).sqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        float CreditRadius => ByIntensity(creditRadiusByIntensity, 240f);
        float RegrowSeconds => ByIntensity(regrowSecondsByIntensity, 3f);

        float ByIntensity(float[] values, float fallback) =>
            values is { Length: > 0 } ? values[Mathf.Clamp(Intensity, 1, values.Length) - 1] : fallback;

        // ── Which nest is yours (local, never replicated) ─────────────────

        /// <summary>
        /// The rings are hidden, so eight blue nests looked alike and nothing said which was yours (Yash, 2026-11-07: "very
        /// hard for me to find out the target"). The local pilot's next gate shows its ring again, round its nest, in the
        /// lime every gate race paints "yours next" (the base controller paints it; this only un-hides it). The ring's
        /// mouth (96 / 72 / 58 / 46 u) is wider than the nest at every intensity and well inside the credit radius, so it
        /// frames the target and tells the truth: fly through it, and you ram the core or clear the opened gate.
        /// </summary>
        protected override void OnLocalNextGateChanged(int previousRing, int nextRing)
        {
            if (_nests.Count == 0) return;   // no nests: the rings were never hidden
            if (previousRing >= 0 && previousRing < _rings.Count && _rings[previousRing])
                _rings[previousRing].SetVisualShown(false, nextTargetBloomSeconds);
            if (nextRing >= 0 && nextRing < _rings.Count && _rings[nextRing])
                _rings[nextRing].SetVisualShown(true, nextTargetBloomSeconds);
        }

        // ── Clearing a gate ───────────────────────────────────────────────

        /// <summary>A gate with a nest is cleared once its nest is open (broken, not yet grown back) and the pilot passes
        /// within <see cref="CreditRadius"/> of it - beside it as it broke, or through its wreck. The rings themselves no
        /// longer count. Without nests (no prefab) the rings thread as before.</summary>
        protected override bool ThreadsGate(int ringIndex, Vector3 prev, Vector3 cur)
        {
            if (ringIndex < 0 || ringIndex >= _nests.Count) return base.ThreadsGate(ringIndex, prev, cur);
            var nest = _nests[ringIndex];
            return !nest.Standing && ThresherTimeNest.PassesNear(nest.Ring.Position, prev, cur, CreditRadius);
        }
    }
}
