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
    /// <para><b>What the mode adds:</b> the thorns, the Time crystals on the rim lines, and the AI's choice of which thorn
    /// to aim at. <b>What it does not:</b> nothing about the vessel. The sling, the dock bonus (x1.6 plain, x2.5
    /// super-shielded), the lance that can break a plain thorn and the crystal's sling reset are the shipped kit.</para>
    ///
    /// <para><b>Every peer lays the same thorns</b> from the broadcast rings (<see cref="ThornlineCourse.ThornsFor"/>), so
    /// nothing new is replicated. A thorn broken by a lance is laid again once nobody is next to it.</para>
    /// </summary>
    public class ThornlineController : GateRaceController
    {
        [Header("Thornline")]
        [Tooltip("The prism every thorn is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_thornline_assets.py. Unset = no thorns, and a warning.")]
        [SerializeField] Prism thornPrefab;

        [Tooltip("Seconds a broken thorn stays gone before it is laid again (once no hull is next to it).")]
        [SerializeField, Min(0.5f)] float thornRegrowSeconds = 3f;

        [Tooltip("Seconds a taken Time crystal stays gone before it grows back in its place.")]
        [SerializeField, Min(0.5f)] float crystalRegrowSeconds = 5f;

        [Tooltip("An AI Shrike snipes the thorn after next when its line threads both rings, instead of docking on the near one.")]
        [SerializeField] bool aiTakesSnipes = true;

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
            public Crystal Crystal;
            public ElementalCrystalImpactor Impactor;
            public float RegrowAt = -1f;
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
            base.OnNetworkDespawn();
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
                    slot.Prism = Lay(slot.Thorn);
                    _thorns.Add(slot);
                }
            }
            else
            {
                CSDebug.LogWarning("[Thornline] no thorn prefab on the controller - the race has nothing to sling to.");
            }

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
                var slot = new CrystalSlot { Position = _crystalScratch[i] + centre };
                _crystals.Add(slot);
                Mint(slot);
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Thornline] intensity {Intensity}: {_thorns.Count} thorns ({settings.ThornSize:F0} u), " +
                $"{settings.SkipBends} snipes, {_crystals.Count} Time crystals.");
        }

        Prism Lay(in Thorn thorn)
        {
            var prism = PrismTrailBuilder.LayOne(thornPrefab,
                new PrismLay(new SpawnPoint(thorn.Position, Quaternion.identity, Vector3.one * thorn.Size), Domains.Blue, thorn.Kind),
                _thornRoot, new Trail(), ThornOwnerId, watchForReveal: true);
            return prism;
        }

        /// <summary>Still this race's thorn: alive, and not re-issued to somebody else by the pool.</summary>
        static bool Holds(Prism prism) => prism && !prism.destroyed && prism.ownerID == ThornOwnerId;

        // ── Re-laying and regrowing (every peer) ─────────────────────────

        // A subclass must not declare Update (GateRaceController.Update runs the race); LateUpdate is free.
        void LateUpdate()
        {
            float now = Time.time;
            if (now < _nextCheck) return;
            _nextCheck = now + 0.5f;

            for (int i = 0; i < _thorns.Count; i++)
            {
                var slot = _thorns[i];
                if (Holds(slot.Prism)) { slot.GoneAt = -1f; continue; }
                if (slot.GoneAt < 0f) { slot.GoneAt = now; continue; }
                if (now - slot.GoneAt < thornRegrowSeconds || HullNear(slot.Thorn.Position, slot.Thorn.Size + 30f)) continue;
                slot.Prism = Lay(slot.Thorn);
                slot.GoneAt = -1f;
            }

            for (int i = 0; i < _crystals.Count; i++)
            {
                var slot = _crystals[i];
                if (IsLive(slot)) continue;
                if (slot.RegrowAt < 0f) { slot.RegrowAt = now + crystalRegrowSeconds; continue; }
                if (now < slot.RegrowAt || slot.Crystal) continue;   // one still flying to its collector is left to finish
                Mint(slot);
            }
        }

        bool HullNear(Vector3 point, float reach)
        {
            var players = gameData ? gameData.Players : null;
            if (players == null) return false;
            for (int i = 0; i < players.Count; i++)
            {
                var tf = players[i]?.Vessel?.Transform;
                if (tf && (tf.position - point).sqrMagnitude < reach * reach) return true;
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

        // ── The AI's choice of thorn (server) ────────────────────────────

        /// <summary>
        /// The AI Shrike aims at a THORN, never at a ring: its autopilot slings when the weapons' axis finds a block, so
        /// the thing to point at is the block behind the ring. In order of preference: the thorn after next when the line
        /// from here threads both rings and clears the near thorn (a snipe, one reload saved); the thorn behind the next
        /// ring when the line from here threads it; otherwise the thorn the next ring's leg starts from (the start thorn
        /// at the gun, or the one a short sling fell short of) - dock there first.
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            if (_thorns.Count < 2 || _course.Count == 0) return false;
            var tf = pilot?.Vessel?.Transform;
            if (tf == null) return false;
            int threaded = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (threaded >= RaceLength) return false;
            int ring = RingIndexFor(threaded);
            if (ring + 1 >= _thorns.Count) return false;
            Vector3 self = tf.position;

            if (aiTakesSnipes && ring + 2 < _thorns.Count && ring + 1 < _course.Count)
            {
                Vector3 far = _thorns[ring + 2].Thorn.Position;
                float nearClear = ThornlineCourse.HalfDiagonal(_thorns[ring + 1].Thorn.Size) + ThornlineCourse.HullRadius + 4f;
                if ((far - self).magnitude <= ThornlineCourse.SlingRange - 30f
                    && Threads(self, far, _course[ring]) && Threads(self, far, _course[ring + 1])
                    && ThornlineCourse.DistanceToSegment(_thorns[ring + 1].Thorn.Position, self, far) > nearClear)
                {
                    target = far;
                    return true;
                }
            }

            Vector3 next = _thorns[ring + 1].Thorn.Position;
            target = Threads(self, next, _course[ring]) ? next : _thorns[ring].Thorn.Position;
            return true;
        }

        static bool Threads(Vector3 from, Vector3 to, in RaceGate ring) =>
            ThornlineCourse.DistanceToSegment(ring.Position, from, to) <= ring.Radius - ThornlineCourse.HullRadius - 2f
            && Vector3.Dot(to - from, ring.Axis) > 0f;

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
        }
    }
}
