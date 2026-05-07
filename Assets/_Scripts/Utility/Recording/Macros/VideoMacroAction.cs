using System;
using UnityEngine;

namespace CosmicShore.Utility.Recording
{
    /// <summary>
    /// Base class for any action that can run inside a <see cref="VideoMacro"/>.
    /// Subclasses must be <c>[Serializable]</c> so they can be stored polymorphically
    /// in <see cref="VideoMacro.Actions"/> via <c>[SerializeReference]</c>.
    /// </summary>
    [Serializable]
    public abstract class VideoMacroAction
    {
        public virtual string DisplayName => GetType().Name;

        public abstract void Execute();

#if UNITY_EDITOR
        /// <summary>
        /// Draws this action's editable fields inside the Video Recording Tools window.
        /// Override to expose action-specific configuration. <paramref name="owner"/>
        /// is the asset that owns this action (used for <c>EditorUtility.SetDirty</c>).
        /// </summary>
        public virtual void DrawEditor(UnityEngine.Object owner) { }
#endif
    }
}
