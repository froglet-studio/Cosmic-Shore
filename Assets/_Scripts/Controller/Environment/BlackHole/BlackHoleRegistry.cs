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
        static int _nextId = 1;
        static BlackHoleConfigSO _config;
        static bool _configResolved;
        static float _nextReport;

        /// <summary>Every live, non-despawning hole, in spawn order.</summary>
        public static IReadOnlyList<BlackHole> Holes => _holes;

        public static int Count => _holes.Count;

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
            if (_holes.Count >= config.MaxBlackHoles)
            {
                CSDebug.LogWarning($"[BlackHole] Spawn refused: {_holes.Count} holes live and BlackHoleConfig.maxBlackHoles is {config.MaxBlackHoles}.");
                return null;
            }
            if (!config.IsSane)
            {
                CSDebug.LogWarning("[BlackHole] Spawn refused: BlackHoleConfig is not sane (see BlackHoleConfigSO.IsSane).");
                return null;
            }

            var go = new GameObject(polarity == HolePolarity.Source ? $"[WhiteHole {_nextId}]" : $"[BlackHole {_nextId}]");
            go.transform.position = position;
            var hole = go.AddComponent<BlackHole>();   // OnEnable registers it
            hole.Configure(strength, velocity, spinAxis ?? Vector3.forward, horizonRadius, polarity);
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[BlackHole] spawned #{hole.Id} {polarity} strength {strength:F1} size {horizonRadius:F1} at {position} GM {hole.GM:F0} " +
                $"horizon {hole.HorizonRadius:F1} influence {hole.InfluenceRadius:F0} velocity {velocity}");
            return hole;
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
        public static BlackHole SpawnFromConfig()
        {
            var config = Config;
            var cam = BlackHoleLens.ViewCamera();
            return Spawn(SpawnPoint(config, cam != null ? cam.transform : null), config.SpawnStrength,
                config.SpawnVelocity, config.SpawnSpinAxis, config.SpawnHorizonRadius);
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

            BlackHoleGravityField.Tick(_holes, config, dt);
            BlackHoleVesselPull.Tick(_holes, config, dt);
            BlackHoleWarp.Flush(_warpHoles, config);
            BlackHoleLens.PublishSmoothWells(_warpHoles);
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
                        sb.Append($"#{h.Id} S{h.Strength:F1} rs {h.HorizonRadius:F1} inf {h.InfluenceRadius:F0} w {h.WarpWeight:F2} at {h.transform.position}; ");
                    }
                    sb.Append($"bodies {BlackHoleGravityField.BodyCount} (captured {BlackHoleGravityField.CapturedThisSecond}/s, " +
                              $"total {BlackHoleGravityField.CapturedTotal}), stretching holes {BlackHoleWarp.LiveSlotCount}, " +
                              $"vessels pulled {BlackHoleVesselPull.PulledVesselCount}");
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
