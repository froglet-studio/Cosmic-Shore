using UnityEditor;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Holds Unity's assembly-reload lock for the whole of play mode, so a script
    /// recompilation that finishes while the game is running can never trigger a domain
    /// reload MID-PLAY. A mid-play reload runs "Run managed callbacks" against a live
    /// scene — FMOD system teardown, Netcode transport, background threads — and is the
    /// most hang-prone reload the editor can attempt (see
    /// Docs/PERFORMANCE_OPTIMIZATION.md Task 10; the 2026-08-21 hang reports). With the
    /// lock held the refresh simply queues, and the reload runs at EnteredEditMode
    /// exactly as the "Recompile After Finished Playing" preference would do — but
    /// enforced project-wide instead of depending on each developer's editor prefs.
    ///
    /// The lock lives only in editor memory: a crash or kill while locked leaves nothing
    /// behind, and a reload cannot occur while the lock is held, so the balanced
    /// Lock/Unlock pair below cannot be split across domains.
    /// </summary>
    [InitializeOnLoad]
    static class PlayModeReloadGuard
    {
        static bool _locked;

        static PlayModeReloadGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                // Lock as the editor LEAVES edit mode, so a compile finishing in the
                // entry window cannot slip a reload into the transition either.
                case PlayModeStateChange.ExitingEditMode:
                    if (!_locked)
                    {
                        EditorApplication.LockReloadAssemblies();
                        _locked = true;
                    }
                    break;

                // EnteredEditMode also fires when a play-mode entry is aborted
                // (compile errors), so the pair always balances.
                case PlayModeStateChange.EnteredEditMode:
                    if (_locked)
                    {
                        EditorApplication.UnlockReloadAssemblies();
                        _locked = false;
                    }
                    break;
            }
        }
    }
}
