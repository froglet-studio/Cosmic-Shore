using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Rides on a piece of the end screen (the scoreboard panel, the reveal toast) and says when something destroys or
    /// moves it during play. Both were found destroyed at the end of a match with nothing in the console (2026-10-17,
    /// Scurry, Skim Race and Maelstrom), so the end screen had nothing left to show. Added in code by the component that
    /// owns the piece; never authored.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndScreenWitness : MonoBehaviour
    {
        static bool s_quitting;

        string _role;

        /// <summary>Puts a witness on <paramref name="target"/> (once) naming what the piece is.</summary>
        public static void Watch(Component target, string role)
        {
            if (!target) return;
            if (!target.TryGetComponent(out EndScreenWitness witness))
                witness = target.gameObject.AddComponent<EndScreenWitness>();
            witness._role = role;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetQuitting()
        {
            s_quitting = false;
            Application.quitting -= MarkQuitting;
            Application.quitting += MarkQuitting;
        }

        static void MarkQuitting() => s_quitting = true;

        void OnTransformParentChanged()
        {
            CSDebug.LogWarning($"[EndScreen] The {_role} was moved to '{PathOf(transform.parent)}' - the end screen may not find it there.");
        }

        void OnDestroy()
        {
            // A scene unload or quitting destroys everything; only a destroy during play is news.
            if (s_quitting || !gameObject.scene.isLoaded) return;
            CSDebug.LogWarning($"[EndScreen] The {_role} '{PathOf(transform)}' was destroyed during play (frame " +
                               $"{Time.frameCount}) - the end screen cannot show it now. Call stack (complete only for " +
                               $"DestroyImmediate):\n{System.Environment.StackTrace}");
        }

        static string PathOf(Transform t)
        {
            if (!t) return "(scene root)";
            var path = t.name;
            for (var p = t.parent; p; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
