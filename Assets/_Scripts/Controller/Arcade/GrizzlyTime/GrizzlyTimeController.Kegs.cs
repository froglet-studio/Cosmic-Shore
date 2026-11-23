using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Time's POWDER KEGS (<see cref="GrizzlyTimeKegs"/>, Docs/GrizzlyTime/POWDER_KEGS_SPEC.md): a keg of prisms
    /// beside every ring. Blow it with your own bomb, or Rush through it, and the gate is yours - a pilot who passes
    /// within the credit radius of an OPEN keg clears its ring - and a bomb blown from the keg's exit side is the launch
    /// down the next leg. The ring mouth still clears the gate as it always did, so the kegs are a second way in, never
    /// a wall.
    ///
    /// <para><b>Every peer lays the same kegs</b> from the broadcast course in <see cref="OnCourseRaised"/>, and every
    /// peer breaks its own copy from the replicated bombs, as Thresher Time's nests did. A Rush's ram runs only where
    /// the vessel has authority, so every peer also hears each hull's Rush (<see cref="SyncRamWatches"/>) and opens its
    /// own copy of a keg that hull rams (<see cref="GrizzlyTimeKegs.RamOpens"/>). Nothing new is replicated: the pilot's
    /// owner decides a gate's credit (<see cref="ThreadsGate"/>) and the server records it. <b>What that does not
    /// promise:</b> a peer hears a Rush only when ITS copy of the hull accepted the press, and each copy keeps its own
    /// charges, cooldown clock and Energy (none is replicated). A press made the instant the Energy or the cooldown
    /// allows can be refused on a peer a tick behind, and that peer's keg then stands until it regrows elsewhere; the
    /// rammer's own credit is decided on its owner and is never lost. Deciding the open once, on the hull's owner, and
    /// sending it is the full fix; it is a wire change (a protocol-version bump), so it was left as a decision.</para>
    ///
    /// <para><b>The vessel is not touched</b> (CLAUDE.md, "A game mode never changes the vessel"): the kegs are the
    /// track, the AI (GrizzlyTimeController.AI.cs) is an aim and a press of the hull's own control, and every word and
    /// sound is an authored field.</para>
    /// </summary>
    public partial class GrizzlyTimeController
    {
        [Header("Powder kegs (GrizzlyTimeKegs)")]
        [Tooltip("On: a powder keg stands beside every ring - blow it with your bomb and the gate is yours, and the blast " +
                 "throws you down the next leg. The ring mouth still clears the gate either way. Off: rings only, as before " +
                 "2026-11-11.")]
        [SerializeField] bool kegsAtGates = true;
        [Tooltip("The prism every keg and chalk dot is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_grizzly_time_assets.py. Unset = rings only, with a warning.")]
        [SerializeField] Prism kegPrefab;
        [Tooltip("Prisms a breaking keg blows apart each frame, core outward (doubled on the local pilot's final lap).")]
        [SerializeField, Min(1)] int kegBurstPerFrame = 24;
        [Tooltip("Speed (u/s) a breaking keg's prisms fly apart at.")]
        [SerializeField, Min(0f)] float kegBurstSpeed = 60f;
        [Tooltip("Prisms a regrowing keg lays each frame.")]
        [SerializeField, Min(1)] int kegRegrowPerFrame = 15;
        [Tooltip("Seconds a lost chalk dot takes to grow back (it waits on while a hull is near it).")]
        [SerializeField, Min(0.5f)] float chalkRegrowSeconds = 3f;
        [Tooltip("Prisms of the crown that bursts off a ring as YOU clear it (local, debris only - it never stands). " +
                 "0 = no crown.")]
        [SerializeField, Min(0)] int crownPrisms = 16;
        [Tooltip("How far (u) outside the ring's mouth the crown bursts from.")]
        [SerializeField, Min(0f)] float crownOutset = 14f;

        [Header("Powder kegs: AI")]
        [Tooltip("On: an AI Grizzly presses Rush for pace on a straight it is lined up on (its own bound control, through " +
                 "the replicated press). Off: it never presses Rush, as before 2026-11-20.")]
        [SerializeField] bool aiRushes = true;
        [Tooltip("On: an AI Grizzly rams a STANDING keg open with Rush when the line through its core to the next ring is " +
                 "no longer than the line through the mouth plus aiKegRamSlack (it opens any keg, shielded too, and clears " +
                 "the gate). Off: it threads the mouth past every standing keg.")]
        [SerializeField] bool aiRamsKegs = true;
        [Tooltip("How much longer (u) the line through a keg's core to the next ring may be than the line through the " +
                 "mouth for the AI still to ram the keg.")]
        [SerializeField] float aiKegRamSlack = 0f;

        [Header("Powder kegs: toasts (GameToastConfig_GrizzlyTime)")]
        [Tooltip("Posted once to the local pilot as the race starts: how a keg is played.")]
        [SerializeField] GameToastSituation hintToast = GameToastSituation.GrizzlyTimeHint;
        [Tooltip("Posted to the local pilot on a BANK SHOT (a keg broke beside you and a bomb threw you down its exit " +
                 "lane); {0} = how many this race.")]
        [SerializeField] GameToastSituation bankShotToast = GameToastSituation.GrizzlyTimeBankShot;
        [Tooltip("Post the bank-shot toast on the first bank shot and then every Nth.")]
        [SerializeField, Min(1)] int bankShotEveryN = 4;
        [Tooltip("How far (deg) the launch may point off the keg's exit lane and still count as a bank shot.")]
        [SerializeField, Range(5f, 60f)] float bankShotMaxDegrees = 30f;
        [Tooltip("Seconds after a keg breaks beside the local hull that its launch is still read for a bank shot. Long " +
                 "enough that a keg rammed open with a Rush, then a bomb-jump down its lane, is one too.")]
        [SerializeField, Min(0.1f)] float bankShotWindowSeconds = 1.5f;
        [Tooltip("Posted to the local pilot when its own Rush rams a keg open; {0} = how many this race.")]
        [SerializeField] GameToastSituation ramKeyToast = GameToastSituation.GrizzlyTimeRamKey;
        [Tooltip("Post the rammed-open toast on the first keg the local pilot rams open and then every Nth.")]
        [SerializeField, Min(1)] int ramKeyEveryN = 4;

        [Header("Powder kegs: sounds (a new slot ships empty; /sound-brief)")]
        [Tooltip("A keg bursts (3D, at the keg, every peer).")]
        [SerializeField] FMODUnity.EventReference kegBurstEvent;
        [Tooltip("A shielded keg core loses its shield (I3-I4; 3D, at the keg, every peer).")]
        [SerializeField] FMODUnity.EventReference kegStrippedEvent;
        [Tooltip("A Rush rams a keg open (3D, at the keg, every peer; it plays with the burst).")]
        [SerializeField] FMODUnity.EventReference kegRammedEvent;
        [Tooltip("The local pilot's bank shot (2D, local).")]
        [SerializeField] FMODUnity.EventReference bankShotEvent;
        [Tooltip("The crown bursting off a ring the local pilot just cleared (3D, at the ring, local).")]
        [SerializeField] FMODUnity.EventReference crownBurstEvent;

        /// <summary>Owner id no pilot carries: the prisms of the kegs and their chalk.</summary>
        const string KegOwnerId = "GrizzlyTimeKeg";

        /// <summary>One ring's keg: its parts and the prisms laid for them (index for index), its chalk, and its state.</summary>
        sealed class KegSite
        {
            public GrizzlyTimeKegs.Keg Keg;
            public readonly List<GrizzlyTimeKegs.Part> Parts = new();
            public readonly List<Prism> Prisms = new();
            public readonly List<GrizzlyTimeKegs.Part> Chalk = new();
            public readonly List<Prism> ChalkPrisms = new();
            public readonly List<float> ChalkLostAt = new();
            public Trail Trail;
            public bool Standing;
            public bool Bursting;
            public bool Regrowing;
            public int Cursor;
            public float RegrowAt = -1f;
            /// <summary>When the standing core was first seen without its shield (I3-I4), or -1.</summary>
            public float StrippedAt = -1f;
            /// <summary>When a standing keg's hoop is next looked over for slabs to lay back.</summary>
            public float MendAt;
        }

        /// <summary>One hull's Rush, heard on this peer: the vessel raises <c>OnRam</c> wherever the replicated press
        /// lands AND that peer's copy of the hull accepts it (its own charges, cooldown and Energy), and for
        /// <see cref="GrizzlyTimeKegs.RamSeconds"/> after it that hull opens the kegs it ploughs into.</summary>
        sealed class RamWatch
        {
            public IPlayer Pilot;
            public GrizzlyRushActionExecutor Rush;
            public System.Action Heard;
            public float Until = -1f;
        }

        /// <summary>Seconds between two looks for a hull whose Rush is not heard yet (a late join, a swapped vessel).</summary>
        const float RamSyncSeconds = 1f;
        /// <summary>A keg that breaks this long (s) after a ram ended still reads as that ram's (the burst is seen a frame
        /// or two after the vessel's own last sweep).</summary>
        const float RamCreditGrace = 0.25f;
        /// <summary>Extra seconds on the END of the ram's window for a hull this machine does not fly: its press arrives a
        /// round trip late and its replica trails its owner, so the last of the owner's ram would fall outside the
        /// window here. Zero for a hull this machine flies, whose window is the vessel's own.</summary>
        const float RamLatencyGrace = 0.2f;

        readonly List<KegSite> _kegs = new();
        readonly List<RamWatch> _rams = new();
        float _ramSyncAt;
        int _ramKeys;
        readonly List<GrizzlyTimeKegs.Keg> _kegScratch = new();
        readonly List<Prism> _crown = new();
        readonly Trail _crownTrail = new();
        Vector3 _crownCentre;
        Transform _kegRoot;
        bool _hintPosted;
        int _bankShots;
        int _bankShotKeg = -1;
        float _bankShotUntil = -1f;
        static readonly ProfilerMarker s_kegMarker = new("GrizzlyTime.Kegs");

        /// <summary>Kegs that broke on this peer this race (the soak and the harness read it).</summary>
        public int KegsBroken { get; private set; }

        /// <summary>The local pilot's bank shots this race.</summary>
        public int BankShots => _bankShots;

        /// <summary>Kegs the local pilot's own Rush rammed open this race.</summary>
        public int RamKeys => _ramKeys;

        public override void OnNetworkDespawn()
        {
            ClearKegs();
            _aiState.Clear();
            base.OnNetworkDespawn();
        }

        // ── Laying the kegs (every peer) ──────────────────────────────────

        protected override void OnCourseRaised()
        {
            ClearKegs();
            _hintPosted = false;
            _bankShots = 0;
            _ramKeys = 0;
            KegsBroken = 0;
            if (!kegsAtGates) return;
            if (!kegPrefab)
            {
                CSDebug.LogWarning("[GrizzlyTime] no kegPrefab - the rings stay and no kegs are laid. " +
                                   "Re-run Tools/Build/author_grizzly_time_assets.py.");
                return;
            }

            _kegRoot = new GameObject("GrizzlyTimeKegs").transform;
            int intensity = Intensity;
            GrizzlyTimeKegs.KegsFor(_course, _kegScratch);
            bool chalk = GrizzlyTimeKegs.HasChalk(intensity);
            for (int k = 0; k < _kegScratch.Count; k++)
            {
                var site = new KegSite { Keg = _kegScratch[k], Trail = new Trail() };
                GrizzlyTimeKegs.Build(site.Keg, intensity, site.Parts);
                for (int p = 0; p < site.Parts.Count; p++) site.Prisms.Add(Lay(site.Parts[p], site.Trail, forReveal: true));
                if (chalk)
                {
                    GrizzlyTimeKegs.Chalk(site.Keg, _course, site.Chalk);
                    for (int d = 0; d < site.Chalk.Count; d++)
                    {
                        site.ChalkPrisms.Add(Lay(site.Chalk[d], site.Trail, forReveal: true));
                        site.ChalkLostAt.Add(-1f);
                    }
                }
                site.Standing = true;
                _kegs.Add(site);
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[GrizzlyTime] {_kegs.Count} powder kegs at I{intensity} ({GrizzlyTimeKegs.PrismsPerLap(intensity)} prisms max)");
        }

        Prism Lay(in GrizzlyTimeKegs.Part part, Trail trail, bool forReveal)
        {
            var prism = PrismTrailBuilder.LayOne(kegPrefab,
                new PrismLay(new SpawnPoint(part.Position, part.Rotation, part.Scale), Domains.Blue, part.Kind),
                _kegRoot, trail, KegOwnerId, watchForReveal: forReveal);
            if (prism && !forReveal) prism.CompletesAsLiveRibbon = true;
            return prism;
        }

        /// <summary>Still a live prism of the mode: alive, and not taken (a stolen prism is re-owned).</summary>
        static bool Holds(Prism prism) => prism && !prism.destroyed && prism.ownerID == KegOwnerId;

        /// <summary>Still THIS slot's prism: alive, the mode's, and standing where its part was laid. The position matters:
        /// a destroyed prism goes back to <c>EnvironmentPrismPool</c> (a stack, last in first out) and is often re-issued to
        /// another keg slot under the same owner id, so a stale reference would otherwise read as alive - a regrow would
        /// skip half the cage, and another keg's break would open this one.</summary>
        static bool Holds(Prism prism, Vector3 at) => Holds(prism) && (prism.transform.position - at).sqrMagnitude < 1f;

        /// <summary>The hull of <paramref name="pilot"/>, or null when it has none or it was destroyed (the null-conditional
        /// operator does not see a destroyed Unity object).</summary>
        static Transform HullOf(IPlayer pilot)
        {
            var vessel = pilot?.Vessel;
            if (vessel == null || vessel is UnityEngine.Object uo && !uo) return null;
            var tf = vessel.Transform;
            return tf ? tf : null;
        }

        void ClearKegs()
        {
            for (int k = 0; k < _kegs.Count; k++)
            {
                var site = _kegs[k];
                for (int p = 0; p < site.Prisms.Count; p++)
                    if (Holds(site.Prisms[p], site.Parts[p].Position))
                        site.Prisms[p].Damage(Vector3.zero, Domains.Blue, string.Empty, devastate: true);
                for (int d = 0; d < site.ChalkPrisms.Count; d++)
                    if (Holds(site.ChalkPrisms[d], site.Chalk[d].Position))
                        site.ChalkPrisms[d].Damage(Vector3.zero, Domains.Blue, string.Empty, devastate: true);
            }
            _kegs.Clear();
            ClearRamWatches();
            BurstCrown();
            _bankShotKeg = -1;
            if (_kegRoot) Destroy(_kegRoot.gameObject);
            _kegRoot = null;
        }

        // A subclass must not declare Update (GateRaceController.Update runs the race); LateUpdate is free.
        void LateUpdate()
        {
            if (_crown.Count > 0) BurstCrown();
            if (_kegs.Count == 0) return;
            using var _ = s_kegMarker.Auto();
            SyncRamWatches(Time.time);
            TickKegs();
            TickLocalFeedback();
        }

        // ── The kegs (every peer, every frame) ─────────────────────────────

        /// <summary>
        /// A standing keg whose core lost a block, or whose core a ramming hull is in, bursts (opening its gate); a
        /// bursting one blows
        /// <see cref="kegBurstPerFrame"/> more prisms apart, core outward; an opened one grows back
        /// <see cref="GrizzlyTimeKegs.RegrowSeconds"/> later, once no hull is inside <see cref="GrizzlyTimeKegs.HullClear"/>.
        /// At I3-I4 a standing core that lost its shield shields itself again <see cref="GrizzlyTimeKegs.ReshieldSeconds"/>
        /// later. A lost chalk dot grows back on its own clock.
        /// </summary>
        void TickKegs()
        {
            float now = Time.time;
            int intensity = Intensity;
            bool shielded = GrizzlyTimeKegs.CoreKind(intensity) == PrismKind.Shielded;
            int burst = kegBurstPerFrame * (LocalOnFinalLap() ? 2 : 1);
            for (int k = 0; k < _kegs.Count; k++)
            {
                var site = _kegs[k];
                TickChalk(site, now);

                if (site.Standing)
                {
                    bool broken = false, stripped = false;
                    for (int p = 0; p < GrizzlyTimeKegs.CoreBlocks && p < site.Prisms.Count; p++)
                    {
                        var core = site.Prisms[p];
                        if (!Holds(core, site.Parts[p].Position)) { site.Prisms[p] = null; broken = true; break; }
                        if (shielded && core.prismProperties is { IsShielded: false }) stripped = true;
                    }
                    // The Rush: the vessel's ram broke the core where it has authority (read whose it was); everywhere
                    // else this peer opens its own copy for a ramming hull in the core, so the keg is open on every peer.
                    IPlayer rammer = null;
                    if (broken) TryRammer(site.Keg, now, GrizzlyTimeKegs.RamCreditReach, RamCreditGrace, out rammer);
                    else if (TryRammer(site.Keg, now, GrizzlyTimeKegs.RamReach, 0f, out rammer)) broken = true;
                    if (broken)
                    {
                        site.Standing = false;
                        site.Bursting = true;
                        site.Cursor = 0;
                        site.StrippedAt = -1f;
                        KegsBroken++;
                        PlayAt(kegBurstEvent, site.Keg.Centre);
                        if (rammer != null) OnKegRammed(site, rammer);
                        WatchForBankShot(k, now);
                    }
                    else if (stripped)
                    {
                        if (site.StrippedAt < 0f)
                        {
                            site.StrippedAt = now;
                            PlayAt(kegStrippedEvent, site.Keg.Centre);
                        }
                        else if (now - site.StrippedAt >= GrizzlyTimeKegs.ReshieldSeconds &&
                                 !HullWithin(site.Keg.Centre, GrizzlyTimeKegs.HullClear))
                        {
                            for (int p = 0; p < GrizzlyTimeKegs.CoreBlocks && p < site.Prisms.Count; p++)
                                if (site.Prisms[p].prismProperties is { IsShielded: false }) site.Prisms[p].ActivateShield();
                            site.StrippedAt = -1f;
                        }
                    }
                    else if (now >= site.MendAt)
                    {
                        // A standing keg lays its lost hoop slabs back (a ram that grazed it breaks slabs only where
                        // the vessel has authority; a blast that stripped the core took the hoop on every peer), so
                        // every peer's keg is whole again once no hull is near. The core is never mended: a lost core
                        // block is the keg opening.
                        site.MendAt = now + chalkRegrowSeconds;
                        if (!HullWithin(site.Keg.Centre, GrizzlyTimeKegs.HullClear))
                            for (int p = GrizzlyTimeKegs.CoreBlocks; p < site.Prisms.Count; p++)
                                if (!Holds(site.Prisms[p], site.Parts[p].Position))
                                    site.Prisms[p] = Lay(site.Parts[p], site.Trail, forReveal: false);
                    }
                }

                if (site.Bursting)
                {
                    int budget = burst;
                    while (budget > 0 && site.Cursor < site.Prisms.Count)
                    {
                        int p = site.Cursor++;
                        var prism = site.Prisms[p];
                        site.Prisms[p] = null;   // dead after this either way: never keep a reference the pool can re-issue
                        if (!Holds(prism, site.Parts[p].Position)) continue;
                        Vector3 impact = (prism.transform.position - site.Keg.Centre).normalized * kegBurstSpeed;
                        prism.Damage(impact, Domains.Blue, string.Empty, devastate: true);
                        budget--;
                    }
                    if (site.Cursor >= site.Prisms.Count)
                    {
                        site.Bursting = false;
                        site.RegrowAt = now + GrizzlyTimeKegs.RegrowSeconds(intensity);
                    }
                    continue;
                }

                if (!site.Standing && !site.Regrowing && now >= site.RegrowAt &&
                    !HullWithin(site.Keg.Centre, GrizzlyTimeKegs.HullClear))
                {
                    site.Regrowing = true;
                    site.Cursor = 0;
                }

                if (site.Regrowing)
                {
                    int budget = kegRegrowPerFrame;
                    while (budget > 0 && site.Cursor < site.Parts.Count)
                    {
                        int p = site.Cursor++;
                        if (Holds(site.Prisms[p], site.Parts[p].Position)) continue;
                        site.Prisms[p] = Lay(site.Parts[p], site.Trail, forReveal: false);
                        budget--;
                    }
                    if (site.Cursor >= site.Parts.Count)
                    {
                        site.Regrowing = false;
                        site.RegrowAt = -1f;
                        site.Standing = true;
                    }
                }
            }
        }

        void TickChalk(KegSite site, float now)
        {
            for (int d = 0; d < site.ChalkPrisms.Count; d++)
            {
                if (Holds(site.ChalkPrisms[d], site.Chalk[d].Position)) continue;
                site.ChalkPrisms[d] = null;
                if (site.ChalkLostAt[d] < 0f) { site.ChalkLostAt[d] = now; continue; }
                if (now - site.ChalkLostAt[d] < chalkRegrowSeconds ||
                    HullWithin(site.Chalk[d].Position, GrizzlyTimeKegs.ChalkHullClear)) continue;
                site.ChalkPrisms[d] = Lay(site.Chalk[d], site.Trail, forReveal: false);
                site.ChalkLostAt[d] = -1f;
            }
        }

        /// <summary>Is any hull within <paramref name="radius"/> of <paramref name="at"/> (so laying prisms there now would
        /// lay them on it)?</summary>
        bool HullWithin(Vector3 at, float radius)
        {
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var tf = HullOf(players[i]);
                if (tf && (tf.position - at).sqrMagnitude < radius * radius) return true;
            }
            return false;
        }

        static void PlayAt(FMODUnity.EventReference sound, Vector3 at)
        {
            if (!sound.IsNull) CosmicShore.Core.AudioSystem.Instance.PlaySFXEvent(sound, at);
        }

        // ── The Rush: every hull's ram, heard on every peer ───────────────

        /// <summary>Hear the Rush of every Grizzly in the race: subscribe each hull's <c>OnRam</c> once, and drop a watch
        /// whose hull is gone or was swapped. The vessel is read, never changed: the event is the one its HUD listens to.</summary>
        void SyncRamWatches(float now)
        {
            if (now < _ramSyncAt) return;
            _ramSyncAt = now + RamSyncSeconds;
            for (int i = _rams.Count - 1; i >= 0; i--)
            {
                var watch = _rams[i];
                var hull = HullOf(watch.Pilot);
                if (watch.Rush && hull && watch.Rush.transform.IsChildOf(hull)) continue;
                if (watch.Rush) watch.Rush.OnRam -= watch.Heard;
                _rams.RemoveAt(i);
            }
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var pilot = players[i];
                if (pilot == null || IsRamWatched(pilot)) continue;
                var hull = HullOf(pilot);
                var rush = hull ? hull.GetComponentInChildren<GrizzlyRushActionExecutor>(true) : null;
                if (!rush) continue;
                var watch = new RamWatch { Pilot = pilot, Rush = rush };
                watch.Heard = () => watch.Until = Time.time + GrizzlyTimeKegs.RamSeconds;
                rush.OnRam += watch.Heard;
                _rams.Add(watch);
            }
        }

        bool IsRamWatched(IPlayer pilot)
        {
            for (int i = 0; i < _rams.Count; i++)
                if (_rams[i].Pilot == pilot) return true;
            return false;
        }

        void ClearRamWatches()
        {
            for (int i = 0; i < _rams.Count; i++)
                if (_rams[i].Rush) _rams[i].Rush.OnRam -= _rams[i].Heard;
            _rams.Clear();
            _ramSyncAt = 0f;
        }

        /// <summary>Is a hull whose Rush is live (or ended within <paramref name="grace"/> s) within
        /// <paramref name="reach"/> of <paramref name="keg"/>'s core? A silenced pilot's ram deflects (the fleet's silence
        /// rule), so it opens nothing here either.</summary>
        bool TryRammer(in GrizzlyTimeKegs.Keg keg, float now, float reach, float grace, out IPlayer rammer)
        {
            rammer = null;
            for (int i = 0; i < _rams.Count; i++)
            {
                var watch = _rams[i];
                if (now > watch.Until + grace + (watch.Pilot.IsNetworkOwner ? 0f : RamLatencyGrace)) continue;
                var hull = HullOf(watch.Pilot);
                if (!hull || !GrizzlyTimeKegs.RamOpens(hull.position, keg, reach)) continue;
                if (VesselSilence.IsSilenced(watch.Pilot.Vessel.VesselStatus)) continue;
                rammer = watch.Pilot;
                return true;
            }
            return false;
        }

        /// <summary>A Rush rammed <paramref name="site"/> open: its sound on every peer, and the toast for the pilot
        /// whose ram it was.</summary>
        void OnKegRammed(KegSite site, IPlayer rammer)
        {
            PlayAt(kegRammedEvent, site.Keg.Centre);
            if (rammer != gameData.LocalPlayer) return;
            _ramKeys++;
            if (_ramKeys == 1 || _ramKeys % ramKeyEveryN == 0)
                GameToastAPI.Post(ramKeyToast, rammer.Domain, _ramKeys.ToString());
        }

        // ── Clearing a gate ───────────────────────────────────────────────

        /// <summary>The ring mouth clears its gate as it always did; so does passing within the credit radius of the
        /// ring's keg while it is OPEN (broken, not yet grown back) - beside it as it broke, or through its wreck.</summary>
        protected override bool ThreadsGate(int ringIndex, Vector3 prev, Vector3 cur)
        {
            if (base.ThreadsGate(ringIndex, prev, cur)) return true;
            if (ringIndex < 0 || ringIndex >= _kegs.Count) return false;
            var site = _kegs[ringIndex];
            return !site.Standing && ThresherTimeNest.PassesNear(site.Keg.Centre, prev, cur, GrizzlyTimeKegs.CreditRadius(Intensity));
        }

        // ── Local feedback: the hint, the bank shot, the crown ─────────────

        bool LocalOnFinalLap()
        {
            var stats = gameData.LocalPlayer?.RoundStats;
            if (stats == null || _rings.Count == 0 || LapsPerRace <= 1) return false;
            return stats.SwitchesThreaded >= RaceLengthFor(_rings.Count, LeadInGates, LapsPerRace - 1);
        }

        /// <summary>A keg broke beside the local hull: watch its launch for <see cref="bankShotWindowSeconds"/> (a bomb's
        /// push lands the frame it goes off or the next; a keg rammed open is followed by the bomb-jump) and call it a
        /// bank shot if a bomb's push points down the keg's exit lane.</summary>
        void WatchForBankShot(int keg, float now)
        {
            var tf = HullOf(gameData.LocalPlayer);
            if (!tf || (tf.position - _kegs[keg].Keg.Centre).sqrMagnitude > GrizzlyTimeKegs.FullReach * GrizzlyTimeKegs.FullReach)
                return;
            _bankShotKeg = keg;
            _bankShotUntil = now + bankShotWindowSeconds;
        }

        void TickLocalFeedback()
        {
            if (!_hintPosted && gameData.IsTurnRunning)
            {
                _hintPosted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(hintToast, local.Domain);
            }

            if (_bankShotKeg < 0) return;
            if (Time.time > _bankShotUntil || _bankShotKeg >= _kegs.Count) { _bankShotKeg = -1; return; }
            var player = gameData.LocalPlayer;
            var hull = HullOf(player);
            if (!hull) return;
            var status = player.Vessel.VesselStatus;
            var transformer = status != null ? status.VesselTransformer : null;
            if (!transformer) return;
            var keg = _kegs[_bankShotKeg].Keg;
            // The bomb launch rides the throttle along the nose (grizzly-v3, 2026-10-16), so it is not in
            // VelocityShift: read the bomb executor's last launch, landed inside this watch window.
            Vector3 push = transformer.VelocityShift;
            var bombs = BombsOf(hull);
            if (bombs && bombs.LastLaunchTime >= _bankShotUntil - bankShotWindowSeconds)
                push = bombs.LastLaunchPush;
            if (!GrizzlyTimeKegs.IsBankShot(keg.Centre, hull.position, push, keg.ExitDir,
                                            bankShotMaxDegrees))
                return;
            _bankShotKeg = -1;
            _bankShots++;
            if (!bankShotEvent.IsNull) CosmicShore.Core.AudioSystem.Instance.PlaySFXEvent(bankShotEvent);
            if (_bankShots == 1 || _bankShots % bankShotEveryN == 0)
                GameToastAPI.Post(bankShotToast, player.Domain, _bankShots.ToString());
        }

        GrizzlyTriggerBombExecutor _bombs;
        Transform _bombsHull;

        /// <summary>The local hull's bomb executor (cached per hull).</summary>
        GrizzlyTriggerBombExecutor BombsOf(Transform hull)
        {
            if (_bombsHull != hull)
            {
                _bombsHull = hull;
                _bombs = hull ? hull.GetComponentInParent<GrizzlyTriggerBombExecutor>() : null;
                if (!_bombs && hull) _bombs = hull.GetComponentInChildren<GrizzlyTriggerBombExecutor>();
            }
            return _bombs;
        }

        /// <summary>The local pilot cleared <paramref name="previousRing"/>: a crown of debris bursts off its rim. Laid and
        /// blown apart the next frame, so it never stands and nothing new can be hit.</summary>
        protected override void OnLocalNextGateChanged(int previousRing, int nextRing)
        {
            if (crownPrisms <= 0 || !_kegRoot || previousRing < 0 || previousRing >= _course.Count) return;
            if (!gameData.IsTurnRunning) return;
            var gate = _course[previousRing];
            _crownCentre = gate.Position;
            Vector3 axis = RaceCourseGeometry.SafeNormalize(gate.Axis, Vector3.forward);
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            float r = gate.Radius + crownOutset;
            for (int i = 0; i < crownPrisms; i++)
            {
                float a = i * Mathf.PI * 2f / crownPrisms;
                Vector3 radial = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                var prism = PrismTrailBuilder.LayOne(kegPrefab,
                    new PrismLay(new SpawnPoint(gate.Position + radial * r, Quaternion.LookRotation(axis, radial),
                                                Vector3.one * 3f), Domains.Blue, PrismKind.Plain),
                    _kegRoot, _crownTrail, KegOwnerId, watchForReveal: false);
                if (prism) _crown.Add(prism);
            }
            PlayAt(crownBurstEvent, gate.Position);
        }

        void BurstCrown()
        {
            for (int i = 0; i < _crown.Count; i++)
            {
                var prism = _crown[i];
                if (!Holds(prism)) continue;
                // Laid this frame: finish its creation first, or the debris takes the pool prism's pre-creation scale.
                prism.CompleteCreationImmediately();
                prism.CompleteGrowthImmediately();
                Vector3 impact = RaceCourseGeometry.SafeNormalize(prism.transform.position - _crownCentre, Vector3.up) * kegBurstSpeed;
                prism.Damage(impact, Domains.Blue, string.Empty, devastate: true);
            }
            _crown.Clear();
        }
    }
}
