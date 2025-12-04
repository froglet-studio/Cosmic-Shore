using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.AdaptivePerformance;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CosmicShore.Utility.Recording
{

    public class AnimationRecorderProcess : MonoBehaviour
    {
        #region Member variables.
        
        /// <summary>
        /// The salt used to keep each recording session distinct.
        ///
        /// <seealso href="https://en.wikipedia.org/wiki/Salt_(cryptography)">Definition of salt on wikipedia</seealso>
        /// </summary>
        private string salt;

        /// <summary>
        /// Whether the callbacks for when the editor changes state is registered.
        /// It should only be necessary to register it once until the editor is closed.
        /// </summary>
        private bool _registeredPlayCallbacks;
        
        /// <summary>
        /// A collection of every recorder, each one associated with a new game object to track.
        /// </summary>
        private readonly List<GameObjectRecorder> Recorders = new();
                
        /// <summary>
        /// Number for each recording of the same object.
        /// If object X is recorded 3 times in one session, add 0, 1, and 2 to each of its
        /// recording's temp files.
        /// </summary>
        private int recordingNumber;
        
        /// <summary>
        /// A property that will either return a reference to a holder, or do its best to instantiate
        /// one and then return it.
        /// </summary>
        private RecordingDataHolder Holder { get; set; }
        
        // /// <summary>
        // /// Where the timeline object should be stored, unless overridden.
        // /// </summary>
        // private const string DefaultTimelinePath = "Assets/_Scripts/Utility/Recording/DefaultTimeline.playable";
        
        /// <summary>
        /// A reference to the data holder that unity can use to handle serialized properties.
        /// </summary>
        internal SerializedObject RecorderSerializedObject;
        
        /// <summary>
        /// Format in which to convert the number of each recording assets to a string,
        /// with the right numbers of leading zeros.
        /// </summary>
        private const string NumberFormat = "D4";
        
        /// <summary>
        /// Whether the utility is in the process of recording.
        /// </summary>
        internal bool IsRecording { get; private set; }
        
        /// <summary>
        /// When recording, time until the next snapshot. When this _timer reaches 0, the utility
        /// takes a new snapshot.
        /// </summary>
        private float _timer;
        
        // /// <summary>
        // /// Moment when the current recording started.
        // /// </summary>
        // private float animationStart;
        
        /// <summary>
        /// The name of the game object that will have the data holder as its component.
        /// This is used to find the object in question. That name should only be held
        /// by the object that contains the data holder.
        /// </summary>
        public const string AnimationRecorderName = "Animation Recorder";
        
        #endregion

        #region Names of serialized object properties
        /// <summary>
        /// The name of the serialized property for the track name in the data holder
        /// object, for use by by Unity's property finder.
        /// </summary>
        internal const string TrackName = "trackName";

        /// <summary>
        /// The name of the serialized property for the salt currently in use by
        /// the utility. Can be changed by this code but not by the user.
        /// </summary>
        internal const string SaltField = "salt";

        /// <summary>
        /// The name of the serialized property for the playable director
        /// in the data holder object, for use by Unity's property finder.
        /// </summary>
        internal const string Director = "director";

        /// <summary>
        /// The name of the serialized property for the objects to track
        /// during the recording, as stored in the data holder object
        /// for use by Unity's property finder.
        /// </summary>
        internal const string ObjectsToTrack = "objectsToTrack";

        /// <summary>
        /// The name of the serialized property for the delay between recording
        /// snapshots, as stored in the data holder object, for use by Unity's property finder.
        /// </summary>
        internal const string RecordingDelay = "recordingDelay";

        /// <summary>
        /// The name of the serialized property that references the timeline asset,
        /// as stored in the data holder object, for use by Unity's property finder.
        /// </summary>
        internal const string TimelineAsset = "timelineAsset";

        /// <summary>
        /// The name of the serialized property for the path of the parent of the folder where the assets used by this
        /// recorder as stored, for use by Unity's property finder.
        /// </summary>
        internal const string AssetsParentPath = "assetsParentPath";

        /// <summary>
        /// The name of the serialized property for the name name of the folder where the assets used by this
        /// recorder are stored, for use by Unity's property finder.
        /// </summary>
        internal const string AssetsDirectoryName = "assetsDirectoryName";
        #endregion
        
        #region Prepare for recording
        
        internal void StartRecording()
        {
            IsRecording = true;
            SetupRecording();
            _timer = Holder.recordingDelay;

        }
        
        private static string GenerateSalt()
        {
            var salt = new System.Random().Next();
            return Convert.ToBase64String(BitConverter.GetBytes(salt)).TrimEnd('=');
        }
        
        public int RecordingNumber
        {
            get => recordingNumber;
            set => recordingNumber = value;
        }

        /// <summary>
        /// The full path of the current animation asset.
        ///
        /// A "new recording" happens when the user stops recording and starts again within one play session.
        /// </summary>
        /// <param name="index">Which recording, if more than one in this session, in which this animation belongs.</param>
        /// <returns></returns>
        private string GameObjectAssetPath(int index)
        {
            var currentAssetsParentPath = Holder.assetsParentPath;
            var currentAssetsDirectoryName = Holder.assetsDirectoryName;
            var crn = index.ToString(NumberFormat);
            return Path.Combine(currentAssetsParentPath, currentAssetsDirectoryName,
                $"{gameObject.name}.{crn}.{salt}.asset");
        }

        internal void SetupRecordingSystem2()
        {
            var newSalt = GenerateSalt();
            // var newAssetPath = Path.Combine(sceneData.assetsParentPath, AssetsDirectoryName,
            //     $"NewTimeline.{newSalt}.playable");
            // AssetDatabase.CopyAsset(DefaultTimelinePath, newAssetPath);
            // Holder.director = director;
            // var newTimelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(newAssetPath);
            // Holder.timelineAsset = newTimelineAsset;
            // director.playableAsset = newTimelineAsset;
        }
        
        /// <summary>
        /// Helper method that returns whether there are enough settings available to start recording.
        /// </summary>
        /// <returns>Whether there are enough settings available to start recording.</returns>
        internal bool ReadyToRecord()
        {
            return
                Holder.trackName != "" &&
                Holder.recordingDelay > 0 &&
                Holder.assetsParentPath != "" &&
                Holder.assetsDirectoryName != "" &&
                !Holder.timelineAsset;
        }
        
        /// <summary>
        /// Makes sure that none of the elements needed for a recording are null.
        /// Called at several points while the current utility is running.
        /// This method should  not overwrite existing data.
        /// </summary>
        internal void Initialize()
        {
            Holder ??= gameObject.GetOrAddComponent<RecordingDataHolder>();
            RecorderSerializedObject ??= new SerializedObject(Holder);
            var playableDirector = gameObject.GetOrAddComponent<PlayableDirector>();
            Holder.director ??= playableDirector;
            Debug.Log($"Holder : {Holder.name} :: playableDirector :  {playableDirector.playableAsset.name}");
            Debug.Log($"Holder : {Holder.name} :: Holder.director : {Holder.director.name}");
            var newAssetPath = GameObjectAssetPath(recordingNumber);
            var newTimelineAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(newAssetPath);
            Holder.timelineAsset = newTimelineAsset;
            Holder.director.playableAsset = newTimelineAsset;
        }
        
        private void OnEnable()
        {
            Initialize();
            if (_registeredPlayCallbacks)
            {
                return;
            }

            EditorApplication.playModeStateChanged += OnPlaymodeChangeState;
            _registeredPlayCallbacks = true;

        }

        /// <summary>
        /// Prepares the utility for a new recording.
        /// </summary>
        private void SetupRecording()
        {
            Recorders.Clear();
            foreach (var objectToTrack in Holder.objectsToTrack)
            {
                GameObjectRecorder gameObjectRecorder = new(gameObject);
                gameObjectRecorder.BindComponentsOfType<Transform>(gameObject, true);
                Recorders.Add(gameObjectRecorder);
            }
        }
        #endregion

        #region Finish recording

        /// <summary>
        /// Called when the user stops the animation. Saves all the data necessary to add the new entries
        /// to the timeline.
        /// </summary>
        internal void EndRecording()
        {
            
            IsRecording = false;
            var currentAssetsParent = Holder.assetsParentPath;
            var currentAssetsFolderName = Holder.assetsDirectoryName;
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
        #endregion
        
        #region Proceed with recording
        /// <summary>
        /// Run when the editor returns to edit mode. Adds new items to the timeline based
        /// on the saved data.
        /// </summary>
        private void BuildTimelineData()
        {
            var currentTimelineAsset = Holder.timelineAsset;
            for (var currentRecordingNumber = 0; currentRecordingNumber < recordingNumber; currentRecordingNumber++)
            {
                var crn = currentRecordingNumber.ToString(NumberFormat);
                IEnumerator<GameObjectRecorder> recordersEnumerator = Recorders.GetEnumerator();
                while (recordersEnumerator.MoveNext())
                {
                    Debug.Log($"Recording {currentRecordingNumber}: {crn} and {recordersEnumerator.Current}");
                    var gameObjectRecorder = recordersEnumerator.Current;
                    var currentAnimator = GetComponent<Animator>();
                    var animationTrack =
                        currentTimelineAsset?.CreateTrack<AnimationTrack>($"{name} #{crn}");
                    var newAssetPath = GameObjectAssetPath(currentRecordingNumber);
                    Debug.Log($"new asset path: {newAssetPath}");
                    var animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newAssetPath);
                    animationTrack?.CreateClip(animationClip);
                    if (!Holder.director)
                    {
                        Debug.LogWarning("Something in the recording is null that shouldn't be.");
                        return;
                    }

                    Holder.director.SetGenericBinding(animationTrack, currentAnimator);
                    AssetDatabase.SaveAssets();
                }
            }
        }


        /// <summary>
        /// Callback for when the playmode changes.
        /// </summary>
        /// <param name="state">The current playmode.</param>
        private void OnPlaymodeChangeState(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredEditMode:
                    BuildTimelineData();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                {
                    recordingNumber = 0;
                    // var director = _serializedObject.FindProperty(Director).objectReferenceValue as PlayableDirector;
                    Holder.salt = GenerateSalt(); // Saved 
                    salt = Holder.salt;
                    break;
                }
                case PlayModeStateChange.ExitingPlayMode when IsRecording:
                    EndRecording();
                    break;
                case PlayModeStateChange.ExitingEditMode:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
        
        /// <summary>
        /// Takes a snapshot for each currently tracked game objects.
        /// </summary>
        private void TakeSnapshot()
        {
            var recordingDelay = Holder.recordingDelay;
            IEnumerator<GameObjectRecorder> recordersEnumerator = Recorders.GetEnumerator();
            while (recordersEnumerator.MoveNext())
            {
                var currentRecorder = recordersEnumerator.Current;
                currentRecorder?.TakeSnapshot(recordingDelay);
            }
        }
        
        /// <summary>
        /// This update method works as a replacement for Coroutines, which are not available
        /// in the current context.
        /// </summary>
        public void Update()
        {
            var recordingDelay = Holder.recordingDelay;
            if (!IsRecording)
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
        #endregion
    }
}