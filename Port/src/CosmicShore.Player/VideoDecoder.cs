using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CosmicShore.Engine;
using CosmicShore.Engine.Video;

namespace CosmicShore.Player
{
    /// <summary>
    /// The desktop player's <see cref="VideoPlayer.FrameDecoder"/>: a clip is decoded once, whole, by the ffmpeg on the PATH
    /// (in the background; the target keeps its last frame until it is ready) into RGBA frames at the target's size (at
    /// most <see cref="MaxWidth"/> wide), and each presented frame is handed to the target as its
    /// <see cref="RenderTexture.cpuFrame"/>. Menu clips are short loops (the Ark's backdrops: 96 frames), so whole-clip
    /// decoding is a few hundred MB at most. With no ffmpeg the clips play their clock with no pixels, as before.
    /// </summary>
    static class VideoDecoder
    {
        const int MaxWidth = 960;

        sealed class Frames
        {
            public int Width, Height;
            public byte[][] Data;
        }

        static readonly ConcurrentDictionary<string, Task<Frames>> s_Clips = new();
        static readonly ConditionalWeakTable<RenderTexture, Texture2D> s_Targets = new();
        static bool s_Missing;

        public static void Install() => VideoPlayer.FrameDecoder = Present;

        static void Present(VideoPlayer player, double time)
        {
            var target = player.targetTexture;
            var clip = player.clip;
            if (target == null || clip == null || string.IsNullOrEmpty(clip.originalPath) || s_Missing) return;
            int w = Math.Min(MaxWidth, Math.Max(16, target.width));
            int h = Math.Max(16, (int)Math.Round((double)w * target.height / Math.Max(1, target.width)));
            var key = $"{clip.originalPath}|{w}x{h}";
            var task = s_Clips.GetOrAdd(key, _ => Task.Run(() => Decode(clip.originalPath, w, h)));
            if (!task.IsCompletedSuccessfully || task.Result == null || task.Result.Data.Length == 0) return;
            var frames = task.Result;
            double fps = clip.frameRate > 0 ? clip.frameRate : 30.0;
            int index = (int)Math.Floor(time * fps);
            index = Math.Clamp(index, 0, frames.Data.Length - 1);
            if (!s_Targets.TryGetValue(target, out var tex) || tex.width != frames.Width || tex.height != frames.Height)
            {
                tex = new Texture2D(frames.Width, frames.Height, frames.Data[index]) { name = "VideoFrame" };
                s_Targets.AddOrUpdate(target, tex);
            }
            else if (!ReferenceEquals(tex.GetRawTextureData(), frames.Data[index]))
            {
                tex.LoadRawTextureData(frames.Data[index]);
            }
            target.cpuFrame = tex;
        }

        static Frames Decode(string path, int w, int h)
        {
            var psi = new ProcessStartInfo("ffmpeg")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in new[] { "-loglevel", "error", "-i", path, "-vf", $"scale={w}:{h},vflip", "-f", "rawvideo", "-pix_fmt", "rgba", "-" })
                psi.ArgumentList.Add(a);
            try
            {
                using var p = Process.Start(psi);
                if (p == null) return null;
                p.ErrorDataReceived += (_, _) => { };
                p.BeginErrorReadLine();
                using var ms = new MemoryStream();
                p.StandardOutput.BaseStream.CopyTo(ms);
                p.WaitForExit();
                var all = ms.ToArray();
                int size = w * h * 4, n = all.Length / size;
                var frames = new Frames { Width = w, Height = h, Data = new byte[n][] };
                for (int i = 0; i < n; i++)
                {
                    frames.Data[i] = new byte[size];
                    Buffer.BlockCopy(all, i * size, frames.Data[i], 0, size);
                }
                Console.WriteLine($"[video] decoded {Path.GetFileName(path)}: {n} frames at {w}x{h}");
                return frames;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                if (!s_Missing) Console.WriteLine("[video] no ffmpeg on the PATH: video clips play without pixels");
                s_Missing = true;
                return null;
            }
        }
    }
}
