using System;

// Multiplayer Play Mode (Unity.Multiplayer.Playmode — kept under its original namespace).
namespace Unity.Multiplayer.Playmode
{
    /// <summary>
    /// Which MPPM virtual player this process is. The port runs exactly one player per process,
    /// so it is always the main editor with no tags — the branch the game takes in a single
    /// ordinary session. <see cref="Tags"/> may be set (tests, a future multi-process launcher)
    /// to exercise the clone paths.
    /// </summary>
    public static class CurrentPlayer
    {
        static string[] s_Tags = Array.Empty<string>();

        /// <summary>True for the main editor (the only player the port has) unless tags make this a clone.</summary>
        public static bool IsMainEditor { get; set; } = true;

        /// <summary>Port: the tags this player was launched with.</summary>
        public static string[] Tags
        {
            get => s_Tags;
            set => s_Tags = value ?? Array.Empty<string>();
        }

        /// <summary>A copy of the player's tags (empty for the main editor).</summary>
        public static string[] ReadOnlyTags() => (string[])s_Tags.Clone();
    }
}
