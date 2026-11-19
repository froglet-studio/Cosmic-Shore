#if UNITY_EDITOR
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Editor only, a test aid for two-player runs (Yash, 2026-11-17): a GUEST whose pilot touches nothing for
    /// <see cref="IdleSeconds"/> of a running turn has its hull handed to the AI (<see cref="PilotControl.TrySetAIFlies"/>,
    /// the Third Eye's own "AI" toggle), in every mode, and gets it back the moment a pad or the keyboard is used.
    /// Before this the second editor's hull sat at its spawn in every match unless someone held its controller; the
    /// Maelstrom runs only looked right because their test driver did the hand-over itself.
    ///
    /// <para>Guests only: the host's hull is whoever is testing. Never in a build (the whole file is editor only), and
    /// off with <b>FrogletTools &gt; Multiplayer &gt; Idle Guest Flies On AI</b> (an EditorPref, so it is per machine).
    /// It adds nothing to the game: who flies a hull is decided by <see cref="PilotControl"/>.</para>
    /// </summary>
    public sealed class IdleGuestAutopilot : MonoBehaviour
    {
        const string PrefKey = "CosmicShore.Multiplayer.IdleGuestFliesOnAI";
        const string MenuPath = "FrogletTools/Multiplayer/Idle Guest Flies On AI";
        const float IdleSeconds = 4f;
        const float StickDeadZoneSqr = 0.06f;
        const float TriggerDeadZone = 0.2f;

        static GameDataSO s_gameData;

        float _idleSince = -1f;
        bool _handedOver;
        string _lastRefusal;

        static bool Enabled => EditorPrefs.GetBool(PrefKey, true);

        [MenuItem(MenuPath)]
        static void Toggle() => EditorPrefs.SetBool(PrefKey, !Enabled);

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject(nameof(IdleGuestAutopilot));
            DontDestroyOnLoad(go);
            go.AddComponent<IdleGuestAutopilot>();
        }

        void Update()
        {
            var you = IdleCandidate(out var gameData);
            if (!you)
            {
                _idleSince = -1f;
                return;
            }

            float now = Time.unscaledTime;
            if (_idleSince < 0f)
            {
                _idleSince = now;
                return;
            }

            if (AnyInput())
            {
                _idleSince = now;
                if (_handedOver && PilotControl.IsHeldByAI(you)) PilotControl.TrySetAIFlies(you, false, gameData, out _);
                _handedOver = false;
                return;
            }

            if (now - _idleSince < IdleSeconds || PilotControl.IsHeldByAI(you)) return;

            if (PilotControl.TrySetAIFlies(you, true, gameData, out var refusal))
            {
                _handedOver = true;
                _lastRefusal = null;
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                    $"[IdleGuestAutopilot] No input for {IdleSeconds:0} s: the AI flies {you.Name}'s hull until a pad or key is used.");
            }
            else
            {
                // Ask again after another idle window, and say why once per reason.
                _idleSince = now;
                if (refusal != _lastRefusal)
                {
                    _lastRefusal = refusal;
                    CSDebug.LogWarning($"[IdleGuestAutopilot] The idle guest hull was not handed to the AI: {refusal}.");
                }
            }
        }

        /// <summary>The local human of a guest in a running turn, or null when this aid has nothing to do.</summary>
        static Player IdleCandidate(out GameDataSO gameData)
        {
            gameData = null;
            if (!Enabled) return null;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient || nm.IsServer) return null;
            gameData = GameData();
            if (gameData == null || !gameData.IsTurnRunning) return null;
            if (gameData.LocalPlayer is not Player you || !you || you.NetIsAI.Value) return null;
            return you.Vessel is Object hull && hull ? you : null;
        }

        static GameDataSO GameData()
        {
            if (s_gameData) return s_gameData;
            var found = Resources.FindObjectsOfTypeAll<GameDataSO>();
            return s_gameData = found.Length > 0 ? found[0] : null;
        }

        /// <summary>Read from the devices, not the game's input status: that is paused while the AI holds the hull.</summary>
        static bool AnyInput()
        {
            var pad = Gamepad.current;
            if (pad != null &&
                (pad.leftStick.ReadValue().sqrMagnitude > StickDeadZoneSqr ||
                 pad.rightStick.ReadValue().sqrMagnitude > StickDeadZoneSqr ||
                 pad.leftTrigger.ReadValue() > TriggerDeadZone ||
                 pad.rightTrigger.ReadValue() > TriggerDeadZone ||
                 pad.buttonSouth.isPressed || pad.buttonEast.isPressed ||
                 pad.buttonWest.isPressed || pad.buttonNorth.isPressed ||
                 pad.leftShoulder.isPressed || pad.rightShoulder.isPressed))
                return true;
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.anyKey.isPressed;
        }
    }
}
#endif
