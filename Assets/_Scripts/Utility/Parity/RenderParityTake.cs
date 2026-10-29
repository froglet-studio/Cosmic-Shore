using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CONTROL TAKE of the render-parity harness (<c>Port/parity/README.md</c> § Render parity):
    /// the same scene, the same camera path and the same events, frame for frame, in Unity and in
    /// Amoebius, so the two renders can be compared pixel by pixel (Garrett, 2026-10-29: "make a
    /// tool in unity that will use the unity recorder in a constructed scene ... create the exact
    /// same scene and video in amoebius ... your loss function will be based on sampling pixel
    /// discrepancies").
    ///
    /// <para>Everything here is a function of the FRAME INDEX, never of wall time: the counter
    /// starts at <c>-warmupFrames</c> on the first Update, the camera stands on the first keyframe
    /// facing the other way through the warm-up and cuts to the path at take frame 0 (the cut is
    /// how the comparison tool aligns the two frame sequences), the path interpolates the keyframes, and
    /// the events fire on their frames (the prism grid spawning, the blast). Unity is held to a
    /// fixed step with <c>Time.captureFramerate</c>; Amoebius's scripted runs step every clock by
    /// 1/fps already.</para>
    ///
    /// <para>The Unity Recorder is driven by the editor tool
    /// (<c>Assets/_Scripts/Editor/Parity/RenderParityRecorder.cs</c>); Amoebius records through the
    /// player's own frame recorder (<c>Port/tools/render_parity.py</c>). This component records
    /// nothing itself, so it compiles and runs the same in both.</para>
    /// </summary>
    public class RenderParityTake : MonoBehaviour
    {
        [Serializable]
        public struct Keyframe
        {
            [Tooltip("Take frame this pose is reached on (0 = the first recorded frame).")]
            public int frame;
            public Vector3 position;
            [Tooltip("The point the camera looks at on this frame.")]
            public Vector3 lookAt;
        }

        [Serializable]
        public struct Event
        {
            [Tooltip("Take frame the event fires on (may be negative: during the warm-up).")]
            public int frame;
            [Tooltip("spawn (the prism grid lays), explode (the grid's blast), or a console verb name.")]
            public string kind;
        }

        [Header("Timing")]
        [Tooltip("Frames per second of the fixed step; both engines render one frame per step.")]
        [SerializeField] int fps = 60;
        [Tooltip("Frames after the scene starts before take frame 0 (the grid lays, materials settle).")]
        [SerializeField] int warmupFrames = 30;
        [Tooltip("Recorded frames.")]
        [SerializeField] int takeFrames = 300;

        [Header("Picture")]
        [SerializeField] int width = 1280;
        [SerializeField] int height = 720;
        [Tooltip("The camera the take poses; Camera.main when empty.")]
        [SerializeField] Camera viewCamera;

        [Header("Content")]
        [Tooltip("The prism-grid rig the spawn / explode events drive (optional).")]
        [SerializeField] PrismGridExplosionHarness harness;
        [SerializeField] List<Keyframe> keyframes = new();
        [SerializeField] List<Event> events = new();

        int _frame;
        bool _started;

        /// <summary>The take frame this Update is on: negative during the warm-up, 0 on the first recorded frame.</summary>
        public int FrameIndex => _started ? _frame : int.MinValue;
        public int Fps => fps;
        public int WarmupFrames => warmupFrames;
        public int TakeFrames => takeFrames;
        public int Width => width;
        public int Height => height;
        public bool Done => _started && _frame >= takeFrames;
        public IReadOnlyList<Keyframe> Keyframes => keyframes;

        void Awake()
        {
            // A fixed step in Unity: every frame advances the clocks by exactly 1/fps however long it
            // takes to draw. (DeterministicSession does the same for replays once it lands.)
            Time.captureFramerate = fps;
            if (viewCamera == null) viewCamera = Camera.main;
            if (harness == null) harness = FindFirstObjectByType<PrismGridExplosionHarness>();
        }

        void OnDestroy()
        {
            Time.captureFramerate = 0;
        }

        void Update()
        {
            if (!_started)
            {
                _started = true;
                _frame = -warmupFrames;
                CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[RenderParityTake] started: warm-up {warmupFrames}, take {takeFrames} frames at {fps} fps, {width}x{height}");
            }
            else
            {
                _frame++;
            }
            if (_frame == 0) CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[RenderParityTake] take frame 0 at Time.frameCount {Time.frameCount}");
            // No UI in a control take: the grid rig's canvas and the diagnostics HUD are screen-space overlays
            // the Unity Recorder's camera input never sees, so neither engine may draw them.
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.enabled) canvas.enabled = false;
            foreach (var e in events)
                if (e.frame == _frame) Fire(e.kind);
        }

        void LateUpdate()
        {
            if (!_started || viewCamera == null || keyframes.Count == 0) return;
            if (_frame < 0) WarmupPose(viewCamera.transform);
            else Pose(viewCamera.transform, _frame);
        }

        /// <summary>
        /// The warm-up pose: keyframe 0's position facing the opposite way, so take frame 0 is a cut
        /// both engines show on the same frame and the comparison aligns on it rather than on frame numbers.
        /// </summary>
        public void WarmupPose(Transform cam)
        {
            var k = keyframes[0];
            var dir = k.position - k.lookAt;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.back;
            cam.SetPositionAndRotation(k.position, Quaternion.LookRotation(dir.normalized, Vector3.up));
        }

        void Fire(string kind)
        {
            switch (kind)
            {
                case "spawn": if (harness != null) harness.Spawn(); break;
                case "explode": if (harness != null) harness.Explode(); break;
                case "clear": if (harness != null) harness.Clear(); break;
                default: CSDebug.LogWarning($"[RenderParityTake] unknown event '{kind}' on frame {_frame}"); break;
            }
        }

        /// <summary>The camera on the path at FRAME: a smoothstep between the two keyframes around it, held at the ends.</summary>
        public void Pose(Transform cam, int frame)
        {
            var a = keyframes[0]; var b = keyframes[keyframes.Count - 1];
            for (int i = 0; i + 1 < keyframes.Count; i++)
            {
                if (frame >= keyframes[i].frame && frame <= keyframes[i + 1].frame) { a = keyframes[i]; b = keyframes[i + 1]; break; }
            }
            float t = b.frame > a.frame ? Mathf.Clamp01((frame - a.frame) / (float)(b.frame - a.frame)) : 0f;
            t = t * t * (3f - 2f * t);
            var pos = Vector3.Lerp(a.position, b.position, t);
            var look = Vector3.Lerp(a.lookAt, b.lookAt, t);
            var dir = look - pos;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            cam.SetPositionAndRotation(pos, Quaternion.LookRotation(dir.normalized, Vector3.up));
        }
    }
}
