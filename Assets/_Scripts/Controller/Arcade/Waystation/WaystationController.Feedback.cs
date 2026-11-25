using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lantern Towers (Docs/Studios/TIGER_MOTH_TIME.md §6, 2026-11-11): what a Drop Tower shows in the world.
    ///
    /// <para><b>The lanterns</b> (every peer): two dotted posts beside every hop that has a hop after it, from the
    /// window's near edge down to a little short of the hop's SHELF - the deepest landing that still recharges and rolls
    /// by the next press point (<see cref="WaystationTower.ShelfDepth"/>). A tight hop's posts are visibly shorter. They are
    /// laid from the broadcast course like the walls, as shielded Blue environment prisms, and regrow with them.</para>
    ///
    /// <para><b>The crown gems</b> (I4, every peer, local like Headlong's stampede crystals): a Time crystal on each
    /// tower's plunge line, <see cref="WaystationTower.GemSkip"/> short of the deepest landing, so the deepest plunge skips
    /// it. Collected by the ordinary crystal rule.</para>
    ///
    /// <para><b>The grade</b> (the LOCAL pilot only, nothing replicated): each landing on a tower ring is graded against
    /// its lanterns (<see cref="WaystationHopPlan.GradeOn"/>). A deep landing lights that hop's lanterns in the pilot's
    /// domain colour (a <see cref="PrismLit"/> light: the prisms stay Blue), a clean one half as bright, and a run of deep
    /// landings lights the whole tower at 3, 6 and 10. A sound slot per grade and per tier, all shipped empty.</para>
    ///
    /// <para><b>The vessel is not changed</b>: no field, asset or code of the Tiger Moth is touched. The lanterns and
    /// gems are environment, the grade reads the Drop executor's public state.</para>
    /// </summary>
    public partial class WaystationController
    {
        [Header("Lantern Towers")]
        [Tooltip("Seconds a landing's light stays up on its hop's lanterns before it fades.")]
        [SerializeField, Min(0.1f)] float hopLightSeconds = 1f;

        [Tooltip("Seconds a streak tier's light stays up over the whole tower.")]
        [SerializeField, Min(0.1f)] float streakLightSeconds = 1.5f;

        [Tooltip("Seconds before a collected crown gem stands again.")]
        [SerializeField, Min(0.5f)] float crownGemRegrowSeconds = 5f;

        [Header("Lantern Towers - audio (local pilot)")]
        [Tooltip("A landing in the deep band. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference hopDeepEvent;

        [Tooltip("A landing inside the lanterns, above the deep band. Shipped empty.")]
        [SerializeField] FMODUnity.EventReference hopCleanEvent;

        [Tooltip("A landing short of the window (on or before the floor plate). Shipped empty.")]
        [SerializeField] FMODUnity.EventReference hopShortEvent;

        [Tooltip("A landing past the hop's far edge (the next ring is out of reach). Shipped empty.")]
        [SerializeField] FMODUnity.EventReference hopPastEvent;

        [Tooltip("Streak tiers 1, 2 and 3 (3, 6 and 10 deep landings in a row). Shipped empty.")]
        [SerializeField] FMODUnity.EventReference[] streakTierEvents = new FMODUnity.EventReference[3];

        [Tooltip("A streak of at least one tier broken. Shipped empty.")]
        [SerializeField] FMODUnity.EventReference streakBrokenEvent;

        [Tooltip("A crown gem collected by the local pilot. Shipped empty.")]
        [SerializeField] FMODUnity.EventReference crownGemEvent;

        const string LanternOwnerId = "WaystationTowerLantern";

        /// <summary>The PrismLit source ids this mode publishes under: one per hop, one for the streak.</summary>
        const int LightIdBase = 0x57A70000;
        const int StreakLightId = LightIdBase + 0xFFFF;

        sealed class LanternPost
        {
            public readonly List<WaystationLantern> Segments = new();
            public Prism[] Pieces;
            public readonly Trail Trail = new();
            public int NextPiece;
        }

        sealed class GemSlot
        {
            public Vector3 Position;   // world
            public Crystal Crystal;
            public ElementalCrystalImpactor Impactor;
            public float RegrowAt = -1f;
        }

        readonly List<LanternPost> _lanternPosts = new();
        readonly List<GemSlot> _gems = new();
        static readonly List<WaystationLantern> s_lanterns = new(512);
        static readonly List<Vector3> s_gems = new(8);
        Transform _lanternRoot;

        // The local pilot's grade state.
        TigerMothDropExecutor _localDrop;
        Transform _localHull;
        bool _localWasDropping;
        int _localDropRing = -1;
        int _localDropThreaded = -1;
        WaystationHopPlan.Streak _streak;
        readonly List<(int ring, float strength, float until)> _hopLights = new();
        float _streakLightUntil;
        int _streakLightTier;
        int _streakFrom = -1, _streakTo = -1;

        /// <summary>Lay the lanterns and the crown gems from the course this peer was sent.</summary>
        void RaiseLanternsAndGems()
        {
            ClearLanternsAndGems();
            if (wallPrefab)
            {
                WaystationTower.Lanterns(_course, Intensity, s_lanterns);
                if (s_lanterns.Count > 0)
                {
                    _lanternRoot = new GameObject($"{ModeName}Lanterns").transform;
                    _lanternRoot.SetParent(transform, false);
                    LanternPost post = null;
                    int lastGate = -1;
                    for (int i = 0; i < s_lanterns.Count; i++)
                    {
                        var seg = s_lanterns[i];
                        if (post == null || seg.Gate != lastGate)
                        {
                            post = new LanternPost();
                            _lanternPosts.Add(post);
                            lastGate = seg.Gate;
                        }
                        post.Segments.Add(seg);
                    }
                    foreach (var p in _lanternPosts)
                    {
                        p.Pieces = new Prism[p.Segments.Count];
                        for (int k = 0; k < p.Segments.Count; k++) LayLantern(p, k, forReveal: true);
                    }
                }
            }

            WaystationTower.CrownGems(_course, Intensity, s_gems);
            if (s_gems.Count == 0) return;
            if (ElementalCrystalSetSO.Load() == null)
            {
                CSDebug.LogWarning($"[{ModeName}] no ElementalCrystalSet in Resources - the towers have no crown gems.");
                return;
            }
            foreach (var local in s_gems)
            {
                var slot = new GemSlot { Position = transform.TransformPoint(local) };
                _gems.Add(slot);
                MintGem(slot);
            }
        }

        void LayLantern(LanternPost post, int index, bool forReveal)
        {
            var seg = post.Segments[index];
            var prism = PrismTrailBuilder.LayOne(wallPrefab,
                new PrismLay(new SpawnPoint(seg.Position, seg.Rotation, seg.Scale), Domains.Blue, PrismKind.SuperShielded),
                _lanternRoot, post.Trail, LanternOwnerId, watchForReveal: forReveal);
            if (!prism) return;
            if (!forReveal) prism.CompletesAsLiveRibbon = true;   // a regrown post must not hold the arena-ready gate
            post.Pieces[index] = prism;
        }

        static bool IsOurLantern(Prism prism) =>
            prism && !prism.destroyed && prism.ownerID == LanternOwnerId && prism.Domain == Domains.Blue;

        /// <summary>Each post grows one missing segment back per regrow tick, as the walls do.</summary>
        void TickLanternRegrow()
        {
            foreach (var post in _lanternPosts)
            {
                for (int step = 0; step < post.Pieces.Length; step++)
                {
                    int k = (post.NextPiece + step) % post.Pieces.Length;
                    var piece = post.Pieces[k];
                    Vector3 world = _lanternRoot ? _lanternRoot.TransformPoint(post.Segments[k].Position)
                                                 : post.Segments[k].Position;
                    if (!WaystationTower.TileNeedsRegrow(piece, piece && piece.destroyed, IsOurLantern(piece),
                            piece ? Vector3.Distance(piece.transform.position, world) : 0f)) continue;
                    LayLantern(post, k, forReveal: false);
                    post.NextPiece = k + 1;
                    break;
                }
            }
        }

        // ── The crown gems (Headlong's stampede recipe) ──────────────────────

        void MintGem(GemSlot slot)
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

        static bool IsLive(GemSlot slot) => slot.Crystal && slot.Impactor && !slot.Impactor.HasBeenCollected;

        void TickGems()
        {
            float now = Time.time;
            for (int i = 0; i < _gems.Count; i++)
            {
                var slot = _gems[i];
                if (IsLive(slot)) continue;
                if (slot.RegrowAt < 0f)
                {
                    slot.RegrowAt = now + crownGemRegrowSeconds;
                    if (_localHull && Vector3.Distance(_localHull.position, slot.Position) < 60f) Play(crownGemEvent);
                    continue;
                }
                if (now < slot.RegrowAt || slot.Crystal) continue;   // one still flying to its collector is left to finish
                MintGem(slot);
            }
        }

        void ClearLanternsAndGems()
        {
            foreach (var post in _lanternPosts)
            {
                if (post.Pieces == null) continue;
                foreach (var piece in post.Pieces)
                    if (IsOurLantern(piece) && !EnvironmentPrismPool.TryRelease(piece))
                        Destroy(piece.gameObject);
            }
            _lanternPosts.Clear();
            if (_lanternRoot && _lanternRoot.childCount == 0) Destroy(_lanternRoot.gameObject);
            _lanternRoot = null;
            foreach (var slot in _gems)
                if (slot.Crystal) Destroy(slot.Crystal.gameObject);
            _gems.Clear();
            _hopLights.Clear();
            _streak = default;
            _localDropRing = -1;
            _pending = (-1, default, 0, 0f);
        }

        // ── The local pilot's grade ──────────────────────────────────────────

        /// <summary>
        /// Watch the local pilot's Drop: at the press, note which tower ring is next; at the landing, grade the depth
        /// against that ring's lanterns and step the streak. A ring the drop did not thread breaks the streak whatever
        /// the depth. Reads only public state (the executor's <see cref="TigerMothDropExecutor.IsDropping"/>, the
        /// pilot's threaded count), so it changes nothing on any machine but this one's screen and speakers.
        /// </summary>
        void TickLocalGrade()
        {
            var local = gameData.LocalPlayer;
            var hull = local?.Vessel?.VesselStatus?.Transform;
            if (!hull || local.RoundStats == null) return;
            if (hull != _localHull)
            {
                _localHull = hull;
                _localDrop = hull.GetComponentInChildren<TigerMothDropExecutor>(true);
                _localWasDropping = false;
            }
            if (!_localDrop) return;

            bool dropping = _localDrop.IsDropping;
            int threaded = local.RoundStats.SwitchesThreaded;
            var s = WaystationTowerSettings.ForIntensity(Intensity);
            if (dropping && !_localWasDropping)
            {
                _localDropRing = threaded >= 1 && threaded < _course.Count && WaystationTower.IsTowerRing(_course[threaded], s)
                    ? threaded : -1;
                _localDropThreaded = threaded;
            }
            else if (!dropping && _localWasDropping && _localDropRing >= 0)
            {
                // Graded now, settled when the ring's credit arrives: on a client the threaded count moves only after
                // the server's round trip, so a landing waits up to MissGraceSeconds before it counts as a miss.
                int ring = _localDropRing;
                _localDropRing = -1;
                Vector3 press = transform.TransformPoint(WaystationTower.PressOf(_course, ring, s));
                Vector3 axis = transform.TransformDirection(_course[ring].Axis);
                float depth = Vector3.Dot(hull.position - press, axis);
                _pending = (ring, WaystationHopPlan.GradeOn(_course, ring, depth, s), _localDropThreaded, Time.time);
            }
            _localWasDropping = dropping;

            if (_pending.ring >= 0)
            {
                bool threadedIt = threaded > _pending.threadedAtPress;
                if (threadedIt || Time.time - _pending.landedAt > MissGraceSeconds)
                {
                    int tierBefore = _streak.Tier;
                    _streak = WaystationHopPlan.Step(_streak, _pending.grade, missedRing: !threadedIt);
                    OnLocalHopGraded(_pending.ring, threadedIt ? _pending.grade : HopGrade.Past, tierBefore);
                    _pending = (-1, default, 0, 0f);
                }
            }
        }

        /// <summary>How long a landing waits for its ring's credit before it counts as a miss (a client's round trip).</summary>
        const float MissGraceSeconds = 1f;

        (int ring, HopGrade grade, int threadedAtPress, float landedAt) _pending = (-1, default, 0, 0f);

        void OnLocalHopGraded(int ring, HopGrade grade, int tierBefore)
        {
            switch (grade)
            {
                case HopGrade.Deep:
                    Play(hopDeepEvent);
                    _hopLights.Add((ring, 1f, Time.time + hopLightSeconds));
                    break;
                case HopGrade.Clean:
                    Play(hopCleanEvent);
                    _hopLights.Add((ring, 0.5f, Time.time + hopLightSeconds));
                    break;
                case HopGrade.Short:
                    Play(hopShortEvent);
                    break;
                default:
                    Play(hopPastEvent);
                    break;
            }
            if (_streak.Tier > tierBefore)
            {
                int tier = _streak.Tier;
                if (streakTierEvents != null && tier - 1 < streakTierEvents.Length) Play(streakTierEvents[tier - 1]);
                TowerSpan(ring, out _streakFrom, out _streakTo);
                _streakLightTier = tier;
                _streakLightUntil = Time.time + streakLightSeconds;
            }
            else if (_streak.Count == 0 && tierBefore > 0)
            {
                Play(streakBrokenEvent);
            }
        }

        /// <summary>The run of tower rings (course indices) the ring <paramref name="ring"/> belongs to.</summary>
        void TowerSpan(int ring, out int from, out int to)
        {
            var s = WaystationTowerSettings.ForIntensity(Intensity);
            from = to = ring;
            while (from - 1 >= 1 && WaystationTower.IsTowerRing(_course[from - 1], s)) from--;
            while (to + 1 < _course.Count && WaystationTower.IsTowerRing(_course[to + 1], s)) to++;
        }

        /// <summary>Publish this frame's lantern lights: each graded hop for its moment, and the streak over its tower.
        /// PrismLit wants a light reported every frame it is up; one that stops fades over its afterglow.</summary>
        void TickLanternLights()
        {
            var local = gameData.LocalPlayer;
            if (local == null) return;
            Domains domain = local.Domain;
            var s = WaystationTowerSettings.ForIntensity(Intensity);
            float now = Time.time;
            for (int i = _hopLights.Count - 1; i >= 0; i--)
            {
                var (ring, strength, until) = _hopLights[i];
                if (now > until || ring >= _course.Count)
                {
                    PrismLit.ClearLight(LightIdBase + ring);
                    _hopLights.RemoveAt(i);
                    continue;
                }
                PrismLit.PublishLight(LightIdBase + ring, HopVolume(ring, s), strength, domain, hopLightSeconds * 0.5f);
            }
            if (_streakLightTier > 0)
            {
                if (now > _streakLightUntil || _streakFrom < 0 || _streakTo >= _course.Count)
                {
                    PrismLit.ClearLight(StreakLightId);
                    _streakLightTier = 0;
                }
                else
                {
                    Vector3 a = transform.TransformPoint(WaystationTower.PressOf(_course, _streakFrom, s));
                    Vector3 b = transform.TransformPoint(_course[_streakTo].Position);
                    Vector3 centre = 0.5f * (a + b);
                    float radius = 0.5f * Vector3.Distance(a, b) + s.HopRingRadius + WaystationTower.PlateMargin + 40f;
                    PrismLit.PublishLight(StreakLightId, LitVolume.Sphere(centre, radius), 0.4f + 0.2f * _streakLightTier,
                                          domain, streakLightSeconds * 0.5f);
                }
            }
        }

        /// <summary>A cylinder down the hop's drop line, wide enough to take in both lantern posts.</summary>
        LitVolume HopVolume(int ring, in WaystationTowerSettings s)
        {
            Vector3 press = transform.TransformPoint(WaystationTower.PressOf(_course, ring, s));
            Vector3 axis = transform.TransformDirection(_course[ring].Axis);
            float reach = WaystationTower.ShelfDepth(_course, ring, s) + WaystationTower.LanternSeg;
            return LitVolume.Cylinder(press, axis, reach, s.HopRingRadius + WaystationTower.PlateMargin + 12f, false);
        }

        static void Play(FMODUnity.EventReference sound)
        {
            if (!sound.IsNull) CosmicShore.Core.AudioSystem.Instance.PlaySFXEvent(sound);
        }

        /// <summary>Everything the lanterns' feedback runs each frame of a running turn.</summary>
        void TickLanternTowers()
        {
            TickGems();
            TickLocalGrade();
            TickLanternLights();
        }
    }
}
