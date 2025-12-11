#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using static UnityEditor.EditorGUILayout;


namespace CosmicShore.Utility.Recording
{
    using ARP = AnimationRecorderProcess;

    /// <summary>
    /// A utility for tracking the transform of specific objects and save them as
    /// animation clips in a given timeline.
    /// </summary>
    [InitializeOnLoadAttribute]
    public class AnimationRecorderWindow : EditorWindow
    {
        private Func<string, GameObject> FindOrCreateGameObject => AnimationRecorderUtilities.FindOrCreateGameObject;

        private static GameObject _recordingSystemGameObject;
        
        /// <summary>
        /// The default vertical space between elements in the GUI.
        /// </summary>
        private const int LayoutVerticalGap = 10;

        /// <summary>
        /// Where the timeline object should be stored, unless overridden.
        /// </summary>
        private const string DefaultTimelinePath = "Assets/_Scripts/Utility/Recording/DefaultTimeline.playable";

        /// <summary>
        /// The actual controller logic of the recording system, as opposed to this class which is only its view.
        /// </summary>
        private ARP _recorderProcess;
        
        /// <summary>
        /// A reference to the data about the objects being recorded. The original is in the <
        /// </summary>
        private static SerializedObject _serializedObject;

        /// <summary>
        /// Creates the menu item "Window/Animation Recorder" and sets it to open the Animation Recorder
        /// window when activated.
        /// </summary>
        [MenuItem("Window/Animation Recorder")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(AnimationRecorderWindow), false, ARP.AnimationRecorderName);
        }

        /// <summary>
        /// Called when the window opens up. Sets up the value for <c ref="_serializedObject" />
        /// and sets up the callbacks that this utility requires in order to run properly.
        /// </summary>
        public void OnEnable()
        {
            // The null-coalescing operator does not work with Unity game objects, apparently.
            _recordingSystemGameObject = GameObject.Find(ARP.AnimationRecorderName);
            if (_recordingSystemGameObject == null)
            {
                _recordingSystemGameObject = new GameObject(ARP.AnimationRecorderName);
            }
            InstantiateRecorderProcess();
        }

        private void InstantiateRecorderProcess()
        {
            _recorderProcess = _recordingSystemGameObject.GetOrAddComponent<ARP>();
            _recorderProcess.Initialize();
            _serializedObject = _recorderProcess.RecorderSerializedObject;
        }

        /// <summary>
        /// Called every frame for the utility's window. Displays  the interface.
        /// </summary>
        public void OnGUI()
        {
            try
            {
                _OnGUI();
            }
            catch (NullReferenceException e)
            {
                Debug.LogWarning($"Something is null that shouldn't be:\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// Called every frame for the utility's window. Displays  the interface.
        /// </summary>
        private void _OnGUI()
        {
            if (_recorderProcess == null)
            {
                InstantiateRecorderProcess();
            }
            else if (_serializedObject == null)
            {
                _recorderProcess.Initialize();
                _serializedObject = _recorderProcess.RecorderSerializedObject;
            }
            GUILayout.BeginVertical("box");
            try 
            {
                PropertyField(_serializedObject.FindProperty(ARP.Director),
                    new GUIContent("Playable  Container"));
                GUI.enabled = (_serializedObject.FindProperty(ARP.Director).objectReferenceValue != null) 
                              && !_recorderProcess.IsRecording;
                PropertyField(_serializedObject.FindProperty(ARP.ObjectsToTrack),
                    new GUIContent("Objects to track"));
                PropertyField(_serializedObject.FindProperty(ARP.RecordingDelay),
                    new GUIContent("Delay between snapshots"));
                PropertyField(_serializedObject.FindProperty(ARP.TrackName),
                    new GUIContent("Name of recording"));
                PropertyField(_serializedObject.FindProperty(ARP.TimelineAsset),
                    new GUIContent("Asset for this timeline"));
                PropertyField(_serializedObject.FindProperty(ARP.AssetsParentPath),
                    new GUIContent("Parent of data directory"));
                PropertyField(_serializedObject.FindProperty(ARP.AssetsDirectoryName),
                    new GUIContent("Name of data directory"));
                GUI.enabled = false;
                PropertyField(_serializedObject.FindProperty(ARP.SaltField), new GUIContent("Current salt"));
                _serializedObject.ApplyModifiedProperties();

                Space(LayoutVerticalGap);
                if (!_recorderProcess.ReadyToRecord())
                {
                    GUILayout.Label("Not ready: no setting can be 0 or null");
                }

                if (!Application.isPlaying)
                {
                    GUILayout.Label("Not available: recording is only possible in Play mode.");
                }

                GUI.enabled = _recorderProcess.ReadyToRecord() && !_recorderProcess.IsRecording && EditorApplication.isPlaying;
                var recordingDelay = _serializedObject.FindProperty("recordingDelay").floatValue;
                GUI.enabled = true;
            }
            finally
            {
                GUILayout.EndVertical();
            }
            if (GUILayout.Button("Start Recording", EditorStyles.miniButton))
            {
                _recorderProcess.StartRecording();
            }

            Space(LayoutVerticalGap * 2);
            GUI.enabled = _recorderProcess.IsRecording;
            if (GUILayout.Button("Stop Recording", EditorStyles.miniButton))
            {
                _recorderProcess.EndRecording();
            }

            // Stop recording when play stops.
            // ARP.IsRecording = ARP.IsRecording && Application.isPlaying;
        }
    }
}
#endif