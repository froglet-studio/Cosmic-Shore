using System;
using System.IO;

namespace CosmicShore.Content.Videos
{
    /// <summary>
    /// What Unity's VideoClipImporter shows of an MP4 / QuickTime movie: its length, and its video track's size and frame
    /// rate, read from the ISO base media boxes (moov/mvhd, trak/tkhd, mdia/mdhd, mdia/hdlr, stbl/stts). Reads the box
    /// headers only; no decoding.
    /// </summary>
    public static class Mp4Header
    {
        public readonly struct Info
        {
            public readonly double Seconds;
            public readonly int Width, Height;
            public readonly double FrameRate;
            public Info(double seconds, int width, int height, double frameRate)
            {
                Seconds = seconds;
                Width = width;
                Height = height;
                FrameRate = frameRate;
            }
        }

        public static bool TryRead(string path, out Info info)
        {
            info = default;
            try
            {
                var data = File.ReadAllBytes(path);
                return TryRead(data, out info);
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public static bool TryRead(byte[] d, out Info info)
        {
            info = default;
            if (!Find(d, 0, d.Length, "moov", out int moov, out int moovEnd)) return false;
            double seconds = 0;
            if (Find(d, moov, moovEnd, "mvhd", out int mvhd, out _))
            {
                int v = d[mvhd];
                long scale = v == 1 ? U32(d, mvhd + 20) : U32(d, mvhd + 12);
                long dur = v == 1 ? (long)U64(d, mvhd + 24) : U32(d, mvhd + 16);
                if (scale > 0) seconds = (double)dur / scale;
            }
            int w = 0, h = 0;
            double fps = 0;
            int at = moov;
            while (Next(d, at, moovEnd, out var type, out int body, out int end))
            {
                at = end;
                if (type != "trak") continue;
                if (!Find(d, body, end, "mdia", out int mdia, out int mdiaEnd)) continue;
                if (!Find(d, mdia, mdiaEnd, "hdlr", out int hdlr, out _) || Tag(d, hdlr + 8) != "vide") continue;
                if (Find(d, body, end, "tkhd", out int tkhd, out int tkhdEnd))
                {
                    w = (int)(U32(d, tkhdEnd - 8) >> 16);
                    h = (int)(U32(d, tkhdEnd - 4) >> 16);
                }
                if (Find(d, mdia, mdiaEnd, "mdhd", out int mdhd, out _)
                    && Find(d, mdia, mdiaEnd, "minf", out int minf, out int minfEnd)
                    && Find(d, minf, minfEnd, "stbl", out int stbl, out int stblEnd)
                    && Find(d, stbl, stblEnd, "stts", out int stts, out _))
                {
                    int v = d[mdhd];
                    long scale = v == 1 ? U32(d, mdhd + 20) : U32(d, mdhd + 12);
                    long dur = v == 1 ? (long)U64(d, mdhd + 24) : U32(d, mdhd + 16);
                    long entries = U32(d, stts + 4), frames = 0;
                    for (long i = 0; i < entries; i++) frames += U32(d, stts + 8 + (int)i * 8);
                    if (scale > 0 && dur > 0) fps = frames / ((double)dur / scale);
                }
                break;
            }
            info = new Info(seconds, w, h, fps);
            return seconds > 0;
        }

        // The box named `type` directly inside [from, to): its body's start and its end.
        static bool Find(byte[] d, int from, int to, string type, out int body, out int end)
        {
            int at = from;
            while (Next(d, at, to, out var t, out body, out end))
            {
                if (t == type) return true;
                at = end;
            }
            body = end = 0;
            return false;
        }

        static bool Next(byte[] d, int at, int to, out string type, out int body, out int end)
        {
            type = null;
            body = end = 0;
            if (at + 8 > to) return false;
            long size = U32(d, at);
            type = Tag(d, at + 4);
            body = at + 8;
            if (size == 1)
            {
                if (at + 16 > to) return false;
                size = (long)U64(d, at + 8);
                body = at + 16;
            }
            else if (size == 0) size = to - at;
            if (size < 8 || at + size > to) return false;
            end = (int)(at + size);
            return true;
        }

        static string Tag(byte[] d, int at) => at + 4 <= d.Length ? System.Text.Encoding.ASCII.GetString(d, at, 4) : string.Empty;
        static long U32(byte[] d, int at) => at + 4 <= d.Length ? (long)((uint)d[at] << 24 | (uint)d[at + 1] << 16 | (uint)d[at + 2] << 8 | d[at + 3]) : 0;
        static ulong U64(byte[] d, int at) => (ulong)U32(d, at) << 32 | (ulong)U32(d, at + 4);
    }
}
