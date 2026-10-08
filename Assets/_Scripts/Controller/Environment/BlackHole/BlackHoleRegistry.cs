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
            public float DriftSpeed { get; internal set; }
            public float Lifetime { get; internal set; }
            public float Age { get; internal set; }
            public bool IsAlive => Black != null && White != null && !Black.IsDespawning && !White.IsDespawning;
            public float HalfGap => BlackHolePairMath.HalfGap(HalfGap0, DriftSpeed, Lifetime, Age);
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
            float horizonRadius = 0f, HolePolarity polarity = HolePolarity.Black)
        {
            Prune();
            var config = Config;
            if (!HasRoom(1, config)) return null;

            string kind = polarity == HolePolarity.White ? "WhiteHole" : "BlackHole";
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
        /// along +axis, both of <paramref name="strength"/> and <paramref name="horizonRadius"/>. They
        /// drift apart at <paramref name="driftSpeed"/>, stop, come back and annihilate after
        /// <paramref name="lifetime"/> seconds; what the black hole captures the white hole emits.
        /// Null when there is no room for two (nothing is spawned).
        /// </summary>
        public static Pair SpawnPair(Vector3 midpoint, Vector3 axis, float strength, float horizonRadius, float halfGap,
            float driftSpeed, float lifetime, Vector3? spinAxis = null, Transform ownerVessel = null)
        {
            Prune();
            var config = Config;
            if (!HasRoom(2, config)) return null;

            var a = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.right;
            halfGap = Mathf.Max(0f, halfGap);
            BlackHolePairMath.Positions(midpoint, a, halfGap, out var blackPos, out var whitePos);
            var black = Spawn(blackPos, strength, Vector3.zero, spinAxis, horizonRadius, HolePolarity.Black);
            if (black == null) return null;
            var white = Spawn(whitePos, strength, Vector3.zero, spinAxis, horizonRadius, HolePolarity.White);
            if (white == null)
            {
                black.BeginDespawn();
                return null;
            }
            black.Partner = white;
            white.Partner = black;
            black.OwnerVessel = ownerVessel;
            white.OwnerVessel = ownerVessel;
            var pair = new Pair
            {
                Black = black, White = white, Midpoint = midpoint, Axis = a, HalfGap0 = halfGap,
                DriftSpeed = Mathf.Max(0f, driftSpeed), Lifetime = Mathf.Max(0.01f, lifetime), Age = 0f,
            };
            _pairs.Add(pair);
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[BlackHole] pair #{black.Id}/#{white.Id} at {midpoint} axis {a} half-gap {halfGap:F1} drift {driftSpeed:F1} u/s " +
                $"lifetime {lifetime:F1} s (widest {BlackHolePairMath.MaxHalfGap(halfGap, driftSpeed, lifetime):F1})");
            return pair;
        }

        /// <summary>
        /// A pair from the config's Spawn and Pair sections, laid across the camera on screen (the
        /// vessel's while flying): the midpoint <see cref="BlackHoleConfigSO.PairAheadHorizons"/>
        /// horizon radii ahead, the holes <see cref="BlackHoleConfigSO.PairHalfGapHorizons"/> to
        /// either side on the camera's own horizontal — the black hole on the LEFT when
        /// <paramref name="blackOnLeft"/>, else on the right. What the Black Hole tool's Spawn Pair
        /// button, the P key and <c>blackhole pair</c> do, and what the Stoat's triggers will do from
        /// the vessel. Without a camera the pair lies along world +X at the spawn position.
        /// </summary>
        public static Pair SpawnPairFromConfig(bool blackOnLeft)
        {
            var config = Config;
            var cam = BlackHoleLens.ViewCamera();
            float rs = config.HorizonRadius(config.SpawnStrength, config.SpawnHorizonRadius);
            Vector3 midpoint, right;
            if (cam != null)
            {
                var t = cam.transform;
                midpoint = t.position + t.forward * (rs * config.PairAheadHorizons);
                right = t.right;
            }
            else
            {
                midpoint = config.SpawnPosition;
                right = Vector3.right;
            }
            // The axis runs black → white: black on the left means the axis points right.
            var axis = blackOnLeft ? right : -right;
            return SpawnPair(midpoint, axis, config.SpawnStrength, config.SpawnHorizonRadius, rs * config.PairHalfGapHorizons,
                config.PairDriftSpeed, config.PairLifetime, config.SpawnSpinAxis);
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
                if (BlackHolePairMath.IsSpent(pair.Lifetime, pair.Age))
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

        /// <summary>
        /// Where a spawn from the config lands: <see cref="BlackHoleConfigSO.SpawnDistanceHorizons"/>
        /// horizon radii straight ahead of <paramref name="camera"/> when
        /// <see cref="BlackHoleConfigSO.SpawnAheadOfCamera"/> is on and there is a camera, else
        /// <see cref="BlackHoleConfigSO.SpawnPosition"/>.
        /// </summary>
        public static Vector3 SpawnPoint(BlackHoleConfigSO config, Transform camera)
        {
            if (!config.SpawnAheadOfCamera || camera == null) return config.SpawnPosition;
            float rs = config.HorizonRadius(config.SpawnStrength, config.SpawnHorizonRadius);
            return camera.position + camera.forward * (rs * config.SpawnDistanceHorizons);
        }

        /// <summary>
        /// Spawn a hole exactly as the config's Spawn section says — strength, size, where
        /// (<see cref="SpawnPoint"/>, ahead of <see cref="BlackHoleLens.ViewCamera"/> — the camera on
        /// screen, the vessel's while flying, which in the real game is NOT <c>Camera.main</c> —
        /// or at the spawn position), velocity, spin — what the Black Hole tool's Spawn button,
        /// Shift+B and <c>blackhole spawn</c> with no strength do. Null when refused (see <see cref="Spawn"/>).
        /// </summary>
        public static BlackHole SpawnFromConfig(HolePolarity polarity = HolePolarity.Black)
        {
            var config = Config;
            var cam = BlackHoleLens.ViewCamera();
            return Spawn(SpawnPoint(config, cam != null ? cam.transform : null), config.SpawnStrength,
                config.SpawnVelocity, config.SpawnSpinAxis, config.SpawnHorizonRadius, polarity);
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
            // The lens reads the camera's opaque + depth copies; keep them on for EVERY enabled game
            // camera while any lens is live (a vessel spawn, the death or end camera switch cameras).
            BlackHoleLens.CameraSupport.Maintain();
            // ...and the sky a ray bent off-screen sees: the scene's own skybox, kept current.
            BlackHoleSky.Maintain(config);

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
                        sb.Append($"{(h.IsWhite ? "W" : "B")}#{h.Id} S{h.Strength:F1} rs {h.HorizonRadius:F1} inf {h.InfluenceRadius:F0} w {h.WarpWeight:F2} at {h.transform.position}; ");
                    }
                    sb.Append($"pairs {_pairs.Count}; bodies {BlackHoleGravityField.BodyCount} (captured {BlackHoleGravityField.CapturedThisSecond}/s, " +
                              $"total {BlackHoleGravityField.CapturedTotal}, emitted {BlackHoleGravityField.EmittedTotal}), " +
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
