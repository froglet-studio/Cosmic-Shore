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
    /// beside every ring. Blow it with your own bomb and the gate is yours - a pilot who passes within the credit radius of
    /// an OPEN keg clears its ring - and the blast that opened it is the launch down the next leg. The ring mouth still
    /// clears the gate as it always did, so the kegs are a second way in, never a wall.
    ///
    /// <para><b>Every peer lays the same kegs</b> from the broadcast course in <see cref="OnCourseRaised"/>, and every
    /// peer breaks its own copy from the replicated bombs, as Thresher Time's nests did. Nothing new is replicated: the
    /// pilot's owner decides a gate's credit (<see cref="ThreadsGate"/>) and the server records it.</para>
    ///
    /// <para><b>The vessel is not touched</b> (CLAUDE.md, "A game mode never changes the vessel"): the kegs are the
    /// track, the AI changes are this controller's aim, and every word and sound is an authored field.</para>
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

        [Header("Powder kegs: toasts (GameToastConfig_GrizzlyTime)")]
        [Tooltip("Posted once to the local pilot as the race starts: how a keg is played.")]
        [SerializeField] GameToastSituation hintToast = GameToastSituation.GrizzlyTimeHint;
        [Tooltip("Posted to the local pilot on a BANK SHOT (a keg blown from its exit side threw you down the lane); {0} = " +
                 "how many this race.")]
        [SerializeField] GameToastSituation bankShotToast = GameToastSituation.GrizzlyTimeBankShot;
        [Tooltip("Post the bank-shot toast on the first bank shot and then every Nth.")]
        [SerializeField, Min(1)] int bankShotEveryN = 4;
        [Tooltip("How far (deg) the launch may point off the keg's exit lane and still count as a bank shot.")]
        [SerializeField, Range(5f, 60f)] float bankShotMaxDegrees = 30f;

        [Header("Powder kegs: sounds (ship empty; /sound-brief)")]
        [Tooltip("A keg bursts (3D, at the keg, every peer).")]
        [SerializeField] FMODUnity.EventReference kegBurstEvent;
        [Tooltip("A shielded keg core loses its shield (I3-I4; 3D, at the keg, every peer).")]
        [SerializeField] FMODUnity.EventReference kegStrippedEvent;
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
        }

        readonly List<KegSite> _kegs = new();
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
            TickKegs();
            TickLocalFeedback();
        }

        // ── The kegs (every peer, every frame) ─────────────────────────────

        /// <summary>
        /// A standing keg whose core lost a block bursts (opening its gate); a bursting one blows
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
                    if (broken)
                    {
                        site.Standing = false;
                        site.Bursting = true;
                        site.Cursor = 0;
                        site.StrippedAt = -1f;
                        KegsBroken++;
                        PlayAt(kegBurstEvent, site.Keg.Centre);
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

        /// <summary>A keg broke beside the local hull: watch its launch for a moment (the push lands the frame the bomb
        /// goes off or the next) and call it a bank shot if it points down the keg's exit lane.</summary>
        void WatchForBankShot(int keg, float now)
        {
            var tf = HullOf(gameData.LocalPlayer);
            if (!tf || (tf.position - _kegs[keg].Keg.Centre).sqrMagnitude > GrizzlyTimeKegs.FullReach * GrizzlyTimeKegs.FullReach)
                return;
            _bankShotKeg = keg;
            _bankShotUntil = now + 0.3f;
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
            if (!GrizzlyTimeKegs.IsBankShot(keg.Centre, hull.position, transformer.VelocityShift, keg.ExitDir,
                                            bankShotMaxDegrees))
                return;
            _bankShotKeg = -1;
            _bankShots++;
            if (!bankShotEvent.IsNull) CosmicShore.Core.AudioSystem.Instance.PlaySFXEvent(bankShotEvent);
            if (_bankShots == 1 || _bankShots % bankShotEveryN == 0)
                GameToastAPI.Post(bankShotToast, player.Domain, _bankShots.ToString());
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

        // ── The AI's aim (server; input only - the bombs stay the vessel's autopilot) ──

        /// <summary>One AI's race state: when it last cleared a gate, which gate's keg it gave up on, and whether it is
        /// being steered back to its ring after a stall.</summary>
        sealed class RacerAI
        {
            public int Threaded = -1;
            public float ThreadedAt;
            public int GaveUpOn = -1;
            public bool Rescue;
            public float RescueSince;
        }

        readonly Dictionary<IPlayer, RacerAI> _aiState = new();

        RacerAI RacerFor(IPlayer pilot)
        {
            if (!_aiState.TryGetValue(pilot, out var state))
            {
                state = new RacerAI { ThreadedAt = Time.time };
                _aiState[pilot] = state;
            }
            return state;
        }

        protected override void OnRacerArmed(IPlayer pilot)
        {
            if (pilot != null) _aiState[pilot] = new RacerAI { ThreadedAt = Time.time };
        }

        /// <summary>Stamp each AI's last cleared gate, so a stalled one can be steered back to its ring.</summary>
        protected override void OnServerTick()
        {
            if (_aiState.Count == 0) return;
            bool running = gameData != null && gameData.IsTurnRunning;
            float now = Time.time;
            foreach (var pair in _aiState)
            {
                var state = pair.Value;
                int threaded = pair.Key?.RoundStats?.SwitchesThreaded ?? 0;
                if (!running || threaded != state.Threaded)
                {
                    state.Threaded = threaded;
                    state.ThreadedAt = now;
                    state.Rescue = false;
                }
            }
        }

        /// <summary>
        /// Where an AI Grizzly flies instead of the ring (Docs/GrizzlyTime/POWDER_KEGS_SPEC.md section 5):
        /// <list type="number">
        /// <item><b>Stall rescue.</b> <see cref="GrizzlyTimeKegs.AIStallSeconds"/> without a gate: a run-up point back along
        /// the ring's axis, then the mouth (handed back to the ring logic within <see cref="GrizzlyTimeKegs.AIStallHandBack"/>
        /// u of it).</item>
        /// <item><b>An open keg</b> (anyone's blast, its own included) within <see cref="GrizzlyTimeKegs.AIKegAimRange"/>:
        /// pass beside it down its exit lane, inside the credit radius.</item>
        /// <item><b>A standing plain keg</b> (I1-I2): the same pass. The autopilot fires a full bomb about every 2.3 s and
        /// blows it behind the hull, so a pass beside the core usually opens it; if the pass comes and the keg still
        /// stands, the AI gives that keg up for this gate and threads the mouth.</item>
        /// <item>A standing <b>shielded</b> keg (I3-I4) takes two hits the AI cannot plan: it threads the mouth.</item>
        /// </list>
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            var self = HullOf(pilot);
            var stats = pilot?.RoundStats;
            if (!self || stats == null || _course.Count == 0) return false;
            int index = stats.SwitchesThreaded;
            if (index < 0 || index >= RaceLength) return false;
            int ring = RingIndexFor(index);
            if (ring < 0 || ring >= _course.Count) return false;
            var state = RacerFor(pilot);
            Vector3 at = self.position;

            if (state.Rescue || Time.time - state.ThreadedAt >= GrizzlyTimeKegs.AIStallSeconds)
            {
                var gate = _course[ring];
                Vector3 runUp = gate.Position - RaceCourseGeometry.SafeNormalize(gate.Axis, Vector3.forward) * GrizzlyTimeKegs.AIStallRunUp;
                if (!state.Rescue)
                {
                    CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[GrizzlyTime] stall rescue for an AI at gate {index}");
                    state.Rescue = true;
                    state.RescueSince = Time.time;
                }
                bool reached = (at - runUp).sqrMagnitude <= GrizzlyTimeKegs.AIStallHandBack * GrizzlyTimeKegs.AIStallHandBack;
                if (!reached && Time.time - state.RescueSince < GrizzlyTimeKegs.AIStallSeconds)
                {
                    target = runUp;
                    return true;
                }
                // At the run-up (or the rescue itself ran out): back to the ring logic for the MOUTH - this gate's keg is
                // given up, so the keg that stalled it cannot pull it back - with a fresh clock.
                state.Rescue = false;
                state.GaveUpOn = index;
                state.ThreadedAt = Time.time;
                return false;
            }

            if (ring >= _kegs.Count || state.GaveUpOn == index) return false;
            var site = _kegs[ring];
            Vector3 pass = site.Keg.Centre + site.Keg.ExitDir * GrizzlyTimeKegs.AIKegPassOffset;
            if ((at - site.Keg.Centre).sqrMagnitude > GrizzlyTimeKegs.AIKegAimRange * GrizzlyTimeKegs.AIKegAimRange) return false;
            bool open = !site.Standing;
            bool plain = GrizzlyTimeKegs.CoreKind(Intensity) == PrismKind.Plain;
            if (!open && !plain) return false;
            if (!open && (at - pass).sqrMagnitude <= GrizzlyTimeKegs.AIKegGiveUp * GrizzlyTimeKegs.AIKegGiveUp)
            {
                state.GaveUpOn = index;
                return false;
            }
            target = pass;
            return true;
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
