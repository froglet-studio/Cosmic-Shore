#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Prisma.Workspace
{
    /// <summary>What a store held: the board's text, or null when there is none yet.</summary>
    public readonly record struct BoardRead(string? Text);

    /// <summary>
    /// Where a board lives. The board does the parsing and merging; a store only moves text,
    /// replaces it atomically, copies an unreadable board aside, and says when it changed.
    /// Today: board.json (<see cref="FileBoardStore"/>). Later: a shared workspace.
    /// </summary>
    public interface IBoardStore
    {
        /// <summary>The stored text, or null when nothing is stored yet.</summary>
        BoardRead Read();
        /// <summary>Replaces the stored text in one step: a reader sees the old board or the new one, never half of one.</summary>
        void Write(string text);
        /// <summary>Keeps a copy of text that could not be read as a board. Returns where it went (null if it could not be kept).</summary>
        string? Preserve(string text);
        /// <summary>Changes whenever the stored text does; null when nothing is stored.</summary>
        string? Stamp();
        /// <summary>Held around read-merge-write so two writers do not interleave.</summary>
        IDisposable Lock();
    }

    /// <summary>
    /// board.json in a folder (Prisma's tracks folder by default). Writes go to a temporary file
    /// in the same folder and are then moved over board.json. An unreadable board is copied to
    /// <c>board.json.corrupt-yyyyMMdd-HHmmss</c> once (an identical copy is not made twice).
    /// Writers serialise on <c>board.json.lock</c>.
    /// </summary>
    public sealed class FileBoardStore : IBoardStore
    {
        public string Dir { get; }
        public string Path => FileIn(Dir);
        public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(3);

        public FileBoardStore(string dir) => Dir = dir;

        public static string FileIn(string dir) => System.IO.Path.Combine(dir, "board.json");

        public BoardRead Read()
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return new BoardRead(File.Exists(Path) ? File.ReadAllText(Path) : null); }
                catch (IOException) when (attempt < 5) { Thread.Sleep(20); }   // a writer is mid-replace (Windows)
            }
        }

        public void Write(string text)
        {
            Directory.CreateDirectory(Dir);
            var tmp = System.IO.Path.Combine(Dir, $"board.json.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var f = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(text);
                    f.Write(bytes, 0, bytes.Length);
                    f.Flush(true);
                }
                for (int attempt = 0; ; attempt++)
                {
                    try { File.Move(tmp, Path, overwrite: true); break; }
                    catch (IOException) when (attempt < 10) { Thread.Sleep(25); }       // a reader holds it open (Windows)
                    catch (UnauthorizedAccessException) when (attempt < 10) { Thread.Sleep(25); }
                }
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { }
            }
        }

        public string? Preserve(string text)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                foreach (var existing in Directory.EnumerateFiles(Dir, "board.json.corrupt-*"))
                    if (File.ReadAllText(existing) == text) return existing;
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var copy = Path + ".corrupt-" + stamp;
                for (int n = 2; File.Exists(copy); n++) copy = Path + ".corrupt-" + stamp + "-" + n;
                File.WriteAllText(copy, text, new UTF8Encoding(false));
                return copy;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        }

        public string? Stamp()
        {
            var f = new FileInfo(Path);
            return f.Exists ? $"{f.LastWriteTimeUtc.Ticks}:{f.Length}" : null;
        }

        /// <summary>
        /// An exclusive handle on board.json.lock (an OS lock on Windows, flock on Linux and macOS).
        /// If another writer holds it past <see cref="LockTimeout"/>, saving goes ahead without it.
        /// The merge still protects the data, and the UI never hangs on a stuck lock.
        /// </summary>
        public IDisposable Lock()
        {
            Directory.CreateDirectory(Dir);
            var deadline = DateTime.UtcNow + LockTimeout;
            while (true)
            {
                try { return new FileStream(Path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) when (DateTime.UtcNow < deadline) { Thread.Sleep(15); }
                catch (IOException) { return new Nothing(); }
                catch (UnauthorizedAccessException) { return new Nothing(); }
            }
        }

        sealed class Nothing : IDisposable { public void Dispose() { } }
    }

    /// <summary>A board kept in memory: for tests, and a model of what a shared store must do.</summary>
    public sealed class MemoryBoardStore : IBoardStore
    {
        readonly object _gate = new();
        int _version;
        public string? Text { get; set; }
        public System.Collections.Generic.List<string> Preserved { get; } = new();

        public BoardRead Read() { lock (_gate) return new BoardRead(Text); }
        public void Write(string text) { lock (_gate) { Text = text; _version++; } }
        public string? Preserve(string text) { lock (_gate) { if (!Preserved.Contains(text)) Preserved.Add(text); return "memory:corrupt-" + Preserved.IndexOf(text); } }
        public string? Stamp() { lock (_gate) return Text == null ? null : $"{_version}:{Text.Length}:{Text.GetHashCode()}"; }
        public IDisposable Lock() { Monitor.Enter(_gate); return new Release(_gate); }

        /// <summary>Another writer replaces the text (what an agent's prisma-mcp does to board.json).</summary>
        public void Replace(string text) => Write(text);

        sealed class Release : IDisposable
        {
            readonly object _g; bool _done;
            public Release(object g) => _g = g;
            public void Dispose() { if (!_done) { _done = true; Monitor.Exit(_g); } }
        }
    }
}
