#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor;
using UnityEditor.Animations;
using System;

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
        /// The name of the serialized property for the salt currently in use by
        /// the utility. Can be changed by this code but not by the user.
        /// </summary>
        private readonly string SaltField= "salt";

        /// <summary>
        /// The name of the serialized property for the playable director
        /// in the data holder object, for use by Unity's property finder.
        /// </summary>
        private readonly string Director = "director";

        /// <summary>
        /// The name of the serialized property for the objects to track
        /// during the recording, as stored in the data holder object
        /// for use by Unity's property finder.
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
                SetupRecordingSystem();
                return holder;
            }
        }

        /// <summary>
        /// The name of the game object that will have the data holder as its component.
        /// This is used to find the object in question. That name should only be held
        /// by the object that contains the data holder.
        /// </summary>
        private static readonly string AnimationRecorderName = "Animation Recorder";

        /// <summary>
        /// A refernce to the data holder that unity can use to handle serialized properites.
        /// </summary>
        private static SerializedObject serializedObject;

        /// <summary>
        /// Whether the utility is in the process of recording.
        /// </summary>
        private static bool isRecording = false;

        /// <summary>
        /// When recording, time until the next snapshot. When this timer reaches 0, the utility
        /// takes a new snapshot.
        /// </summary>
        private static float timer = 0;

        /// <summary>
        /// Moment when the current recording started.
        /// </summary>
        private float animationStart = 0;

        /// <summary>
        /// Number for each recording of the same object.
        /// If object X is recorded 3 times in one session, add 0, 1, and 2 to each of its
        /// recording's temp files.
        /// </summary>
        private int recordingNumber = 0;

        /// <summary>
        /// A collection of every recorder, each one associated with a new game object to track.
        /// </summary>
        private readonly static List<GameObjectRecorder> Recorders = new();

        /// <summary>
        /// The default vertical space between elements in the GUI.
        /// </summary>
        private static readonly int LAYOUT_VERTICAL_GAP = 10;

        /// <summary>
        /// Format in which to convert the number of each recording assets to a string,
        /// with the right numbers of leading zeros.
        /// </summary>
        private static readonly string numberFormat = "D4";

        /// <summary>
        /// The salt used to keep each recording session distinct.
        ///
        /// <seealso href="https://en.wikipedia.org/wiki/Salt_(cryptography)">Definition of salt on wikipedia</seealso>
        /// </summary>
        private string salt;

        private readonly string defaultTimelinePath = "Assets/_Scripts/Utility/Recording/DefaultTimeline.playable";

        /// <summary>
        /// Creates the menu item "Window/Animation Recorder" and sets it to open the Animation Recorder
        /// window when activated.
        /// </summary>
        [MenuItem("Window/Animation Recorder")]
        public static void ShowWindow()
        {
            EditorWindow recorder = EditorWindow.GetWindow(typeof(AnimationRecorder), false, AnimationRecorderName);
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
        public void OnGUI()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(Director), new GUIContent("Playable Director Container"));
            GUI.enabled = (serializedObject.FindProperty(Director).objectReferenceValue != null) && !isRecording;
            GUILayout.BeginVertical();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(ObjectsToTrack), new GUIContent("Objects to track"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(RecordingDelay), new GUIContent("Delay between snapshots"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(timelineAsset), new GUIContent("Asset for this timeline"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(assetsPath), new GUIContent("Save path for recordings"));
            GUI.enabled = false;
            EditorGUILayout.PropertyField(serializedObject.FindProperty(SaltField), new GUIContent("Current salt"));
            serializedObject.ApplyModifiedProperties();
            GUILayout.EndVertical();

            GUILayout.Space(LAYOUT_VERTICAL_GAP);
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
            GUILayout.Space(LAYOUT_VERTICAL_GAP * 2);
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
        /// This update method works as a replacement for Coroutines, which are not available
        /// in the current context.
        /// </summary>
        public void Update()
        {
            float recordingDelay = 1;
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
        private void SetupRecording()
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
        private void TakeSnapshot()
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
        private void EndRecording()
        {
            string currentAssetsPath = serializedObject.FindProperty(assetsPath).stringValue;
            if (AssetDatabase.GetMainAssetTypeAtPath(currentAssetsPath) == null)
            {
                CreateAssetFolder(currentAssetsPath);
            }
            IEnumerator<GameObjectRecorder> recordersEnumerator = Recorders.GetEnumerator();
            while (recordersEnumerator.MoveNext())
            {
                GameObjectRecorder currentRecorder = recordersEnumerator.Current;
                GameObject currentGameObject = currentRecorder.root;
                AnimationClip animationClip = new();
                currentRecorder.SaveToClip(animationClip);
                AssetDatabase.CreateAsset(animationClip, GameObjectAssetPath(currentGameObject, recordingNumber));
            }
            recordingNumber++;
        }

        /// <summary>
        /// The full path of the current animaiton asset.
        ///
        /// A "new recording" happens when the user stops recording and starts again within one play session.
        /// </summary>
        /// <param name="gameObject">The GameObject to which the animation refers.</param>
        /// <param name="index">Which recording, if more than one in this session, in which this animation belongs.</param>
        /// <returns></returns>
        private string GameObjectAssetPath(GameObject gameObject, int index)
        {
            string currentAssetsPath = serializedObject.FindProperty(assetsPath).stringValue;
            string salt = serializedObject.FindProperty(SaltField).stringValue;
            string crn = index.ToString(numberFormat);
            return $"{currentAssetsPath}/{gameObject.name}.{crn}.{salt}.asset";
        }

        /// <summary>
        /// Create the container folder for the saved animations.
        /// </summary>
        /// <param name="currentAssetsPath">The full path of the folder to create.</param>
        private void CreateAssetFolder(string currentAssetsPath)
        {
            Stack<string> splitPath = new(currentAssetsPath.Split('/'));
            string last = splitPath.Pop();
            string parentPath = string.Join('/', splitPath);

            if (!currentAssetsPath.StartsWith("Assets/"))
            {
                Debug.LogError("The set asset path must start with \"Assets/\".");
            }
            AssetDatabase.CreateFolder(parentPath, last);

        }

        /// <summary>
        /// Run when the editor returns to edit mode. Adds new items to the timeline based
        /// on the saved data.
        /// </summary>
        private void BuildTimelineData()
        {
            PlayableDirector director = serializedObject.FindProperty(Director).objectReferenceValue as PlayableDirector;
            TimelineAsset currentTimelineAsset = serializedObject.FindProperty(timelineAsset).objectReferenceValue as TimelineAsset;
            string currentAssetsPath = serializedObject.FindProperty(assetsPath).stringValue;
            for (int currentRecordingNumber = 0; currentRecordingNumber < recordingNumber; currentRecordingNumber++)
            {
                string crn = currentRecordingNumber.ToString(numberFormat);
                IEnumerator<GameObjectRecorder> recordersEnumerator = Recorders.GetEnumerator();
                while (recordersEnumerator.MoveNext())
                {
                    GameObjectRecorder gameObjectRecorder = recordersEnumerator.Current;
                    GameObject currentGameObject = gameObjectRecorder.root;
                    Animator currentAnimator = currentGameObject.GetComponent<Animator>();
                    AnimationTrack animationTrack = currentTimelineAsset.CreateTrack<AnimationTrack>($"{currentGameObject.name} #{crn}");
                    string newAssetPath = GameObjectAssetPath(currentGameObject, currentRecordingNumber);
                    Debug.Log($"new asset path: {newAssetPath}");
                    AnimationClip animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newAssetPath);
                    animationTrack.CreateClip(animationClip);
                    director.SetGenericBinding(animationTrack, currentAnimator);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        /// <summary>
        /// Helper method that returns whether there are enough settings available to start recording.
        /// </summary>
        /// <returns>Whether there are enough settings available to start recording.</returns>
        private bool ReadyToRecord()
        {
            IEnumerator animators = serializedObject.FindProperty(ObjectsToTrack).GetEnumerator();
            string currentAssetsPath = serializedObject.FindProperty(assetsPath).stringValue;
            TimelineAsset currentTimelineAsset = serializedObject.FindProperty(timelineAsset).objectReferenceValue as TimelineAsset;
            float recordingDelay = serializedObject.FindProperty(RecordingDelay).floatValue;
            return
                animators.MoveNext() != false  &&
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
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                recordingNumber = 0;
                PlayableDirector director = serializedObject.FindProperty(Director).objectReferenceValue as PlayableDirector;
                GameObject currentGameObject = director.gameObject;
                DataHolder sceneData = currentGameObject.GetComponent<DataHolder>();
                sceneData.salt = GenerateSalt();
            }
            if (state == PlayModeStateChange.ExitingPlayMode && isRecording)
            {
                EndRecording();
            }
        }

        private string GenerateSalt()
        {
            int salt = new System.Random().Next();
            return Convert.ToBase64String(BitConverter.GetBytes(salt)).TrimEnd('=');

        }

        private void SetupRecordingSystem()
        {
            GameObject gameObject = GameObject.Find(AnimationRecorderName);
            if (gameObject == null)
            {
                Debug.LogWarning($"There needs to be an object in the scene called \"{AnimationRecorderName}\". One will be added now.");
                gameObject = new GameObject(AnimationRecorderName);
            }
            DataHolder sceneData = gameObject.GetComponent<DataHolder>();
            if (sceneData != null)
            {
                holder = sceneData;
            }
            else
            {
                sceneData = gameObject.AddComponent<DataHolder>();
                holder = sceneData;
            }
            if (gameObject.GetComponent<PlayableDirector>() == null)
            {
                PlayableDirector director = gameObject.AddComponent<PlayableDirector>();
                string salt = GenerateSalt();
                sceneData.salt = salt;
                string newAssetPath = $"{sceneData.assetsPath}/NewTimeline.{salt}.playable";
                AssetDatabase.CopyAsset(defaultTimelinePath, newAssetPath);
                TimelineAsset newTimelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(newAssetPath);
                holder.director = director;
                holder.timelineAsset = newTimelineAsset;
                director.playableAsset = newTimelineAsset;
            }
        }
    }
}
#endif