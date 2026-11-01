using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The FLIGHT TAKE of the render-parity harness (<c>Port/parity/README.md</c> § Render parity): vessel hulls
    /// racing through each other's prism trails, the footage highlight reels are cut from, played identically in
    /// Unity and in Amoebius so the two renders can be scored pixel by pixel.
    ///
    /// <para>Nothing here simulates. A real vessel's flight runs its input, AI and physics, and two engines
    /// never agree on those to the pixel; the take is about rendering, so every hull follows a closed-form path
    /// of the take's frame index (<see cref="RenderParityTake.FrameIndex"/>): a straight run plus a sine sway
    /// per axis, facing along its own velocity. Each hull lays a prism of its domain behind it every few
    /// frames through the game's one lay primitive (<see cref="PrismTrailBuilder.LayOne"/>), so the trails grow,
    /// colour and light exactly as a vessel's do. From take frame 0 the camera chases one hull (the warm-up keeps
    /// the take's own pose, so the cut both engines align on stays).</para>
    ///
    /// <para>Runs after <see cref="RenderParityTake"/> (execution order 100): its Update reads the frame the
    /// take has just counted, and its LateUpdate replaces the take's camera pose.</para>
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class RenderParityFlight : MonoBehaviour
    {
        [Serializable]
        public struct Flyer
        {
            [Tooltip("The hull this path moves (a mesh at rest: no vessel components).")]
            public Transform hull;
            [Tooltip("The domain of the trail it lays.")]
            public Domains domain;
            [Tooltip("Where it is on the flight's first frame.")]
            public Vector3 origin;
            [Tooltip("Its straight-line velocity, units per second.")]
            public Vector3 velocity;
            [Tooltip("The sway added on each axis, units.")]
            public Vector3 swayAmplitude;
            [Tooltip("The sway's frequency on each axis, cycles per second.")]
            public Vector3 swayFrequency;
            [Tooltip("The sway's phase on each axis, cycles.")]
            public Vector3 swayPhase;
        }

        [Header("Take")]
        [Tooltip("The take whose frame index drives the flight.")]
        [SerializeField] RenderParityTake take;
        [Tooltip("Take frame the flight starts on (negative: during the warm-up, so the trails exist by frame 0).")]
        [SerializeField] int startFrame = -90;

        [Header("Trails")]
        [Tooltip("The prism every hull lays (the game's trail prism prefab).")]
        [SerializeField] Prism prismPrefab;
        [Tooltip("A prism is laid every this many frames.")]
        [SerializeField] int layEveryFrames = 2;
        [Tooltip("How far behind the hull's centre a prism is laid, units.")]
        [SerializeField] float layBehind = 6f;
        [Tooltip("The laid prism's size.")]
        [SerializeField] Vector3 prismScale = new(3f, 1.5f, 8f);

        [Header("Flyers")]
        [SerializeField] List<Flyer> flyers = new();

        [Header("Chase camera")]
        [Tooltip("The flyer the camera chases from take frame 0 (-1: the take's own path).")]
        [SerializeField] int chase = 0;
        [Tooltip("The camera's offset from the chased hull, in the hull's yaw frame (x right, y up, z forward).")]
        [SerializeField] Vector3 chaseOffset = new(0f, 3.5f, -13f);
        [Tooltip("How far ahead of the hull the camera looks, units.")]
        [SerializeField] float lookAhead = 30f;

        readonly List<Trail> _trails = new();
        Transform _trailRoot;

        public IReadOnlyList<Flyer> Flyers => flyers;

        /// <summary>Where flyer <paramref name="f"/> is, and which way it faces, at take frame <paramref name="frame"/>.</summary>
        public void PoseAt(in Flyer f, int frame, out Vector3 position, out Vector3 forward)
        {
            float fps = take != null ? take.Fps : 60f;
            float t = Mathf.Max(0, frame - startFrame) / fps;
            const float Tau = 2f * Mathf.PI;
            var arg = new Vector3(
                Tau * (f.swayFrequency.x * t + f.swayPhase.x),
                Tau * (f.swayFrequency.y * t + f.swayPhase.y),
                Tau * (f.swayFrequency.z * t + f.swayPhase.z));
            position = f.origin + f.velocity * t + new Vector3(
                f.swayAmplitude.x * Mathf.Sin(arg.x),
                f.swayAmplitude.y * Mathf.Sin(arg.y),
                f.swayAmplitude.z * Mathf.Sin(arg.z));
            var v = f.velocity + new Vector3(
                f.swayAmplitude.x * Tau * f.swayFrequency.x * Mathf.Cos(arg.x),
                f.swayAmplitude.y * Tau * f.swayFrequency.y * Mathf.Cos(arg.y),
                f.swayAmplitude.z * Tau * f.swayFrequency.z * Mathf.Cos(arg.z));
            forward = v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        void Awake()
        {
            if (take == null) take = FindFirstObjectByType<RenderParityTake>();
        }

        void Update()
        {
            if (take == null) return;
            int frame = take.FrameIndex;
            if (frame == int.MinValue) return;

            bool lay = prismPrefab != null && frame >= startFrame && (frame - startFrame) % Mathf.Max(1, layEveryFrames) == 0;
            if (lay && _trailRoot == null)
            {
                _trailRoot = new GameObject("[RenderParityTrails]").transform;
                for (int i = 0; i < flyers.Count; i++) _trails.Add(new Trail());
            }

            for (int i = 0; i < flyers.Count; i++)
            {
                var f = flyers[i];
                PoseAt(f, frame, out var pos, out var fwd);
                var rot = Quaternion.LookRotation(fwd, Vector3.up);
                if (f.hull != null) f.hull.SetPositionAndRotation(pos, rot);
                if (!lay) continue;
                var point = new SpawnPoint(pos - fwd * layBehind, rot, prismScale);
                PrismTrailBuilder.LayOne(prismPrefab, new PrismLay(point, f.domain), _trailRoot, _trails[i],
                    $"parity-flyer-{i}", watchForReveal: false);
            }
        }

        void LateUpdate()
        {
            if (take == null || chase < 0 || chase >= flyers.Count) return;
            int frame = take.FrameIndex;
            if (frame == int.MinValue || frame < 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            PoseAt(flyers[chase], frame, out var pos, out var fwd);
            var flat = new Vector3(fwd.x, 0f, fwd.z);
            var yaw = Quaternion.LookRotation(flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward, Vector3.up);
            var eye = pos + yaw * chaseOffset;
            var look = pos + fwd * lookAhead;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        }
    }
}
