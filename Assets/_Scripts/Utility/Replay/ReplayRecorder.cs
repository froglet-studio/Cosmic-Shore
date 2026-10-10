using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Records the parity replay's <c>status</c> stream: one <see cref="ReplayStatusFrame"/> per
    /// <see cref="IInputStrategy.ProcessInput"/> call, snapshotted by <see cref="InputController"/>
    /// right after the live strategy wrote <see cref="IInputStatus"/>, plus every
    /// <see cref="InputEvents"/> the strategy raised through <c>OnButtonPressed</c> /
    /// <c>OnButtonReleased</c> since the previous snapshot.
    ///
    /// <para>Inert unless <see cref="Start"/> was called: the one call site in
    /// <c>InputController.Update</c> tests <see cref="Recording"/> first. Nothing here writes
    /// input; it only reads what the strategy produced.</para>
    /// </summary>
    public static class ReplayRecorder
    {
        static readonly List<ReplayStatusFrame> s_frames = new();
        static readonly List<string> s_pressed = new();
        static readonly List<string> s_released = new();
        static ReplayFile s_header;
        static IInputStatus s_bound;

        /// <summary>True between <see cref="Start"/> and <see cref="Stop"/>.</summary>
        public static bool Recording { get; private set; }

        /// <summary>Frames captured so far in this recording.</summary>
        public static int FrameCount => s_frames.Count;

        /// <summary>
        /// Begins a recording. <paramref name="header"/> supplies scene, seed, frames,
        /// checkpointEvery, record and do; its status stream is replaced by what is recorded.
        /// Null starts from a default header carrying the active scene and the deterministic
        /// session's seed when one is running.
        /// </summary>
        public static void Start(ReplayFile header = null)
        {
            Stop();
            s_header = header ?? new ReplayFile
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                seed = DeterministicSession.IsActive ? DeterministicSession.Seed : 0,
            };
            s_frames.Clear();
            s_pressed.Clear();
            s_released.Clear();
            Recording = true;
            CSDebug.LogVerbose(CSLogChannel.Parity, "[ReplayRecorder] recording started");
        }

        /// <summary>
        /// Snapshots <paramref name="status"/> as the next frame. Called by InputController after
        /// <c>currentStrategy.ProcessInput()</c>; the button events raised during that call were
        /// collected by the subscriptions <see cref="Bind"/> made on this status' event assets.
        /// </summary>
        public static void Capture(IInputStatus status)
        {
            if (!Recording || status == null) return;
            Bind(status);
            s_frames.Add(new ReplayStatusFrame
            {
                f = Time.frameCount,
                XSum = status.XSum,
                YSum = status.YSum,
                XDiff = status.XDiff,
                YDiff = status.YDiff,
                Throttle = status.Throttle,
                LeftTriggerAnalog = status.LeftTriggerAnalog,
                RightTriggerAnalog = status.RightTriggerAnalog,
                pressed = s_pressed.ToArray(),
                released = s_released.ToArray(),
            });
            s_pressed.Clear();
            s_released.Clear();
        }

        /// <summary>Ends the recording and returns the replay with its status stream filled in (null when nothing was recording).</summary>
        public static ReplayFile Stop()
        {
            if (!Recording) return null;
            Recording = false;
            Unbind();
            var file = s_header;
            file.status = s_frames.ToArray();
            if (file.frames <= 0) file.frames = file.status.Length;
            s_header = null;
            s_frames.Clear();
            CSDebug.LogVerbose(CSLogChannel.Parity, $"[ReplayRecorder] recording stopped: {file.status.Length} frame(s)");
            return file;
        }

        /// <summary>Ends the recording and writes it to <paramref name="path"/>; returns the file, or null when nothing was recording.</summary>
        public static ReplayFile StopAndSave(string path)
        {
            var file = Stop();
            file?.Save(path);
            return file;
        }

        static void Bind(IInputStatus status)
        {
            if (ReferenceEquals(s_bound, status)) return;
            Unbind();
            s_bound = status;
            if (status.OnButtonPressed != null) status.OnButtonPressed.OnRaised += OnPressed;
            if (status.OnButtonReleased != null) status.OnButtonReleased.OnRaised += OnReleased;
        }

        static void Unbind()
        {
            if (s_bound == null) return;
            if (s_bound.OnButtonPressed != null) s_bound.OnButtonPressed.OnRaised -= OnPressed;
            if (s_bound.OnButtonReleased != null) s_bound.OnButtonReleased.OnRaised -= OnReleased;
            s_bound = null;
        }

        static void OnPressed(InputEvents e) => s_pressed.Add(e.ToString());
        static void OnReleased(InputEvents e) => s_released.Add(e.ToString());
    }
}
