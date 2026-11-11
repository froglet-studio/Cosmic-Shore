using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Keeps the world colour trace from writing a colour set into an asset file. <see cref="WorldColorTrace"/> paints
    /// materials and prefab assets where they live in memory (so everything spawned later is born in the worn set); in
    /// the editor those are the project's own assets, and a material changed in play is saved with the project. So:
    /// <list type="bullet">
    /// <item>it tells the painter which objects live in an asset file (<see cref="EditorUtility.IsPersistent"/>), which
    /// is what it records, and which are an editor preview's (Prefab Mode's stage, an inspector preview), which it
    /// never paints;</item>
    /// <item>when play stops it ends painting first (<see cref="WorldColorTrace.Stop"/>: the scenes torn down after
    /// this point may wear a set again), then puts back every colour a repaint gave that the asset still holds. A
    /// colour a person changed during play is theirs and stays. An asset's dirty flag is cleared only when every
    /// recorded colour went back, it was not dirty before play painted it and nothing else marked it dirty since, so
    /// its file is unchanged and a person's other unsaved edit keeps its flag. A second pass when the editor is back in
    /// edit mode catches anything painted after the first;</item>
    /// <item>when the editor saves assets during play it puts them back the same way first, then paints again after
    /// the save; and before scripts recompile during play (the record would not survive the domain reload).</item>
    /// </list>
    /// UIThemeLive's UI prefab asset colours are recorded the same way (<see cref="WorldColorTrace.RecordAssetChange{T}"/>).
    /// A reader of the editor only: it writes no asset of its own.
    /// </summary>
    [InitializeOnLoad]
    sealed class WorldColorTraceEditor : WorldColorTrace.IEditorHooks
    {
        // per asset: dirty before the first repaint changed it, and its dirty count right after the last change
        static readonly Dictionary<Object, (bool wasDirty, int count)> State = new();

        static WorldColorTraceEditor()
        {
            WorldColorTrace.Editor = new WorldColorTraceEditor();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    WorldColorTrace.Stop();
                    Restore();
                    ColorGrade.Stop();   // uGUI's own default material and no grade in edit mode
                }
                else if (state == PlayModeStateChange.EnteredEditMode && WorldColorTrace.HasAssetChanges) Restore();
            };
            // Scripts recompiled while playing reload the domain: the record of what was painted would be lost, and the
            // painter is not installed again until the next play, so put everything back first.
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                if (EditorApplication.isPlaying && WorldColorTrace.HasAssetChanges) Restore();
            };
        }

        public bool IsAsset(Object o)
        {
            if (!EditorUtility.IsPersistent(o)) return false;
            if (!State.ContainsKey(o)) State[o] = (EditorUtility.IsDirty(o), 0);
            return true;
        }

        public bool IsPreview(Object o) =>
            o is Component c && c && !EditorUtility.IsPersistent(c) && EditorSceneManager.IsPreviewScene(c.gameObject.scene);

        public void Changed(Object asset)
        {
            if (State.TryGetValue(asset, out var s)) State[asset] = (s.wasDirty, EditorUtility.GetDirtyCount(asset));
        }

        internal static void Restore()
        {
            // which assets nothing else touched since the trace last changed them: read before the colours go back
            var untouched = new HashSet<Object>();
            foreach (var asset in WorldColorTrace.ChangedAssets)
                if (State.TryGetValue(asset, out var s) && !s.wasDirty && EditorUtility.GetDirtyCount(asset) == s.count)
                    untouched.Add(asset);
            WorldColorTrace.RestoreAssets((asset, allBack) =>
            {
                if (allBack && untouched.Contains(asset)) EditorUtility.ClearDirty(asset);
            });
            State.Clear();
        }
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
