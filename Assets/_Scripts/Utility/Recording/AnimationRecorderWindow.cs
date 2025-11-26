#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor;
using static UnityEditor.EditorGUILayout;

namespace CosmicShore.Utility
{
    /// <summary>
    /// A utility for tracking the transform of specific objects and save them as
    /// animation clips in a given timeline.
    /// </summary>
    [InitializeOnLoadAttribute]
    public class AnimationRecorderWindow : EditorWindow
    {
        /// <summary>
        /// The name of the serialized property for the track name in the data holder
        /// object, for use by by Unity's property finder.
        /// </summary>
        private const string TrackName = "trackName";

        /// <summary>
        /// The name of the serialized property for the salt currently in use by
        /// the utility. Can be changed by this code but not by the user.
        /// </summary>
        private const string SaltField = "salt";

        /// <summary>
        /// The name of the serialized property for the playable director
        /// in the data holder object, for use by Unity's property finder.
        /// </summary>
        private const string Director = "director";

        /// <summary>
        /// The name of the serialized property for the objects to track
        /// during the recording, as stored in the data holder object
        /// for use by Unity's property finder.
        /// </summary>
        private const string ObjectsToTrack = "objectsToTrack";

        /// <summary>
        /// The name of the serialized property for the delay between recording
        /// snapshots, as stored in the data holder object, for use by Unity's property finder.
        /// </summary>
        private const string RecordingDelay = "recordingDelay";

        /// <summary>
        /// The name of the serialized property that refrences the timeline asset,
        /// as stored in the data holder object, for use by Unity's property finder.
        /// </summary>
        private const string TimelineAsset = "timelineAsset";

        /// <summary>
        /// The name of the serialized property for the path of the parent of the folder where the assets used by this
        /// recorder as stored, for use by Unity's property finder.
        /// </summary>
        private const string AssetsParentPath = "assetsParentPath";

        /// <summary>
        /// The name of the serialized property for the name name of the folder where the assets used by this
        /// recorder are stored, for use by Unity's property finder.
        /// </summary>
        private const string AssetsDirectoryName = "assetsDirectoryName";

        /// <summary>
        /// Whether the callbacks for when the editor changes state is registered.
        /// It should only be necessary to register it once until the editor is closed.
        /// </summary>
        private static bool _registeredPlayCallbacks;

        private static GameObject _recordingSystemGameObject;

        /// <summary>
        /// A property that will either return a reference to a holder, or do its best to instantiate
        /// one and then return it.
        /// </summary>
        /// <value>holder</value>
        private RecordingDataHolder Holder { get; set; }

        /// <summary>
        /// The name of the game object that will have the data holder as its component.
        /// This is used to find the object in question. That name should only be held
        /// by the object that contains the data holder.
        /// </summary>
        private const string AnimationRecorderName = "Animation Recorder";

        /// <summary>
        /// A reference to the data holder that unity can use to handle serialized properties.
        /// </summary>
        private static SerializedObject _serializedObject;

        /// <summary>
        /// Whether the utility is in the process of recording.
        /// </summary>
        private static bool _isRecording;

        /// <summary>
        /// When recording, time until the next snapshot. When this _timer reaches 0, the utility
        /// takes a new snapshot.
        /// </summary>
        private static float _timer;

        /// <summary>
        /// Moment when the current recording started.
        /// </summary>
        private float animationStart;

        /// <summary>
        /// Number for each recording of the same object.
        /// If object X is recorded 3 times in one session, add 0, 1, and 2 to each of its
        /// recording's temp files.
        /// </summary>
        private int recordingNumber;

        /// <summary>
        /// The salt used to keep each recording session distinct.
        ///
        /// <seealso href="https://en.wikipedia.org/wiki/Salt_(cryptography)">Definition of salt on wikipedia</seealso>
        /// </summary>
        private string salt;

        /// <summary>
        /// A collection of every recorder, each one associated with a new game object to track.
        /// </summary>
        private static readonly List<GameObjectRecorder> Recorders = new();

        /// <summary>
        /// The default vertical space between elements in the GUI.
        /// </summary>
        private const int LayoutVerticalGap = 10;

        /// <summary>
        /// Format in which to convert the number of each recording assets to a string,
        /// with the right numbers of leading zeros.
        /// </summary>
        private const string NumberFormat = "D4";

        /// <summary>
        /// Where the timeline object should be stored, unless overridden.
        /// </summary>
        private const string DefaultTimelinePath = "Assets/_Scripts/Utility/Recording/DefaultTimeline.playable";

        /// <summary>
        /// Creates the menu item "Window/Animation Recorder" and sets it to open the Animation Recorder
        /// window when activated.
        /// </summary>
        [MenuItem("Window/Animation Recorder")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(AnimationRecorderWindow), false, AnimationRecorderName);
        }

        /// <summary>
        /// Called when the window opens up. Sets up the value for <c ref="_serializedObject" />
        /// and sets up the callbacks that this utility requires in order to run properly.
        /// </summary>
        public void OnEnable()
        {
            _recordingSystemGameObject = new GameObject("Recording system");
            Holder = _recordingSystemGameObject.AddComponent<RecordingDataHolder>();
            _serializedObject ??= new SerializedObject(Holder);
            if (_registeredPlayCallbacks)
            {
                return;
            }

            EditorApplication.playModeStateChanged += OnPlaymodeChangeState;
            _registeredPlayCallbacks = true;
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
            GUILayout.BeginVertical("box");
            try 
            {
                PropertyField(_serializedObject.FindProperty(Director),
                    new GUIContent("Playable Director Container"));
                GUI.enabled = (_serializedObject.FindProperty(Director).objectReferenceValue != null) && !_isRecording;
                PropertyField(_serializedObject.FindProperty(ObjectsToTrack),
                    new GUIContent("Objects to track"));
                PropertyField(_serializedObject.FindProperty(RecordingDelay),
                    new GUIContent("Delay between snapshots"));
                PropertyField(_serializedObject.FindProperty(TrackName),
                    new GUIContent("Name of recording"));
                PropertyField(_serializedObject.FindProperty(TimelineAsset),
                    new GUIContent("Asset for this timeline"));
                PropertyField(_serializedObject.FindProperty(AssetsParentPath),
                    new GUIContent("Parent of data directory"));
                PropertyField(_serializedObject.FindProperty(AssetsDirectoryName),
                    new GUIContent("Name of data directory"));
                GUI.enabled = false;
                PropertyField(_serializedObject.FindProperty(SaltField), new GUIContent("Current salt"));
                _serializedObject.ApplyModifiedProperties();

                Space(LayoutVerticalGap);
                if (!ReadyToRecord())
                {
                    GUILayout.Label("Not ready: no setting can be 0 or null");
                }

                if (!Application.isPlaying)
                {
                    GUILayout.Label("Not available: recording is only possible in Play mode.");
                }

                GUI.enabled = ReadyToRecord() && !_isRecording && EditorApplication.isPlaying;
                var recordingDelay = _serializedObject.FindProperty("recordingDelay").floatValue;
                GUI.enabled = true;
        }
        finally

        {
                GUILayout.EndVertical();
            }
            if (GUILayout.Button("Start Recording", EditorStyles.miniButton))
            {
                _isRecording = true;
                SetupRecording();
                var recordingDelay = _serializedObject.FindProperty("recordingDelay").floatValue;
                _timer = recordingDelay;
                animationStart = Time.time;
            }

            Space(LayoutVerticalGap * 2);
            GUI.enabled = _isRecording;
            if (GUILayout.Button("Stop Recording", EditorStyles.miniButton))
            {
                _isRecording = false;
                EndRecording();
            }

            // Stop recording when play stops.
            _isRecording = _isRecording && Application.isPlaying;
        }

        /// <summary>
        /// This update method works as a replacement for Coroutines, which are not available
        /// in the current context.
        /// </summary>
        public void Update()
        {
            var recordingDelay = _serializedObject.FindProperty("recordingDelay").floatValue;
            if (!_isRecording)
            {
                return;
            }

            if (_timer <= 0)
            {
                TakeSnapshot();
                _timer = recordingDelay;
                return;
            }

            _timer -= Time.deltaTime;
        }

        /// <summary>
        /// Prepares the utility for a new recording.
        /// </summary>
        private void SetupRecording()
        {
            Recorders.Clear();
            var objectsToTrackEnumerator = _serializedObject.FindProperty(ObjectsToTrack).GetEnumerator();
            using var objectsToTrackEnumerator1 = objectsToTrackEnumerator as IDisposable;
            while (objectsToTrackEnumerator.MoveNext())
            {
                var serializedObjectToTrack = (SerializedProperty)objectsToTrackEnumerator.Current;
                var currentAnimator = serializedObjectToTrack?.objectReferenceValue as Animator;
                var currentGameObject = currentAnimator?.gameObject;
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
            float recordingDelay = _serializedObject.FindProperty(RecordingDelay).floatValue;
            IEnumerator<GameObjectRecorder> recordersEnumerator = Recorders.GetEnumerator();
            while (recordersEnumerator.MoveNext())
            {
                GameObjectRecorder currentRecorder = recordersEnumerator.Current;
                currentRecorder?.TakeSnapshot(recordingDelay);
            }
        }

        /// <summary>
        /// Called when the user stops the animation. Saves all the data necessary to add the new entries
        /// to the timeline.
        /// </summary>
        private static void EndRecording()
        {
            var currentAssetsParent = _serializedObject.FindProperty(AssetsParentPath).stringValue;
            var currentAssetsFolderName = _serializedObject.FindProperty(AssetsDirectoryName).stringValue;
            var assetsFolderFullPath = Path.Combine(currentAssetsParent, currentAssetsFolderName);
            if (AssetDatabase.GetMainAssetTypeAtPath(assetsFolderFullPath) == null)
            {
                AssetDatabase.CreateFolder(currentAssetsParent, currentAssetsFolderName);
            }

            IEnumerator<GameObjectRecorder> recordersEnumerator = Recorders.GetEnumerator();
            while (recordersEnumerator.MoveNext())
            {
                var currentRecorder = recordersEnumerator.Current;
                var currentGameObject = currentRecorder?.root;
                if (!currentRecorder || !currentGameObject)
                {
                    Debug.LogWarning("Something in the recording is null that shouldn't be.");
                    return;
                }

                AnimationClip animationClip = new();
                currentRecorder.SaveToClip(animationClip);
                AssetDatabase.CreateAsset(animationClip, Path.Combine(assetsFolderFullPath,
                    $"{currentGameObject.name}.asset"));
            }
        }

        /// <summary>
        /// The full path of the current animation asset.
        ///
        /// A "new recording" happens when the user stops recording and starts again within one play session.
        /// </summary>
        /// <param name="gameObject">The GameObject to which the animation refers.</param>
        /// <param name="index">Which recording, if more than one in this session, in which this animation belongs.</param>
        /// <returns></returns>
        private string GameObjectAssetPath(GameObject gameObject, int index)
        {
            var currentAssetsParentPath = _serializedObject.FindProperty(AssetsParentPath).stringValue;
            var currentAssetsDirectoryName = _serializedObject.FindProperty(AssetsDirectoryName).stringValue;
            string crn = index.ToString(NumberFormat);
            return Path.Combine(currentAssetsParentPath, currentAssetsDirectoryName,
                $"{gameObject.name}.{crn}.{salt}.asset");
        }

        /// <summary>
        /// Run when the editor returns to edit mode. Adds new items to the timeline based
        /// on the saved data.
        /// </summary>
        private void BuildTimelineData()
        {
            var director = _serializedObject.FindProperty(Director).objectReferenceValue as PlayableDirector;
            var currentTimelineAsset = _serializedObject.FindProperty(TimelineAsset).objectReferenceValue
                as TimelineAsset;
            var assetsParentPath = _serializedObject.FindProperty(AssetsParentPath).stringValue;
            var assetsDirectoryName = _serializedObject.FindProperty(AssetsDirectoryName).stringValue;
            for (var currentRecordingNumber = 0; currentRecordingNumber < recordingNumber; currentRecordingNumber++)
            {
                var crn = currentRecordingNumber.ToString(NumberFormat);
                IEnumerator<GameObjectRecorder> recordersEnumerator = Recorders.GetEnumerator();
                while (recordersEnumerator.MoveNext())
                {
                    var gameObjectRecorder = recordersEnumerator.Current;
                    var currentGameObject = gameObjectRecorder?.root;
                    if (!currentGameObject)
                    {
                        Debug.LogWarning("Something in the recording is null that shouldn't be.");
                        return;
                    }

                    var currentAnimator = currentGameObject.GetComponent<Animator>();
                    var animationTrack =
                        currentTimelineAsset?.CreateTrack<AnimationTrack>($"{currentGameObject.name} #{crn}");
                    var newAssetPath = GameObjectAssetPath(currentGameObject, currentRecordingNumber);
                    Debug.Log($"new asset path: {newAssetPath}");
                    var animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newAssetPath);
                    animationTrack?.CreateClip(animationClip);
                    if (!director)
                    {
                        Debug.LogWarning("Something in the recording is null that shouldn't be.");
                        return;
                    }

                    director.SetGenericBinding(animationTrack, currentAnimator);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        /// <summary>
        /// Helper method that returns whether there are enough settings available to start recording.
        /// </summary>
        /// <returns>Whether there are enough settings available to start recording.</returns>
        private static bool ReadyToRecord()
        {
            var animators = _serializedObject.FindProperty(ObjectsToTrack).GetEnumerator();
            using var animators1 = animators as IDisposable;
            var trackName = _serializedObject.FindProperty(TrackName).stringValue;
            var assetsParentPath = _serializedObject.FindProperty(AssetsParentPath).stringValue;
            var assetsDirectoryName = _serializedObject.FindProperty(AssetsDirectoryName).stringValue;
            var currentTimelineAsset = _serializedObject.FindProperty(TimelineAsset)
                .objectReferenceValue as TimelineAsset;
            var recordingDelay = _serializedObject.FindProperty(RecordingDelay).floatValue;
            return
                !animators.MoveNext() &&
                trackName != "" &&
                recordingDelay > 0 &&
                assetsParentPath != "" &&
                assetsDirectoryName != "" &&
                !currentTimelineAsset;
        }

        /// <summary>
        /// Callback for when the playmode changes.
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
                var director = _serializedObject.FindProperty(Director).objectReferenceValue as PlayableDirector;
                var currentGameObject = director?.gameObject;
                var sceneData = currentGameObject?.GetComponent<RecordingDataHolder>();
                if (!sceneData)
                {
                    Debug.LogWarning("Something in the recording is null that shouldn't be with the saving of data.");
                    return;
                }

                sceneData.salt = GenerateSalt(); // Saved 
                salt = sceneData.salt;
            }

            if (state == PlayModeStateChange.ExitingPlayMode && _isRecording)
            {
                EndRecording();
            }
        }

        private static string GenerateSalt()
        {
            var salt = new System.Random().Next();
            return Convert.ToBase64String(BitConverter.GetBytes(salt)).TrimEnd('=');
        }

        private void SetupRecordingSystem()
        {
            var gameObject = GameObject.Find(AnimationRecorderName);
            if (gameObject == null)
            {
                Debug.LogWarning(
                    $"There needs to be an object in the scene called \"{AnimationRecorderName}\". One will be added now.");
                gameObject = new GameObject(AnimationRecorderName);
            }

            var sceneData = gameObject.GetComponent<RecordingDataHolder>();
            Holder = sceneData;
            if (gameObject.GetComponent<PlayableDirector>())
            {
                return;
            }

            var director = gameObject.AddComponent<PlayableDirector>();
            var newSalt = GenerateSalt();
            sceneData.salt = newSalt;
            var newAssetPath = Path.Combine(sceneData.assetsParentPath, AssetsDirectoryName,
                $"NewTimeline.{newSalt}.playable");
            AssetDatabase.CopyAsset(DefaultTimelinePath, newAssetPath);
            var newTimelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(newAssetPath);
            Holder.director = director;
            Holder.timelineAsset = newTimelineAsset;
            director.playableAsset = newTimelineAsset;
        }
    }
}
#endif