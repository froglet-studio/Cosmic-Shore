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
        /// <summary>A fixed vantage orbiting the whole recorded action, framed to fit it.</summary>
        Orbit,
        /// <summary>Behind one pilot, at a distance derived from that hull's own size.</summary>
        Follow
    }

    /// <summary>
    /// Plays a <see cref="TheaterRecording"/> back as domain-coloured ghost vessels on the shared
    /// manually-driven replay camera.
    ///
    /// <para><b>A puppet is harvested from the prefab ASSET, never instantiated.</b>
    /// <see cref="VesselModelBuilder"/> reads meshes off the prefab without ever waking it, which
    /// buys three things at once: no gameplay controller can fly a puppet off its recorded pose,
    /// no <c>Awake</c> side effect fires, and — the one that matters most — <b>there is no
    /// <c>NetworkObject</c> to neutralise</b>. Instantiating a vessel prefab without spawning it is
    /// the B16 trap (Netcode adopts the stray as an in-scene placed object and the SECOND one
    /// breaks synchronisation for the rest of the session); harvesting sidesteps it by
    /// construction rather than by remembering a guard. The cost, stated plainly: a P0 puppet has
    /// no jets, no tail, no hull morph and no animation. It is a ghost.</para>
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
        TheaterRecording _recording;
        TheaterConfigSO _config;
        CameraManager _cameraManager;
        Transform _camera;
        GameObject _root;

        float _time;
        float _orbitPhase;

        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public float Speed { get; set; } = 1f;
        public TheaterShot Shot { get; set; } = TheaterShot.Orbit;

        /// <summary>Index into <see cref="TheaterRecording.Tracks"/> the follow shot is riding.</summary>
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

            _root = new GameObject("[TheaterPuppets]");
            BuildPuppets();

            // The replay camera is a sanctioned holder of the prism-occlusion-corridor and
            // speed-tunnel suppressions (Docs/PRISM_ANIMATION.md 4.7, Docs/SPEED_TUNNEL.md), and it
            // is one rig rather than a second live camera — Camera.main, the speed tunnel and the
            // graphics settings all key off the single gameplay rig, so a theater that stood up its
            // own camera would fall outside all three.
            _camera = _cameraManager != null ? _cameraManager.BeginManualReplayCamera() : null;

            IsPlaying = true;
            Apply();
            return true;
        }

        public void Stop()
        {
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

        public void CycleFollow(int step)
        {
            if (_recording == null || _recording.Tracks.Count == 0) return;
            int n = _recording.Tracks.Count;
            FollowIndex = ((FollowIndex + step) % n + n) % n;
            Shot = TheaterShot.Follow;
        }

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
                }
            }

            _orbitPhase += unscaledDeltaTime;
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

            if (_camera == null) return;
            if (!anyVisible) return;

            if (Shot == TheaterShot.Follow && TryFollowPose(out Vector3 followPos, out Quaternion followRot))
            {
                _camera.SetPositionAndRotation(followPos, followRot);
                return;
            }

            ApplyOrbit(framed);
        }

        bool TryFollowPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (FollowIndex < 0 || FollowIndex >= _puppets.Count) return false;
            var puppet = _puppets[FollowIndex];
            if (!puppet.Visible || puppet.Model == null) return false;

            // Distance in units of the HULL'S OWN measured radius, so one authored number frames
            // every ship in a fleet whose sizes span two orders of magnitude.
            float distance = puppet.Radius * (_config != null ? _config.followDistance : 9f);
            Transform t = puppet.Transform;
            position = t.position - t.forward * distance + t.up * (distance * 0.25f);
            rotation = Quaternion.LookRotation((t.position - position).normalized, Vector3.up);
            return true;
        }

        void ApplyOrbit(Bounds framed)
        {
            float margin = _config != null ? Mathf.Max(1f, _config.framingMargin) : 2.5f;
            float period = _config != null ? Mathf.Max(1f, _config.orbitSeconds) : 40f;
            float elevation = _config != null ? _config.orbitElevation : 0.35f;

            float radius = Mathf.Max(framed.extents.magnitude, 20f);
            float fov = _cameraManager != null ? _cameraManager.ReplayCameraFieldOfView : 60f;
            float halfTan = Mathf.Max(0.1f, Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));
            float distance = radius * margin / halfTan;

            float angle = (_orbitPhase / period) * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance
                             + Vector3.up * (distance * elevation);

            Vector3 position = framed.center + offset;
            _camera.SetPositionAndRotation(
                position,
                Quaternion.LookRotation((framed.center - position).normalized, Vector3.up));
        }

        void BuildPuppets()
        {
            var container = _config != null ? _config.vesselPrefabs : null;
            var colorSet = PrismLit.ColorSet;

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
                    VesselModelBuilder.TryBuild(prefab, 0f, color, out model);
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
        /// The stand-in when no <see cref="VesselPrefabContainer"/> is authored on the config: a
        /// flat wedge that states a POSE unambiguously (long axis forward, a fin so roll reads).
        /// Deliberately not a sphere — the whole point of a ghost is which way the pilot was facing.
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
