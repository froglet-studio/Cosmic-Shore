using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Keeps the world colour trace from writing a colour set into an asset file. <see cref="WorldColorTrace"/> paints
    /// materials and prefab assets where they live in memory (so everything spawned later is born in the worn set); in
    /// the editor those are the project's own assets, and a material changed in play is saved with the project. So:
    /// <list type="bullet">
    /// <item>it tells the painter which objects live in an asset file (<see cref="EditorUtility.IsPersistent"/>), which
    /// is what it records;</item>
    /// <item>when play ends it puts every recorded colour back and clears the asset's dirty flag (its file is
    /// unchanged);</item>
    /// <item>when the editor saves assets during play it puts them back first, then paints again after the save.</item>
    /// </list>
    /// A reader of the editor only: it writes no asset of its own.
    /// </summary>
    [InitializeOnLoad]
    static class WorldColorTraceEditor
    {
        static WorldColorTraceEditor()
        {
            WorldColorTrace.IsPersistent = EditorUtility.IsPersistent;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) Restore();
            };
        }

        internal static void Restore() => WorldColorTrace.RestoreAssets(EditorUtility.ClearDirty);
    }

    /// <summary>Puts the trace's colours back before any save during play (<see cref="WorldColorTraceEditor"/>).</summary>
    class WorldColorTraceSaveGuard : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            if (!EditorApplication.isPlaying || !WorldColorTrace.HasAssetChanges) return paths;
            WorldColorTraceEditor.Restore();
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying) WorldColorTrace.Repaint();
            };
            return paths;
        }
    }
}
