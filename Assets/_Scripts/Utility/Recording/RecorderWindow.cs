#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor;
using UnityEditor.Animations;

namespace CosmicShore.Utility
{
    /// <summary>
    /// A utility for tracking the transform of specific objects and save them as
    /// animation clips in a given timeline.
    /// </summary>
    [InitializeOnLoadAttribute]
    public class AnimationRecorder : EditorWindow
    {
        /// <summary>
        /// The name of the serialized property for the track name in the data holder
        /// object, for use by by Unity's property finder.
        /// </summary>
        private readonly string TrackName = "trackName";

        /// <summary>
        /// The name of the serialized property for the playable director
        /// in the data holder object, for use by Unity's property finder.
        /// </summary>
        private readonly string Director = "director";

        /// <summary>
        /// The name of the serialized property for the objects to track
        /// during the recording, as stored in the data holder object
        // for use by Unity's property finder.
        /// </summary>
        private readonly string ObjectsToTrack = "objectsToTrack";

        /// <summary>
        /// The name of the serialized property for the delay between recording
        /// snapshots, as stored in the data holder object, for use by Unity's property finder.
        /// </summary>
        private readonly string RecordingDelay = "recordingDelay";

        /// <summary>
        /// The name of the serialized property that refrences the timeline asset,
        /// as stored in the data holder object, for use by Unity's property finder.
        /// </summary>
        private readonly string timelineAsset = "timelineAsset";

        /// <summary>
        /// The name of the serialized property for the path to the assets used by this
        /// recorder, as stored in the data holder object, for use by Unity's property finder.
        /// </summary>
        private readonly string assetsPath = "assetsPath";

        /// <summary>
        /// Whether the callbacks for when the editor changes state is registered.
        /// It should only be necessary to register it once until the editor is closed.
        /// </summary>
        private static bool registeredPlayCallbacks = false;

        /// <summary>
        /// A refrence to the object that contains the data holder for the present session.
        /// </summary>
        private DataHolder holder;

        /// <summary>
        /// A property that will either return a refrence to a holder, or do its best to instantiate
        /// one and then return it.
        /// </summary>
        /// <value>holder</value>
        private DataHolder Holder
        {
            get
            {
                if (holder != null)
                {
                    return holder;
                }
                GameObject gameObject = GameObject.Find(ManagerName);
                if (gameObject == null)
                {
                    Debug.LogError($"There needs to be an object in the scene called \"{ManagerName}\".");
                    return null;
                }
                DataHolder sceneData = gameObject.GetComponent<DataHolder>();
                if (sceneData != null)
                {
                    return sceneData;
                }
                sceneData = gameObject.AddComponent<DataHolder>();
                return sceneData;
            }
        }

        /// <summary>
        /// The name of the game object that will have the data holder as its component.
        /// This is used to find the object in question. That name should only be held
        /// by the object that contains the data holder.
        /// </summary>
        private static readonly string ManagerName = "Animation Manager";

        /// <summary>
        /// A refernce to the data holder that unity can use to handle serialized properites.
        /// </summary>
        private static SerializedObject serializedObject;

        /// <summary>
        /// Whether the utility is in the process of recording.
        /// </summary>
        private static bool isRecording = false;

        /// <summary>
        /// When recording, time until the next snapshot. When this timer reacher 0, the utility
        /// takes a new snapshot.
        /// </summary>
        private static float timer = 0;

        /// <summary>
        /// Moment when the current recording started.
        /// </summary>
        private float animationStart = 0;

        /// <summary>
        /// A collection of every recorder, each one associated with a new game object to track.
        /// </summary>
        private static List<GameObjectRecorder> Recorders = new();

        /// <summary>
        /// Creates the menu item "Window/Animation Recorder" and sets it to open the Animation Recorder
        /// window when uctivated.
        /// </summary>
        [MenuItem("Window/Animation Recorder")]
        public static void ShowWindow()
        {
            EditorWindow recorder = EditorWindow.GetWindow(typeof(AnimationRecorder), false, "Animation Recorder");
        }

        /// <summary>
        /// Called when the window opens up. Sets up the value for <c ref="serializedObject" />
        /// and sets up the callbacks that this utitily requires in order to run properly.
        /// </summary>
        public void OnEnable()
        {
            serializedObject = new SerializedObject(Holder) ?? serializedObject;
            if (!registeredPlayCallbacks)
            {
                EditorApplication.playModeStateChanged += OnPlaymodeChangeState;
                registeredPlayCallbacks = true;
            }
        }

        /// <summary>
        /// Called every frame for the utility's window. Displays  the interface.
        /// </summary>
        void OnGUI()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(Director), new GUIContent("Animation Manager"));
            GUI.enabled = (serializedObject.FindProperty(Director).objectReferenceValue != null) && !isRecording;
            GUILayout.BeginVertical();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(ObjectsToTrack), new GUIContent("Objects to track"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(RecordingDelay), new GUIContent("Delay between snapshots"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(TrackName), new GUIContent("Name of recording"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(timelineAsset), new GUIContent("Asset for this timeline"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(assetsPath), new GUIContent("Save path for recordings"));
            serializedObject.ApplyModifiedProperties();
            GUILayout.EndVertical();

            GUILayout.Space(10);
            if (!ReadyToRecord())
            {
                GUILayout.Label("Not ready: no setting can be 0 or null");

            }
            if (!Application.isPlaying)
            {
                GUILayout.Label("Not available: recording is only possible in Play mode.");
            }
            GUI.enabled = ReadyToRecord() && !isRecording && EditorApplication.isPlaying;
            float recordingDelay = serializedObject.FindProperty("recordingDelay").floatValue;
            if (GUILayout.Button("Start Recording", EditorStyles.miniButton))
            {
                isRecording = true;
                SetupRecording();
                timer = recordingDelay;
                animationStart = Time.time;
            }
            GUILayout.Space(10);
            GUILayout.Space(10);
            GUI.enabled = isRecording;
            if (GUILayout.Button("Stop Recording", EditorStyles.miniButton))
            {
                isRecording = false;
                EndRecording();
            }
            // Stop recording when play stops.
            isRecording = isRecording && Application.isPlaying;
        }

        /// <summary>
        /// This update method words as a replacement for Coroutines, which are not available
        /// in the current context.
        /// </summary>
        public void Update()
        {
            float recordingDelay = 1; // serializedObject.FindProperty(RecordingDelay).floatValue;
            if (!isRecording)
            {
                return;
            }
            if (timer <= 0)
            {
                TakeSnapshot();
                timer = recordingDelay;
                return;
            }
            timer -= Time.deltaTime;
        }

        /// <summary>
        /// Prepares the utility for a new recording.
        /// </summary>
        void SetupRecording()
        {
            Recorders.Clear();
            IEnumerator objectsToTrackEnumerator = serializedObject.FindProperty(ObjectsToTrack).GetEnumerator();
            while (objectsToTrackEnumerator.MoveNext())
            {
                SerializedProperty serializedObjectToTrack = (SerializedProperty) objectsToTrackEnumerator.Current;
                Animator currentAnimator = serializedObjectToTrack.objectReferenceValue as Animator;
                GameObject currentGameObject = currentAnimator.gameObject;
                GameObjectRecorder gameObjectRecorder = new(currentGameObject);
                gameObjectRecorder.BindComponentsOfType<Transform>(currentGameObject, true);
                Recorders.Add(gameObjectRecorder);
            }
        }

        /// <summary>
        /// Takes a snapshot for each currently tracked game objects.
        /// </summary>
        void TakeSnapshot()
        {
            float recordingDelay = serializedObject.FindProperty(RecordingDelay).floatValue;
            IEnumerator<GameObjectRecorder> recordersEnumerator = Recorders.GetEnumerator();
            while (recordersEnumerator.MoveNext())
            {
                GameObjectRecorder currentRecorder = recordersEnumerator.Current;
                currentRecorder.TakeSnapshot(recordingDelay);
            }
        }

        /// <summary>
        /// Called when the user stops the animation. Saves all the data necessary to add the new entries
        /// to the timeline.
        /// </summary>
        void EndRecording()
        {
            string currentAssetsPath = serializedObject.FindProperty(assetsPath).stringValue;
            IEnumerator<GameObjectRecorder> recordersEnumerator = Recorders.GetEnumerator();
            while (recordersEnumerator.MoveNext())
            {
                GameObjectRecorder currentRecorder = recordersEnumerator.Current;
                GameObject currentGameObject = currentRecorder.root;
                AnimationClip animationClip = new();
                currentRecorder.SaveToClip(animationClip);
                AssetDatabase.CreateAsset(animationClip, $"{currentAssetsPath}/{currentGameObject.name}.asset");
            }
        }

        /// <summary>
        /// Ren when the editor returns to edit mode. Adds new items to the timeline based
        /// on the saved data.
        /// </summary>
        void BuildTimelineData()
        {
            PlayableDirector director = serializedObject.FindProperty(Director).objectReferenceValue as PlayableDirector;
            TimelineAsset currentTimelineAsset = serializedObject.FindProperty(timelineAsset).objectReferenceValue as TimelineAsset;
            string currentAssetsPath = serializedObject.FindProperty(assetsPath).stringValue;
            IEnumerator<GameObjectRecorder> recordersEnumerator = Recorders.GetEnumerator();
            string trackName = serializedObject.FindProperty(TrackName).stringValue;
            while (recordersEnumerator.MoveNext())
            {
                GameObjectRecorder gameObjectRecorder = recordersEnumerator.Current;
                GameObject currentGameObject = gameObjectRecorder.root;
                Animator currentAnimator = currentGameObject.GetComponent<Animator>();
                AnimationTrack animationTrack = currentTimelineAsset.CreateTrack<AnimationTrack>($"{trackName} :: {currentGameObject.name}");
                AnimationClip animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{currentAssetsPath}/{currentGameObject.name}.asset");
                animationTrack.CreateClip(animationClip);
                director.SetGenericBinding(animationTrack, currentAnimator);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// Helper method that returns whether there are enough settings available to start recording.
        /// </summary>
        /// <returns>Whether there are enough settings available to start recording.</returns>
        private bool ReadyToRecord()
        {
            IEnumerator animators = serializedObject.FindProperty(ObjectsToTrack).GetEnumerator();
            string trackName = serializedObject.FindProperty(TrackName).stringValue;
            string currentAssetsPath = serializedObject.FindProperty(assetsPath).stringValue;
            TimelineAsset currentTimelineAsset = serializedObject.FindProperty(timelineAsset).objectReferenceValue as TimelineAsset;
            float recordingDelay = serializedObject.FindProperty(RecordingDelay).floatValue;
            return
                animators.MoveNext() != false  &&
                trackName != "" &&
                recordingDelay > 0 &&
                currentAssetsPath != "" &&
                currentTimelineAsset != null;
        }

        /// <summary>
        /// Callback for when the the playmode changes.
        /// </summary>
        /// <param name="state">The current playmode.</param>
        private void OnPlaymodeChangeState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                BuildTimelineData();
            }
            if (state == PlayModeStateChange.ExitingPlayMode && isRecording)
            {
                EndRecording();
            }
        }
    }
}
#endif