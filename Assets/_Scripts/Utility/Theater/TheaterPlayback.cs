using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>How the theater camera is framing the recording.</summary>
    public enum TheaterShot
    {
        /// <summary>Flown by hand — Halo Forge's monitor. See <see cref="TheaterFreeCamera"/>.</summary>
        Free,
        /// <summary>A vantage orbiting the whole recorded action, framed to fit it.</summary>
        Orbit,
        /// <summary>Behind one pilot and carried by them, so the shot turns as they turn.</summary>
        Chase,
        /// <summary>A tripod: parked where it was anchored, turning to keep one pilot in frame.</summary>
        Static
    }

    /// <summary>
    /// Plays a <see cref="TheaterRecording"/> back as ghost vessels on the shared manually-driven
    /// replay camera, inside the <see cref="TheaterStage"/>.
    ///
    /// <para><b>A puppet is harvested from the prefab ASSET, never instantiated.</b>
    /// <see cref="VesselModelBuilder"/> reads meshes off the prefab without ever waking it, which
    /// buys three things at once: no gameplay controller can fly a puppet off its recorded pose,
    /// no <c>Awake</c> side effect fires, and — the one that matters most — <b>there is no
    /// <c>NetworkObject</c> to neutralise</b>. Instantiating a vessel prefab without spawning it is
    /// the B16 trap (Netcode adopts the stray as an in-scene placed object and the SECOND one
    /// breaks synchronisation for the rest of the session); harvesting sidesteps it by
    /// construction rather than by remembering a guard. The cost, stated plainly: a P0 puppet has
    /// no jets, no tail, no hull morph and no animation. It is a ghost of the right SHIP.</para>
    ///
    /// <para><b>A ghost wears the ship's OWN materials, with its pilot's real domain accent</b> —
    /// the ship as it looks in the game, which is the point of watching a replay. The first cut
    /// painted a flat domain fill, on <see cref="VesselModelBuilder"/>'s reasoning that a vessel's
    /// real materials read as a black blob out of their lit context; that reasoning was about a
    /// mini hull floating in a toy station, and it stopped applying the moment the theater kept the
    /// world (and its lighting) in shot. The accent is resolved the way the toys resolve it, off
    /// the theme's own per-domain material set, so a Ruby pilot's ghost is Ruby rather than the
    /// jade placeholder every vessel prefab is authored with. The flat fill survives behind
    /// <see cref="TheaterConfigSO.liveHullMaterials"/>.</para>
    ///
    /// <para><b>Seeking backward is free here and will not stay free.</b> A vessel track is
    /// SAMPLED STATE, so any timestamp is a binary search with nothing to rebuild — scrubbing in
    /// either direction costs the same. Prisms arrive in P1 as EVENTS, and events have history, so
    /// that is the phase where a backward seek becomes rebuild-and-replay-forward off a liveness
    /// snapshot. P0 seeks perfectly; do not read that as a promise the whole system will.</para>
    /// </summary>
    public class TheaterPlayback
    {
        class Puppet
        {
            public TheaterTrack Track;
            public GameObject Model;
            public Transform Transform;
            public bool Visible;
            public float Radius = 1f;
        }

        readonly List<Puppet> _puppets = new();
        readonly TheaterStage _stage = new();
        readonly TheaterFreeCamera _freeCamera = new();

        TheaterRecording _recording;
        TheaterConfigSO _config;
        CameraManager _cameraManager;
        Transform _camera;
        GameObject _root;

        float _time;
        float _orbitPhase;
        float _sceneRadius = 100f;
        Vector3 _freePosition;
        Vector3 _staticAnchor;
        bool _staticAnchored;
        TheaterShot _shot = TheaterShot.Orbit;

        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public float Speed { get; set; } = 1f;

        /// <summary>Whether the recording area is up (the live world masked off the camera).</summary>
        public bool StageIsUp => _stage.IsUp;

        /// <summary>Speed gear the free camera's modifiers are holding, for the overlay.</summary>
        public float FreeCameraGear => _freeCamera.Gear;

        public TheaterShot Shot
        {
            get => _shot;
            set
            {
                if (_shot == value) return;

                // Hand the outgoing shot's pose to the incoming one so a shot change re-frames
                // rather than teleporting: a free cam seeded from the orbit's own pose starts
                // exactly where the director was already looking.
                if (value == TheaterShot.Free && _camera != null)
                {
                    _freePosition = _camera.position;
                    _freeCamera.Seed(_camera.rotation);
                }
                if (value == TheaterShot.Static) _staticAnchored = false;

                _shot = value;
            }
        }

        /// <summary>Index into <see cref="TheaterRecording.Tracks"/> the subject shots are riding.</summary>
        public int FollowIndex { get; private set; }

        public TheaterRecording Recording => _recording;

        /// <summary>Seconds into the recording, from its own first sample.</summary>
        public float Position => _recording != null ? _time - _recording.StartTime : 0f;

        public float Duration => _recording != null ? _recording.Duration : 0f;

        public string FollowName =>
            _recording != null && FollowIndex >= 0 && FollowIndex < _recording.Tracks.Count
                ? _recording.Tracks[FollowIndex].PlayerName
                : "-";

        public bool Begin(TheaterRecording recording, TheaterConfigSO config, CameraManager cameraManager)
        {
            if (recording == null || recording.Tracks.Count == 0) return false;

            Stop();

            _recording = recording;
            _config = config;
            _cameraManager = cameraManager;
            _time = recording.StartTime;
            _orbitPhase = 0f;
            IsPaused = false;
            Speed = 1f;
            FollowIndex = 0;
            _shot = TheaterShot.Orbit;
            _staticAnchored = false;
            _freeCamera.Reset();

            _root = new GameObject("[TheaterPuppets]");
            BuildPuppets();

            // The replay camera is a sanctioned holder of the prism-occlusion-corridor and
            // speed-tunnel suppressions (Docs/PRISM_ANIMATION.md 4.7, Docs/SPEED_TUNNEL.md), and it
            // is one rig rather than a second live camera — Camera.main, the speed tunnel and the
            // graphics settings all key off the single gameplay rig, so a theater that stood up its
            // own camera would fall outside all three.
            _camera = _cameraManager != null ? _cameraManager.BeginManualReplayCamera() : null;

            if (_camera == null)
            {
                // Say so once. With no replay rig there is no camera to pose and none to mask, so
                // the ghosts are drawn from wherever the player is already looking - which is a
                // real, watchable degradation and an utterly baffling one if nothing names it.
                CSDebug.LogWarning(
                    "[Theater] No replay camera in this scene (CameraManager has no end camera), so " +
                    "the recording is drawn from the live camera and the shots do nothing.");
            }

            _stage.Enter(ResolveCamera(), _root.transform, _config);

            IsPlaying = true;
            Apply();
            return true;
        }

        public void Stop()
        {
            // The stage comes down FIRST and unconditionally: it holds the camera's culling mask,
            // and a mask left pointing at an empty layer is a black screen for the rest of the
            // session — a far worse failure than anything below it.
            _stage.Exit();

            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
            _puppets.Clear();

            if (_camera != null)
            {
                _cameraManager?.RestoreGameplayCamera();
                _camera = null;
            }

            IsPlaying = false;
            IsPaused = false;
            _recording = null;
        }

        public void TogglePause() => IsPaused = !IsPaused;

        /// <summary>
        /// Jump to a point in the recording. The whole seek — forward or backward, by any distance
        /// — is this one clamp, because nothing about a vessel track accumulates.
        /// </summary>
        public void Seek(float secondsIntoRecording)
        {
            if (_recording == null) return;
            _time = _recording.StartTime + Mathf.Clamp(secondsIntoRecording, 0f, _recording.Duration);
            Apply();
        }

        /// <summary>Snap backward (or forward) by a fixed step — the transport the team asked for.</summary>
        public void Nudge(float seconds) => Seek(Position + seconds);

        /// <summary>Change which pilot the Chase and Static shots are watching.</summary>
        public void CycleFollow(int step)
        {
            if (_recording == null || _recording.Tracks.Count == 0) return;
            int n = _recording.Tracks.Count;
            FollowIndex = ((FollowIndex + step) % n + n) % n;
            _staticAnchored = false;
            if (_shot is not (TheaterShot.Chase or TheaterShot.Static)) Shot = TheaterShot.Chase;
        }

        /// <summary>Cycle the shot itself, in the order the overlay lists them.</summary>
        public void CycleShot(int step)
        {
            int count = ShotCount;
            Shot = (TheaterShot)((((int)_shot + step) % count + count) % count);
        }

        /// <summary>Pick a shot outright, by its position in <see cref="TheaterShot"/>.</summary>
        public void SetShotByIndex(int index)
        {
            if (index < 0 || index >= ShotCount) return;
            Shot = (TheaterShot)index;
        }

        static int ShotCount => System.Enum.GetValues(typeof(TheaterShot)).Length;

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsPlaying || _recording == null) return;

            if (!IsPaused)
            {
                _time += unscaledDeltaTime * Speed;
                float end = _recording.StartTime + _recording.Duration;
                if (_time >= end)
                {
                    // Loop rather than stop. A director is looking at the same three seconds over
                    // and over; making them press play each time is the wrong default.
                    _time = _recording.StartTime;

                    // And the ORBIT restarts with it. Left running, the vantage kept advancing
                    // across loops, so the same three seconds arrived from a different angle every
                    // time and read as a different recording. A loop has to be a loop in the SHOT
                    // as well as in the data - anything the camera accumulates is part of what the
                    // viewer is comparing against.
                    _orbitPhase = 0f;
                }
            }

            _orbitPhase += unscaledDeltaTime;

            // The free camera flies whether or not the recording is running — a paused replay you
            // can still walk around is most of what a theater is for.
            if (_shot == TheaterShot.Free && _camera != null)
            {
                if (!_freeCamera.IsSeeded)
                {
                    _freePosition = _camera.position;
                    _freeCamera.Seed(_camera.rotation);
                }

                _freeCamera.Tick(unscaledDeltaTime, _sceneRadius,
                    _config != null ? _config.freeCameraSpeed : 0.9f,
                    _config != null ? _config.freeCameraLookSpeed : 140f,
                    ref _freePosition, out Quaternion freeRotation);
                _camera.SetPositionAndRotation(_freePosition, freeRotation);
            }

            Apply();
        }

        void Apply()
        {
            Bounds framed = default;
            bool anyVisible = false;

            for (int i = 0; i < _puppets.Count; i++)
            {
                var puppet = _puppets[i];
                if (puppet.Model == null) continue;

                bool live = puppet.Track.TryEvaluate(_time, out Vector3 position, out Quaternion rotation, out _);
                if (live != puppet.Visible)
                {
                    puppet.Visible = live;
                    puppet.Model.SetActive(live);
                }
                if (!live) continue;

                puppet.Transform.SetPositionAndRotation(position, rotation);

                if (!anyVisible) { framed = new Bounds(position, Vector3.zero); anyVisible = true; }
                else framed.Encapsulate(position);
            }

            if (!anyVisible) return;
            _sceneRadius = Mathf.Max(framed.extents.magnitude, 20f);

            if (_camera == null) return;

            switch (_shot)
            {
                // Free is posed in Tick, not here: it must keep flying while the replay is paused,
                // and Apply only runs against the recording.
                case TheaterShot.Free:
                    return;
                case TheaterShot.Chase when TryChasePose(out Vector3 chasePos, out Quaternion chaseRot):
                    _camera.SetPositionAndRotation(chasePos, chaseRot);
                    return;
                case TheaterShot.Static when TryStaticPose(out Vector3 staticPos, out Quaternion staticRot):
                    _camera.SetPositionAndRotation(staticPos, staticRot);
                    return;
                default:
                    ApplyOrbit(framed);
                    return;
            }
        }

        bool TrySubject(out Puppet puppet)
        {
            puppet = null;
            if (FollowIndex < 0 || FollowIndex >= _puppets.Count) return false;
            var candidate = _puppets[FollowIndex];
            if (!candidate.Visible || candidate.Model == null) return false;
            puppet = candidate;
            return true;
        }

        bool TryChasePose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (!TrySubject(out var puppet)) return false;

            // Distance in units of the HULL'S OWN measured radius, so one authored number frames
            // every ship in a fleet whose sizes span two orders of magnitude.
            float distance = puppet.Radius * (_config != null ? _config.followDistance : 9f);
            Transform t = puppet.Transform;
            position = t.position - t.forward * distance + t.up * (distance * 0.25f);
            rotation = Quaternion.LookRotation((t.position - position).normalized, t.up);
            return true;
        }

        bool TryStaticPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (!TrySubject(out var puppet)) return false;

            if (!_staticAnchored)
            {
                // Anchor once, off the subject's pose at the moment the shot was chosen — a tripod
                // planted where the action was, which the subject then flies through and past.
                float distance = puppet.Radius * (_config != null ? _config.followDistance : 9f);
                Transform t = puppet.Transform;
                _staticAnchor = t.position - t.forward * distance + Vector3.up * (distance * 0.4f);
                _staticAnchored = true;
            }

            position = _staticAnchor;
            Vector3 toSubject = puppet.Transform.position - position;
            if (toSubject.sqrMagnitude < 1e-4f) return false;
            rotation = Quaternion.LookRotation(toSubject.normalized, Vector3.up);
            return true;
        }

        void ApplyOrbit(Bounds framed)
        {
            float margin = _config != null ? Mathf.Max(1f, _config.framingMargin) : 2.5f;
            float period = _config != null ? Mathf.Max(1f, _config.orbitSeconds) : 40f;
            float elevation = _config != null ? _config.orbitElevation : 0.35f;

            float fov = _cameraManager != null ? _cameraManager.ReplayCameraFieldOfView : 60f;
            float halfTan = Mathf.Max(0.1f, Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));
            float distance = _sceneRadius * margin / halfTan;

            float angle = (_orbitPhase / period) * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance
                             + Vector3.up * (distance * elevation);

            Vector3 position = framed.center + offset;
            _camera.SetPositionAndRotation(
                position,
                Quaternion.LookRotation((framed.center - position).normalized, Vector3.up));
        }

        Camera ResolveCamera()
        {
            if (_camera == null) return null;
            var camera = _camera.GetComponent<Camera>();
            return camera != null ? camera : _camera.GetComponentInChildren<Camera>(true);
        }

        void BuildPuppets()
        {
            var container = ResolveVesselPrefabs();
            var colorSet = PrismLit.ColorSet;
            bool live = _config != null && _config.liveHullMaterials;

            for (int i = 0; i < _recording.Tracks.Count; i++)
            {
                var track = _recording.Tracks[i];
                var domain = (Domains)track.Domain;

                // The DOMAIN SIGNAL colour — the same accessor the vessel vision band marks a real
                // hull with, read off the static ThemeManager already publishes. White when no
                // theme has loaded, because a ghost that renders black is one nobody can see.
                Color color = colorSet != null ? colorSet.GetDomainSignalColor(domain) : Color.white;
                if (color.a <= 0f) color = Color.white;

                GameObject model = null;

                if (container != null && container.TryGetShipPrefab((VesselClassType)track.VesselType,
                        out Transform prefab))
                {
                    // Radius 0 = NATIVE scale and native pivot: the recorded pose is relative to the
                    // ship's own origin, so a re-centred model would sit off by the hull's bounds
                    // offset for the whole replay.
                    if (live) VesselModelBuilder.TryBuildLive(prefab, 0f, DomainMaterial(domain), out model);
                    else VesselModelBuilder.TryBuild(prefab, 0f, color, out model);
                }

                if (model == null) model = BuildProxy(color);

                model.name = $"Ghost_{track.PlayerName}";
                model.transform.SetParent(_root.transform, false);
                float radius = MeasureRadius(model.transform);

                _puppets.Add(new Puppet
                {
                    Track = track,
                    Model = model,
                    Transform = model.transform,
                    Radius = radius,
                    Visible = true
                });
            }
        }

        /// <summary>
        /// The fleet's prefab registry, asked for in three places because it lives in a different
        /// one in every context the theater can be opened from.
        ///
        /// <para>The authored config field is the intended answer. <c>Resources</c> is the answer
        /// for a scene with no spawner. The live scene's <see cref="ServerPlayerVesselInitializer"/>
        /// is the answer that always works in a game scene and needs nothing authored — and it is
        /// last because a scene-wide search is the expensive one, not because it is the least
        /// reliable.</para>
        ///
        /// <para>Falling through all three is what produced the placeholder wedge, so the miss is
        /// reported by name: a silent fallback to a proxy reads as "the theater cannot draw ships",
        /// which is a much larger and much wronger conclusion than "nothing told it where they
        /// are".</para>
        /// </summary>
        VesselPrefabContainer ResolveVesselPrefabs()
        {
            if (_config != null && _config.vesselPrefabs != null) return _config.vesselPrefabs;

            var fromResources = Resources.Load<VesselPrefabContainer>("VesselPrefabContainer");
            if (fromResources != null) return fromResources;

            var initializer = UnityEngine.Object.FindFirstObjectByType<ServerPlayerVesselInitializer>();
            if (initializer != null && initializer.VesselPrefabContainer != null)
                return initializer.VesselPrefabContainer;

            CSDebug.LogWarning(
                "[Theater] No VesselPrefabContainer reachable, so ghosts are placeholder wedges " +
                "rather than real hulls. Assign one to Resources/TheaterConfig's 'Vessel Prefabs', " +
                "or put a copy of the container in a Resources folder.");
            return null;
        }

        /// <summary>
        /// The ship material for a recorded pilot's domain, off the theme's own per-domain set —
        /// the same list <c>ToyVesselRoster</c> paints a live mini hull from.
        ///
        /// <para>Those sets are BUILT at <c>ThemeManager.Awake</c> and exist nowhere on disk, so
        /// there is no asset to load and no way to reach them but the static. Null before the
        /// manager wakes, which <see cref="VesselModelBuilder.TryBuildLive"/> reads as
        /// "keep the authored materials" — a jade-accented ghost rather than no ghost.</para>
        /// </summary>
        static Material DomainMaterial(Domains domain)
        {
            var sets = ThemeManager.Data != null ? ThemeManager.Data.TeamMaterialSets : null;
            if (sets == null) return null;
            return sets.TryGetValue(domain, out var set) && set != null ? set.ShipMaterial : null;
        }

        /// <summary>
        /// The stand-in when no <see cref="VesselPrefabContainer"/> can be reached: a flat wedge
        /// that states a POSE unambiguously (long axis forward, a fin so roll reads). Deliberately
        /// not a sphere — the whole point of a ghost is which way the pilot was facing.
        /// </summary>
        static GameObject BuildProxy(Color color)
        {
            var root = new GameObject("GhostProxy");

            // Resolve the shader BEFORE constructing: new Material(null) throws, so a missing URP
            // Unlit has to fall through rather than be detected afterwards.
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var material = shader != null ? new Material(shader) { color = color } : null;

            AddBox(root.transform, material, new Vector3(0f, 0f, 1.5f), new Vector3(1.4f, 0.5f, 6f));
            AddBox(root.transform, material, new Vector3(0f, 0f, -1.4f), new Vector3(4.5f, 0.35f, 1.6f));
            AddBox(root.transform, material, new Vector3(0f, 0.9f, -1.9f), new Vector3(0.3f, 1.6f, 1.2f));
            return root;
        }

        static void AddBox(Transform parent, Material material, Vector3 localPosition, Vector3 scale)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = scale;

            // A puppet must never collide with anything: it is a picture of a ship, not a ship.
            var collider = box.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            var renderer = box.GetComponent<MeshRenderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
        }

        static float MeasureRadius(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 1f;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return Mathf.Max(1f, b.extents.magnitude);
        }
    }
}
