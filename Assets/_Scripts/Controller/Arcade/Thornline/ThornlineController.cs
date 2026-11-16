using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Thornline - the Shrike-only sling race, and the Shrike's TIME card (THORNLINE.md). A chain of THORNS - one big
    /// block each - hangs through the cell with a ring at the middle of every leg. The Shrike cruises at 18 u/s, so the
    /// race is its Void Sling: dock on a thorn, aim down the weapons' axis at the next one, sling, and wait out the
    /// cooldown docked. Threading the ring is hitting the thorn behind it.
    ///
    /// <para><b>What the mode adds:</b> the thorns, the Time crystals on the rim lines, an equal start (every seat one
    /// sling from the start thorn, <see cref="TryBuildSpawnPoses"/>), the AI's choice of aim
    /// (<see cref="ThornlineAIPlan"/>, ThornlineController.AI.cs) and what the local pilot sees and hears
    /// (ThornlineController.Feedback.cs). <b>What it does not:</b> nothing about the vessel. The sling, its 12 u anchor
    /// reach, the dock bonus (x1.6 plain, x2.5 super-shielded), the lance that can break a plain thorn and the crystal's
    /// sling reset are the shipped kit.</para>
    ///
    /// <para><b>Every peer lays the same thorns</b> from the broadcast rings (<see cref="ThornlineCourse.ThornsFor"/>), so
    /// nothing new is replicated. A thorn broken by a lance is laid again once no hull is inside its box.</para>
    /// </summary>
    public partial class ThornlineController : GateRaceController, IPlayerSpawnLine
    {
        [Header("Thornline")]
        [Tooltip("The prism every thorn is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_thornline_assets.py. Unset = no thorns, and a warning.")]
        [SerializeField] Prism thornPrefab;

        [Tooltip("Seconds a broken thorn stays gone before it is laid again (once no hull is inside its box).")]
        [SerializeField, Min(0.5f)] float thornRegrowSeconds = 3f;

        [Tooltip("Seconds a taken Time crystal stays gone before it grows back in its place.")]
        [SerializeField, Min(0.5f)] float crystalRegrowSeconds = 5f;

        [Tooltip("An AI Shrike snipes the thorn after next when its line threads both rings, instead of docking on the near " +
                 "one - for the difficulties ThornlineAIPlan.SnipesFromDifficulty allows. Off = no AI snipes.")]
        [SerializeField] bool aiTakesSnipes = true;

        // Every serialized field of the partials lives HERE, in one file, so the order Unity writes them in is fixed
        // (author_thornline_assets.py checks the scene's block verbatim). The code that reads them is in
        // ThornlineController.AI.cs and ThornlineController.Feedback.cs.

        [Header("Thornline - AI (by difficulty: Easy, Medium, Hard, Legendary)")]
        [Tooltip("The AI's nose lead (AIPilot.NoseLeadGain) by its seat's difficulty. With none, the stick's cubic law closes " +
                 "the last degree onto the 12 u the sling catches at 1,000 u so slowly that 14 of 20 I1 races finish " +
                 "(run.sh --ai). 2.5 / 3 / 8 / 16 race 64 / 56 / 49 / 49 s at I1, every seat finishing every rung.")]
        [SerializeField] float[] aiNoseLeadByDifficulty = { 2.5f, 3f, 8f, 16f };

        [Tooltip("The AI's nose follow rate (VesselTransformer.AINoseFollowPerSecond) by difficulty; below 0 = the fleet lag " +
                 "the nose lead is stated at. Set on every arming, so no other mode's rate carries into this race.")]
        [SerializeField] float[] aiNoseFollowByDifficulty = { -1f, -1f, -1f, -1f };

        [Tooltip("How far past the aim the nose lead may put the command, degrees. Tight, so the lead sharpens the last few " +
                 "degrees (at 40 the replica's Hard lost its lead over Medium at I2, run.sh --ai).")]
        [SerializeField, Range(1f, 40f)] float aiNoseLeadMaxExtraDegrees = 10f;

        [Header("Thornline - toasts (local pilot; text on GameToastConfig_Thornline)")]
        [SerializeField] GameToastSituation hintToast = GameToastSituation.ThornlineHint;
        [SerializeField] GameToastSituation snipeToast = GameToastSituation.ThornlineSnipe;
        [SerializeField] GameToastSituation crystalResetToast = GameToastSituation.ThornlineCrystalReset;
        [SerializeField] GameToastSituation crystalTakenToast = GameToastSituation.ThornlineCrystalTaken;
        [SerializeField] GameToastSituation caughtShortToast = GameToastSituation.ThornlineCaughtShort;
        [SerializeField] GameToastSituation passedToast = GameToastSituation.ThornlinePassed;
        [SerializeField] GameToastSituation overtakenToast = GameToastSituation.ThornlineOvertaken;

        [Tooltip("A caught-short dock is called this many times a race, then only heard.")]
        [SerializeField, Min(0)] int caughtShortToastsPerRace = 2;

        [Tooltip("Seconds between two pass / passed toasts.")]
        [SerializeField, Min(0f)] float passToastSeconds = 1.5f;

        [Header("Thornline - audio (all shipped empty, FMOD convention)")]
        [Tooltip("A sling that threaded two rings at once. 2D, local.")]
        [SerializeField] FMODUnity.EventReference snipeEvent;
        [Tooltip("The local pilot took a Time crystal: the sling is ready. 2D, local.")]
        [SerializeField] FMODUnity.EventReference crystalResetEvent;
        [Tooltip("A rival took the crystal on the local pilot's leg first. 2D, local.")]
        [SerializeField] FMODUnity.EventReference crystalTakenEvent;
        [Tooltip("A dock on the thorn that was aimed at. 2D, local.")]
        [SerializeField] FMODUnity.EventReference cleanDockEvent;
        [Tooltip("A dock caught short on a trail or a nest, off any thorn. 2D, local.")]
        [SerializeField] FMODUnity.EventReference caughtShortEvent;
        [Tooltip("The sight turned ready (the sling reloaded). 2D, local.")]
        [SerializeField] FMODUnity.EventReference sightReadyEvent;
        [Tooltip("The local pilot passed a rival. 2D, local.")]
        [SerializeField] FMODUnity.EventReference passedEvent;
        [Tooltip("A rival passed the local pilot. 2D, local.")]
        [SerializeField] FMODUnity.EventReference overtakenEvent;
        [Tooltip("A broken thorn grew back. 3D at the thorn, every peer.")]
        [SerializeField] FMODUnity.EventReference thornRegrowEvent;

        [Header("Thornline - the sight")]
        [Tooltip("The sight's size while the sling has just fired, as a share of its full size (it grows to full as it reloads).")]
        [SerializeField, Range(0.05f, 1f)] float sightReloadingScale = 0.35f;

        [Tooltip("How bright the thorn a rival sits on glows in that rival's colour (0 = off).")]
        [SerializeField, Range(0f, 1f)] float rivalThornGlow = 0.35f;


        const string ThornOwnerId = "Thornline";

        sealed class ThornSlot
        {
            public Thorn Thorn;
            public Prism Prism;
            public float GoneAt = -1f;
        }

        sealed class CrystalSlot
        {
            public Vector3 Position;
            public int Leg;                 // the far thorn of the leg it sits on
            public Crystal Crystal;
            public ElementalCrystalImpactor Impactor;
            public float RegrowAt = -1f;
            public bool SeenLive;           // the local feedback's last read (ThornlineController.Feedback.TickCrystals)
        }

        readonly List<ThornSlot> _thorns = new();
        readonly List<CrystalSlot> _crystals = new();
        readonly List<Vector3> _thornScratch = new();
        readonly List<Thorn> _laidScratch = new();
        readonly List<Vector3> _crystalScratch = new();
        Transform _thornRoot;
        float _nextCheck;

        protected override string ModeName => "Thornline";

        protected override RaceCourseSource CreateCourseSource() => new ThornlineCourseSource();

        public override void OnNetworkDespawn()
        {
            ClearThorns();
            ClearFeedback();
            base.OnNetworkDespawn();
        }

        // ── The start: every seat one sling from the start thorn ─────────

        /// <summary>Seats on a cone below the start thorn, every one <see cref="ThornlineCourse.StartSeatDistance"/> from
        /// it and facing it (<see cref="ThornlineCourse.StartPoses"/>): the first sling is the same shot for everyone, and
        /// the walk keeps every later thorn and ring off those lines. Seed-free, so it is ready before the course is.</summary>
        public bool TryBuildSpawnPoses(int count, out Pose[] poses)
        {
            // The cell must answer first, or the poses would latch on the origin (the SerpentTime rule).
            poses = null;
            if (!gameData || (cellData && !Cell.FindByRuntimeData(cellData))) return false;
            poses = ThornlineCourse.StartPoses(count);
            Vector3 centre = ResolveCellCentre();
            for (int i = 0; i < poses.Length; i++) poses[i].position += centre;
            return true;
        }

        // ── Laying (every peer, from the broadcast rings) ────────────────

        protected override void OnCourseRaised()
        {
            ClearThorns();
            var settings = ThornlineCourse.ForIntensity(Intensity);
            settings.RingCount = _course.Count;
            ThornlineCourse.ThornsFor(_course, _thornScratch);
            ThornlineCourse.Lay(_thornScratch, settings, _laidScratch);
            if (thornPrefab)
            {
                _thornRoot = new GameObject("ThornlineThorns").transform;
                for (int i = 0; i < _laidScratch.Count; i++)
                {
                    var slot = new ThornSlot { Thorn = _laidScratch[i] };
                    slot.Prism = Lay(slot.Thorn, forReveal: true);
                    _thorns.Add(slot);
                }
            }
            else
            {
                CSDebug.LogWarning("[Thornline] no thorn prefab on the controller - the race has nothing to sling to.");
            }
            RebuildPlanLists();

            // The crystals are measured cell-relative like the walk, so the chain is moved back to the cell first.
            Vector3 centre = ResolveCellCentre();
            for (int i = 0; i < _thornScratch.Count; i++) _thornScratch[i] -= centre;
            ThornlineCourse.PlaceCrystals(_thornScratch, settings, _crystalScratch);
            if (_crystalScratch.Count > 0 && ElementalCrystalSetSO.Load() == null)
            {
                CSDebug.LogWarning("[Thornline] no ElementalCrystalSet in Resources - the race has no Time crystals.");
                return;
            }
            for (int i = 0; i < _crystalScratch.Count; i++)
            {
                var slot = new CrystalSlot
                {
                    Position = _crystalScratch[i] + centre,
                    Leg = NearestLeg(_thornScratch, _crystalScratch[i]),
                };
                _crystals.Add(slot);
                Mint(slot);
            }
            RebuildPlanLists();
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Thornline] intensity {Intensity}: {_thorns.Count} thorns ({settings.ThornSize:F0} u), " +
                $"{settings.SkipBends} snipes, {_crystals.Count} Time crystals.");
        }

        static int NearestLeg(List<Vector3> thorns, Vector3 p)
        {
            int best = 1;
            float bd = float.MaxValue;
            for (int j = 1; j < thorns.Count; j++)
            {
                float d = ThornlineCourse.DistanceToSegment(p, thorns[j - 1], thorns[j]);
                if (d < bd) { bd = d; best = j; }
            }
            return best;
        }

        /// <summary>Lay one thorn. The first cohort reveals with the arena (<paramref name="forReveal"/>); a regrown thorn
        /// must not hold the arena-ready gate, so it lands as a live ribbon (the /arcadegame Serpent Time rule).</summary>
        Prism Lay(in Thorn thorn, bool forReveal)
        {
            var prism = PrismTrailBuilder.LayOne(thornPrefab,
                new PrismLay(new SpawnPoint(thorn.Position, Quaternion.identity, Vector3.one * thorn.Size), Domains.Blue, thorn.Kind),
                _thornRoot, new Trail(), ThornOwnerId, watchForReveal: forReveal);
            if (prism && !forReveal) prism.CompletesAsLiveRibbon = true;
            return prism;
        }

        /// <summary>Still this race's thorn: alive, and not re-issued to somebody else by the pool.</summary>
        static bool Holds(Prism prism) => prism && !prism.destroyed && prism.ownerID == ThornOwnerId;

        // ── Re-laying and regrowing (every peer) ─────────────────────────

        // A subclass must not declare Update (GateRaceController.Update runs the race); LateUpdate is free.
        void LateUpdate()
        {
            TickFeedback();

            float now = Time.time;
            if (now < _nextCheck) return;
            _nextCheck = now + 0.5f;

            for (int i = 0; i < _thorns.Count; i++)
            {
                var slot = _thorns[i];
                if (Holds(slot.Prism)) { slot.GoneAt = -1f; continue; }
                if (slot.GoneAt < 0f) { slot.GoneAt = now; continue; }
                if (now - slot.GoneAt < thornRegrowSeconds || HullInside(slot.Thorn)) continue;
                slot.Prism = Lay(slot.Thorn, forReveal: false);
                slot.GoneAt = -1f;
                OnThornRegrown(slot.Thorn.Position);
            }

            for (int i = 0; i < _crystals.Count; i++)
            {
                var slot = _crystals[i];
                if (IsLive(slot)) continue;
                if (slot.RegrowAt < 0f)
                {
                    slot.RegrowAt = now + crystalRegrowSeconds;
                    continue;
                }
                if (now < slot.RegrowAt || slot.Crystal) continue;   // one still flying to its collector is left to finish
                Mint(slot);
            }
        }

        /// <summary>A hull inside the broken thorn's box (<see cref="ThornlineCourse.RegrowBlocked"/>) holds its regrow; one
        /// hanging off where the face was does not (it did, by a sphere of size + 30 u, until 2026-11-17).</summary>
        bool HullInside(in Thorn thorn)
        {
            var players = gameData ? gameData.Players : null;
            if (players == null) return false;
            for (int i = 0; i < players.Count; i++)
            {
                var tf = players[i]?.Vessel?.Transform;
                if (tf && ThornlineCourse.RegrowBlocked(thorn.Position, thorn.Size, tf.position)) return true;
            }
            return false;
        }

        // ── The Time crystals (Redline's recipe) ─────────────────────────

        void Mint(CrystalSlot slot)
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

        static bool IsLive(CrystalSlot slot) => slot.Crystal && slot.Impactor && !slot.Impactor.HasBeenCollected;

        void ClearThorns()
        {
            for (int i = 0; i < _thorns.Count; i++)
            {
                var prism = _thorns[i].Prism;
                if (Holds(prism) && !EnvironmentPrismPool.TryRelease(prism)) Destroy(prism.gameObject);
            }
            _thorns.Clear();
            if (_thornRoot && _thornRoot.childCount == 0) Destroy(_thornRoot.gameObject);
            _thornRoot = null;
            for (int i = 0; i < _crystals.Count; i++)
                if (_crystals[i].Crystal) Destroy(_crystals[i].Crystal.gameObject);
            _crystals.Clear();
            RebuildPlanLists();
        }
    }
}
