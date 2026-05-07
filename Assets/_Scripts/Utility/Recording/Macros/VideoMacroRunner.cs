using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Utility.Recording
{
    /// <summary>
    /// Listens for the trigger keys defined in a <see cref="VideoMacroLibrarySO"/>
    /// and runs the macro's actions in order. Drop one onto a GameObject in the
    /// active scene (the Video Recording Tools window can spawn one for you).
    /// </summary>
    public class VideoMacroRunner : MonoBehaviour
    {
        [SerializeField] VideoMacroLibrarySO library;

        public VideoMacroLibrarySO Library
        {
            get => library;
            set => library = value;
        }

        void Update()
        {
            if (library == null) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (int i = 0; i < library.Macros.Count; i++)
            {
                var macro = library.Macros[i];
                if (macro == null || macro.TriggerKey == Key.None) continue;
                if (!keyboard[macro.TriggerKey].wasPressedThisFrame) continue;

                for (int j = 0; j < macro.Actions.Count; j++)
                    macro.Actions[j]?.Execute();
            }
        }
    }
}
