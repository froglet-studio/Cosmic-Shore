using System;
using Object = UnityEngine.Object;

namespace CosmicShore.UI
{
    /// <summary>
    /// Runs a kit component's re-apply from <c>OnValidate</c> safely. Setting a Graphic's colour or
    /// a TMP text's font inside <c>OnValidate</c> marks layout and vertices dirty, which Unity
    /// rejects there ("SendMessage cannot be called during ... OnValidate"); in the editor the call
    /// is deferred one tick and skipped if the component was destroyed meanwhile. Outside the
    /// editor <c>OnValidate</c> never runs, so the action simply runs.
    /// </summary>
    internal static class KitEditorDefer
    {
        public static void Run(Object owner, Action action)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (owner) action();
            };
#else
            action();
#endif
        }
    }
}
