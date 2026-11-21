using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Replays on disk: a Theater recording (<c>.cstheater</c>, the frames) with a
    /// <see cref="ReplaySummary"/> beside it (<c>.replay.json</c>, what the browser lists).
    ///
    /// <para><b>The summary is what makes a file a REPLAY.</b> It is written last, so a crash
    /// between the two writes leaves a bare recording, which the Theater can still play and which
    /// this store never deletes, rather than a summary pointing at nothing. Pruning and listing work
    /// only from summaries, which is also why a recording somebody made by hand with the Theater's
    /// key (no summary) can share the folder and is never touched.</para>
    ///
    /// <para><b>Pure file I/O, folder injected.</b> No Unity object and no <c>Application</c> path
    /// is read here, so the whole store runs in an edit-mode test against a temporary folder. The
    /// service hands it the Theater's own output folder, which is why the Theater's play key (8)
    /// plays the newest replay with no browser at all.</para>
    ///
    /// <para>Never throws for an I/O fault: every operation reports a reason instead, because a
    /// replay that fails to save should say where it tried to go, not end a match with an
    /// exception.</para>
    /// </summary>
    public class ReplayStore
    {
        readonly string _folder;

        public ReplayStore(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                throw new ArgumentException("A replay store needs a folder.", nameof(folder));
            _folder = folder;
        }

        public string Folder => _folder;

        /// <summary>Absolute path of a replay's recording.</summary>
        public string RecordingPath(ReplaySummary summary) => Path.Combine(_folder, summary.RecordingFile);

        /// <summary>
        /// Write one replay. Fills <see cref="ReplaySummary.RecordingFile"/> with the name it chose.
        /// Names are <c>&lt;Mode&gt;_&lt;yyyy-MM-dd&gt;_&lt;HH-mm&gt;</c> like every other capture in
        /// the project; a second replay in the same minute takes <c>_2</c>, then <c>_3</c>, instead
        /// of overwriting the first.
        /// </summary>
        public bool TrySave(TheaterRecording recording, ReplaySummary summary, out string recordingPath, out string error)
        {
            recordingPath = null;
            error = null;

            if (recording == null || recording.Tracks.Count == 0) { error = "nothing was recorded"; return false; }
            if (summary == null) { error = "no summary"; return false; }

            try
            {
                Directory.CreateDirectory(_folder);

                string stem = UniqueStem(BuildStem(summary.GameModeName, summary.RecordedAtUtcTicks));
                string path = Path.Combine(_folder, stem + "." + TheaterRecording.Extension);
                using (var stream = File.Create(path)) recording.Write(stream);

                summary.RecordingFile = Path.GetFileName(path);
                File.WriteAllText(SidecarPathFor(stem), summary.ToJson(), Encoding.UTF8);

                recordingPath = path;
                return true;
            }
            catch (IOException e) { error = $"{e.Message} (folder: {_folder})"; return false; }
            catch (UnauthorizedAccessException e) { error = $"{e.Message} (folder: {_folder})"; return false; }
        }

        /// <summary>
        /// Every complete replay in the folder, newest first. A summary whose recording is gone, or
        /// that cannot be read, is left out of the list (and named in <paramref name="skipped"/> when
        /// asked for) - one bad file must not hide the other nineteen.
        /// </summary>
        public List<ReplaySummary> List(List<string> skipped = null)
        {
            var result = new List<ReplaySummary>();
            foreach (var entry in Scan(skipped))
                if (entry.RecordingExists) result.Add(entry.Summary);
            return result;
        }

        /// <summary>
        /// Delete the oldest replays until at most <paramref name="keepNewest"/> remain. Counts and
        /// deletes only summaries and the recordings they name; returns how many replays went.
        /// </summary>
        public int Prune(int keepNewest)
        {
            if (keepNewest < 0) keepNewest = 0;

            var entries = Scan(null);
            int removed = 0;
            for (int i = keepNewest; i < entries.Count; i++)
                if (DeleteFiles(entries[i].SidecarPath, entries[i].Summary)) removed++;
            return removed;
        }

        /// <summary>Delete one replay: its summary and its recording.</summary>
        public bool Delete(ReplaySummary summary)
        {
            if (summary == null || string.IsNullOrEmpty(summary.RecordingFile)) return false;

            foreach (var entry in Scan(null))
                if (ReplaySummary.SameReplay(entry.Summary, summary))
                    return DeleteFiles(entry.SidecarPath, entry.Summary);
            return false;
        }

        /// <summary>Load a replay's frames. Never throws; says why when it cannot.</summary>
        public bool TryLoadRecording(ReplaySummary summary, out TheaterRecording recording, out string error)
        {
            recording = null;
            error = null;
            if (summary == null || string.IsNullOrEmpty(summary.RecordingFile)) { error = "no replay"; return false; }

            string path = RecordingPath(summary);
            try
            {
                if (!File.Exists(path)) { error = $"recording missing: {summary.RecordingFile}"; return false; }
                using var stream = File.OpenRead(path);
                return TheaterRecording.TryRead(stream, out recording, out error);
            }
            catch (IOException e) { error = e.Message; return false; }
            catch (UnauthorizedAccessException e) { error = e.Message; return false; }
        }

        /// <summary>
        /// <c>&lt;Mode&gt;_&lt;yyyy-MM-dd&gt;_&lt;HH-mm&gt;</c> in local time - the mode leads because
        /// that is what a folder is triaged by, and seconds are left out because a person reads the
        /// name (the same rule the Theater and the screenshot director settled on).
        /// </summary>
        public static string BuildStem(string modeName, long utcTicks)
        {
            string mode = string.IsNullOrWhiteSpace(modeName) ? "Match" : modeName.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) mode = mode.Replace(c, '_');

            var stamp = new DateTime(Math.Max(0L, utcTicks), DateTimeKind.Utc).ToLocalTime();
            return $"{mode}_{stamp.ToString("yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture)}";
        }

        string SidecarPathFor(string stem) => Path.Combine(_folder, stem + ReplaySummary.SidecarExtension);

        string UniqueStem(string stem)
        {
            if (IsFree(stem)) return stem;
            for (int i = 2; i < 1000; i++)
            {
                string candidate = $"{stem}_{i}";
                if (IsFree(candidate)) return candidate;
            }
            // A thousand replays in one minute is not a real case; a GUID keeps the write from failing.
            return $"{stem}_{Guid.NewGuid():N}";
        }

        bool IsFree(string stem) =>
            !File.Exists(Path.Combine(_folder, stem + "." + TheaterRecording.Extension))
            && !File.Exists(SidecarPathFor(stem));

        struct Entry
        {
            public ReplaySummary Summary;
            public string SidecarPath;
            public bool RecordingExists;
        }

        /// <summary>Every readable summary, newest first.</summary>
        List<Entry> Scan(List<string> skipped)
        {
            var entries = new List<Entry>();
            string[] sidecars;
            try
            {
                if (!Directory.Exists(_folder)) return entries;
                sidecars = Directory.GetFiles(_folder, "*" + ReplaySummary.SidecarExtension);
            }
            catch (IOException e) { skipped?.Add($"{_folder}: {e.Message}"); return entries; }
            catch (UnauthorizedAccessException e) { skipped?.Add($"{_folder}: {e.Message}"); return entries; }

            for (int i = 0; i < sidecars.Length; i++)
            {
                string json;
                try { json = File.ReadAllText(sidecars[i], Encoding.UTF8); }
                catch (IOException e) { skipped?.Add($"{Path.GetFileName(sidecars[i])}: {e.Message}"); continue; }
                catch (UnauthorizedAccessException e) { skipped?.Add($"{Path.GetFileName(sidecars[i])}: {e.Message}"); continue; }

                if (!ReplaySummary.TryParse(json, out var summary, out string error))
                {
                    skipped?.Add($"{Path.GetFileName(sidecars[i])}: {error}");
                    continue;
                }

                bool exists = File.Exists(Path.Combine(_folder, summary.RecordingFile));
                if (!exists) skipped?.Add($"{Path.GetFileName(sidecars[i])}: recording missing ({summary.RecordingFile})");
                entries.Add(new Entry { Summary = summary, SidecarPath = sidecars[i], RecordingExists = exists });
            }

            entries.Sort((a, b) =>
            {
                int byTime = b.Summary.RecordedAtUtcTicks.CompareTo(a.Summary.RecordedAtUtcTicks);
                return byTime != 0 ? byTime : string.CompareOrdinal(b.Summary.RecordingFile, a.Summary.RecordingFile);
            });
            return entries;
        }

        bool DeleteFiles(string sidecarPath, ReplaySummary summary)
        {
            try
            {
                string recording = Path.Combine(_folder, summary.RecordingFile);
                if (File.Exists(recording)) File.Delete(recording);
                if (File.Exists(sidecarPath)) File.Delete(sidecarPath);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
