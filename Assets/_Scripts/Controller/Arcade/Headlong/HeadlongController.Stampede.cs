using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Headlong's stampede (<see cref="HeadlongHedges"/>): the hedges of mass across the straights, laid on every peer
    /// from the broadcast course, stood back up after the Rhinos have ploughed through, and the Time crystals inside
    /// the tightest apexes. The vessel is untouched: the plough, the burn, the spool and Momentum are its shipped kit.
    /// </summary>
    public partial class HeadlongController
    {
        [Header("Stampede (HeadlongHedges)")]
        [Tooltip("The prism every hedge slab is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_headlong_assets.py. Unset = no hedges, and a warning.")]
        [SerializeField] Prism hedgePrefab;

        [Tooltip("Seconds after a hedge was last disturbed (ploughed, cut) before it stands back up, once no hull is " +
                 "near it. Shorter than a lap (~6-8 s at I1-I4), so every lap meets the hedge whole.")]
        [SerializeField, Min(0.5f)] float hedgeRestandSeconds = 2.5f;

        [Tooltip("Slabs a hedge puts back each frame while it stands up.")]
        [SerializeField, Min(1)] int hedgeRestandPerFrame = 40;

        [Tooltip("Seconds a taken Time crystal stays gone before it grows back in its place (Redline's number).")]
        [SerializeField, Min(0.5f)] float stampedeCrystalRegrowSeconds = 5f;

        [Tooltip("An AI Rhino swerves through a thorn hedge's plain window instead of ploughing the thorns, on every lap " +
                 "but the last (where a burn has no wind-ups left to cost). Off = it ploughs every hedge.")]
        [SerializeField] bool aiTakesThornWindows = true;

        [Tooltip("How far ahead (u) an AI Rhino starts its swerve for a thorn window.")]
        [SerializeField, Min(50f)] float aiWindowRunUp = 400f;

        const string HedgeOwnerId = "HeadlongHedge";

        sealed class StandingHedge
        {
            public HeadlongHedges.Hedge Hedge;
            public readonly List<HeadlongHedges.Part> Parts = new();
            public readonly List<Prism> Prisms = new();
            public Trail Trail;
            public float DisturbedAt = -1f;
            public int Cursor = -1;   // >= 0 while standing back up
        }

        sealed class CrystalSlot
        {
            public Vector3 Position;
            public Crystal Crystal;
            public ElementalCrystalImpactor Impactor;
            public float RegrowAt = -1f;
        }

        readonly List<StandingHedge> _hedges = new();
        readonly List<CrystalSlot> _stampedeCrystals = new();
        readonly List<HeadlongHedges.Hedge> _hedgeScratch = new();
        readonly List<Vector3> _crystalScratch = new();
        Transform _hedgeRoot;
        float _nextStampedeCheck;

        public override void OnNetworkDespawn()
        {
            ClearStampede();
            base.OnNetworkDespawn();
        }

        // ── Laying (every peer, from the broadcast course) ───────────────────

        protected override void OnCourseRaised()
        {
            ClearStampede();
            HeadlongHedges.Place(_course, Intensity, _hedgeScratch);
            if (hedgePrefab)
            {
                _hedgeRoot = new GameObject("HeadlongHedges").transform;
                for (int h = 0; h < _hedgeScratch.Count; h++)
                {
                    var standing = new StandingHedge { Hedge = _hedgeScratch[h], Trail = new Trail() };
                    HeadlongHedges.Build(standing.Hedge, standing.Parts);
                    for (int k = 0; k < standing.Parts.Count; k++) standing.Prisms.Add(Lay(standing, k, forReveal: true));
                    _hedges.Add(standing);
                }
            }
            else if (_hedgeScratch.Count > 0)
            {
                CSDebug.LogWarning("[Headlong] no hedge prefab on the controller - the stampede has no hedges.");
            }

            HeadlongHedges.PlaceCrystals(_course, Intensity, _hedgeScratch, _crystalScratch);
            if (_crystalScratch.Count > 0 && ElementalCrystalSetSO.Load() == null)
            {
                CSDebug.LogWarning("[Headlong] no ElementalCrystalSet in Resources - the stampede has no Time crystals.");
                return;
            }
            for (int i = 0; i < _crystalScratch.Count; i++)
            {
                var slot = new CrystalSlot { Position = _crystalScratch[i] };
                _stampedeCrystals.Add(slot);
                Mint(slot);
            }
            CSDebug.Log($"[Headlong] STAMPEDE intensity {Intensity}: {_hedges.Count} hedges " +
                        $"({_hedgeScratch.FindAll(x => x.Danger).Count} danger), {_stampedeCrystals.Count} Time crystals.");
        }

        Prism Lay(StandingHedge standing, int k, bool forReveal)
        {
            var part = standing.Parts[k];
            var prism = PrismTrailBuilder.LayOne(hedgePrefab,
                new PrismLay(new SpawnPoint(part.Position, part.Rotation, part.Scale), Domains.Blue, part.Kind),
                _hedgeRoot, standing.Trail, HedgeOwnerId, watchForReveal: forReveal);
            if (prism && !forReveal) prism.CompletesAsLiveRibbon = true;
            return prism;
        }

        /// <summary>Still a slab of its hedge: alive, and not re-issued to somebody else by the pool.</summary>
        static bool Holds(Prism prism) => prism && !prism.destroyed && prism.ownerID == HedgeOwnerId;

        /// <summary>A slab ploughed off its place (the plough throws 2-30 u sideways), or gone.</summary>
        static bool Displaced(Prism prism, in HeadlongHedges.Part part) =>
            !Holds(prism) || (prism.transform.position - part.Position).sqrMagnitude > 1f;

        // ── Standing back up (every peer) ────────────────────────────────────

        // A subclass must not declare Update (GateRaceController.Update runs the race); LateUpdate is free.
        void LateUpdate()
        {
            float now = Time.time;
            TickRestand();
            if (now < _nextStampedeCheck) return;
            _nextStampedeCheck = now + 0.5f;

            for (int h = 0; h < _hedges.Count; h++)
            {
                var standing = _hedges[h];
                if (standing.Cursor >= 0) continue;
                bool disturbed = false;
                for (int k = 0; k < standing.Parts.Count && !disturbed; k++)
                    disturbed = Displaced(standing.Prisms[k], standing.Parts[k]);
                if (!disturbed) { standing.DisturbedAt = -1f; continue; }
                if (HullNear(standing.Hedge)) { standing.DisturbedAt = now; continue; }
                if (standing.DisturbedAt < 0f) standing.DisturbedAt = now;
                if (now - standing.DisturbedAt >= hedgeRestandSeconds) standing.Cursor = 0;
            }

            for (int i = 0; i < _stampedeCrystals.Count; i++)
            {
                var slot = _stampedeCrystals[i];
                if (IsLive(slot)) continue;
                if (slot.RegrowAt < 0f) { slot.RegrowAt = now + stampedeCrystalRegrowSeconds; continue; }
                if (now < slot.RegrowAt || slot.Crystal) continue;   // one still flying to its collector is left to finish
                Mint(slot);
            }
        }

        void TickRestand()
        {
            for (int h = 0; h < _hedges.Count; h++)
            {
                var standing = _hedges[h];
                if (standing.Cursor < 0) continue;
                int budget = hedgeRestandPerFrame;
                while (budget > 0 && standing.Cursor < standing.Parts.Count)
                {
                    int k = standing.Cursor++;
                    var part = standing.Parts[k];
                    var prism = standing.Prisms[k];
                    if (!Displaced(prism, part)) continue;
                    if (Holds(prism))
                    {
                        prism.transform.SetPositionAndRotation(part.Position, part.Rotation);
                        prism.NotifyPositionChanged();
                    }
                    else
                    {
                        standing.Prisms[k] = Lay(standing, k, forReveal: false);
                    }
                    budget--;
                }
                if (standing.Cursor >= standing.Parts.Count)
                {
                    standing.Cursor = -1;
                    standing.DisturbedAt = -1f;
                }
            }
        }

        /// <summary>Is any hull near enough this hedge that standing it back up now would lay slabs on it?</summary>
        bool HullNear(in HeadlongHedges.Hedge hedge)
        {
            float reach = hedge.Radius + 150f;
            var players = gameData ? gameData.Players : null;
            if (players == null) return false;
            for (int i = 0; i < players.Count; i++)
            {
                var tf = players[i]?.Vessel?.Transform;
                if (tf && (tf.position - hedge.Centre).sqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        // ── The Time crystals (Redline's recipe) ─────────────────────────────

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

        // ── The AI's choice at a thorn (server) ──────────────────────────────

        /// <summary>
        /// An AI Rhino swerves for a thorn hedge's plain window when the hedge is on its leg, ahead of it and within
        /// <see cref="aiWindowRunUp"/>, on every lap but the last: the choice <c>headlong_stampede_model.py</c>'s reading
        /// pilot makes on most early laps (a burn costs every wind-up left in the race, so the last lap ploughs). A
        /// choice of the mode's AI, not of the vessel.
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            if (!aiTakesThornWindows || _hedges.Count == 0 || _course.Count == 0) return false;
            var tf = pilot?.Vessel?.Transform;
            if (tf == null) return false;
            int threaded = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (threaded <= 0 || threaded >= RaceLength) return false;
            int n = _course.Count;
            if (threaded >= RaceLength - n) return false;   // the last lap ploughs
            int leg = (RingIndexFor(threaded) - 1 + n) % n;
            Vector3 self = tf.position;
            for (int h = 0; h < _hedges.Count; h++)
            {
                var hedge = _hedges[h].Hedge;
                if (hedge.Leg != leg || hedge.Kind != HeadlongHedges.Kind.Thorn) continue;
                float s = Vector3.Dot(self - hedge.Centre, hedge.Normal);
                if (s >= 0f || s < -aiWindowRunUp) continue;
                target = hedge.Window;
                return true;
            }
            return false;
        }

        void ClearStampede()
        {
            for (int h = 0; h < _hedges.Count; h++)
            {
                var prisms = _hedges[h].Prisms;
                for (int i = 0; i < prisms.Count; i++)
                    if (Holds(prisms[i]) && !EnvironmentPrismPool.TryRelease(prisms[i]))
                        Destroy(prisms[i].gameObject);
            }
            _hedges.Clear();
            if (_hedgeRoot && _hedgeRoot.childCount == 0) Destroy(_hedgeRoot.gameObject);
            _hedgeRoot = null;
            for (int i = 0; i < _stampedeCrystals.Count; i++)
                if (_stampedeCrystals[i].Crystal) Destroy(_stampedeCrystals[i].Crystal.gameObject);
            _stampedeCrystals.Clear();
        }
    }
}
