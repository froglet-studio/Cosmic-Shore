using System;
using System.IO;

namespace CosmicShore.Engine
{
    /// <summary>
    /// The per-user writable data folder, never empty. .NET returns "" for
    /// <see cref="Environment.SpecialFolder.LocalApplicationData"/> when the XDG data folder does not exist
    /// (a fresh Linux account, a container), and Path.Combine("", x) is a RELATIVE path: saves then land
    /// in whatever the working directory is, and two players launched from one folder share a save.
    /// Falls back to ~/.local/share, the XDG default (docs/MULTIPLAYER.md §6.1).
    /// </summary>
    public static class LocalDataPath
    {
        public static string Root
        {
            get
            {
                var dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(dir))
                    dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
                return dir;
            }
        }

        public static string Combine(string folder) => Path.Combine(Root, folder);
    }
}
