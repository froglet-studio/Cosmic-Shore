using System;
using System.IO;
using System.IO.Compression;

namespace CosmicShore.Mobile
{
    /// <summary>
    /// Puts a build's player data where the content runtime can read it. On Android the data
    /// ships inside the APK as one archive (APK assets are not files), so the first launch of each
    /// new build unpacks it into the app's private storage — Unity's own player reads its data
    /// out of the APK the same way, once. Unchanged builds skip straight to the game: the archive
    /// carries its content hash, and the unpacked copy remembers the hash it came from.
    /// </summary>
    public static class PlayerDataInstaller
    {
        public const string Archive = "data.pak";
        public const string HashFile = "data.hash";
        const string Stamp = ".installed";

        /// <param name="open">Opens a packaged file by name (the APK's asset manager).</param>
        /// <param name="destination">Where the player data lives on disk (rewritten when the build changed).</param>
        /// <param name="scratch">A writable folder for the archive copy (deleted afterwards).</param>
        /// <returns>The data root to hand the content runtime.</returns>
        public static string Install(Func<string, Stream> open, string destination, string scratch, Action<string> log = null)
        {
            log ??= _ => { };
            string hash;
            using (var reader = new StreamReader(open(HashFile))) hash = reader.ReadToEnd().Trim();
            var stamp = Path.Combine(destination, Stamp);
            if (File.Exists(stamp) && File.ReadAllText(stamp).Trim() == hash && File.Exists(Path.Combine(destination, "PlayerData.json")))
            {
                log($"[data] player data {hash} already installed");
                return destination;
            }

            var started = DateTime.UtcNow;
            if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
            Directory.CreateDirectory(destination);
            Directory.CreateDirectory(scratch);
            var copy = Path.Combine(scratch, Archive);
            // ZipArchive needs a seekable stream; an APK asset stream is not one.
            using (var src = open(Archive))
            using (var dst = File.Create(copy))
                src.CopyTo(dst, 1 << 20);
            ZipFile.ExtractToDirectory(copy, destination, overwriteFiles: true);
            File.Delete(copy);
            File.WriteAllText(stamp, hash);
            log($"[data] installed player data {hash} in {(DateTime.UtcNow - started).TotalSeconds:F1} s");
            return destination;
        }
    }
}
