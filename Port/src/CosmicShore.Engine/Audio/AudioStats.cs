using System.Collections.Generic;

namespace CosmicShore.Engine.Audio.Fmod
{
    /// <summary>
    /// What the game asked the audio layer for during a run: event instances by path, events the
    /// loaded banks do not have, and one-shots fired with an unwired (empty) EventReference. The
    /// player's session report carries these, so audio problems show up in Prisma's tracks.
    /// </summary>
    public static class AudioStats
    {
        static readonly object Gate = new();
        public static readonly Dictionary<string, int> ByEvent = new();
        public static readonly HashSet<string> Missing = new();
        public static int Instances;
        public static int UnwiredOneShots;

        public static void Created(string evt)
        {
            lock (Gate)
            {
                Instances++;
                ByEvent.TryGetValue(evt, out int n);
                ByEvent[evt] = n + 1;
            }
        }

        public static List<KeyValuePair<string, int>> Snapshot()
        {
            lock (Gate) return new List<KeyValuePair<string, int>>(ByEvent);
        }
    }
}
