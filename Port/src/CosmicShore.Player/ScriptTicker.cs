using System;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary>
    /// Runs a script action from inside the frame (Update, before every LateUpdate flush), for a
    /// fixed number of frames — the way a real producer reports a per-frame light or hull. A
    /// report made from the script's pre-tick hook carries the PREVIOUS Time.frameCount and is
    /// dropped as stale by a frame-stamped publisher.
    /// </summary>
    public sealed class ScriptTicker : MonoBehaviour
    {
        public Action Act;
        public int Remaining;

        void Update()
        {
            if (Remaining-- <= 0) { Destroy(gameObject); return; }
            Act?.Invoke();
        }

        public static void Run(string name, Action act, int frames)
        {
            var go = new GameObject("ScriptTicker:" + name);
            var t = go.AddComponent<ScriptTicker>();
            t.Act = act;
            t.Remaining = frames;
        }
    }
}
