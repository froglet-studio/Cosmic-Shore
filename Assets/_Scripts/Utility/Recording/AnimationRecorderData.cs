#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CosmicShore.Utility
{
    /// <summary>
    /// A simple container for the data that will be used by the Asset Recorder.
    /// There should be one instance of this components on an object whose name is
    /// set in the member <see cref="RecorderWindow.ManagerName" />.
    /// </summary>
    public class RecordingDataHolder : MonoBehaviour
    {
#pragma warning disable 0414

        /// <summary>
        /// The overall manager for the timeline to record.
        /// </summary>
        [SerializeField] public PlayableDirector director;

        /// <summary>
        /// An asset (on disk) for the contents of the timeline.
        /// </summary>
        [SerializeField] public TimelineAsset timelineAsset;

        /// <summary>
        /// The parent of the folder where all items generated or expected from this utility should be stored.
        /// The path is relative to the project and must start with "Assets".
        /// </summary>
        [SerializeField] public string assetsParentPath = "Assets";

        /// <summary>
        /// The name of the directory where all the itames generated or xpected from this utility should be stored.
        /// </summary>
        [SerializeField] public string assetsDirectoryName = "Recorder";

        [SerializeField] public string trackName = "trackName";
        
        /// <summary>
        /// The game objects that this recorder will track.
        /// </summary>
        [SerializeField] private Animator[] objectsToTrack;

        /// <summary>
        /// The time that the recorder will wait for between each snapshot, in seconds.
        /// A larger number means a less detailed capture. A smaller number means
        /// a larger amount of data will be recorder.
        /// </summary>
        [SerializeField] private float recordingDelay = 1;

        /// <summary>
        /// The salt currently used in recording names.
        /// </summary>
        [SerializeField] internal string salt;

#pragma warning restore 0414
    }
}
#endif