using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino's MASS ability, <b>Afterimage</b>: once the energized blade goes out, the sword
    /// keeps leaving copies of itself - prisms in the blade's own shape and pose - for a few
    /// seconds (<see cref="RhinoSwordAfterimageConfigSO.WindowSeconds"/>). An omni-crystal surge
    /// lays them for as long as it holds the blade energized. MASS scales their volume; at Mass
    /// level 5 they are laid as DANGER prisms. See <c>R_VesselActions/RHINO_SWORD_AFTERIMAGE.md</c>.
    ///
    /// <para><b>Every peer lays its own.</b> Prisms are simulated per machine, like the trail. The
    /// window is opened by the energize PHASE, which is owner-authoritative and replicated
    /// (<see cref="RhinoSwordNetworkRelay"/>), and the blade's pose is read from the replicated
    /// trigger mirrors (<see cref="ShieldSwipeActionExecutor"/>), so every machine lays the same
    /// afterimages to within a network tick. The size and the danger flag read REPLICATED state
    /// (the integer element level, the unlock bit) for the same reason.</para>
    ///
    /// <para><b>Its own afterimages are prisms like any other.</b> The blade cuts them and banks their
    /// energy (Garrett, 2026-10-17: "not immune to our destruction"); only the fleet's 1 s self-trail
    /// grace keeps the blade and the hull off one just laid. Each window lays into its own
    /// <see cref="RhinoAfterimageTrail"/>, stamped AFTER <c>Prism.Initialize</c> (whose pool reset
    /// clears membership), so <see cref="IsMine"/> is an identity test a recycled prism can never
    /// pass - the AI's sword hand uses it to aim at something else first.</para>
    ///
    /// <para>Passive: bound to no input, so the config is wired directly here rather than resolved
    /// through the action handler (vessel skill rule 20).</para>
    /// </summary>
    public sealed class RhinoSwordAfterimage : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] RhinoSwordAfterimageConfigSO config;

        [Header("Refs")]
        [Tooltip("The sword's brain on this GameObject - its energize phase opens the window.")]
        [SerializeField] ShieldSkimmerScaleDriver driver;

        RhinoAfterimageTrail _trail;   // the open window's container; null between windows
        float _windowEnd;
        float _nextLayAt;
        int _laidThisWindow;
        Vector3 _lastTip;
        bool _hasLastTip;
        bool _warnedNoChannel;

        /// <summary>True while afterimages are being laid - the HUD's Mass card reads it.</summary>
        public bool IsLaying => isActiveAndEnabled && config && driver && (driver.IsSurging || Time.time < _windowEnd);

        /// <summary>Is <paramref name="prism"/> one of THIS sword's afterimages?</summary>
        public bool IsMine(Prism prism) =>
            prism && prism.Trail is RhinoAfterimageTrail trail && trail.Owner == this;

        void Awake()
        {
            if (!driver) TryGetComponent(out driver);
            if (!config)
                CSDebug.LogWarning($"[{nameof(RhinoSwordAfterimage)}] No {nameof(RhinoSwordAfterimageConfigSO)} " +
                                   $"wired on '{name}' - the Rhino lays no afterimages. Wire it on the prefab.", this);
        }

        void OnEnable()
        {
            if (driver) driver.EnergizePhaseChanged += HandlePhaseChanged;
            CloseWindow();
        }

        void OnDisable()
        {
            if (driver) driver.EnergizePhaseChanged -= HandlePhaseChanged;
            CloseWindow();
        }

        void HandlePhaseChanged(RhinoSwordEnergizePhase from, RhinoSwordEnergizePhase to)
        {
            // The ability WAITS for the energize to end: the window opens on the way out of it.
            // Always a FRESH window - its own container and its own budget - so the window that
            // follows a surge is not left with the surge's spent budget.
            if (from == RhinoSwordEnergizePhase.Energized && to != RhinoSwordEnergizePhase.Energized && config)
                OpenWindow(Time.time + config.WindowSeconds, fresh: true);
        }

        void OpenWindow(float until, bool fresh)
        {
            _windowEnd = Mathf.Max(_windowEnd, until);
            if (_trail != null && !fresh) return;
            _trail = new RhinoAfterimageTrail(this);
            _laidThisWindow = 0;
            _hasLastTip = false;
            _nextLayAt = 0f;
        }

        void CloseWindow()
        {
            _trail = null;
            _windowEnd = 0f;
            _hasLastTip = false;
        }

        void Update()
        {
            if (!config || !driver) return;

            // A surge lays the whole time it holds the blade: open (or keep open) a window for it.
            if (driver.IsSurging) OpenWindow(Time.time, fresh: false);

            if (!IsLaying)
            {
                if (_trail != null) CloseWindow();
                return;
            }

            float now = Time.time;
            if (now < _nextLayAt || _laidThisWindow >= config.MaxAfterimagesPerWindow) return;

            var skimmer = driver.Skimmer;
            var swing = skimmer ? skimmer.SwingKinematics : null;
            var status = skimmer ? skimmer.VesselStatus : null;
            if (swing == null || !swing.IsReady || status == null) return;

            Vector3 tip = swing.TipPoint;
            if (_hasLastTip && (tip - _lastTip).sqrMagnitude < config.MinTipTravel * config.MinTipTravel) return;

            if (LayAfterimage(swing, status))
            {
                _lastTip = tip;
                _hasLastTip = true;
                _laidThisWindow++;
                _nextLayAt = now + config.LayIntervalSeconds;
            }
        }

        bool LayAfterimage(SkimmerSwingKinematics swing, IVesselStatus status)
        {
            var prisms = status.VesselPrismController;
            var channel = prisms ? prisms.PrismSpawnChannel : null;
            if (!channel)
            {
                if (!_warnedNoChannel)
                {
                    _warnedNoChannel = true;
                    CSDebug.LogWarning($"[{nameof(RhinoSwordAfterimage)}] '{name}': the vessel has no prism " +
                                       "spawn channel (VesselPrismController), so no afterimage can be laid.", this);
                }
                return false;
            }

            Vector3 hilt = swing.HiltPoint;
            Vector3 tip = swing.TipPoint;
            Vector3 along = tip - hilt;
            float bladeLength = along.magnitude;
            if (bladeLength < 0.5f) return false;
            Vector3 axis = along / bladeLength;

            float covered = bladeLength * config.LengthCoverage;
            int count = RhinoSwordAfterimageConfigSO.SegmentCount(covered, config.SegmentLength, config.MaxSegments);
            float segment = covered / count;
            Vector3 start = hilt + axis * (0.5f * (bladeLength - covered));

            // The blade's thin axis is the afterimage's thickness, so a copy reads as the flat of the
            // sword it was cut from rather than a rod.
            var blade = driver.BladeRoot;
            Vector3 thin = blade ? blade.right : status.ShipTransform.up;
            if (Mathf.Abs(Vector3.Dot(thin, axis)) > 0.99f) thin = status.ShipTransform.up;
            var rotation = Quaternion.LookRotation(axis, thin);

            // Volume, not length, is what Mass buys - cube-rooted into every axis.
            float perAxis = Mathf.Pow(Mathf.Max(0.01f, config.MassVolumeMultiplier(status)), 1f / 3f);
            var scale = new Vector3(config.Width * perAxis, config.Thickness * perAxis, segment * perAxis);

            bool danger = config.DangerActive(status);

            for (int i = 0; i < count; i++)
                LayOne(channel, prisms.SpawnPrismType, start + axis * (segment * (i + 0.5f)), rotation, scale,
                       danger, status);
            return true;
        }

        void LayOne(PrismEventChannelWithReturnSO channel, PrismType type, Vector3 position, Quaternion rotation,
                    Vector3 scale, bool danger, IVesselStatus status)
        {
            var ret = channel.RaiseEvent(new PrismEventData
            {
                ownDomain = status.Domain,
                Rotation = rotation,
                SpawnPosition = position,
                Scale = scale,
                PrismType = type,
            });
            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) return;

            prism.TargetScale = scale;
            prism.ownerID = status.PlayerName;   // the self-trail grace keys on who LAID it
            prism.ChangeTeam(status.Domain);
            prism.waitTime = config.ColliderDelaySeconds;
            // The flag is the whole job: Initialize runs MakeDangerous through the real pipeline
            // (VesselPrismController.CreateBlock's danger mode is the precedent).
            if (danger) prism.prismProperties.IsDangerous = true;

            prism.Initialize(status.PlayerName);

            // A stated size, admitted past the pool's scale window AFTER Initialize (whose reset
            // restores and re-clamps it) - a long blade's segments are past the z = 10 ceiling.
            prism.AdmitTargetScale(scale);
            prism.TargetScale = scale;

            // AFTER Initialize, which clears membership on pool reuse.
            prism.AssignTrail(_trail);
            _trail.Add(prism);
        }
    }

    /// <summary>
    /// One afterimage window's container. A distinct type so the sword can recognise its own
    /// afterimages by identity (<see cref="RhinoSwordAfterimage.IsMine"/>) without any registry.
    /// </summary>
    public sealed class RhinoAfterimageTrail : Trail
    {
        public RhinoSwordAfterimage Owner { get; }

        public RhinoAfterimageTrail(RhinoSwordAfterimage owner) => Owner = owner;
    }
}
