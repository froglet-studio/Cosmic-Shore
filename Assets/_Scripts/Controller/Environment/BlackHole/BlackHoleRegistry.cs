using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The list of live black holes and the ONE driver that runs them (Docs/BLACK_HOLE.md).
    ///
    /// Spawn and despawn go through here (the console, the test harness and any future toy or
    /// mode all call the same two methods), and every frame the driver runs the three systems a
    /// hole acts through, in this order and from one <c>LateUpdate</c>:
    ///
    /// <list type="number">
    /// <item><see cref="BlackHoleGravityField"/> — moves the prism bodies (live gameplay data:
    /// the movers contract, one Burst job, one bulk render write, one bulk index write).</item>
    /// <item><see cref="BlackHoleVesselPull"/> — pulls vessels through their transformer's
    /// velocity-shift channel.</item>
    /// <item><see cref="BlackHoleWarp"/> — publishes the GPU warp bank (photons only) and
    /// reconciles which prisms near a horizon carry the high-poly mesh.</item>
    /// </list>
    ///
    /// The driver runs at execution order 29500: after every gameplay <c>LateUpdate</c> that
    /// might move a hole or a prism, after the debris carrier (29000), and before
    /// <c>PrismRenderService</c>'s transform flush (30000) — so a body's new pose and the warp
    /// bank both land in the frame that computed them. One writer for the bank, for the reason
    /// the speed tunnel records: N per-hole writers of an un-ref-counted global stomp each other.
    /// </summary>
    public static class BlackHoleRegistry
    {
        public const string ConfigResourcePath = "BlackHoleConfig";

        static readonly List<BlackHole> _holes = new();
        // Despawning holes: no longer pulling, still publishing a falling warp weight until the
        // ease completes and the object destroys itself.
        static readonly List<BlackHole> _fading = new();
        static readonly List<BlackHole> _warpHoles = new();
        static readonly List<BlackHole> _stale = new();
        static readonly List<Pair> _pairs = new();
        static int _nextId = 1;
        static BlackHoleConfigSO _config;
        static bool _configResolved;
        static float _nextReport;

        /// <summary>Every live, non-despawning hole, in spawn order.</summary>
        public static IReadOnlyList<BlackHole> Holes => _holes;

        public static int Count => _holes.Count;

        /// <summary>Live black–white pairs, oldest first (Docs/BLACK_HOLE.md §11).</summary>
        public static IReadOnlyList<Pair> Pairs => _pairs;

        /// <summary>
        /// A black hole and its white hole, born together, drifting apart and back along one axis
        /// and annihilating when they meet (<see cref="BlackHolePairMath"/>). The registry moves both
        /// each frame — paired holes have no velocity of their own.
        /// </summary>
        public sealed class Pair
        {
            public BlackHole Black { get; internal set; }
            public BlackHole White { get; internal set; }
            public Vector3 Midpoint { get; internal set; }
            /// <summary>Unit axis from the black hole to the white hole.</summary>
            public Vector3 Axis { get; internal set; }
            public float HalfGap0 { get; internal set; }
            /// <summary>The speed the two holes close at once let go, u/s (the serialized "drift speed").</summary>
            public float DriftSpeed { get; internal set; }
            /// <summary>Seconds the let-go fall takes to reach <see cref="DriftSpeed"/>.</summary>
            public float CloseRamp { get; internal set; }
            /// <summary>The longest the pair may be HELD before it is let go on its own, seconds.</summary>
            public float Lifetime { get; internal set; }
            /// <summary>Seconds since birth.</summary>
            public float Age { get; internal set; }
            /// <summary>Seconds since it was let go (0 while held).</summary>
            public float CloseAge { get; internal set; }
            /// <summary>True while its owner holds it in place (the Stoat orbiting the black hole).</summary>
            public bool Held { get; internal set; }
            public bool IsAlive => Black != null && White != null && !Black.IsDespawning && !White.IsDespawning;
            public float HalfGap => BlackHolePairMath.ClosingHalfGap(HalfGap0, DriftSpeed, CloseRamp, CloseAge);
        }

        /// <summary>
        /// Tuning. Falls back to the SO's defaults when no <c>Resources/BlackHoleConfig</c> asset
        /// exists, so a spawn works with nothing authored.
        /// </summary>
        public static BlackHoleConfigSO Config
        {
            get
            {
                if (!_configResolved)
                {
                    _config = Resources.Load<BlackHoleConfigSO>(ConfigResourcePath);
                    if (_config == null)
                        _config = ScriptableObject.CreateInstance<BlackHoleConfigSO>();
                    _configResolved = true;
                }
                return _config;
            }
        }

        /// <summary>Forget the cached config so the next read reloads it (editor tooling).</summary>
        public static void InvalidateConfig() => _configResolved = false;

        /// <summary>
        /// Spawn a hole of <paramref name="strength"/> at <paramref name="position"/>. Returns
        /// null (with a warning) past the config's hole budget — the shader bank and the job's
        /// well list are both sized to it, and a hole whose tides the shader cannot draw is not one the
        /// field should pull toward either.
        /// </summary>
        public static BlackHole Spawn(Vector3 position, float strength, Vector3 velocity = default, Vector3? spinAxis = null,
            float horizonRadius = 0f, HolePolarity polarity = HolePolarity.Sink)
        {
            Prune();
            var config = Config;
            if (!HasRoom(1, config)) return null;

            string kind = polarity == HolePolarity.Source ? "WhiteHole" : "BlackHole";
            var go = new GameObject($"[{kind} {_nextId}]");
            go.transform.position = position;
            var hole = go.AddComponent<BlackHole>();   // OnEnable registers it
            hole.Configure(strength, velocity, spinAxis ?? Vector3.forward, horizonRadius, polarity);
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[BlackHole] spawned {kind} #{hole.Id} strength {strength:F1} size {horizonRadius:F1} at {position} GM {hole.GM:F0} " +
                $"horizon {hole.HorizonRadius:F1} influence {hole.InfluenceRadius:F0} velocity {velocity}");
            return hole;
        }

        /// <summary>
        /// The quiet PROBE: could <paramref name="needed"/> more holes spawn right now? No log — a
        /// vessel asks this every trigger release, and a full budget is a normal answer there, not a
        /// fault (CLAUDE.md: a lookup must be askable as a question). <see cref="SpawnPair"/> stays
        /// the loud DEMAND.
        /// </summary>
        public static bool CanSpawn(int needed)
        {
            Prune();
            var config = Config;
            return _holes.Count + needed <= config.MaxBlackHoles && config.IsSane;
        }

        /// <summary>
        /// Room for <paramref name="needed"/> more holes under the budget, with a sane config. Logs
        /// the refusal: the shader bank and the job's well list are both sized to the budget.
        /// </summary>
        static bool HasRoom(int needed, BlackHoleConfigSO config)
        {
            if (_holes.Count + needed > config.MaxBlackHoles)
            {
                CSDebug.LogWarning($"[BlackHole] Spawn refused: {_holes.Count} holes live, {needed} more wanted, and " +
                                   $"BlackHoleConfig.maxBlackHoles is {config.MaxBlackHoles}.");
                return false;
            }
            if (!config.IsSane)
            {
                CSDebug.LogWarning("[BlackHole] Spawn refused: BlackHoleConfig is not sane (see BlackHoleConfigSO.IsSane).");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Spawn a black–white PAIR (Docs/BLACK_HOLE.md §11): the black hole <paramref name="halfGap"/>
        /// along −<paramref name="axis"/> from <paramref name="midpoint"/>, the white hole the same
        /// along +axis, both of <paramref name="strength"/> and <paramref name="horizonRadius"/>. A
        /// <paramref name="held"/> pair stands still until <see cref="LetGo"/> (or until
        /// <paramref name="lifetime"/> lets it go on its own); an unheld one is let go at birth. Let go,
        /// the two fall together, accelerating to <paramref name="driftSpeed"/>, and annihilate where
        /// their horizons touch; what the black hole captures meanwhile the white hole emits.
        /// Null when there is no room for two (nothing is spawned).
        /// </summary>
        public static Pair SpawnPair(Vector3 midpoint, Vector3 axis, float strength, float horizonRadius, float halfGap,
            float driftSpeed, float lifetime, Vector3? spinAxis = null, Transform ownerVessel = null, bool held = false)
        {
            Prune();
            var config = Config;
            if (!HasRoom(2, config)) return null;

            var a = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.right;
            halfGap = Mathf.Max(0f, halfGap);
            BlackHolePairMath.Positions(midpoint, a, halfGap, out var blackPos, out var whitePos);
            var black = Spawn(blackPos, strength, Vector3.zero, spinAxis, horizonRadius, HolePolarity.Sink);
            if (black == null) return null;
            var white = Spawn(whitePos, strength, Vector3.zero, spinAxis, horizonRadius, HolePolarity.Source);
            if (white == null)
            {
                black.BeginDespawn();
                return null;
            }
            black.Throat = white;
            white.Throat = black;
            black.OwnerVessel = ownerVessel;
            white.OwnerVessel = ownerVessel;
            var pair = new Pair
            {
                Black = black, White = white, Midpoint = midpoint, Axis = a, HalfGap0 = halfGap,
                DriftSpeed = Mathf.Max(0f, driftSpeed), CloseRamp = config.PairCloseRampSeconds,
                Lifetime = Mathf.Max(0.01f, lifetime), Age = 0f, Held = held,
            };
            _pairs.Add(pair);
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[BlackHole] pair #{black.Id}/#{white.Id} at {midpoint} axis {a} half-gap {halfGap:F1} closing {driftSpeed:F1} u/s " +
                $"{(held ? $"held (at most {lifetime:F1} s)" : "let go")}");
            return pair;
        }

        /// <summary>
        /// How a let-go pair's <paramref name="hole"/> is moving: each hole falls toward the midpoint at the
        /// pair's closing speed, so a vessel carried out of the white hole takes that velocity with it for the
        /// <paramref name="secondsLeft"/> until the pair meets — otherwise a white hole closing faster than the
        /// hull flies runs it back down (Docs/BLACK_HOLE.md §11). False for a held, still or unpaired hole.
        /// </summary>
        public static bool TryGetMouthMotion(BlackHole hole, out Vector3 velocity, out float secondsLeft)
        {
            velocity = Vector3.zero;
            secondsLeft = 0f;
            if (hole == null) return false;
            for (int i = 0; i < _pairs.Count; i++)
            {
                var pair = _pairs[i];
                if (pair.Black != hole && pair.White != hole) continue;
                if (pair.Held || pair.DriftSpeed <= 0f || !pair.IsAlive) return false;
                secondsLeft = BlackHolePairMath.SecondsLeft(pair.HalfGap0, pair.Black.HorizonRadius, pair.DriftSpeed,
                    pair.CloseRamp, pair.CloseAge);
                if (!(secondsLeft > 0f) || float.IsInfinity(secondsLeft)) return false;
                velocity = (pair.White == hole ? -pair.Axis : pair.Axis) * pair.DriftSpeed;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Let a held pair go: from now the two holes fall together and annihilate where their horizons
        /// touch (the Stoat's trigger release — its pilot slingshots out of the orbit).
        /// </summary>
        public static void LetGo(Pair pair)
        {
            if (pair == null || !pair.Held) return;
            pair.Held = false;
            pair.CloseAge = 0f;
        }

        /// <summary>
        /// The settings a CRYSTAL pair (§13) is opened with: the config's Crystal Pair section, a throat of
        /// the size dial a drift pair's horizon is (<see cref="BlackHoleConfigSO.HorizonRadius"/>), and the
        /// pilots its mouths may carry. A pilot's sling passes only that pilot (a vessel may not move an
        /// opposing vessel — Docs/ELEMENTAL_ECONOMY.md §9); an environmental pair passes everyone.
        /// </summary>
        public static CrystalWormhole.Settings CrystalSettings(float strength, float horizonRadius, Vector3 spinAxis,
            IReadOnlyList<IPlayer> riders)
        {
            var config = Config;
            return new CrystalWormhole.Settings
            {
                Strength = strength,
                ThroatRadius = config.HorizonRadius(strength, horizonRadius),
                LensStrength = config.CrystalLensStrength,
                FeltStrength = config.CrystalFeltStrength,
                FeltCap = config.CrystalFeltCap,
                FeltReach = config.CrystalFeltReach,
                SpinAxis = spinAxis,
                MouthMaterial = config.CrystalMouthMaterial,
                Players = riders,
                MouthExactRange = config.CrystalMouthExactRange,
                MouthExactFadeBand = config.CrystalMouthExactFadeBand,
                MouthExactRenderScale = config.CrystalMouthExactRenderScale,
                MouthPanoramaFaceSize = config.CrystalMouthPanoramaFaceSize,
                FormSeconds = config.CrystalFormSeconds,
                AnnihilateSeconds = config.CrystalAnnihilateSeconds,
                LifetimeSeconds = config.CrystalStandSeconds,
                SpiralTurns = config.CrystalSpiralTurns,
                BeatCycles = config.CrystalBeatCycles,
                BeatDepth = config.CrystalBeatDepth,
            };
        }

        /// <summary>
        /// Lay a CRYSTAL pair (§13 — charming-cerf's crystal wormhole) where <see cref="SpawnPair"/> lays a
        /// drift pair: the attractor <paramref name="halfGap"/> along −<paramref name="axis"/> from
        /// <paramref name="midpoint"/>, the repulsor along +axis. It forms out of nothing, stands, and
        /// annihilates on its own (<see cref="CrystalWormhole"/>); its host object goes with it
        /// (<see cref="CrystalPairHost"/>). An <paramref name="ownerVessel"/> makes both wells pull only
        /// that vessel. Null when there is no room for two (logged, as <see cref="SpawnPair"/>).
        /// </summary>
        public static CrystalWormhole SpawnCrystalPair(Vector3 midpoint, Vector3 axis, float halfGap,
            CrystalWormhole.Settings settings, Transform ownerVessel = null)
        {
            Prune();
            if (!HasRoom(2, Config)) return null;
            var a = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.right;
            halfGap = Mathf.Max(settings.ThroatRadius, halfGap);    // never closer than a throat apart
            var host = new GameObject("[CrystalPair]");
            host.transform.position = midpoint;
            var wormhole = CrystalWormhole.Open(host, -a * halfGap, a * halfGap, settings);
            if (wormhole == null)
            {
                Object.Destroy(host);
                return null;
            }
            host.AddComponent<CrystalPairHost>().Bind(wormhole);
            if (wormhole.Attractor) wormhole.Attractor.OwnerVessel = ownerVessel;
            if (wormhole.Repulsor) wormhole.Repulsor.OwnerVessel = ownerVessel;
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[BlackHole] crystal pair at {midpoint} axis {a} half-gap {halfGap:F1} throat {settings.ThroatRadius:F1} " +
                $"strength {settings.Strength:F1} owner {(ownerVessel ? ownerVessel.name : "none")}");
            return wormhole;
        }

        /// <summary>Annihilate a pair now: both holes ease out together. The pair is forgotten.</summary>
        public static void Annihilate(Pair pair)
        {
            if (pair == null) return;
            if (pair.Black != null && !pair.Black.IsDespawning) pair.Black.BeginDespawn();
            if (pair.White != null && !pair.White.IsDespawning) pair.White.BeginDespawn();
            _pairs.Remove(pair);
        }

        /// <summary>Annihilate every live pair (the Stoat chains by annihilating the last pair before its next).</summary>
        public static void AnnihilateAllPairs()
        {
            for (int i = _pairs.Count - 1; i >= 0; i--) Annihilate(_pairs[i]);
        }

        /// <summary>The pair a hole belongs to, or null.</summary>
        public static Pair PairOf(BlackHole hole)
        {
            if (hole == null) return null;
            for (int i = 0; i < _pairs.Count; i++)
                if (_pairs[i].Black == hole || _pairs[i].White == hole) return _pairs[i];
            return null;
        }

        /// <summary>
        /// Age every pair, move its holes along their axis, and annihilate the ones whose lifetime
        /// is spent — or whose halves lost each other (one despawned alone: the other goes too).
        /// </summary>
        static void TickPairs(float dt)
        {
            for (int i = _pairs.Count - 1; i >= 0; i--)
            {
                var pair = _pairs[i];
                if (!pair.IsAlive)
                {
                    Annihilate(pair);
                    continue;
                }
                pair.Age += dt;
                // Held too long: it lets go on its own (the owner reads Held and ends its orbit).
                if (pair.Held && pair.Age >= pair.Lifetime) LetGo(pair);
                if (pair.Held) continue;   // standing still: its owner is orbiting the black hole
                pair.CloseAge += dt;
                float rs = Mathf.Max(pair.Black.HorizonRadius, pair.White.HorizonRadius);
                // Met (horizons touching) — or, with nothing to close them, a fallback so a pair never lingers.
                if (BlackHolePairMath.HaveMet(pair.HalfGap, rs) || (pair.DriftSpeed <= 0f && pair.CloseAge >= pair.Lifetime))
                {
                    CSDebug.LogVerbose(CSLogChannel.BlackHole, $"[BlackHole] pair #{pair.Black.Id}/#{pair.White.Id} annihilated after {pair.Age:F1} s");
                    Annihilate(pair);
                    continue;
                }
                BlackHolePairMath.Positions(pair.Midpoint, pair.Axis, pair.HalfGap, out var blackPos, out var whitePos);
                pair.Black.transform.position = blackPos;
                pair.White.transform.position = whitePos;
            }
        }

        /// <summary>Begin a hole's despawn (eased warp release, then destroy). False if no such id.</summary>
        public static bool Despawn(int id)
        {
            var hole = Find(id);
            if (hole == null) return false;
            hole.BeginDespawn();
            return true;
        }

        public static void DespawnAll()
        {
            for (int i = _holes.Count - 1; i >= 0; i--)
            {
                var hole = _holes[i];
                if (hole != null) hole.BeginDespawn();
            }
            _holes.Clear();
            _pairs.Clear();
        }

        public static BlackHole Find(int id)
        {
            for (int i = 0; i < _holes.Count; i++)
                if (_holes[i] != null && _holes[i].Id == id) return _holes[i];
            return null;
        }

        internal static void Register(BlackHole hole)
        {
            if (hole == null || _holes.Contains(hole)) return;
            if (hole.Id == 0) hole.Id = _nextId++;
            _holes.Add(hole);
            EnsureDriver();
        }

        internal static void Unregister(BlackHole hole)
        {
            _holes.Remove(hole);
            if (hole != null && hole.IsDespawning && !_fading.Contains(hole))
                _fading.Add(hole);
        }

        static void Prune()
        {
            _stale.Clear();
            for (int i = 0; i < _holes.Count; i++)
                if (_holes[i] == null || _holes[i].IsDespawning) _stale.Add(_holes[i]);
            for (int i = 0; i < _stale.Count; i++)
            {
                _holes.Remove(_stale[i]);
                if (_stale[i] != null && !_fading.Contains(_stale[i])) _fading.Add(_stale[i]);
            }
            for (int i = _fading.Count - 1; i >= 0; i--)
                if (_fading[i] == null || !_fading[i].isActiveAndEnabled) _fading.RemoveAt(i);

            _warpHoles.Clear();
            _warpHoles.AddRange(_holes);
            _warpHoles.AddRange(_fading);
        }

        /// <summary>Drives the three systems for this frame. Called once per frame by the driver.</summary>
        static void Tick()
        {
            Prune();
            var config = Config;
            float dt = Time.deltaTime;

            // Pairs first: their holes' positions for this frame, and any annihilation, before the
            // field reads the wells.
            TickPairs(dt);
            if (_pairs.Count > 0) Prune();

            BlackHoleGravityField.Tick(_holes, config, dt);
            BlackHoleVesselPull.Tick(_holes, config, dt);
            BlackHoleWarp.Flush(_warpHoles, config);
            // The lens reads the camera's depth texture; keep it on for EVERY enabled game camera while any lens
            // is live (a vessel spawn, the death or end camera switch cameras). Each camera's lens pass reads the
            // holes itself (BlackHoleLens.ScreenWells).
            BlackHoleLens.CameraSupport.Maintain();

            if (CSDebug.IsVerbose(CSLogChannel.BlackHole) && Time.unscaledTime >= _nextReport)
            {
                _nextReport = Time.unscaledTime + 1f;
                if (_holes.Count == 0)
                {
                    if (BlackHoleGravityField.BodyCount > 0 || BlackHoleWarp.IsActive)
                        CSDebug.LogVerbose(CSLogChannel.BlackHole,
                            $"[BlackHole] idle: no holes live; {BlackHoleGravityField.BodyCount} bodies coasting to release, " +
                            $"{BlackHoleWarp.LiveSlotCount} holes still easing their stretch out");
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[BlackHole] ");
                    for (int i = 0; i < _holes.Count; i++)
                    {
                        var h = _holes[i];
                        if (h == null) continue;
                        sb.Append($"{(h.IsSource ? "R" : "A")}#{h.Id} S{h.Strength:F1} rs {h.HorizonRadius:F1} inf {h.InfluenceRadius:F0} w {h.WarpWeight:F2} at {h.transform.position}; ");
                    }
                    sb.Append($"pairs {_pairs.Count}; bodies {BlackHoleGravityField.BodyCount} (captured {BlackHoleGravityField.CapturedThisSecond}/s, " +
                              $"total {BlackHoleGravityField.CapturedTotal}, through the throat {BlackHoleGravityField.ThroatTransitsTotal}), " +
                              $"stretching holes {BlackHoleWarp.LiveSlotCount}, vessels pulled {BlackHoleVesselPull.PulledVesselCount}");
                    CSDebug.LogVerbose(CSLogChannel.BlackHole, sb.ToString());
                }
                BlackHoleGravityField.ResetSecondCounters();
            }
        }

        // ---------------- Lifecycle ----------------

        static Driver _driver;

        static void EnsureDriver()
        {
            if (_driver != null) return;
            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from play-mode-exit
            // cleanup), the same pattern the corridor's and the cradle's publishers use.
            var go = new GameObject("[BlackHoleRegistry]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<Driver>();
        }

        /// <summary>
        /// Shader globals survive play-mode exit in the editor, so a hole left live when play
        /// stopped would keep bending mass around nothing. Publish the off state before anything
        /// renders — the guard every §4.7 publisher installs — and drop every bit of bookkeeping
        /// (the prisms of the previous session are gone; touching one is the failure the guard
        /// exists to avoid).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            _holes.Clear();
            _fading.Clear();
            _warpHoles.Clear();
            _stale.Clear();
            _pairs.Clear();
            _nextId = 1;
            _configResolved = false;
            _driver = null;
            BlackHoleGravityField.ResetOnLoad();
            BlackHoleVesselPull.ResetOnLoad();
            BlackHoleWarp.ResetOnLoad();
            EnsureDriver();
        }

        /// <summary>
        /// Order 29500: after every gameplay LateUpdate, after the debris carrier (29000), before
        /// the render service's transform flush (30000).
        /// </summary>
        [DefaultExecutionOrder(29500)]
        sealed class Driver : MonoBehaviour
        {
            void LateUpdate() => Tick();

            void OnDisable()
            {
                // A teardown, not a domain reload: the prisms are still alive here, so hand every
                // body its own state back rather than orphaning it, and stop the stretch.
                BlackHoleGravityField.ReleaseAll();
                BlackHoleWarp.PublishOff();
                if (_driver == this) _driver = null;
            }
        }
    }
}
